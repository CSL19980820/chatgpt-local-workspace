using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;

static class AttachmentImport
{
    // This csc-built application has no target-framework config. Require TLS 1.2
    // instead of .NET's legacy SSL3/TLS1 fallback; keep certificate validation.
    static AttachmentImport(){ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;}
    const long MaxBytes=32L*1024*1024;
    static string Text(Dictionary<string,object> args,string key){object value;if(!args.TryGetValue(key,out value)||!(value is string))throw new ArgumentException(key+" must be a string");return (string)value;}
    internal static bool PublicAddress(IPAddress address)
    {
        if(IPAddress.IsLoopback(address))return false;
        if(address.IsIPv4MappedToIPv6)return PublicAddress(address.MapToIPv4());
        byte[] b=address.GetAddressBytes();
        if(address.AddressFamily==AddressFamily.InterNetworkV6)return !address.Equals(IPAddress.IPv6Any)&&!address.IsIPv6LinkLocal&&!address.IsIPv6SiteLocal&&!address.IsIPv6Multicast&&(b[0]&0xfe)!=0xfc;
        return b[0]!=0&&b[0]!=10&&b[0]!=127&&b[0]<224&&!(b[0]==169&&b[1]==254)&&!(b[0]==172&&b[1]>=16&&b[1]<=31)&&!(b[0]==192&&(b[1]==168||b[1]==0))&&!(b[0]==100&&b[1]>=64&&b[1]<=127)&&!(b[0]==198&&(b[1]==18||b[1]==19));
    }
    static Uri DownloadUri(string value)
    {
        Uri uri;if(!Uri.TryCreate(value,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.UserInfo.Length>0||uri.Port!=443)throw new ArgumentException("Attachment download must use a public HTTPS URL without credentials (port 443).");
        IPAddress literal;bool isLiteral=IPAddress.TryParse(uri.DnsSafeHost.Trim('[',']'),out literal);
        var addresses=Dns.GetHostAddresses(uri.DnsSafeHost);
        // TUN proxies commonly resolve public hostnames into 198.18/15. Permit that
        // synthetic range only for DNS names; HTTPS still validates the original host.
        // Literal benchmark IPs, private LAN addresses and loopback remain rejected.
        if(addresses.Length==0||Array.Exists(addresses,a=>{var b=a.GetAddressBytes();bool synthetic=!isLiteral&&b.Length==4&&b[0]==198&&(b[1]==18||b[1]==19);return !PublicAddress(a)&&!synthetic;}))throw new ArgumentException("Private and loopback download addresses are not allowed.");
        return uri;
    }
    public static object Import(Dictionary<string,object> file,string path)
    {
        string fileId=Text(file,"file_id");if(fileId.Length==0||fileId.Length>512)throw new ArgumentException("A valid file_id is required.");
        if(File.Exists(path)||Directory.Exists(path))throw new IOException("Destination already exists; choose a new path. Existing files are never overwritten.");
        string parent=Path.GetDirectoryName(path);if(!Directory.Exists(parent))throw new DirectoryNotFoundException("Create the destination directory first.");
        string name=Path.GetFileName(path);if(name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new ArgumentException("Invalid destination filename.");
        string temp=Path.Combine(parent,".attachment-"+Guid.NewGuid().ToString("N")+".tmp");
        var deadline=DateTime.UtcNow.AddSeconds(45);long size=0;string mime="application/octet-stream";
        try{
            Uri uri=DownloadUri(Text(file,"download_url"));
            for(int redirect=0;;redirect++){
                int remaining=(int)(deadline-DateTime.UtcNow).TotalMilliseconds;if(remaining<=0)throw new IOException("Attachment download timed out; request a fresh attachment URL and retry.");
                var request=(HttpWebRequest)WebRequest.Create(uri);request.Method="GET";request.AllowAutoRedirect=false;request.Timeout=remaining;request.ReadWriteTimeout=Math.Min(remaining,10000);request.MaximumResponseHeadersLength=32;request.Credentials=null;request.UseDefaultCredentials=false;
                // No raw URL or response error is included in receipts: signed URLs are secrets.
                using(var timeout=new Timer(_=>request.Abort(),null,remaining,Timeout.Infinite))
                using(var response=(HttpWebResponse)request.GetResponse()){
                    int status=(int)response.StatusCode;
                    if(status>=300&&status<400){if(redirect>=3)throw new IOException("Too many attachment redirects.");Uri next;if(!Uri.TryCreate(uri,response.Headers["Location"],out next))throw new IOException("Invalid attachment redirect.");uri=DownloadUri(next.AbsoluteUri);continue;}
                    if(response.StatusCode!=HttpStatusCode.OK)throw new IOException("Attachment download did not return a complete file.");
                    if(response.ContentLength>MaxBytes)throw new IOException("Attachment exceeds 32 MiB.");
                    mime=(response.ContentType??mime).Split(';')[0];
                    using(var input=response.GetResponseStream())using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                        byte[] buffer=new byte[65536];int count;
                        while((count=input.Read(buffer,0,buffer.Length))>0){size+=count;if(size>MaxBytes)throw new IOException("Attachment exceeds 32 MiB.");output.Write(buffer,0,count);}
                    }
                    if(response.ContentLength>=0&&size!=response.ContentLength)throw new IOException("Attachment download was incomplete; no destination file was created.");
                    break;
                }
            }
            string digest;using(var stream=File.OpenRead(temp))using(var sha=SHA256.Create())digest=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            File.Move(temp,path); // Atomic create; a racing writer is never overwritten.
            return new{path=Presentation.DisplayPath(path),file_id=fileId,mime_type=mime,size_bytes=size,sha256=digest,created=true};
        }catch(WebException ex){var response=ex.Response as HttpWebResponse;string code=response==null?ex.Status.ToString():"HTTP "+(int)response.StatusCode;if(response!=null)response.Close();throw new IOException("Attachment download failed ("+code+"). Reattach the file to obtain a fresh URL; no existing file was overwritten.");}
        catch(SocketException){throw new IOException("Attachment download host could not be resolved.");}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
