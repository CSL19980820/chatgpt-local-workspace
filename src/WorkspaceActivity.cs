using System;
using System.Linq;
using System.Collections.Generic;

// Logs and task validation are process-local; a new runtime starts without old history.
static class WorkspaceActivity
{
    public sealed class Entry { public string Id,Tool,Target,Status,Error,Preview,ThreadId,Trace,TraceId,TurnId,TurnSource; public DateTime Started; public bool Verified; public long Elapsed; public Dictionary<string,object> Detail; }
    public sealed class Proof { public string Id,Tool,Target,Status,Error,ThreadId,SessionId; public DateTime Started; public bool Verified,CommandRunning; public long Elapsed; }
    sealed class Viewer { public string Id,Path,Bridge,Mode; public DateTime Seen; public int Reads; }
    static readonly object Gate=new object();
    static readonly List<Entry> Entries=new List<Entry>();
    static readonly List<Proof> Proofs=new List<Proof>();
    // Per-conversation counters survive the 100-row timeline cap; clearing completed logs resets them.
    sealed class Stat { public int Calls,Failed; public long Added,Removed; public DateTime Last; }
    static readonly Dictionary<string,Stat> Stats=new Dictionary<string,Stat>();
    static Stat StatFor(string thread){string key=string.IsNullOrEmpty(thread)?"unassigned":thread;Stat s;if(!Stats.TryGetValue(key,out s)){s=new Stat();Stats[key]=s;}return s;}
    static readonly string[] WriteTools={"write_file","edit_file","apply_patch","restore_change"};
    // Only lines that reached the disk count: previews, rejected writes and unapplied restores do not.
    static bool CountsAsWrite(Entry e)
    {
        if(e.Detail==null||Array.IndexOf(WriteTools,e.Tool)<0)return false;
        object applied;bool hasApplied=e.Detail.TryGetValue("applied",out applied)&&applied!=null;
        if(hasApplied&&!Convert.ToBoolean(applied))return false;
        if(e.Tool=="restore_change")return hasApplied;
        return true;
    }
    static long DetailLong(Dictionary<string,object> detail,string key){object v;if(detail==null||!detail.TryGetValue(key,out v)||v==null)return 0;try{return Convert.ToInt64(v);}catch{return 0;}}
    public static Dictionary<string,object> StatsFor(string thread,int runningCommands)
    {
        lock(Gate){
            Stat s;Stats.TryGetValue(string.IsNullOrEmpty(thread)?"unassigned":thread,out s);
            var running=Entries.Where(e=>e.ThreadId==thread&&e.Status=="running").ToArray();
            DateTime last=s==null?DateTime.MinValue:s.Last;
            foreach(var e in running)if(e.Started>last)last=e.Started;
            return new Dictionary<string,object>{{"calls",s==null?0:s.Calls},{"running",running.Length+runningCommands},{"failed",s==null?0:s.Failed},{"added",s==null?0:s.Added},{"removed",s==null?0:s.Removed},{"last_at",last==DateTime.MinValue?null:last.ToString("o")}};
        }
    }
    static Proof Compact(Entry row)
    {
        object running;
        return new Proof{Id=row.Id,Tool=row.Tool,Target=row.Target,Status=row.Status,Error=row.Error,ThreadId=row.ThreadId,Started=row.Started,Verified=row.Verified,Elapsed=row.Elapsed,SessionId=Session(row.Detail),CommandRunning=row.Detail!=null&&row.Detail.TryGetValue("running",out running)&&Convert.ToBoolean(running)};
    }
    public static void Initialize(){WorkspaceStore.Delete("activity");WorkspaceStore.Delete("activity-state");}
    public static int ClearCompleted()
    {
        lock(Gate){
            int removed=Entries.RemoveAll(e=>e.Status!="running");
            // Counters restart from what is still visible: the running calls only.
            Stats.Clear();
            foreach(var e in Entries){var s=StatFor(e.ThreadId);s.Calls++;if(e.Started>s.Last)s.Last=e.Started;}
            return removed;
        }
    }
    public static int RunningCalls(){lock(Gate)return Entries.Count(e=>e.Status=="running");}
    static void RecordProof(Entry row)
    {
        Proofs.RemoveAll(p=>p.Id==row.Id);Proofs.Add(Compact(row));
        if(Proofs.Count>100)Proofs.RemoveRange(0,Proofs.Count-100);
    }
    static readonly List<Viewer> Viewers=new List<Viewer>();
    public static bool Within(string target,string root) { return string.IsNullOrEmpty(root)||target.Equals(root,StringComparison.OrdinalIgnoreCase)||target.StartsWith(root.TrimEnd('/')+"/",StringComparison.OrdinalIgnoreCase); }
    // W3C traceparent: version-traceid-parentid-flags; an all-zero trace id is invalid.
    public static string TraceId(string traceparent)
    {
        if(string.IsNullOrEmpty(traceparent))return null;
        var parts=traceparent.Trim().Split('-');if(parts.Length<4||parts[1].Length!=32)return null;
        string id=parts[1].ToLowerInvariant();if(!id.All(c=>(c>='0'&&c<='9')||(c>='a'&&c<='f'))||id.All(c=>c=='0'))return null;
        return id;
    }
    public static string Begin(string tool,string target,string thread="unassigned",string trace="",string turnId=null,string turnSource=null)
    {
        lock(Gate){var e=new Entry{Id=Guid.NewGuid().ToString("N"),Tool=tool,ThreadId=thread,Trace=trace!=null&&trace.Length>200?trace.Substring(0,200):trace,TraceId=TraceId(trace),TurnId=string.IsNullOrEmpty(turnId)?null:turnId,TurnSource=string.IsNullOrEmpty(turnId)?null:turnSource,Target=target,Status="running",Started=DateTime.UtcNow};
            var stat=StatFor(thread);stat.Calls++;if(e.Started>stat.Last)stat.Last=e.Started;Entries.Add(e);if(Entries.Count>100)Entries.RemoveAt(0);RecordProof(e);return e.Id;}
    }
    public static void Finish(string id,bool failed,long elapsed,string error,string preview,Dictionary<string,object> detail=null,bool verified=false)
    {
        lock(Gate){var e=Entries.Find(x=>x.Id==id);if(e==null)return;e.Status=failed?"failed":"returned";e.Elapsed=elapsed;e.Error=error;e.Preview=preview==null?null:preview.Substring(0,Math.Min(preview.Length,12000));e.Detail=detail;e.Verified=verified;RecordProof(e);
            var stat=StatFor(e.ThreadId);if(failed)stat.Failed++;
            if(CountsAsWrite(e)){stat.Added+=DetailLong(detail,"added");stat.Removed+=DetailLong(detail,"removed");}
            var end=e.Started.AddMilliseconds(elapsed);if(end>stat.Last)stat.Last=end;}
    }
    // The local dashboard reads Detail and skips the larger raw receipt it never renders.
    public static object[] Read(string path,string thread="",bool receipts=true)
    {
        lock(Gate)return Entries.Where(x=>Within(x.Target,path)&&(thread.Length==0||x.ThreadId==thread)).Select(x=>new{thread_id=x.ThreadId,id=x.Id,tool=x.Tool,target=x.Target,status=x.Status,started_at=x.Started.ToString("o"),elapsed_ms=x.Status=="running"?(long)(DateTime.UtcNow-x.Started).TotalMilliseconds:x.Elapsed,error_code=x.Error,trace=x.Trace,trace_id=x.TraceId,turn_id=x.TurnId,turn_source=x.TurnSource,preview=receipts?x.Preview:null,detail=x.Detail,session_id=Session(x.Detail)}).ToArray();
    }
    static string Session(Dictionary<string,object> detail){object value;if(detail!=null&&detail.TryGetValue("session_id",out value))return value as string;return null;}
    public static Dictionary<string,object> TaskObservation(string path,string thread)
    {
        lock(Gate){var rows=Proofs.Where(e=>e.ThreadId==thread&&Within(e.Target,path)&&!new[]{"update_plan","check_task_completion","get_workspace_status","read_workspace_activity","open_workspace"}.Contains(e.Tool)).ToArray();
            var failed=rows.LastOrDefault(e=>e.Status=="failed");return new Dictionary<string,object>{{"running",rows.Any(e=>e.Status=="running")},{"last_at",rows.Length==0?DateTime.MinValue:rows.Max(e=>e.Started.AddMilliseconds(e.Elapsed))},{"failure",failed==null?"":failed.Tool+": "+failed.Error},{"failure_id",failed==null?"":failed.Id},{"failure_at",failed==null?DateTime.MinValue:failed.Started.AddMilliseconds(failed.Elapsed)}};}
    }
    // The live command and its activity receipt identify the same observed failure.
    public static bool SameFailure(string first,string second,string path,string thread)
    {
        if(string.IsNullOrEmpty(first)||string.IsNullOrEmpty(second))return false;
        lock(Gate)return Proofs.Any(e=>e.Status=="failed"&&!e.CommandRunning&&!string.IsNullOrEmpty(e.SessionId)&&!(e.Error??"").StartsWith("PROCESS_RESTARTED",StringComparison.Ordinal)&&e.ThreadId==thread&&Within(e.Target,path)&&((first==e.Id&&second=="command:"+e.SessionId)||(second==e.Id&&first=="command:"+e.SessionId)));
    }
    public static bool Evidence(string id,string path,string thread,DateTime after){lock(Gate)return Proofs.Any(e=>e.Id==id&&e.ThreadId==thread&&Within(e.Target,path)&&e.Status=="returned"&&e.Verified&&e.Started>=after);}
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
