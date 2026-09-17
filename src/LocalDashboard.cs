using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Script.Serialization;

// Read-only loopback view. No MCP execution, file browser or configuration endpoint.
sealed class LocalDashboard : IDisposable
{
    readonly TcpListener listener; readonly Func<string,object> snapshot; readonly SemaphoreSlim slots=new SemaphoreSlim(8); bool stopped;
    public static string Url="";
    public LocalDashboard(Func<string,object> getSnapshot)
    {
        snapshot=getSnapshot;listener=new TcpListener(IPAddress.Loopback,0);listener.Start();Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/";
        Task.Run(async()=>{while(!stopped){TcpClient client;try{client=await listener.AcceptTcpClientAsync();}catch{break;}if(!slots.Wait(0)){client.Close();continue;}ThreadPool.QueueUserWorkItem(_=>{try{Serve(client);}finally{client.Close();slots.Release();}});}});
        Console.Error.WriteLine("[Dashboard] "+Url);
    }
    void Serve(TcpClient client)
    {
        try{client.ReceiveTimeout=3000;client.SendTimeout=3000;using(var stream=client.GetStream()){
            var bytes=new List<byte>();int b;while(bytes.Count<8192&&(b=stream.ReadByte())>=0){bytes.Add((byte)b);int n=bytes.Count;if(n>=4&&bytes[n-4]==13&&bytes[n-3]==10&&bytes[n-2]==13&&bytes[n-1]==10)break;}
            string header=Encoding.ASCII.GetString(bytes.ToArray());if(!header.EndsWith("\r\n\r\n")){Send(stream,400,"text/plain","Invalid request");return;}
            var lines=header.Split(new[]{"\r\n"},StringSplitOptions.None);var first=lines[0].Split(' ');var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);for(int i=1;i<lines.Length;i++){int colon=lines[i].IndexOf(':');if(colon>0)headers[lines[i].Substring(0,colon)]=lines[i].Substring(colon+1).Trim();}
            string host,origin,site;var baseUri=new Uri(Url);
            if(!headers.TryGetValue("Host",out host)||host!=baseUri.Authority||(headers.TryGetValue("Origin",out origin)&&origin!=Url.TrimEnd('/'))||(headers.TryGetValue("Sec-Fetch-Site",out site)&&site!="same-origin"&&site!="none")){Send(stream,403,"text/plain","Local same-origin access only");return;}
            if(first.Length!=3||first[0]!="GET"){Send(stream,405,"text/plain","GET only");return;}
            Uri uri;if(!Uri.TryCreate(baseUri,first[1],out uri)||uri.Authority!=baseUri.Authority){Send(stream,400,"text/plain","Invalid target");return;}
            if(uri.AbsolutePath=="/"){
                // Keep the UI replaceable without restarting the MCP worker or its commands.
                string page=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"dashboard.html");
                if(File.Exists(page))Send(stream,200,"text/html",File.ReadAllText(page,Encoding.UTF8));
                else using(var reader=new StreamReader(typeof(LocalDashboard).Assembly.GetManifestResourceStream("dashboard.html")))Send(stream,200,"text/html",reader.ReadToEnd());
            }
            else if(uri.AbsolutePath=="/api/snapshot"){
                string thread="";foreach(string pair in uri.Query.TrimStart('?').Split('&')){var parts=pair.Split(new[]{'='},2);if(parts[0]=="thread")thread=Uri.UnescapeDataString(parts.Length>1?parts[1]:"");}
                try{Send(stream,200,"application/json",new JavaScriptSerializer{MaxJsonLength=8*1024*1024}.Serialize(snapshot(thread)));}catch(ArgumentException ex){Send(stream,400,"application/json",new JavaScriptSerializer().Serialize(new{error=ex.Message}));}
            }else Send(stream,404,"text/plain","Not found");
        }}catch(IOException){}catch(SocketException){}catch(ObjectDisposedException){}
    }
    static void Send(Stream stream,int status,string type,string body){byte[] data=Encoding.UTF8.GetBytes(body);string headers="HTTP/1.1 "+status+" "+(status==200?"OK":"Error")+"\r\nContent-Type: "+type+"; charset=utf-8\r\nContent-Length: "+data.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'\r\n\r\n";byte[] head=Encoding.ASCII.GetBytes(headers);stream.Write(head,0,head.Length);stream.Write(data,0,data.Length);}
    public void Dispose(){stopped=true;listener.Stop();}
}
