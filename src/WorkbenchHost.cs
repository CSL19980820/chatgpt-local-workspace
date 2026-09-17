using System;
using System.IO;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

// The shipped artifact is a single exe, so the WebView2 assemblies travel inside it as
// manifest resources and are resolved here on first use instead of sitting next to the exe.
static class WebviewLoader
{
    static bool hooked;
    static string nativeDir;
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool SetDllDirectory(string path);
    public static void Hook()
    {
        if(hooked)return;hooked=true;
        AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{
            string name=new System.Reflection.AssemblyName(e.Name).Name;
            string res=name=="Microsoft.Web.WebView2.Core"?"wv2.core.dll":name=="Microsoft.Web.WebView2.WinForms"?"wv2.winforms.dll":null;
            if(res==null)return null;
            using(var stream=typeof(WebviewLoader).Assembly.GetManifestResourceStream(res)){if(stream==null)return null;var bytes=new byte[stream.Length];stream.Read(bytes,0,bytes.Length);return System.Reflection.Assembly.Load(bytes);}
        };
    }
    public static void PrepareNative()
    {
        if(nativeDir!=null)return;
        string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LocalWorkspacePlugin","webview2");
        Directory.CreateDirectory(dir);
        string target=Path.Combine(dir,"WebView2Loader.dll");
        using(var stream=typeof(WebviewLoader).Assembly.GetManifestResourceStream("wv2.loader.dll")){var bytes=new byte[stream.Length];stream.Read(bytes,0,bytes.Length);if(!File.Exists(target)||new FileInfo(target).Length!=bytes.Length)File.WriteAllBytes(target,bytes);}
        SetDllDirectory(dir);nativeDir=dir;
    }
}

// Hosts the embedded workbench page. Lives in its own type so merely constructing the
// main window never loads the WebView2 assemblies; a missing runtime degrades to the
// browser fallback instead of failing at startup.
sealed class WorkbenchHost:IDisposable
{
    readonly WebView2 view=new WebView2{Dock=DockStyle.Fill,DefaultBackgroundColor=Color.White};
    public event Action NavigationCompleted;
    bool ready;
    public Control Control{get{return view;}}
    public static async Task<WorkbenchHost> CreateAsync(string userDataFolder)
    {
        WebviewLoader.PrepareNative();
        var host=new WorkbenchHost();
        Directory.CreateDirectory(userDataFolder);
        var env=await CoreWebView2Environment.CreateAsync(null,userDataFolder);
        await host.view.EnsureCoreWebView2Async(env);
        var st=host.view.CoreWebView2.Settings;st.AreDefaultContextMenusEnabled=false;st.AreDevToolsEnabled=false;st.IsStatusBarEnabled=false;st.IsZoomControlEnabled=false;st.AreBrowserAcceleratorKeysEnabled=false;
        host.view.CoreWebView2.NavigationCompleted+=(s,e)=>{if(host.NavigationCompleted!=null)host.NavigationCompleted();};
        host.ready=true;
        return host;
    }
    public void Navigate(string url){if(ready)view.CoreWebView2.Navigate(url);}
    public void Reload(){if(ready)view.CoreWebView2.Reload();}
    public void Dispose(){try{view.Dispose();}catch{ }}
}
