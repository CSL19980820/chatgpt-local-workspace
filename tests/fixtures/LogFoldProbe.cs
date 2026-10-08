// Drives the shipped LocalWorkspace.exe through reflection (C# 5, .NET Framework csc), without Tunnel or real settings.
// Usage: LogFoldProbe.exe <LocalWorkspace.exe> <sample.jsonl> <result.json> [--desktop-only]
//   default         TunnelLogFold verdicts plus the desktop raw-log rows produced by a preview MainForm
//   --desktop-only  only the desktop raw-log rows (works against builds that predate TunnelLogFold)
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;
static class LogFoldProbe
{
    static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Field(object owner, string name) { return owner.GetType().GetField(name, Hidden).GetValue(owner); }
    static object Invoke(object owner, string name, params object[] args) { return owner.GetType().GetMethod(name, Hidden).Invoke(owner, args); }
    static List<string> Rows(Form form)
    {
        var text = (string)Field(form, "logView").GetType().GetProperty("Text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).GetValue(Field(form, "logView"), null);
        return new List<string>(text.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries));
    }
    static void Feed(Form form, string[] lines)
    {
        // [Dashboard] lines would start an embedded WebView2; the fold never touches them, so they are not fed here.
        foreach (var line in lines) if (line.Length > 0 && !line.StartsWith("[Dashboard] ", StringComparison.Ordinal)) Invoke(form, "Log", line);
        for (int i = 0; i < 100 && (int)Field(form, "queued") > 0; i++) Invoke(form, "FlushLogs");
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            var app = Assembly.LoadFrom(args[0]); app.GetType("WebviewLoader").GetMethod("Hook").Invoke(null, null);
            var lines = File.ReadAllLines(args[1], Encoding.UTF8);
            var result = new Dictionary<string, object>();
            if (args.Length < 4 || args[3] != "--desktop-only")
            {
                var type = app.GetType("TunnelLogFold", true);
                var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                MethodInfo noise = type.GetMethod("IsStartupNoise", flags), same = type.GetMethod("SameBurst", flags), summary = type.GetMethod("Summary", flags);
                var verdicts = new List<bool>();
                foreach (var line in lines) if (line.Length > 0) verdicts.Add((bool)noise.Invoke(null, new object[] { line }));
                var t0 = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
                result["verdicts"] = verdicts;
                result["nullLine"] = (bool)noise.Invoke(null, new object[] { null });
                result["sameBurst3s"] = (bool)same.Invoke(null, new object[] { t0, t0.AddSeconds(3) });
                result["sameBurst11s"] = (bool)same.Invoke(null, new object[] { t0, t0.AddSeconds(11) });
                result["sameBurstBackwards"] = (bool)same.Invoke(null, new object[] { t0, t0.AddSeconds(-1) });
                result["summary"] = (string)summary.Invoke(null, new object[] { 247 });
                result["events"] = new List<string>((IEnumerable<string>)type.GetField("Events", flags).GetValue(null));
            }
            // Preview MainForm never starts Tunnel and never reads or writes settings.json; it is never shown.
            using (var form = (Form)Activator.CreateInstance(app.GetType("MainForm"), new object[] { true }))
            {
                Feed(form, lines); result["desktopRows"] = Rows(form);
                Feed(form, lines); result["desktopRowsTwice"] = Rows(form);
                Invoke(form, "ClearDisplayLogs"); Feed(form, lines); result["desktopRowsAfterClear"] = Rows(form);
            }
            File.WriteAllText(args[2], new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(result), new UTF8Encoding(false));
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(args[2] + ".error", ex.ToString()); return 1; }
    }
}
