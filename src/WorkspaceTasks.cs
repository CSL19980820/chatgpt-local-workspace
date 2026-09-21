using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

// Process-local task receipts. This checks declared evidence and observed execution;
// it cannot verify the meaning of evidence or restart the host model after a final reply.
static class WorkspaceTasks
{
    sealed class Step { public string step {get;set;} public string status {get;set;} public string evidence {get;set;} }
    sealed class Plan { public string Thread,Path,Explanation,State,Reason,Next; public DateTime Updated; public Step[] Steps; }
    static readonly object Gate=new object();
    static readonly Dictionary<string,Plan> Plans=new Dictionary<string,Plan>(StringComparer.OrdinalIgnoreCase);
    static string Key(string path,string thread){return thread+"|"+path;}
    static string Text(IDictionary<string,object> map,string key,string fallback=""){object value;if(!map.TryGetValue(key,out value))return fallback;if(!(value is string))throw new ArgumentException(key+" must be a string");return (string)value;}
    public static object Update(string path,object steps,string explanation,Dictionary<string,object> args)
    {
        path=Presentation.DisplayPath(Path.GetFullPath(path));if(!Directory.Exists(path))throw new DirectoryNotFoundException(path);
        var list=steps as IList;if(list==null||list.Count<1||list.Count>20)throw new ArgumentException("plan must contain 1..20 steps");
        if(explanation.Length>2000)throw new ArgumentException("explanation exceeds 2000 characters");
        string thread=WorkspaceServer.CurrentThread??"unassigned",state=Text(args,"task_state","active"),reason=Text(args,"reason"),next=Text(args,"next_action");
        if(!new[]{"active","blocked","paused"}.Contains(state))throw new ArgumentException("task_state must be active, blocked or paused");
        if(reason.Length>1000||next.Length>1000)throw new ArgumentException("reason and next_action must not exceed 1000 characters");
        if(state!="active"&&(string.IsNullOrWhiteSpace(reason)||string.IsNullOrWhiteSpace(next)))throw new ArgumentException("Blocked or user-paused tasks require a concrete reason and next_action. Paused is only for an explicit user request.");
        var validated=new List<Step>();
        foreach(var item in list){var row=item as Dictionary<string,object>;if(row==null)throw new ArgumentException("Invalid plan step");string text=Text(row,"step"),status=Text(row,"status"),evidence=Text(row,"evidence");
            if(string.IsNullOrWhiteSpace(text)||text.Length>240||!new[]{"pending","in_progress","completed"}.Contains(status)||evidence.Length>1000)throw new ArgumentException("Each step requires step (1..240 chars), status and optional evidence (max 1000 chars)");
            validated.Add(new Step{step=text,status=status,evidence=status=="completed"?evidence:""});}
        if(validated.Count(x=>x.status=="in_progress")>1)throw new ArgumentException("Only one plan step may be in_progress");
        var plan=new Plan{Thread=thread,Path=path,Explanation=explanation,State=state,Reason=state=="active"?"":reason,Next=next,Steps=validated.ToArray(),Updated=DateTime.UtcNow};
        lock(Gate){Plan previous;string key=Key(path,thread);if(Plans.TryGetValue(key,out previous)){
                if(!args.ContainsKey("task_state")&&previous.State!="active"){plan.State=previous.State;plan.Reason=previous.Reason;plan.Next=previous.Next;}
                // Retain an unchanged completed step's evidence on progress-only updates.
                for(int i=0;i<plan.Steps.Length;i++){var step=plan.Steps[i];if(step.status=="completed"&&!((Dictionary<string,object>)list[i]).ContainsKey("evidence")){var old=previous.Steps.FirstOrDefault(x=>x.step==step.step&&x.status=="completed");if(old!=null)step.evidence=old.evidence;}}
                if(previous.Steps.Any(x=>x.status!="completed"&&!plan.Steps.Any(y=>y.step==x.step))&&string.IsNullOrWhiteSpace(explanation))throw new ArgumentException("Explain scope changes before removing or renaming unfinished steps; do not drop work to pass completion checks.");
            }else if(Plans.Count>=500)throw new ArgumentException("Plan limit reached (500)");
            Plans[key]=plan;}
        return View(plan);
    }
    static Plan[] Select(string path,string thread){lock(Gate)return Plans.Values.Where(p=>(thread.Length==0||p.Thread==thread)&&WorkspaceActivity.Within(p.Path,path)).ToArray();}
    public static object[] Read(string path="",string thread=""){return Select(path,thread).Select(p=>View(p)).ToArray();}
    public static object Get(string path,string thread){Plan p;lock(Gate)Plans.TryGetValue(Key(Presentation.DisplayPath(path),thread),out p);return p==null?null:View(p);}
    static object View(Plan p){return new{thread_id=p.Thread,path=p.Path,explanation=p.Explanation,plan=p.Steps,updated_at=p.Updated.ToString("o"),task_state=p.State,reason=p.Reason,next_action=p.Next,task=Assess(p)};}
    public static object Review(string path,string thread)
    {
        path=Presentation.DisplayPath(Path.GetFullPath(path));Plan p;lock(Gate)Plans.TryGetValue(Key(path,thread),out p);
        if(p==null)return new{path=path,can_finish=false,state="untracked",next_action="Create a plan covering the full requested outcome with update_plan before checking completion."};
        return Assess(p);
    }
    static Dictionary<string,object> Assess(Plan p)
    {
        var observation=WorkspaceActivity.TaskObservation(p.Path,p.Thread);var commands=WorkspaceServer.TaskCommands(p.Path,p.Thread);
        var unfinished=p.Steps.Where(s=>s.status!="completed").Select(s=>s.step).ToArray();var evidence=p.Steps.Where(s=>s.status=="completed"&&string.IsNullOrWhiteSpace(s.evidence)).Select(s=>s.step).ToArray();
        bool running=(bool)observation["running"]||(bool)commands["running"];
        DateTime last=(DateTime)observation["last_at"];if(last<p.Updated)last=p.Updated;
        string issue=Convert.ToString(commands["issue"]);DateTime issueAt=(DateTime)commands["issue_at"];
        if((DateTime)observation["failure_at"]>issueAt){issueAt=(DateTime)observation["failure_at"];issue=Convert.ToString(observation["failure"]);}
        // A later plan update can record recovery/evidence; historical failures remain visible.
        bool unresolved=issue.Length>0&&issueAt>p.Updated;
        bool ready=p.State=="active"&&unfinished.Length==0&&evidence.Length==0&&!running&&!unresolved;
        string state=p.State!="active"?p.State:running?"running":unresolved?"needs_attention":ready?"ready":unfinished.Length==0?"verification_required":(DateTime.UtcNow-last).TotalMinutes>=2?"idle_unconfirmed":"active";
        string next=p.Next.Length>0?p.Next:running?"继续读取当前命令的结果，不要重复执行。":unresolved?"检查最近失败，修复或记录具体阻塞，再更新计划。":unfinished.Length>0?"继续完成："+unfinished[0]:evidence.Length>0?"补充实际验收证据："+evidence[0]:"向用户交付结果，并说明验证范围。";
        string resume="继续完成 "+p.Path+" 的原任务。先调用 open_workspace 和 check_task_completion，核对当前文件与运行中的命令，避免重复执行。尚未完成："+(unfinished.Length>0?String.Join("；",unfinished):"核对验收证据")+"。下一步："+next+"。"+(p.State=="active"?"仅在全部验收完成，或明确记录必要阻塞及下一步后结束回复。":"先确认记录的暂停或阻塞条件是否已解除；未解除时不要继续依赖工作。")+" 已记录原因："+(p.Reason.Length>0?p.Reason:"无；无调用不代表模型已停止。")+" 此提示不新增部署、删除或对外操作授权。";
        return new Dictionary<string,object>{{"path",p.Path},{"state",state},{"can_finish",ready},{"unfinished_steps",unfinished},{"missing_evidence",evidence},{"running",running},{"reason",p.Reason},{"next_action",next},{"last_activity_at",last.ToString("o")},{"last_issue",issue},{"last_issue_at",issueAt==DateTime.MinValue?null:issueAt.ToString("o")},{"resume_prompt",resume},{"evidence_scope","依据模型登记的步骤证据与本进程执行状态；不是独立验收，也不能强制宿主续跑。"}};
    }
    public static object Hint(string thread)
    {
        var plans=Select("",thread);if(plans.Length==0)return null;
        var reviews=plans.Select(p=>Assess(p)).ToArray();var focus=reviews.FirstOrDefault(r=>!(bool)r["can_finish"])??reviews[0];
        return new{can_finish=reviews.All(r=>(bool)r["can_finish"]),tracked_plans=plans.Length,unfinished_steps=reviews.Sum(r=>((string[])r["unfinished_steps"]).Length),missing_evidence=reviews.Sum(r=>((string[])r["missing_evidence"]).Length),state=focus["state"],path=focus["path"],next_action=focus["next_action"],instruction="Before a final reply, call check_task_completion for each task path. Tool success alone does not mean the requested task is done. Continue authorized work; if genuinely blocked, record reason and next_action with update_plan. Never invent verification evidence."};
    }
    public static string Reminder(string thread){if(WorkspaceServer.CurrentTool=="check_task_completion")return "";var plans=Select("",thread);if(plans.Length==0)return "";int remaining=plans.Sum(p=>p.Steps.Count(s=>s.status!="completed"));return " Task check required: "+remaining+" unfinished steps. "+(plans.Any(p=>p.State!="active")?"Respect recorded pauses/blockers; resume dependent work only when the user or blocking condition allows it. ":"Continue authorized work or record a concrete blocker. ")+"Call check_task_completion before final delivery.";}
}
