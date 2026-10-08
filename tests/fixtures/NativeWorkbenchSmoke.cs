using System;
using System.IO;
using System.Reflection;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

// Exercises the shipped assembly in hidden native controls, without Tunnel or real settings.
class NativeWorkbenchSmoke
{
    static Assembly app;
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Field(object owner,string name){return owner.GetType().GetField(name,Hidden).GetValue(owner);}
    static object Invoke(object owner,string name,params object[] args){return owner.GetType().GetMethod(name,Hidden).Invoke(owner,args);}
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    [STAThread] static void Main(string[] args)
    {
        app=Assembly.LoadFrom(args[0]);app.GetType("WebviewLoader").GetMethod("Hook").Invoke(null,null);
        Application.EnableVisualStyles();bool started=false;
        Application.Idle+=async(s,e)=>{if(started)return;started=true;try{await Check(args);}catch(Exception ex){File.WriteAllText(args[3]+".error",ex.ToString());Environment.ExitCode=1;}finally{Application.Exit();}};
        Application.Run();
    }
    static async Task Check(string[] args)
    {
        using(var form=(Form)Activator.CreateInstance(app.GetType("MainForm"),new object[]{true})){
            // WebView2's controller needs the WinForms shown lifecycle. The window stays
            // fully transparent and absent from the taskbar throughout the test.
            form.ShowInTaskbar=false;form.Opacity=0;form.ClientSize=new Size(1000,640);form.Show();var handle=form.Handle;
            Invoke(form,"Log","[Workspace] read_file | synthetic.txt | RETURNED");Invoke(form,"FlushLogs");
            var tabs=Field(form,"tabs");var counts=(System.Collections.Generic.List<string>)tabs.GetType().GetField("counts",Hidden).GetValue(tabs);
            Require(counts[1].Length>0&&counts[2].Length>0,"log counters did not increment");
            Invoke(form,"ClearDisplayLogs");Invoke(form,"FlushLogs");
            Require(counts[1]==""&&counts[2]=="","clearing logs left stale tab counters");
            var hostType=app.GetType("WorkbenchHost");var creating=(Task)hostType.GetMethod("CreateAsync").Invoke(null,new object[]{args[2],form,1.25});
            Require(await Task.WhenAny(creating,Task.Delay(15000))==creating,"native WebView2 initialization timed out");
            await creating;var host=creating.GetType().GetProperty("Result").GetValue(creating,null);
            try{
                var view=(WebView2)hostType.GetProperty("Control").GetValue(host,null);
                view.Bounds=new Rectangle(0,0,1000,640);view.BringToFront();view.CreateControl();
                var loaded=new TaskCompletionSource<bool>();view.CoreWebView2.NavigationCompleted+=(s,e)=>{if(e.IsSuccess)loaded.TrySetResult(true);else loaded.TrySetException(new Exception("Native navigation failed: "+e.WebErrorStatus));};
                hostType.GetMethod("Navigate").Invoke(host,new object[]{args[1]});
                Require(await Task.WhenAny(loaded.Task,Task.Delay(15000))==loaded.Task,"native navigation timed out");await loaded.Task;
                Require(view.DefaultBackgroundColor.ToArgb()==Color.FromArgb(247,247,247).ToArgb(),"host background is not #f7f7f7");
                Require(view.CoreWebView2.Profile.PreferredColorScheme==CoreWebView2PreferredColorScheme.Light,"host does not request the light color scheme");
                Require(view.Source!=null&&view.Source.Query.Contains("host=desktop"),"host did not pass the desktop signal");
                // A remembered zoom (settings.json Zoom) is applied once the page has loaded.
                Require(Math.Abs(view.ZoomFactor-1.25)<0.01&&Math.Abs((double)hostType.GetProperty("Zoom").GetValue(host,null)-1.25)<0.01,"remembered zoom was not restored");
                double seen=0;hostType.GetEvent("ZoomChanged").AddEventHandler(host,(Action<double>)(z=>seen=z));
                hostType.GetMethod("ZoomIn").Invoke(host,null);
                Require(Math.Abs(view.ZoomFactor-1.5)<0.01&&Math.Abs(seen-1.5)<0.01,"zoom step was not applied and reported");
                hostType.GetProperty("Zoom").SetValue(host,5.0,null);
                Require(Math.Abs(view.ZoomFactor-2.0)<0.01&&Math.Abs(seen-2.0)<0.01,"zoom above 200% was not clamped");
                hostType.GetProperty("Zoom").SetValue(host,0.1,null);
                Require(Math.Abs(view.ZoomFactor-0.67)<0.01&&Math.Abs(seen-0.67)<0.01,"zoom below 67% was not clamped");
                hostType.GetMethod("ZoomReset").Invoke(host,null);
                Require(Math.Abs(view.ZoomFactor-1.0)<0.01&&Math.Abs(seen-1.0)<0.01,"zoom reset was not applied and reported");
                string footer="";for(int i=0;i<50;i++){footer=await view.CoreWebView2.ExecuteScriptAsync("document.querySelector('#footer')?.textContent || ''");if(footer.Contains("2.4.0"))break;await Task.Delay(100);}
                Require(footer.Contains("2.4.1"),"embedded page did not receive the real backend version");
                string empty=await view.CoreWebView2.ExecuteScriptAsync("document.body.innerText.includes('暂无调用记录') && document.querySelectorAll('#threads .thread').length===1");
                Require(empty=="true","embedded page restored old logs or conversations");
                using(var file=File.Create(args[3]+".png"))await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
                var report=new{desktop_log_counters_cleared=true,embedded_dashboard_loaded=true,footer=footer,sdk=typeof(CoreWebView2Environment).Assembly.GetName().Version.ToString(),runtime=view.CoreWebView2.Environment.BrowserVersionString};
                File.WriteAllText(args[3]+".json",new JavaScriptSerializer().Serialize(report));
            }finally{((IDisposable)host).Dispose();}
        }
    }
}
