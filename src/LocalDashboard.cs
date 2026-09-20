using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

// Loopback view plus an explicit, same-origin Windows reveal action. Never execute files.
sealed class LocalDashboard : IDisposable
{
    readonly TcpListener listener; readonly Func<string,object> snapshot; readonly SemaphoreSlim slots=new SemaphoreSlim(8); bool stopped;
    [ThreadStatic] static JavaScriptSerializer jsonInstance;
    // Reused per pool thread; the snapshot endpoint is polled ~1/sec per open dashboard.
    static JavaScriptSerializer Json { get { var j=jsonInstance; if(j==null){ j=new JavaScriptSerializer{MaxJsonLength=8*1024*1024}; jsonInstance=j; } return j; } }
    public static string Url="";
    readonly string actionToken=Guid.NewGuid().ToString("N");
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
            if(first.Length!=3){Send(stream,400,"text/plain","Invalid request");return;}
            Uri uri;if(!Uri.TryCreate(baseUri,first[1],out uri)||uri.Authority!=baseUri.Authority){Send(stream,400,"text/plain","Invalid target");return;}
            if(uri.AbsolutePath=="/api/open")
            {
                string token;
                if(first[0]!="POST"){Send(stream,405,"text/plain","POST only");return;}
                if(!headers.TryGetValue("Origin",out origin)||origin!=Url.TrimEnd('/')||!headers.TryGetValue("X-Workspace-Token",out token)||token!=actionToken){Send(stream,403,"application/json",Json.Serialize(new{error="请从本地工作台点击路径。"}));return;}
                try{Reveal(Query(uri,"target"));Send(stream,200,"application/json","{\"opened\":true}");}
                catch(Exception ex){if(!(ex is ArgumentException||ex is IOException||ex is System.ComponentModel.Win32Exception||ex is UnauthorizedAccessException||ex is COMException||ex is NotSupportedException))throw;Send(stream,400,"application/json",Json.Serialize(new{error=ex.Message}));}
                return;
            }
            if(first[0]!="GET"){Send(stream,405,"text/plain","GET only");return;}
            if(uri.AbsolutePath=="/"){
                // Keep the UI replaceable without restarting the MCP worker or its commands.
                string page=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"dashboard.html");
                if(File.Exists(page))Send(stream,200,"text/html",File.ReadAllText(page,Encoding.UTF8));
                else using(var reader=new StreamReader(typeof(LocalDashboard).Assembly.GetManifestResourceStream("dashboard.html")))Send(stream,200,"text/html",reader.ReadToEnd());
            }
            else if(uri.AbsolutePath=="/api/local-actions")Send(stream,200,"application/json",Json.Serialize(new{token=actionToken}));
            else if(uri.AbsolutePath.StartsWith("/api/images/",StringComparison.Ordinal)){
                var image=DashboardImages.Get(uri.AbsolutePath.Substring("/api/images/".Length));
                if(image==null)Send(stream,404,"text/plain","图片预览已过期，请重新读取图片。");
                else SendBytes(stream,200,image.Mime,image.Bytes);
            }
            else if(uri.AbsolutePath=="/api/snapshot"){
                string thread="";foreach(string pair in uri.Query.TrimStart('?').Split('&')){var parts=pair.Split(new[]{'='},2);if(parts[0]=="thread")thread=Uri.UnescapeDataString(parts.Length>1?parts[1]:"");}
                try{Send(stream,200,"application/json",Json.Serialize(snapshot(thread)));}catch(ArgumentException ex){Send(stream,400,"application/json",Json.Serialize(new{error=ex.Message}));}
            }else Send(stream,404,"text/plain","Not found");
        }}catch(IOException){}catch(SocketException){}catch(ObjectDisposedException){}
    }
    static string Query(Uri uri,string key){foreach(string pair in uri.Query.TrimStart('?').Split('&')){var parts=pair.Split(new[]{'='},2);if(parts[0]==key)return Uri.UnescapeDataString(parts.Length>1?parts[1]:"");}return "";}
    static string Quote(string value){string trimmed=value.TrimEnd('\\');return "\""+trimmed+new string('\\',(value.Length-trimmed.Length)*2)+"\"";}
    static void Reveal(string target)
    {
        if(target.Length==0||target.IndexOfAny(new[]{'\r','\n','\0','"'})>=0)throw new ArgumentException("无效的路径或地址。");
        Uri url;
        if(Uri.TryCreate(target,UriKind.Absolute,out url)&&(url.Scheme=="http"||url.Scheme=="https"))
        {Process.Start(new ProcessStartInfo(url.AbsoluteUri){UseShellExecute=true});return;}
        if(!Regex.IsMatch(target,@"^[A-Za-z]:[\\/]|^\\\\[^\\?\.]+\\[^\\]+")||target.StartsWith(@"\\?\")||target.StartsWith(@"\\.\"))throw new ArgumentException("请选择完整的 Windows 文件或目录路径。");
        string path=Path.GetFullPath(target);
        bool directory=Directory.Exists(path);
        if(!directory&&!File.Exists(path))throw new FileNotFoundException("路径已不存在或当前用户无法访问："+target);
        string explorer=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");
        if(directory){Process.Start(new ProcessStartInfo(explorer,Quote(path)){UseShellExecute=true});return;}
        // Explorer's /select can open the folder without selecting the file. Use its
        // view API and read the selection back before reporting success.
        string parent=Path.GetDirectoryName(path);
        Process.Start(new ProcessStartInfo(explorer,Quote(parent)){UseShellExecute=true});
        Exception failure=null;
        var reveal=new Thread(()=>{
            object shell=null,windows=null;
            try{
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                for(int attempt=0;attempt<30;attempt++){
                    windows=((dynamic)shell).Windows();
                    try{
                        for(int i=((dynamic)windows).Count-1;i>=0;i--){
                            object window=null,document=null,folder=null,folderItem=null,item=null,selection=null,selected=null;
                            try{
                                window=((dynamic)windows).Item(i);
                                if(!String.Equals(Path.GetFileName((string)((dynamic)window).FullName),"explorer.exe",StringComparison.OrdinalIgnoreCase))continue;
                                document=((dynamic)window).Document;folder=((dynamic)document).Folder;folderItem=((dynamic)folder).Self;
                                if(!String.Equals((string)((dynamic)folderItem).Path,parent,StringComparison.OrdinalIgnoreCase))continue;
                                item=((dynamic)folder).ParseName(Path.GetFileName(path));if(item==null)continue;
                                ((dynamic)document).SelectItem(item,29); // Select, deselect others, ensure visible, focus.
                                selection=((dynamic)document).SelectedItems();
                                if(((dynamic)selection).Count==1){selected=((dynamic)selection).Item(0);if(String.Equals((string)((dynamic)selected).Path,path,StringComparison.OrdinalIgnoreCase))return;}
                            }catch(COMException){}
                            finally{Release(selected);Release(selection);Release(item);Release(folderItem);Release(folder);Release(document);Release(window);}
                        }
                    }finally{Release(windows);windows=null;}
                    Thread.Sleep(100);
                }
                failure=new IOException("已打开目录，但未能选中文件，请在目录中查找："+path);
            }catch(Exception ex){failure=new IOException("Windows 未能定位文件："+ex.Message,ex);}
            finally{Release(windows);Release(shell);}
        });
        reveal.IsBackground=true;reveal.SetApartmentState(ApartmentState.STA);reveal.Start();
        if(!reveal.Join(5000))throw new IOException("Windows 打开位置超时，请稍后重试。");
        if(failure!=null)throw failure;
    }
    static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
    static void Send(Stream stream,int status,string type,string body){SendBytes(stream,status,type+"; charset=utf-8",Encoding.UTF8.GetBytes(body));}
    static void SendBytes(Stream stream,int status,string type,byte[] data){string headers="HTTP/1.1 "+status+" "+(status==200?"OK":"Error")+"\r\nContent-Type: "+type+"\r\nContent-Length: "+data.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'\r\n\r\n";byte[] head=Encoding.ASCII.GetBytes(headers);stream.Write(head,0,head.Length);stream.Write(data,0,data.Length);}
    public void Dispose(){stopped=true;listener.Stop();}
}
