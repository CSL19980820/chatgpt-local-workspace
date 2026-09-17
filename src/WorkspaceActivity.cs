using System;
using System.Linq;
using System.Collections.Generic;

// This bounded, process-local journal is independent of the ordered tool worker.
static class WorkspaceActivity
{
    sealed class Entry { public string Id,Tool,Target,Status,Error,Preview,ThreadId; public DateTime Started; public long Elapsed; public Dictionary<string,object> Detail; }
    sealed class Viewer { public string Id,Path,Bridge,Mode; public DateTime Seen; public int Reads; }
    static readonly object Gate=new object();
    static readonly List<Entry> Entries=new List<Entry>();
    static readonly List<Viewer> Viewers=new List<Viewer>();
    public static bool Within(string target,string root) { return string.IsNullOrEmpty(root)||target.Equals(root,StringComparison.OrdinalIgnoreCase)||target.StartsWith(root.TrimEnd('/')+"/",StringComparison.OrdinalIgnoreCase); }
    public static string Begin(string tool,string target,string thread="unassigned")
    {
        lock(Gate){var e=new Entry{Id=Guid.NewGuid().ToString("N"),Tool=tool,ThreadId=thread,Target=target,Status="running",Started=DateTime.UtcNow};Entries.Add(e);if(Entries.Count>100)Entries.RemoveAt(0);return e.Id;}
    }
    public static void Finish(string id,bool failed,long elapsed,string error,string preview,Dictionary<string,object> detail=null)
    {
        lock(Gate){var e=Entries.Find(x=>x.Id==id);if(e==null)return;e.Status=failed?"failed":"returned";e.Elapsed=elapsed;e.Error=error;e.Preview=preview==null?null:preview.Substring(0,Math.Min(preview.Length,12000));e.Detail=detail;}
    }
    // The local dashboard reads Detail and skips the larger raw receipt it never renders.
    public static object[] Read(string path,string thread="",bool receipts=true)
    {
        lock(Gate)return Entries.Where(x=>Within(x.Target,path)&&(thread.Length==0||x.ThreadId==thread)).Select(x=>new{thread_id=x.ThreadId,id=x.Id,tool=x.Tool,target=x.Target,status=x.Status,started_at=x.Started.ToString("o"),elapsed_ms=x.Status=="running"?(long)(DateTime.UtcNow-x.Started).TotalMilliseconds:x.Elapsed,error_code=x.Error,preview=receipts?x.Preview:null,detail=x.Detail,session_id=Session(x.Detail)}).ToArray();
    }
    static string Session(Dictionary<string,object> detail){object value;if(detail!=null&&detail.TryGetValue("session_id",out value))return value as string;return null;}
    public static void Seen(string id,string path,string bridge,string mode)
    {
        if(string.IsNullOrEmpty(id))return;
        if(id.Length>80||!id.All(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'))throw new ArgumentException("Invalid viewer_id");
        if(!new[]{"standard","legacy"}.Contains(bridge))throw new ArgumentException("Invalid bridge");
        if(!new[]{"inline","pip","fullscreen","unknown"}.Contains(mode))throw new ArgumentException("Invalid display_mode");
        lock(Gate){var v=Viewers.Find(x=>x.Id==id);if(v==null){v=new Viewer{Id=id};Viewers.Add(v);if(Viewers.Count>20)Viewers.RemoveAt(0);Console.Error.WriteLine("[Workspace] UI_CONNECTED | "+bridge+" | "+path);}v.Path=path;v.Bridge=bridge;v.Mode=mode;v.Seen=DateTime.UtcNow;v.Reads++;}
    }
    public static object Diagnostics(string path)
    {
        lock(Gate)return new{viewers=Viewers.Where(x=>Within(x.Path,path)).Select(x=>new{viewer_id=x.Id,path=x.Path,bridge=x.Bridge,display_mode=x.Mode,last_seen_at=x.Seen.ToString("o"),reads=x.Reads,active=(DateTime.UtcNow-x.Seen).TotalSeconds<15}).ToArray(),note="UI_CONNECTED 表示有视图正在查询状态；心跳超时也可能是页面隐藏或用户暂停。"};
    }
}
