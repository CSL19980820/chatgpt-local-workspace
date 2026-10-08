using System;
using System.IO;
using System.Linq;
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
    public const string SdkVersion="1.0.4258.31";
    static bool hooked;
    static string nativeDir;
    static readonly System.Collections.Generic.Dictionary<string,System.Reflection.Assembly> assemblies=new System.Collections.Generic.Dictionary<string,System.Reflection.Assembly>();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool SetDllDirectory(string path);
    public static void Hook()
    {
        if(hooked)return;hooked=true;
        AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{
            string name=new System.Reflection.AssemblyName(e.Name).Name;
            string res=name=="Microsoft.Web.WebView2.Core"?"wv2.core.dll":name=="Microsoft.Web.WebView2.WinForms"?"wv2.winforms.dll":null;
            if(res==null)return null;
            // Loading the same bytes twice creates separate type identities on .NET Framework.
            // Core and WinForms must share one cached assembly for each embedded resource.
            lock(assemblies){System.Reflection.Assembly loaded;if(assemblies.TryGetValue(name,out loaded))return loaded;
                using(var stream=typeof(WebviewLoader).Assembly.GetManifestResourceStream(res)){if(stream==null)return null;var bytes=new byte[stream.Length];stream.Read(bytes,0,bytes.Length);loaded=System.Reflection.Assembly.Load(bytes);assemblies[name]=loaded;return loaded;}}
        };
    }
    public static void PrepareNative()
    {
        if(nativeDir!=null)return;
        string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LocalWorkspacePlugin","webview2","sdk-"+SdkVersion);
        Directory.CreateDirectory(dir);
        string target=Path.Combine(dir,"WebView2Loader.dll");
        using(var stream=typeof(WebviewLoader).Assembly.GetManifestResourceStream("wv2.loader.dll")){var bytes=new byte[stream.Length];stream.Read(bytes,0,bytes.Length);if(!File.Exists(target)||!File.ReadAllBytes(target).SequenceEqual(bytes))File.WriteAllBytes(target,bytes);}
        SetDllDirectory(dir);nativeDir=dir;
    }
    public static string RuntimeVersion(){try{Hook();PrepareNative();return AvailableRuntime();}catch{return "";}}
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static string AvailableRuntime(){return CoreWebView2Environment.GetAvailableBrowserVersionString();}
}

// Zoom range shared by the window settings and the host. Kept outside WorkbenchHost so
// reading settings never loads the WebView2 assemblies.
static class WorkbenchZoom
{
    public const double Min=0.67,Max=2.0;
    public static double Clamp(double value)
    {
        if(double.IsNaN(value)||double.IsInfinity(value)||value<=0)return 1.0;
        return Math.Max(Min,Math.Min(Max,Math.Round(value,2)));
    }
}

// Hosts the embedded workbench page. Lives in its own type so merely constructing the
// main window never loads the WebView2 assemblies; a missing runtime degrades to the
// browser fallback instead of failing at startup.
sealed class WorkbenchHost:IDisposable
{
    // #f7f7f7 matches the page shell (--frame) and Theme.Window, so there is no flash before first paint.
    readonly WebView2 view=new WebView2{Dock=DockStyle.Fill,DefaultBackgroundColor=Color.FromArgb(247,247,247)};
    public event Action NavigationCompleted;
    public event Action<double> ZoomChanged;
    static readonly double[] ZoomSteps={0.67,0.75,0.8,0.9,1.0,1.1,1.25,1.5,1.75,2.0};
    bool ready;double zoom=1.0;
    public Control Control{get{return view;}}
    public double Zoom{get{return zoom;}set{SetZoom(value,true);}}
    public void ZoomIn(){foreach(var s in ZoomSteps)if(s>zoom+0.005){SetZoom(s,true);return;}}
    public void ZoomOut(){for(int i=ZoomSteps.Length-1;i>=0;i--)if(ZoomSteps[i]<zoom-0.005){SetZoom(ZoomSteps[i],true);return;}}
    public void ZoomReset(){SetZoom(1.0,true);}
    void SetZoom(double value,bool notify)
    {
        double next=WorkbenchZoom.Clamp(value);bool changed=Math.Abs(next-zoom)>0.005;zoom=next;
        ApplyZoom();
        if(changed&&notify&&ZoomChanged!=null)ZoomChanged(zoom);
    }
    void ApplyZoom(){if(ready&&Math.Abs(view.ZoomFactor-zoom)>0.005)view.ZoomFactor=zoom;}
    public static async Task<WorkbenchHost> CreateAsync(string userDataFolder,Control container,double initialZoom)
    {
        WebviewLoader.PrepareNative();
        var host=new WorkbenchHost();host.zoom=WorkbenchZoom.Clamp(initialZoom);
        try{
            container.Controls.Add(host.view);var handle=host.view.Handle;
            Directory.CreateDirectory(userDataFolder);
            var env=await CoreWebView2Environment.CreateAsync(null,userDataFolder);
            await host.view.EnsureCoreWebView2Async(env);
            var st=host.view.CoreWebView2.Settings;st.AreDefaultContextMenusEnabled=false;st.AreDevToolsEnabled=false;st.IsStatusBarEnabled=false;st.IsZoomControlEnabled=true;st.AreBrowserAcceleratorKeysEnabled=false;
            // The workbench only ships a light theme; older runtimes without profile support keep the default.
            try{host.view.CoreWebView2.Profile.PreferredColorScheme=CoreWebView2PreferredColorScheme.Light;}catch{ }
            // Ctrl+wheel changes ZoomFactor; keep it inside the supported range and report it so the window can remember it.
            host.view.ZoomFactorChanged+=(s,e)=>{if(host.ready)host.SetZoom(host.view.ZoomFactor,true);};
            host.view.CoreWebView2.NavigationCompleted+=(s,e)=>{host.ApplyZoom();if(host.NavigationCompleted!=null)host.NavigationCompleted();};
            host.ready=true;host.ApplyZoom();return host;
        }catch{host.Dispose();throw;}
    }
    // The embedded page learns it runs inside the desktop window from ?host=desktop (before any #fragment).
    public static string DesktopUrl(string url)
    {
        if(string.IsNullOrEmpty(url)||!url.StartsWith("http://127.0.0.1:",StringComparison.Ordinal))return url;
        int hash=url.IndexOf('#');string head=hash<0?url:url.Substring(0,hash),tail=hash<0?"":url.Substring(hash);
        if(head.IndexOf("host=desktop",StringComparison.Ordinal)>=0)return url;
        return head+(head.IndexOf('?')<0?"?":"&")+"host=desktop"+tail;
    }
    public void Navigate(string url){if(ready)view.CoreWebView2.Navigate(DesktopUrl(url));}
    public void Reload(){if(ready)view.CoreWebView2.Reload();}
    public void Dispose(){try{view.Dispose();}catch{ }}
}
