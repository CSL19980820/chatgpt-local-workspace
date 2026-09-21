using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;

// MCP uses stdio; the independent loopback dashboard only exposes read-only snapshots.
static class WorkspaceServer
{
    [ThreadStatic] static JavaScriptSerializer jsonInstance;
    // Reusable per-thread serializer: JavaScriptSerializer is not thread-safe but is safe to reuse, so cache one per thread instead of allocating an 8 MB-limit instance on every access.
    static JavaScriptSerializer Json { get { var j = jsonInstance; if (j == null) { j = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }; jsonInstance = j; } return j; } }
    static readonly Dictionary<string, Command> Commands = new Dictionary<string, Command>();
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
    [ThreadStatic] internal static string CurrentTool;
    [ThreadStatic] internal static string CurrentThread;
    static readonly object CommandsGate=new object();
    static readonly System.Collections.Concurrent.BlockingCollection<Action> Work=new System.Collections.Concurrent.BlockingCollection<Action>(128);
    public const string Version="2.2.1";
    // 2026-07-28 modern era: stateless per-request negotiation; the legacy initialize handshake keeps serving 2025-06-18 clients.
    public const string ModernVersion="2026-07-28";
    const string PvKey="io.modelcontextprotocol/protocolVersion",CapsKey="io.modelcontextprotocol/clientCapabilities",ServerInfoKey="io.modelcontextprotocol/serverInfo",TasksExt="io.modelcontextprotocol/tasks";
    public static int ToolCount {get{return Tools().Length;}}
    static readonly string InstanceId=Guid.NewGuid().ToString("N");
    static readonly object ServerInfo=new{name="local-workspace",version=Version};
    static readonly object LegacyCapabilities=new{tools=new{listChanged=false}};
    static readonly byte[] StateKey=System.Security.Cryptography.SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes("local-workspace-request-state:"+InstanceId));
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string,byte> UsedNonces=new System.Collections.Concurrent.ConcurrentDictionary<string,byte>();
    static readonly string Instructions="Complete the user's authorized outcome, not just some edits. For multi-step work, use update_plan for the full scope, keep statuses current and record actual evidence for completed steps. Before final delivery call check_task_completion. If unfinished, continue; if genuinely blocked, record reason and next_action. Only pause at the user's request. Use open_workspace before editing. Commands still running must be polled, never rerun. Group by host session metadata or register_conversation/thread_id. Full data is in structuredContent.result. Never invent evidence or infer new authorization from task state.";
    static readonly object OutputGate=new object();
    static string S(Dictionary<string, object> a, string k, string fallback = "") { object v; if(!a.TryGetValue(k,out v))return fallback;if(!(v is string))throw new ArgumentException(k+" must be a string");return (string)v; }
    static string Alias(Dictionary<string,object> a,string key,string legacy,string fallback=""){if(a.ContainsKey(key)&&a.ContainsKey(legacy)&&S(a,key)!=S(a,legacy))throw new ArgumentException(key+" and "+legacy+" conflict; provide one");return a.ContainsKey(key)?S(a,key):S(a,legacy,fallback);}
    static int WaitTime(Dictionary<string,object> a,int fallback){if(a.ContainsKey("yield_time_ms")&&a.ContainsKey("yield_ms")&&N(a,"yield_time_ms",fallback,0,10000)!=N(a,"yield_ms",fallback,0,10000))throw new ArgumentException("Conflicting yield parameters");return a.ContainsKey("yield_time_ms")?N(a,"yield_time_ms",fallback,0,10000):N(a,"yield_ms",fallback,0,10000);}
    static int N(Dictionary<string, object> a, string k, int fallback, int min, int max) { object v; if(!a.TryGetValue(k,out v)||v==null)return fallback;if(v is bool||Convert.ToDouble(v)!=Math.Truncate(Convert.ToDouble(v)))throw new ArgumentException(k+" must be an integer");int n=Convert.ToInt32(v);if(n<min||n>max)throw new ArgumentException(k+" must be between "+min+" and "+max);return n; }
    static string Full(string path,bool readRepair=false) {
        if(!Path.IsPathRooted(path)||(!path.StartsWith("\\\\")&&(path.Length<3||path[1]!=':'||(path[2]!='\\'&&path[2]!='/'))))throw new ArgumentException("Use a fully qualified absolute path returned by list_directory.");
        string full=Path.GetFullPath(path);if(File.Exists(full)||Directory.Exists(full))return full;
        string repaired=full.Replace("\\_","_");if(repaired!=full&&(File.Exists(repaired)||Directory.Exists(repaired))) {if(readRepair)return repaired;throw new ArgumentException("Path contains a likely Markdown escape. Retry with the existing path: "+Presentation.DisplayPath(repaired));}return full;
    }
    static object Result(object value, bool error = false) { var command=value as Dictionary<string,object>;if(command!=null&&command.ContainsKey("error_code")&&command["error_code"]!=null)error=true;if(command!=null&&command.ContainsKey("exit_code")&&command["exit_code"]!=null&&!Convert.ToBoolean(command["stopped"]))error=Convert.ToInt32(command["exit_code"])!=0||Convert.ToBoolean(command["timed_out"]);return new { content = new[] { new { type = "text", text = WorkspaceContracts.Summary(CurrentTool,value,error)+WorkspaceTasks.Reminder(CurrentThread??"unassigned") } }, structuredContent=new{tool=CurrentTool,result=value,isError=error,thread_id=CurrentThread??"unassigned",task=WorkspaceTasks.Hint(CurrentThread??"unassigned")}, isError = error }; }
    static string Title(string name){string[] names={"get_workspace_status","list_commands","read_command","stop_command","git_status","git_diff","read_image","create_directory","list_directory","read_file","write_file","edit_file","exec_command","poll_command","show_changes","file_info","search_files","search_text","write_stdin","open_workspace","update_plan","apply_patch"};string[] titles={"连接与工具诊断","命令会话","查看命令进度","停止命令","Git 状态","Git 差异","查看图片","创建目录","浏览目录","阅读文件","写入文件","编辑文件","执行命令","读取命令输出","修改汇总","文件信息","搜索文件","搜索内容","发送命令输入","打开工作区","更新执行计划","应用文件补丁"};int i=Array.IndexOf(names,name);return name=="check_task_completion"?"检查任务完成情况":name=="import_file"?"接收聊天附件":name=="read_workspace_activity"?"读取工作区活动":i<0?name:titles[i];}
    static object Schema(string type, string description) { return new { type = type, description = description }; }
    static object Tool(string name, string description, bool read, Dictionary<string, object> properties, params string[] required)
    {
        properties["thread_id"]=Schema("string","Optional local conversation ID. Host session metadata groups calls automatically; when unavailable, register_conversation and pass its thread_id.");
        string desc=description;
        var inputSchema=new { type = "object", properties = properties, required = required, additionalProperties = false };
        var outputSchema=WorkspaceContracts.Output(name);
        var annotations = new { readOnlyHint = read, destructiveHint = !read&&name!="register_conversation"&&name!="create_directory"&&name!="update_plan"&&name!="import_file", idempotentHint = read||name=="create_directory"||name=="stop_command"||name=="update_plan", openWorldHint = name=="exec_command"||name=="write_stdin"||name=="import_file" };
        // Legacy discovery remains icon-free for ChatGPT connector validation.
        if (EmitIcons) return new { name = name, title=Title(name), icons=new[]{new{src=IconFor(name),mimeType="image/svg+xml",sizes=new[]{"16x16"}}}, description = desc, inputSchema = inputSchema, outputSchema = outputSchema, annotations = annotations, _meta=ToolMeta(name) };
        return new { name = name, title=Title(name), description = desc, inputSchema = inputSchema, outputSchema = outputSchema, annotations = annotations, _meta=ToolMeta(name) };
    }
    static object ToolMeta(string name)
    {
        var meta=new Dictionary<string,object>{{"openai/toolInvocation/invoking",Title(name)+"…"},{"openai/toolInvocation/invoked",Title(name)+"已返回"}};
        if(name=="import_file")meta["openai/fileParams"]=new[]{"file"};
        return meta;
    }
    // Tool icons are embedded as data URIs: the server is loopback-only and ships no static assets.
    static string IconFor(string name)
    {
        string[] terminal={"exec_command","poll_command","write_stdin","list_commands","read_command","stop_command"};
        string[] git={"git_status","git_diff","show_changes"};
        string[] search={"search_files","search_text"};
        string[] write={"write_file","edit_file","create_directory","apply_patch"};
        string[] workspace={"register_conversation","open_workspace","update_plan","read_workspace_activity","get_workspace_status"};
        string svg=
            terminal.Contains(name)?"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'><rect x='1' y='2' width='14' height='12' rx='2' fill='#16a34a'/><path d='M4 6l2.5 2L4 10' stroke='#ffffff' stroke-width='1.4' fill='none'/><rect x='8' y='9.4' width='4' height='1.3' fill='#ffffff'/></svg>"
            :git.Contains(name)?"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'><circle cx='5' cy='3' r='2' fill='#ea580c'/><circle cx='5' cy='13' r='2' fill='#ea580c'/><circle cx='11' cy='6' r='2' fill='#ea580c'/><path d='M5 5v6' stroke='#ea580c' stroke-width='1.5' fill='none'/><path d='M11 8c0 2.6-2.6 2.9-4 3' stroke='#ea580c' stroke-width='1.4' fill='none'/></svg>"
            :search.Contains(name)?"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'><circle cx='7' cy='7' r='4.4' stroke='#7c3aed' stroke-width='1.7' fill='none'/><path d='M10.4 10.4L14 14' stroke='#7c3aed' stroke-width='1.9' fill='none'/></svg>"
            :write.Contains(name)?"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'><path d='M3 1h6l4 4v10H3z' fill='#2563eb'/><path d='M9 1l4 4H9z' fill='#93c5fd'/><rect x='5' y='8' width='6' height='1.2' fill='#ffffff'/><rect x='5' y='10.6' width='4' height='1.2' fill='#ffffff'/></svg>"
            :workspace.Contains(name)?"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'><rect x='1.5' y='1.5' width='5.5' height='5.5' rx='1' fill='#0891b2'/><rect x='9' y='1.5' width='5.5' height='5.5' rx='1' fill='#67e8f9'/><rect x='1.5' y='9' width='5.5' height='5.5' rx='1' fill='#67e8f9'/><rect x='9' y='9' width='5.5' height='5.5' rx='1' fill='#0891b2'/></svg>"
            :"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'><path d='M3 1h6l4 4v10H3z' fill='#64748b'/><path d='M9 1l4 4H9z' fill='#cbd5e1'/><rect x='5' y='8' width='6' height='1.2' fill='#ffffff'/><rect x='5' y='10.6' width='6' height='1.2' fill='#ffffff'/><rect x='5' y='13.2' width='3' height='1.2' fill='#ffffff'/></svg>";
        return "data:image/svg+xml;base64,"+Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
    }
    static object[] toolsCache;
    static object[] toolsModernCache;
    static string[] toolNamesCache;
    static readonly object toolsGate = new object();
    static bool EmitIcons;
    // Tool descriptors are fully static; build once and reuse instead of reconstructing 25 nested objects on every tools/list and get_workspace_status.
    // Two variants: legacy descriptors are icon-free; modern hosts additionally get icons.
    static object[] Tools()
    {
        var cached = toolsCache;
        if (cached != null) return cached;
        lock (toolsGate) { if (toolsCache == null) toolsCache = BuildTools(false); return toolsCache; }
    }
    static object[] ToolsModern()
    {
        var cached = toolsModernCache;
        if (cached != null) return cached;
        lock (toolsGate) { if (toolsModernCache == null) toolsModernCache = BuildTools(true); return toolsModernCache; }
    }
    static string[] ToolNames()
    {
        var cached = toolNamesCache;
        if (cached != null) return cached;
        lock (toolsGate) { if (toolNamesCache == null) toolNamesCache = Tools().Select(t => (string)t.GetType().GetProperty("name").GetValue(t, null)).ToArray(); return toolNamesCache; }
    }
    static object[] BuildTools(bool withIcons)
    {
        lock (toolsGate) { EmitIcons = withIcons; }
        try { return BuildToolsInner(); }
        finally { lock (toolsGate) { EmitIcons = false; } }
    }
    static object[] BuildToolsInner()
    {
        return new[] {
            Tool("register_conversation","Name or configure a conversation, or register it when host session metadata is absent. title defaults to the directory name. chat_id must be an actual known ChatGPT /c/ UUID; omit it otherwise. Use the returned thread_id for clients without host session metadata.",false,new Dictionary<string,object>{{"title",Schema("string","Optional human-readable conversation/task title, 1..120 characters; omit to auto-derive from the workspace directory name")},{"path",Schema("string","Absolute workspace directory")},{"chat_id",Schema("string","Actual known ChatGPT chat UUID, optional; never guess")}},"path"),
            Tool("read_workspace_activity","Read a non-blocking live snapshot of a workspace: running tool, recent activity, plan, bounded command output and UI connection diagnostics. This does not consume output, execute commands or change files. The local desktop app observes activity on its own; use this only when a textual snapshot is needed in chat.",true,new Dictionary<string,object>{{"path",Schema("string","Absolute workspace directory")},{"viewer_id",Schema("string","Optional UI instance identifier, max 80 characters")},{"bridge",new{type="string",@enum=new[]{"standard","legacy"}}},{"display_mode",new{type="string",@enum=new[]{"inline","pip","fullscreen","unknown"}}}},"path"),
            Tool("open_workspace","Open a workspace before coding: discover scoped AGENTS.override.md/AGENTS.md guidance, Git root, standalone skill paths, available shells and current plan. The local desktop app watches subsequent tools, plans and commands automatically. Read nested guidance again when changing directory scope. Does not grant extra permissions.",true,new Dictionary<string,object>{{"path",Schema("string","Absolute working directory")}},"path"),
            Tool("update_plan","Track the entire authorized task including validation and requested delivery. Keep actual status and evidence current. Blocked or explicitly user-paused tasks require reason and next_action. Before final delivery call check_task_completion; this process-local plan does not schedule the host.",false,new Dictionary<string,object>{{"path",Schema("string","Absolute workspace directory")},{"explanation",Schema("string","Reason for update; required when removing or renaming unfinished steps")},{"task_state",new{type="string",@enum=new[]{"active","blocked","paused"}}},{"reason",Schema("string","Concrete blocker or explicit user pause request, max 1000 chars")},{"next_action",Schema("string","Next useful action or condition needed to resume, max 1000 chars")},{"plan",new{type="array",minItems=1,maxItems=20,items=new{type="object",properties=new Dictionary<string,object>{{"step",Schema("string","Concrete step, maximum 240 characters")},{"status",new{type="string",@enum=new[]{"pending","in_progress","completed"}}},{"evidence",Schema("string","Actual verification receipt or result for a completed step, max 1000 chars; never invent it")}},required=new[]{"step","status"},additionalProperties=false}}}},"path","plan"),
            Tool("check_task_completion","Call before final delivery. Returns unfinished steps, missing declared evidence, running commands, observed failures and the next action. can_finish=false means continue authorized work or record a concrete blocker with update_plan. This check cannot validate evidence semantics or force another model turn.",true,new Dictionary<string,object>{{"path",Schema("string","Exact absolute directory used for this task's update_plan")}},"path"),
            Tool("apply_patch","Apply a Codex-style multi-file patch: *** Begin Patch, Add/Update/Delete File, optional Move to, @@ context, *** End of File, *** End Patch. File paths are relative to cwd. Validates all changes before writing; refuses ambiguous context, target overwrites and path escapes. Inspect partial/error results if disk writes fail.",false,new Dictionary<string,object>{{"cwd",Schema("string","Absolute workspace directory")},{"patch",Schema("string","Complete Codex patch text")}},"cwd","patch"),
            Tool("get_workspace_status","Diagnose the actual connected server: version, instance, executable, complete available tool names, running command count and recent operation outcomes. Call at the start of a workspace task; do not infer read-only access from missing cached tools.",true,new Dictionary<string,object>()),
            Tool("list_commands","List command sessions owned by this server, including running/completed status and exit codes. Does not consume output.",true,new Dictionary<string,object>()),
            Tool("read_command","Read a bounded cumulative snapshot of command output without consuming it. Safe for UI auto-refresh and repeated inspection; does not stop or restart a command.",true,new Dictionary<string,object>{{"session_id",Schema("string","Session returned by exec_command")}},"session_id"),
            Tool("stop_command","Explicitly stop an owned command process tree. Returns the final output snapshot. Does not stop unrelated processes.",false,new Dictionary<string,object>{{"session_id",Schema("string","Session returned by exec_command")}},"session_id"),
            Tool("write_stdin","Continue a command session: omit chars or pass empty chars to poll new output; send chars to stdin; Ctrl-C (U+0003) stops the process tree. Include newline when needed. text is a legacy alias for chars. Pipes only, not PTY. Do not blindly retry sent input.",false,new Dictionary<string,object>{{"session_id",Schema("string","Session returned by exec_command")},{"chars",Schema("string","Exact input, or empty/omitted to poll")},{"text",Schema("string","Legacy alias for chars")},{"yield_time_ms",Schema("integer","Wait 0..10000 ms, default 1000")},{"close",Schema("boolean","Close stdin after writing, default false")}},"session_id"),
            Tool("git_status","Read Git working-tree status, including edits made through commands or external editors. Requires Git on PATH; does not stage, commit or push.",true,new Dictionary<string,object>{{"path",Schema("string","Existing repository directory")}},"path"),
            Tool("git_diff","Read actual Git diff for a repository directory. Defaults to unstaged tracked edits; staged=true reads the index diff. Untracked files are listed by git_status, not diff. External diff drivers and text conversion are disabled.",true,new Dictionary<string,object>{{"path",Schema("string","Existing repository directory")},{"staged",Schema("boolean","Read staged changes instead of unstaged")}},"path"),
            Tool("import_file","Save an attachment supplied in this chat to a new absolute file path. Accepts the host-provided file object; never invent download URLs. Maximum 32 MiB; destination directory must exist. Existing files are never overwritten; expired URLs require reattaching the file.",false,new Dictionary<string,object>{{"path",Schema("string","New absolute destination file path, including filename")},{"file",new{type="object",properties=new{download_url=new{type="string"},file_id=new{type="string"},mime_type=new{type="string"},file_name=new{type="string"}},required=new[]{"download_url","file_id"},additionalProperties=false}}},"path","file"),
            Tool("read_image","Read a local PNG, JPEG, GIF or WebP image as native MCP image content for visual inspection. Maximum 4 MiB. Not a screen capture tool.",true,new Dictionary<string,object>{{"path",Schema("string","Absolute image file path")}},"path"),
            Tool("create_directory","Create a directory and missing parent directories. Existing directories succeed without changes; does not delete or replace files.",false,new Dictionary<string,object>{{"path",Schema("string","Absolute directory path")}},"path"),
            Tool("list_directory", "List a Windows directory with pagination. Empty path lists available disk roots.", true, new Dictionary<string,object>{{"path",Schema("string","Absolute path, or empty for disks")},{"offset",Schema("integer","Starting item, default 0")},{"limit",Schema("integer","Page size 1..500, default 100")}}),
            Tool("read_file", "Read a text file by line, with line numbers and an explicit continuation. UTF-8 and BOM encodings are supported.", true, new Dictionary<string,object>{{"path",Schema("string","Absolute file path")},{"start_line",Schema("integer","First line, 1-based")},{"limit",Schema("integer","Lines 1..1000, default 200")}}, "path"),
            Tool("write_file", "Write UTF-8 text to a file. Existing files require overwrite=true. Creates parent directories.", false, new Dictionary<string,object>{{"path",Schema("string","Absolute file path")},{"content",Schema("string","Complete new content")},{"overwrite",Schema("boolean","Explicitly allow replacing an existing file")}}, "path","content"),
            Tool("edit_file", "Replace one exact occurrence of old_text. Fails if missing or ambiguous; preserves the existing text encoding and BOM.", false, new Dictionary<string,object>{{"path",Schema("string","Absolute file path")},{"old_text",Schema("string","Nonempty exact text occurring once")},{"new_text",Schema("string","Replacement text")}}, "path","old_text","new_text"),
            Tool("exec_command", "Run a hidden shell command. Prefer cmd, cwd and yield_time_ms; command/yield_ms remain compatible aliases. Returns session_id when still running; continue with write_stdin instead of rerunning. Git Bash is the default (shell=git_bash; bash alias supported). Use shell=powershell or shell=pwsh explicitly for PowerShell syntax. No automatic fallback between shells. tty=true is explicitly unsupported.", false, new Dictionary<string,object>{{"cmd",Schema("string","Shell command")},{"command",Schema("string","Legacy alias for cmd")},{"cwd",Schema("string","Existing absolute working directory")},{"shell",new{type="string",@enum=new[]{"git_bash","bash","powershell","pwsh"}}},{"tty",Schema("boolean","Only false is supported; no pseudo-terminal")},{"yield_time_ms",Schema("integer","Wait 0..10000 ms, default 1000")},{"yield_ms",Schema("integer","Legacy alias for yield_time_ms")},{"timeout_seconds",Schema("integer","Terminate after 1..3600 seconds, default 300")}}, "cwd"),
            Tool("poll_command", "Read new output and exit status of a command session. Set stop=true to terminate that command tree.", false, new Dictionary<string,object>{{"session_id",Schema("string","Session returned by exec_command")},{"stop",Schema("boolean","Stop the process tree")},{"yield_ms",Schema("integer","Wait 0..10000 ms, default 1000")}}, "session_id"),
            Tool("show_changes", "Show the accumulated before/after file changes recorded by write_file/edit_file in this server process, scoped to a directory. Does not include shell or external edits and does not reset the review.",true,new Dictionary<string,object>{{"path",Schema("string","Absolute directory to review")}},"path"),
            Tool("file_info","Read file/directory size, timestamps and attributes.",true,new Dictionary<string,object>{{"path",Schema("string","Absolute path")}},"path"),
            Tool("search_files","Search filenames using * and ? wildcards, with pagination. Reparse points are skipped; budget limits and omissions are explicit.",true,new Dictionary<string,object>{{"path",Schema("string","Directory to search")},{"pattern",Schema("string","Filename wildcard, default *")},{"recursive",Schema("boolean","Include subdirectories, default true")},{"offset",Schema("integer","Match offset, default 0")},{"limit",Schema("integer","Maximum 1..200 matches, default 50")}},"path"),
            Tool("search_text","Search literal text in files; returns paths, line/column and excerpts. Files over 2 MiB and detected binary files are skipped. Partial scans are explicitly marked.",true,new Dictionary<string,object>{{"path",Schema("string","Directory to search")},{"query",Schema("string","Nonempty literal text")},{"pattern",Schema("string","Filename wildcard, default *")},{"recursive",Schema("boolean","Include subdirectories, default true")},{"case_sensitive",Schema("boolean","Case-sensitive matching, default false")},{"offset",Schema("integer","Match offset, default 0")},{"limit",Schema("integer","Maximum 1..200 matches, default 50")}},"path","query")
        };
    }
    sealed class Command : IDisposable
    {
        public Process Process; public string Text,Cwd,Shell,ThreadId;public DateTime Started=DateTime.UtcNow;public DateTime Deadline; public System.Threading.Timer Timeout; public System.Threading.Tasks.Task OutputReader,ErrorReader; public readonly StringBuilder Output = new StringBuilder(),History=new StringBuilder(); public readonly object Gate = new object(); public bool Truncated,HistoryTruncated,TimedOut,Stopped,CancelRequested;
        public void Append(string line){if(line!=null)AppendRaw(line+Environment.NewLine);}
        public void AppendRaw(string text) { lock(Gate) { Output.Append(text);History.Append(text); if (Output.Length > 128000) { Output.Remove(0, Output.Length - 128000); Truncated = true; }if(History.Length>128000){History.Remove(0,History.Length-128000);HistoryTruncated=true;} } }
        public async System.Threading.Tasks.Task Pump(StreamReader reader){char[] buffer=new char[4096];int count;try{while((count=await reader.ReadAsync(buffer,0,buffer.Length))>0)AppendRaw(new string(buffer,0,count));}catch(ObjectDisposedException){}catch(IOException ex){AppendRaw("\n[output stream error] "+ex.Message+"\n");}}
        public void Stop() { if (Process.HasExited) return; using(var kill = ProcessStart("taskkill.exe", "/PID " + Process.Id + " /T /F", Environment.CurrentDirectory)) { kill.WaitForExit(5000); } }
        public void Dispose() { if(Timeout!=null)Timeout.Dispose();try { Stop(); } catch {} Process.Dispose(); }
    }
    static string QuoteArgument(string value)
    {
        var b=new StringBuilder("\"");int slashes=0;
        foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"'){b.Append('\\',slashes*2+1);b.Append(c);}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}
        b.Append('\\',slashes*2);b.Append('"');return b.ToString();
    }
    static Process ProcessStart(string exe, string args, string cwd)
    {
        return Process.Start(new ProcessStartInfo(exe,args) { UseShellExecute=false, CreateNoWindow=true, WorkingDirectory=cwd, RedirectStandardOutput=true, RedirectStandardError=true });
    }
    static object Snapshot(string id, int wait, bool stop, bool consume=true)
    {
        Command c; if (!Commands.TryGetValue(id,out c)) throw new ArgumentException("Unknown or expired session_id");
        if(stop){c.Stopped=true;c.Stop();}
        if (!c.Process.HasExited && DateTime.UtcNow >= c.Deadline) { c.TimedOut=true; c.Stop(); }
        int remaining=(int)Math.Max(0,Math.Min(wait,(c.Deadline-DateTime.UtcNow).TotalMilliseconds));
        c.Process.WaitForExit(remaining);
        if(!c.Process.HasExited && DateTime.UtcNow >= c.Deadline) { c.TimedOut=true;c.Stop(); }
        bool done=c.Process.HasExited;if(done&&c.OutputReader!=null)System.Threading.Tasks.Task.WaitAll(new[]{c.OutputReader,c.ErrorReader},1000);
        lock(c.Gate) { string text=consume?c.Output.ToString():c.History.ToString();bool truncated=consume?c.Truncated:c.HistoryTruncated;if(consume){c.Output.Clear();c.Truncated=false;}return new Dictionary<string,object>{{"thread_id",c.ThreadId},{"session_id",id},{"running",!done},{"exit_code",done?(int?)c.Process.ExitCode:null},{"output",text},{"full_output",c.History.ToString()},{"output_mode",consume?"delta":"snapshot"},{"truncated",truncated||c.HistoryTruncated},{"timed_out",c.TimedOut},{"stopped",c.Stopped},{"command",c.Text},{"shell",c.Shell},{"shell_executable",Presentation.DisplayPath(c.Process.StartInfo.FileName)},{"cwd",Presentation.DisplayPath(c.Cwd)},{"elapsed_seconds",Math.Round(((done?c.Process.ExitTime.ToUniversalTime():DateTime.UtcNow)-c.Started).TotalSeconds,1)}}; }
    }
    static object CommandList(){return Commands.Select(x=>new{session_id=x.Key,running=!x.Value.Process.HasExited,exit_code=x.Value.Process.HasExited?(int?)x.Value.Process.ExitCode:null,command=x.Value.Text,shell=x.Value.Shell,cwd=Presentation.DisplayPath(x.Value.Cwd),elapsed_seconds=Math.Round(((x.Value.Process.HasExited?x.Value.Process.ExitTime.ToUniversalTime():DateTime.UtcNow)-x.Value.Started).TotalSeconds,1),timed_out=x.Value.TimedOut}).ToArray();}
    public static Dictionary<string,object> TaskCommands(string path,string thread)
    {
        lock(CommandsGate){var commands=Commands.Values.Where(c=>c.ThreadId==thread&&WorkspaceActivity.Within(Presentation.DisplayPath(c.Cwd),path)).ToArray();
            var failed=commands.Where(c=>c.TimedOut||c.Stopped||(c.Process.HasExited&&c.Process.ExitCode!=0)).OrderByDescending(c=>c.Process.HasExited?c.Process.ExitTime.ToUniversalTime():c.Started).FirstOrDefault();
            return new Dictionary<string,object>{{"running",commands.Any(c=>!c.Process.HasExited)},{"issue",failed==null?"":failed.TimedOut?"COMMAND_TIMEOUT":failed.Stopped?"COMMAND_STOPPED":"COMMAND_FAILED"},{"issue_at",failed==null?DateTime.MinValue:failed.Process.HasExited?failed.Process.ExitTime.ToUniversalTime():failed.Started}};}
    }
    // Git must not inherit MCP stdin: the transport reader now waits concurrently on that pipe.
    static object GitRead(string name,Dictionary<string,object> a){string path=Full(S(a,"path"),true);if(!Directory.Exists(path))throw new DirectoryNotFoundException(path);object staged;string args=name=="git_status"?"--no-optional-locks -c core.fsmonitor=false status --short --branch --untracked-files=normal":"--no-optional-locks -c core.fsmonitor=false diff --no-ext-diff --no-textconv --no-color "+(a.TryGetValue("staged",out staged)&&Convert.ToBoolean(staged)?"--cached ":"")+"--";using(var c=new Command()){c.Text="git "+args;c.Cwd=path;c.Process=new Process{StartInfo=new ProcessStartInfo("git.exe",args){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=path,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Utf8,StandardErrorEncoding=Utf8}};c.Process.OutputDataReceived+=(s,e)=>c.Append(e.Data);c.Process.ErrorDataReceived+=(s,e)=>c.Append(e.Data);c.Process.Start();c.Process.StandardInput.Close();c.Process.BeginOutputReadLine();c.Process.BeginErrorReadLine();if(!c.Process.WaitForExit(15000)){c.TimedOut=true;c.Stop();}c.Process.WaitForExit();return Result(new{path=Presentation.DisplayPath(path),output=c.History.ToString(),exit_code=c.Process.ExitCode,truncated=c.HistoryTruncated,timed_out=c.TimedOut},c.TimedOut||c.Process.ExitCode!=0);}}
    static object ReadImage(string path){byte[] bytes;using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(f.Length>4*1024*1024)throw new IOException("Image exceeds 4 MiB; resize or crop it before reading.");bytes=new byte[f.Length];int offset=0;while(offset<bytes.Length){int n=f.Read(bytes,offset,bytes.Length-offset);if(n==0)throw new EndOfStreamException();offset+=n;}}string mime=null;if(bytes.Length>=8&&bytes.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))mime="image/png";else if(bytes.Length>=3&&bytes[0]==255&&bytes[1]==216&&bytes[2]==255)mime="image/jpeg";else if(bytes.Length>=6&&(Encoding.ASCII.GetString(bytes,0,6)=="GIF87a"||Encoding.ASCII.GetString(bytes,0,6)=="GIF89a"))mime="image/gif";else if(bytes.Length>=12&&Encoding.ASCII.GetString(bytes,0,4)=="RIFF"&&Encoding.ASCII.GetString(bytes,8,4)=="WEBP")mime="image/webp";if(mime==null)throw new ArgumentException("Unsupported image data; use PNG, JPEG, GIF or WebP.");var value=new{path=Presentation.DisplayPath(path),mime_type=mime,size_bytes=bytes.Length,is_image=true,preview_url=DashboardImages.Add(bytes,mime)};return new{content=new object[]{new{type="text",text="Image read from "+Presentation.DisplayPath(path)+" ("+bytes.Length+" bytes)."},new{type="image",data=Convert.ToBase64String(bytes),mimeType=mime}},structuredContent=new{tool=CurrentTool,result=value,isError=false,thread_id=CurrentThread??"unassigned",task=WorkspaceTasks.Hint(CurrentThread??"unassigned")},isError=false};}
    static object Call(string name, Dictionary<string,object> a)
    {
        CurrentTool=name;
        if(name=="register_conversation"){
            string path=Full(S(a,"path"),true);if(!Directory.Exists(path))throw new DirectoryNotFoundException(path);
            object registration=WorkspaceThreads.Register(S(a,"title"),Presentation.DisplayPath(path),S(a,"chat_id"),S(a,"thread_id"));
            string id=(string)registration.GetType().GetProperty("thread_id").GetValue(registration,null);
            string resolvedTitle=(string)registration.GetType().GetProperty("title").GetValue(registration,null);
            Console.Error.WriteLine("[Workspace] [thread="+id+"] 对话登记 | "+resolvedTitle.Replace("\n"," ").Replace("\r"," ")+" | "+LocalDashboard.Url+"#thread="+id);
            CurrentThread=id;return Result(registration);
        }
        if(name=="read_workspace_activity")return Result(LiveSnapshot(a));
        if(name=="import_file"){object file;if(!a.TryGetValue("file",out file)||!(file is Dictionary<string,object>))throw new ArgumentException("file must be a host-provided attachment object");return Result(AttachmentImport.Import((Dictionary<string,object>)file,Full(S(a,"path"))));}
        if(name=="get_workspace_status")return Result(new{version=Version,host_session_observed=WorkspaceDiagnostics.HostSessionObserved,thread_id=CurrentThread??"unassigned",protocol_versions=new[]{"2025-06-18 (legacy initialize)","2026-07-28 (modern stateless: server/discover, MRTR, tasks extension)"},default_shell=WorkspaceContext.DefaultShell,instance_id=InstanceId,dashboard_url=LocalDashboard.Url,conversations=WorkspaceThreads.List(),executable=Presentation.DisplayPath(System.Windows.Forms.Application.ExecutablePath),tool_count=ToolCount,tools=ToolNames(),running_commands=Commands.Count(x=>!x.Value.Process.HasExited),activity=WorkspaceActivity.Read("","",false),plans=WorkspaceContext.AllPlans(),ui=WorkspaceActivity.Diagnostics(""),scope="当前 MCP 进程；其他连接、过去进程和模型思考不可见。若工具缺失，请刷新宿主工具元数据并核对实际连接。"});
        if(name=="open_workspace")return Result(WorkspaceContext.Open(Full(S(a,"path"),true)));
        if(name=="update_plan"){object plan;if(!a.TryGetValue("plan",out plan))throw new ArgumentException("plan is required");return Result(WorkspaceTasks.Update(Full(S(a,"path")),plan,S(a,"explanation"),a));}
        if(name=="check_task_completion")return Result(WorkspaceTasks.Review(Full(S(a,"path")),CurrentThread??"unassigned"));
        if(name=="apply_patch")return Result(PatchEditor.Apply(Full(S(a,"cwd")),S(a,"patch")));
        if(name=="list_commands")return Result(new{commands=CommandList(),count=Commands.Count});
        if(name=="read_command"||name=="stop_command")return Result(Snapshot(S(a,"session_id"),0,name=="stop_command",false));
        if(name=="write_stdin"){
            string id=S(a,"session_id"),input=Alias(a,"chars","text");Command c;if(!Commands.TryGetValue(id,out c))throw new ArgumentException("Unknown or expired session_id");if(input.Length>65536)throw new ArgumentException("Input exceeds 65536 characters");object close;bool closeInput=a.TryGetValue("close",out close)&&Convert.ToBoolean(close);int wait=WaitTime(a,1000);
            if(input=="\u0003")return Result(Snapshot(id,wait,true));
            if(input.Length>0||closeInput){if(c.Process.HasExited)throw new ArgumentException("Cannot write to a completed command session");var sending=System.Threading.Tasks.Task.Run(()=>{c.Process.StandardInput.Write(input);c.Process.StandardInput.Flush();if(closeInput)c.Process.StandardInput.Close();});if(!sending.Wait(1500)){c.Stopped=true;c.Stop();try{sending.Wait(1000);}catch{}throw new IOException("Command did not accept stdin; its process tree was stopped to unblock the server. Do not retry the input blindly.");}}
            return Result(Snapshot(id,wait,false));
        }
        if(name=="git_status"||name=="git_diff")return GitRead(name,a);
        if(name=="read_image")return ReadImage(Full(S(a,"path"),true));
        if(name=="create_directory"){string path=Full(S(a,"path"));bool existed=Directory.Exists(path);Directory.CreateDirectory(path);return Result(new{path=Presentation.DisplayPath(path),created=!existed,exists=true});}
        if(name=="file_info")return Result(FileSearch.Info(Full(S(a,"path"),true)));
        if(name=="search_files"||name=="search_text"){object value;bool recursive=!a.TryGetValue("recursive",out value)||value==null||Convert.ToBoolean(value);bool sensitive=a.TryGetValue("case_sensitive",out value)&&value!=null&&Convert.ToBoolean(value);return Result(FileSearch.Find(Full(S(a,"path"),true),S(a,"pattern","*"),S(a,"query"),name=="search_text",recursive,sensitive,N(a,"offset",0,0,int.MaxValue),N(a,"limit",50,1,200)));}
        string[] required = name=="write_file"?new[]{"path","content"}:name=="edit_file"?new[]{"path","old_text","new_text"}:name=="read_file"||name=="show_changes"?new[]{"path"}:name=="exec_command"?new[]{"cwd"}:name=="poll_command"?new[]{"session_id"}:new string[0];
        foreach(string field in required)if(!a.ContainsKey(field)||!(a[field] is string))throw new ArgumentException(field+" is required and must be a string");
        if(name=="list_directory") {
            string requested=S(a,"path");if(requested=="")return Result(new{path="",exists=true,entries=Directory.GetLogicalDrives().Select(p=>new{name=p,path=Presentation.DisplayPath(p),directory=true}).ToArray(),next_offset=(int?)null});
            string path=Full(requested,true);if(!Directory.Exists(path))throw new DirectoryNotFoundException("Directory not found: "+Presentation.DisplayPath(path));
            int offset=N(a,"offset",0,0,int.MaxValue),limit=N(a,"limit",100,1,500);var all=new DirectoryInfo(path).EnumerateFileSystemInfos().OrderBy(f=>(f.Attributes&FileAttributes.Directory)==0).ThenBy(f=>f.Name,StringComparer.OrdinalIgnoreCase).ToArray();var items=all.Skip(offset).Take(limit).ToArray();
            return Result(new{path=Presentation.DisplayPath(path),requested_path=requested,path_corrected=!path.Equals(Path.GetFullPath(requested),StringComparison.OrdinalIgnoreCase),exists=true,empty=all.Length==0,total_entries=all.Length,returned_count=items.Length,offset=offset,entries=items.Select(f=>new{name=f.Name,path=Presentation.DisplayPath(f.FullName),directory=(f.Attributes&FileAttributes.Directory)!=0}).ToArray(),next_offset=offset+items.Length<all.Length?(int?)(offset+items.Length):null});
        }
        if(name=="show_changes")return Result(Presentation.Review(Full(S(a,"path"),true)));
        if(name=="read_file") {
            string requested=S(a,"path"),path=Full(requested,true);int start=N(a,"start_line",1,1,int.MaxValue),limit=N(a,"limit",200,1,1000);var lines=new List<string>();int number=0;bool more=false;int chars=0;
            using(var r=new StreamReader(path,Utf8,true)) {string line;while((line=r.ReadLine())!=null){number++;if(number<start)continue;if(lines.Count>=limit || chars>100000){more=true;break;}if(line.Length>100000)throw new IOException("Line exceeds 100000 characters; use a scoped shell read.");lines.Add(number+": "+line);chars+=line.Length;}}
            return Result(new{path=Presentation.DisplayPath(path),requested_path=requested,path_corrected=!path.Equals(Path.GetFullPath(requested),StringComparison.OrdinalIgnoreCase),lines=lines,start_line=start,returned_count=lines.Count,next_line=more?(int?)number:null});
        }
        if(name=="write_file") {
            string path=Full(S(a,"path"));object v;bool overwrite=a.TryGetValue("overwrite",out v)&&Convert.ToBoolean(v);string content=S(a,"content");Directory.CreateDirectory(Path.GetDirectoryName(path));string before="";bool existed=File.Exists(path);
            using(var f=new FileStream(path,overwrite?FileMode.OpenOrCreate:FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)){
                if(f.Length>8*1024*1024)throw new IOException("File exceeds the 8 MiB reviewed-write limit; use a scoped command for large data files.");
                using(var reader=new StreamReader(f,Utf8,true,4096,true))before=reader.ReadToEnd();byte[] bytes=Utf8.GetBytes(content);f.Position=0;f.Write(bytes,0,bytes.Length);f.SetLength(f.Position);
            }
            Presentation.Record(path,before,content);return Result(new{path=Presentation.DisplayPath(path),written=true,created=!existed,diff=Presentation.Diff(before,content)});
        }
        if(name=="edit_file") {
            string path=Full(S(a,"path")),old=S(a,"old_text"),replacement=S(a,"new_text");if(old.Length==0)throw new ArgumentException("old_text must not be empty");
            string previous="",updatedText="";using(var f=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
                if(f.Length>8*1024*1024)throw new IOException("File too large for exact edit");byte[] bytes=new byte[f.Length];int count=0;while(count<bytes.Length){int n=f.Read(bytes,count,bytes.Length-count);if(n==0)break;count+=n;}
                string content;Encoding enc;using(var mem=new MemoryStream(bytes))using(var r=new StreamReader(mem,Utf8,true)){content=r.ReadToEnd();enc=r.CurrentEncoding;}
                int at=content.IndexOf(old,StringComparison.Ordinal);if(at<0||content.IndexOf(old,at+old.Length,StringComparison.Ordinal)>=0)throw new ArgumentException("old_text must occur exactly once");
                string updated=content.Substring(0,at)+replacement+content.Substring(at+old.Length);previous=content;updatedText=updated;byte[] pre=enc.GetPreamble();bool bom=pre.Length>0&&bytes.Take(pre.Length).SequenceEqual(pre);byte[] body=enc.GetBytes(updated);f.Position=0;if(bom)f.Write(pre,0,pre.Length);f.Write(body,0,body.Length);f.SetLength(f.Position);
            } Presentation.Record(path,previous,updatedText);return Result(new{path=Presentation.DisplayPath(path),edited=true,diff=Presentation.Diff(previous,updatedText)});
        }
        if(name=="exec_command") {
            lock(CommandsGate){foreach(var expiredId in Commands.Where(x=>x.Value.Process.HasExited).OrderByDescending(x=>x.Value.Started).Skip(32).Select(x=>x.Key).ToArray()){Commands[expiredId].Dispose();Commands.Remove(expiredId);}}if(Commands.Count(x=>!x.Value.Process.HasExited)>=16)throw new Exception("Too many running commands; stop or wait for an existing session");
            string cwd=Full(S(a,"cwd"));if(!Directory.Exists(cwd))throw new DirectoryNotFoundException(cwd);string command=Alias(a,"cmd","command");if(command.Length==0)throw new ArgumentException("cmd is empty");int wait=WaitTime(a,1000);object tty;if(a.TryGetValue("tty",out tty)&&Convert.ToBoolean(tty))throw new ArgumentException("tty=true is unsupported; this server provides pipes, not a PTY");string shell=WorkspaceContext.NormalizeShell(S(a,"shell",WorkspaceContext.DefaultShell)),executable=WorkspaceContext.ShellPath(shell);
            var c=new Command{ThreadId=CurrentThread,Text=command,Cwd=cwd,Shell=shell,Deadline=DateTime.UtcNow.AddSeconds(N(a,"timeout_seconds",300,1,3600))};
            string pre="$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='Stop'; [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false); $OutputEncoding=[Console]::OutputEncoding; ";
            string encoded=Convert.ToBase64String(Encoding.Unicode.GetBytes(pre+command));c.Process=new Process{StartInfo=new ProcessStartInfo(executable,shell=="git_bash"?"--noprofile --norc -c "+QuoteArgument(command):"-NoLogo -NoProfile -NonInteractive -OutputFormat Text -EncodedCommand "+encoded){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=cwd,StandardOutputEncoding=Utf8,StandardErrorEncoding=Utf8}};
            c.Process.Start();c.OutputReader=c.Pump(c.Process.StandardOutput);c.ErrorReader=c.Pump(c.Process.StandardError);c.Timeout=new System.Threading.Timer(_=>{try{if(!c.Process.HasExited){c.TimedOut=true;c.Stop();}}catch{}},null,Math.Max(1,(int)(c.Deadline-DateTime.UtcNow).TotalMilliseconds),System.Threading.Timeout.Infinite);string id=Guid.NewGuid().ToString("N");lock(CommandsGate)Commands[id]=c;
            return Result(Snapshot(id,wait,false));
        }
        if(name=="poll_command"){object v;return Result(Snapshot(S(a,"session_id"),N(a,"yield_ms",1000,0,10000),a.TryGetValue("stop",out v)&&Convert.ToBoolean(v)));}
        throw new ArgumentException("Unknown tool: "+name);
    }
    static object LiveSnapshot(Dictionary<string,object> args, bool local=false)
    {
        string path=local?"":Presentation.DisplayPath(Full(S(args,"path"),true)).TrimEnd('/');
        string thread=S(args,"thread_id");if(thread.Length>0)WorkspaceThreads.Validate(thread);
        WorkspaceActivity.Seen(S(args,"viewer_id"),path,S(args,"bridge","standard"),S(args,"display_mode","unknown"));
        var commands=new List<object>();int omitted=0;
        lock(CommandsGate){var selected=Commands.Where(x=>WorkspaceActivity.Within(Presentation.DisplayPath(x.Value.Cwd).TrimEnd('/'),path)&&(thread.Length==0||x.Value.ThreadId==thread)).OrderBy(x=>x.Value.Process.HasExited).ThenByDescending(x=>x.Value.Started).ToArray();omitted=Math.Max(0,selected.Length-8);foreach(var pair in selected.Take(8)){var c=pair.Value;lock(c.Gate){bool done=c.Process.HasExited;string output=c.History.ToString();commands.Add(new{thread_id=c.ThreadId,started_at=c.Started.ToString("o"),session_id=pair.Key,command=c.Text,cwd=Presentation.DisplayPath(c.Cwd),shell=c.Shell,running=!done,exit_code=done?(int?)c.Process.ExitCode:null,elapsed_seconds=Math.Round(((done?c.Process.ExitTime.ToUniversalTime():DateTime.UtcNow)-c.Started).TotalSeconds,1),output=output.Substring(Math.Max(0,output.Length-8000)),truncated=c.HistoryTruncated||output.Length>8000,timed_out=c.TimedOut,stopped=c.Stopped});}}}
        return new{version=Version,instance_id=InstanceId,dashboard_url=LocalDashboard.Url,conversations=WorkspaceThreads.List(),thread_id=thread,path=path,checked_at=DateTime.UtcNow.ToString("o"),default_shell=WorkspaceContext.DefaultShell,activity=WorkspaceActivity.Read(path,thread,!local),plans=WorkspaceContext.PlansWithin(path,thread),commands=commands,omitted_commands=omitted,queued_calls=Work.Count,ui=WorkspaceActivity.Diagnostics(path),scope="仅当前 MCP 进程与此目录及子目录。没有工具调用不代表模型已完成；模型思考不可见。历史上限 100 条，命令输出为尾部快照；每条调用附带有界的结构化详情。"};
    }
    static Dictionary<string,object> PrepareCall(Dictionary<string,object> call)
    {
        CurrentTool=S(call,"name");CurrentThread="unassigned";
        var args=call.ContainsKey("arguments")?new Dictionary<string,object>((Dictionary<string,object>)call["arguments"]):new Dictionary<string,object>();
        call["arguments"]=args;
        object value;var meta=call.TryGetValue("_meta",out value)?value as Dictionary<string,object>:null;
        string key=WorkspaceThreads.HostKey(meta),path="";
        if(key.Length>0){WorkspaceDiagnostics.HostSessionObserved=true;string target=S(args,args.ContainsKey("cwd")?"cwd":"path");try{if(Path.IsPathRooted(target)){string full=Path.GetFullPath(target);path=Directory.Exists(full)?full:Path.GetDirectoryName(full);if(!Directory.Exists(path))path="";}}catch{} }
        CurrentThread=WorkspaceThreads.Resolve(key,S(args,"thread_id"),Presentation.DisplayPath(path));
        if(CurrentThread!="unassigned")args["thread_id"]=CurrentThread;
        return args;
    }
    static object RunCall(Dictionary<string,object> p,StreamWriter output,string trace)
    {
        string name=S(p,"name");var args=PrepareCall(p);
        string target=args.ContainsKey("path")?Convert.ToString(args["path"]):args.ContainsKey("cwd")?Convert.ToString(args["cwd"]):args.ContainsKey("session_id")?Convert.ToString(args["session_id"]):"";
        if(name=="register_conversation"){var registered=Call(name,args);WorkspaceDiagnostics.Completed(name,false);return registered;}
        if(args.ContainsKey("session_id")){Command owner;if(Commands.TryGetValue(S(args,"session_id"),out owner)){target=owner.Cwd;if(args.ContainsKey("thread_id")&&CurrentThread!=owner.ThreadId)return Result(new{error_code="THREAD_MISMATCH",message="Command belongs to another conversation"},true);CurrentThread=owner.ThreadId;}}
        string logPrefix="[Workspace] [thread="+CurrentThread+"] ";
        string activityId=WorkspaceActivity.Begin(name,Presentation.DisplayPath(target).TrimEnd('/'),CurrentThread,trace);
        var watch=Stopwatch.StartNew();string started=DateTime.UtcNow.ToString("o");object token=null,meta;if(p.TryGetValue("_meta",out meta)&&meta is Dictionary<string,object>)((Dictionary<string,object>)meta).TryGetValue("progressToken",out token);
        bool active=true;int step=0;object progressGate=new object();
        Action<string> progress=message=>{lock(progressGate){if(!active||token==null)return;lock(OutputGate)output.WriteLine(Json.Serialize(new{jsonrpc="2.0",method="notifications/progress",@params=new{progressToken=token,progress=step++,message=message}}));}};
        Console.Error.WriteLine(logPrefix+name+" | "+Presentation.DisplayPath(target)+" | START");progress(Title(name)+"已开始");
        using(var timer=new System.Threading.Timer(_=>{lock(progressGate){if(!active)return;Console.Error.WriteLine(logPrefix+name+" | RUNNING | "+watch.ElapsedMilliseconds+" ms");progress(Title(name)+"仍在执行 · "+watch.Elapsed.TotalSeconds.ToString("F0")+" 秒");}},null,2000,2000))
        {
            object result;bool failed=false;string code=null;
            try{result=Call(name,args);failed=(bool)result.GetType().GetProperty("isError").GetValue(result,null);if(failed)code=WorkspaceContracts.FailureCode(result);}
            catch(Exception ex){failed=true;code=ex is DirectoryNotFoundException||ex is FileNotFoundException?"PATH_NOT_FOUND":ex is UnauthorizedAccessException?"ACCESS_DENIED":"TOOL_ERROR";result=Result(new{error_code=code,message=ex.Message,requested_path=target},true);}
            progress(Title(name)+(failed?"失败":"已返回"));lock(progressGate)active=false;
            object receipt=result.GetType().GetProperty("structuredContent").GetValue(result,null);
            Dictionary<string,object> detail;
            // Dashboard shaping must never turn a completed tool call into a failure.
            try{detail=WorkspaceDetail.Build(name,args,result);}
            catch(Exception ex){detail=new Dictionary<string,object>{{"kind","none"},{"session_id",null},{"tool",name},{"target",Presentation.DisplayPath(target)},{"error","详情构建失败："+ex.Message}};}
            WorkspaceDiagnostics.Completed(name,failed);
            WorkspaceActivity.Finish(activityId,failed,watch.ElapsedMilliseconds,code,Json.Serialize(receipt),detail);
            Console.Error.WriteLine(logPrefix+name+" | "+Presentation.DisplayPath(target)+" | "+(failed?"FAIL":"RETURNED")+" | "+watch.ElapsedMilliseconds+" ms");return result;
        }
    }
    static void Reply(StreamWriter output,object response){lock(OutputGate)output.WriteLine(Json.Serialize(response));}
    // ---- MCP 2026-07-28 modern era: stateless per-request negotiation, MRTR confirmations, Tasks extension ----
    static Dictionary<string,object> ModernWrap(object result)
    {
        var dict=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(result));
        if(!dict.ContainsKey("resultType"))dict["resultType"]="complete";
        object existing;var meta=new Dictionary<string,object>();
        if(dict.TryGetValue("_meta",out existing)&&existing is Dictionary<string,object>)meta=(Dictionary<string,object>)existing;
        meta[ServerInfoKey]=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(ServerInfo));
        dict["_meta"]=meta;return dict;
    }
    static object Discover()
    {
        return new{resultType="complete",supportedVersions=new[]{ModernVersion},capabilities=new{tools=new{listChanged=false},extensions=new Dictionary<string,object>{{TasksExt,new{}}}},instructions=Instructions,ttlMs=3600000,cacheScope="private",_meta=new Dictionary<string,object>{{ServerInfoKey,ServerInfo}}};
    }
    static string B64Url(byte[] bytes){return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');}
    static byte[] FromB64Url(string text){string s=text.Replace('-','+').Replace('_','/');switch(s.Length%4){case 2:s+="==";break;case 3:s+="=";break;}return Convert.FromBase64String(s);}
    static byte[] Hmac(byte[] payload){using(var h=new System.Security.Cryptography.HMACSHA256(StateKey))return h.ComputeHash(payload);}
    static string Fingerprint(Dictionary<string,object> args){var canonical=string.Join("\n",args.Keys.OrderBy(k=>k,StringComparer.Ordinal).Select(k=>k+"="+Convert.ToString(args[k],System.Globalization.CultureInfo.InvariantCulture)));using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-","").ToLowerInvariant();}
    static string MakeState(string tool,Dictionary<string,object> args)
    {
        var payload=new Dictionary<string,object>{{"t",tool},{"f",Fingerprint(args)},{"e",DateTime.UtcNow.AddMinutes(10).Ticks},{"n",Guid.NewGuid().ToString("N")}};
        byte[] body=Encoding.UTF8.GetBytes(Json.Serialize(payload));
        return B64Url(body)+"."+B64Url(Hmac(body));
    }
    // requestState is attacker-controlled input: verify HMAC, tool binding, argument fingerprint, expiry and single use.
    static bool ValidateState(string state,string tool,Dictionary<string,object> args,out string error)
    {
        error=null;int dot=state.LastIndexOf('.');if(dot<=0||dot==state.Length-1){error="Malformed requestState";return false;}
        byte[] body,sig;try{body=FromB64Url(state.Substring(0,dot));sig=FromB64Url(state.Substring(dot+1));}catch(Exception){error="Malformed requestState encoding";return false;}
        if(!Hmac(body).SequenceEqual(sig)){error="requestState failed integrity verification";return false;}
        Dictionary<string,object> payload;try{payload=Json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(body));}catch(Exception){error="Unreadable requestState payload";return false;}
        if(S(payload,"t")!=tool){error="requestState was issued for a different tool";return false;}
        if(S(payload,"f")!=Fingerprint(args)){error="Retried arguments differ from the confirmation request; reissue the call unchanged";return false;}
        if(DateTime.UtcNow.Ticks>Convert.ToInt64(payload["e"])){error="Confirmation expired; reissue the call to request a new one";return false;}
        string nonce=S(payload,"n");
        if(UsedNonces.Count>1024)foreach(string used in UsedNonces.Keys.Take(UsedNonces.Count-512).ToArray()){byte removed;UsedNonces.TryRemove(used,out removed);}
        if(!UsedNonces.TryAdd(nonce,0)){error="requestState was already consumed; reissue the call";return false;}
        return true;
    }
    static string ConfirmSubject(string tool,Dictionary<string,object> args)
    {
        if(tool=="apply_patch"){string cwd=args.ContainsKey("cwd")?Convert.ToString(args["cwd"]):"";string patch=args.ContainsKey("patch")?Convert.ToString(args["patch"]):"";return "在 "+cwd+" 应用多文件补丁（"+patch.Split('\n').Length+" 行）";}
        if(tool=="write_file"){object v;if(!args.TryGetValue("overwrite",out v)||v==null||!Convert.ToBoolean(v))return null;string path=args.ContainsKey("path")?Convert.ToString(args["path"]):"";try{if(!File.Exists(Full(path,true)))return null;}catch(Exception){return null;}return "覆盖写入已存在的文件 "+path;}
        return null;
    }
    static object InputRequired(string tool,string subject,Dictionary<string,object> args)
    {
        var confirm=new Dictionary<string,object>{{"method","elicitation/create"},{"params",new{mode="form",message="确认执行 "+tool+"："+subject+"？该操作会修改本机文件。",requestedSchema=new{type="object",properties=new Dictionary<string,object>{{"confirm",new{type="string",@enum=new[]{"accept","decline"},description="Accept or decline this local file modification"}}},required=new[]{"confirm"},additionalProperties=false}}}};
        return new{resultType="input_required",inputRequests=new Dictionary<string,object>{{"confirm",confirm}},requestState=MakeState(tool,args)};
    }
    // Returns null when the call may proceed; otherwise the result to reply with immediately.
    static object ConfirmGate(string tool,Dictionary<string,object> call,Dictionary<string,object> args)
    {
        string subject=ConfirmSubject(tool,args);if(subject==null)return null;
        object stateObj;if(!call.TryGetValue("requestState",out stateObj)||!(stateObj is string)||((string)stateObj).Length==0)return InputRequired(tool,subject,args);
        string error;
        if(!ValidateState((string)stateObj,tool,args,out error))return Result(new{error_code="CONFIRM_STATE_INVALID",message=error},true);
        object responsesObj;var responses=call.TryGetValue("inputResponses",out responsesObj)&&responsesObj is Dictionary<string,object>?(Dictionary<string,object>)responsesObj:null;
        object confirmObj;if(responses==null||!responses.TryGetValue("confirm",out confirmObj)||!(confirmObj is Dictionary<string,object>))return Result(new{error_code="CONFIRM_MISSING_RESPONSE",message="inputResponses.confirm (ElicitResult) is required to retry a confirmation"},true);
        if(S((Dictionary<string,object>)confirmObj,"action")!="accept")return Result(new{error_code="CONFIRM_DECLINED",message="User declined the confirmation for "+tool+"; nothing was changed."},true);
        return null;
    }
    static object Taskify(string tool,object answer)
    {
        if(tool!="exec_command")return null;
        try{
            var dict=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(answer));
            var sc=dict.ContainsKey("structuredContent")?dict["structuredContent"] as Dictionary<string,object>:null;
            var res=sc!=null&&sc.ContainsKey("result")?sc["result"] as Dictionary<string,object>:null;
            if(res==null||!res.ContainsKey("running")||!Convert.ToBoolean(res["running"])||!res.ContainsKey("session_id"))return null;
            string sessionId=Convert.ToString(res["session_id"]);Command c;if(!Commands.TryGetValue(sessionId,out c))return null;
            return new{resultType="task",taskId=sessionId,status="working",statusMessage="命令仍在运行："+c.Text,createdAt=c.Started.ToString("o"),lastUpdatedAt=DateTime.UtcNow.ToString("o"),ttlMs=3600000,pollIntervalMs=1000};
        }catch(Exception){return null;}
    }
    static object TaskRpc(string method,Dictionary<string,object> p)
    {
        string taskId=S(p,"taskId");
        if(method=="tasks/update")return new{};
        Command c;if(!Commands.TryGetValue(taskId,out c))throw new ArgumentException("Unknown or expired taskId");
        object metadata;string hostKey=WorkspaceThreads.HostKey(p.TryGetValue("_meta",out metadata)?metadata as Dictionary<string,object>:null);
        if(hostKey.Length>0&&WorkspaceThreads.Resolve(hostKey,"","")!=c.ThreadId)throw new ArgumentException("THREAD_MISMATCH: task belongs to another conversation");
        CurrentTool="exec_command";CurrentThread=c.ThreadId;
        if(method=="tasks/cancel"){if(!c.Process.HasExited){c.CancelRequested=true;c.Stopped=true;c.Stop();}return new{};}
        if(!c.Process.HasExited)return new{taskId=taskId,status="working",statusMessage="命令仍在运行 · "+Math.Round((DateTime.UtcNow-c.Started).TotalSeconds,0)+" 秒",createdAt=c.Started.ToString("o"),lastUpdatedAt=DateTime.UtcNow.ToString("o"),ttlMs=3600000,pollIntervalMs=1000};
        if(c.CancelRequested)return new{taskId=taskId,status="cancelled",statusMessage="任务已按请求取消",createdAt=c.Started.ToString("o"),lastUpdatedAt=DateTime.UtcNow.ToString("o"),ttlMs=3600000};
        int exitCode=0;try{exitCode=c.Process.ExitCode;}catch(Exception){}
        DateTime finished;try{finished=c.Process.ExitTime.ToUniversalTime();}catch(Exception){finished=DateTime.UtcNow;}
        return new{taskId=taskId,status="completed",statusMessage="命令已结束 · exit_code="+exitCode,createdAt=c.Started.ToString("o"),lastUpdatedAt=(finished>c.Started?finished:DateTime.UtcNow).ToString("o"),ttlMs=3600000,result=Result(Snapshot(taskId,0,false,false))};
    }
    static void Modern(object id,string method,Dictionary<string,object> p,Dictionary<string,object> meta,string trace,StreamWriter output)
    {
        string pv=S(meta,PvKey);
        if(pv!=ModernVersion){Reply(output,new{jsonrpc="2.0",id=id,error=new{code=-32022,message="Unsupported protocol version '"+pv+"'; supported: "+ModernVersion,data=new{supportedVersions=new[]{ModernVersion}}}});return;}
        object capsObj;if(!meta.TryGetValue(CapsKey,out capsObj)||!(capsObj is Dictionary<string,object>)){Reply(output,new{jsonrpc="2.0",id=id,error=new{code=-32021,message="Missing required client capabilities declaration: "+CapsKey}});return;}
        var caps=(Dictionary<string,object>)capsObj;
        bool elicitation=caps.ContainsKey("elicitation");
        object extObj;bool tasks=caps.TryGetValue("extensions",out extObj)&&extObj is Dictionary<string,object>&&((Dictionary<string,object>)extObj).ContainsKey(TasksExt);
        if(method=="server/discover"){WorkspaceDiagnostics.Initialize();Console.Error.WriteLine("[Workspace] server/discover (modern) | "+Version+" | instance="+InstanceId);Reply(output,new{jsonrpc="2.0",id=id,result=Discover()});return;}
        if(method=="tools/list"){WorkspaceDiagnostics.Discover();Console.Error.WriteLine("[Workspace] tools/list (modern) | "+ToolsModern().Length+" tools | "+Version);Reply(output,new{jsonrpc="2.0",id=id,result=ModernWrap(new{tools=ToolsModern(),ttlMs=300000,cacheScope="private"})});return;}
        if(method=="tools/call"){
            string tool=S(p,"name");CurrentTool=tool;
            Dictionary<string,object> args;try{args=PrepareCall(p);}catch(Exception ex){Reply(output,new{jsonrpc="2.0",id=id,result=ModernWrap(Result(new{error_code="THREAD_MISMATCH",message=ex.Message},true))});return;}
            if(elicitation){object gate;try{gate=ConfirmGate(tool,p,args);}catch(Exception ex){gate=Result(new{error_code="CONFIRM_ERROR",message=ex.Message},true);}if(gate!=null){Reply(output,new{jsonrpc="2.0",id=id,result=ModernWrap(gate)});return;}}
            if(tool=="read_workspace_activity"){try{Reply(output,new{jsonrpc="2.0",id=id,result=ModernWrap(Call(tool,args))});}catch(Exception ex){CurrentTool=tool;Reply(output,new{jsonrpc="2.0",id=id,result=ModernWrap(Result(new{error_code="TOOL_ERROR",message=ex.Message},true))});}return;}
            object responseId=id;bool taskCapable=tasks;
            if(!Work.TryAdd(()=>{try{object answer=RunCall(p,output,trace);if(taskCapable){object task=Taskify(tool,answer);if(task!=null){Reply(output,new{jsonrpc="2.0",id=responseId,result=ModernWrap(task)});return;}}Reply(output,new{jsonrpc="2.0",id=responseId,result=ModernWrap(answer)});}catch(Exception ex){Reply(output,new{jsonrpc="2.0",id=responseId,error=new{code=-32603,message=ex.Message}});}}))Reply(output,new{jsonrpc="2.0",id=id,error=new{code=-32000,message="Tool queue is full; wait for existing calls."}});
            return;
        }
        if(method=="tasks/get"||method=="tasks/update"||method=="tasks/cancel"){object answer;try{answer=TaskRpc(method,p);}catch(Exception ex){Reply(output,new{jsonrpc="2.0",id=id,error=new{code=-32602,message=ex.Message}});return;}Reply(output,new{jsonrpc="2.0",id=id,result=ModernWrap(answer)});return;}
        Reply(output,new{jsonrpc="2.0",id=id,error=new{code=-32601,message="Method not found"}});
    }
    public static void Run()
    {
        using(var dashboard=new LocalDashboard(thread=>LiveSnapshot(new Dictionary<string,object>{{"thread_id",thread}},true)))
        using(var input=new StreamReader(Console.OpenStandardInput(),Utf8))using(var output=new StreamWriter(Console.OpenStandardOutput(),Utf8){AutoFlush=true}) {
            var worker=System.Threading.Tasks.Task.Run(()=>{foreach(var action in Work.GetConsumingEnumerable())action();});
            string line;try{while((line=input.ReadLine())!=null){object id=null;try{
                var req=Json.Deserialize<Dictionary<string,object>>(line);if(!req.TryGetValue("id",out id))continue;string method=S(req,"method");object result;
                var rpcParams=req.ContainsKey("params")&&req["params"] is Dictionary<string,object>?(Dictionary<string,object>)req["params"]:null;
                var meta=rpcParams!=null&&rpcParams.ContainsKey("_meta")&&rpcParams["_meta"] is Dictionary<string,object>?(Dictionary<string,object>)rpcParams["_meta"]:null;
                string trace="";if(meta!=null){object tv;if(meta.TryGetValue("traceparent",out tv)&&tv is string)trace=(string)tv;}
                if(meta!=null&&(meta.ContainsKey(PvKey)||meta.ContainsKey(CapsKey))){Modern(id,method,rpcParams,meta,trace,output);continue;}
                if(method=="initialize"){WorkspaceDiagnostics.Initialize();Console.Error.WriteLine("[Workspace] initialize | "+Version+" | instance="+InstanceId);result=new{protocolVersion="2025-06-18",capabilities=LegacyCapabilities,serverInfo=ServerInfo,instructions=Instructions};}
                else if(method=="ping")result=new{};
                else if(method=="tools/list"){WorkspaceDiagnostics.Discover();Console.Error.WriteLine("[Workspace] tools/list | "+Tools().Length+" tools | "+Version);result=new{tools=Tools()};}
                else if(method=="tools/call"){
                    var call=(Dictionary<string,object>)req["params"];string tool=S(call,"name");
                    if(tool=="read_workspace_activity"){try{result=Call(tool,PrepareCall(call));}catch(Exception ex){CurrentTool=tool;result=Result(new{error_code="TOOL_ERROR",message=ex.Message},true);}}
                    else {object responseId=id;if(!Work.TryAdd(()=>{try{object answer=RunCall(call,output,trace);Reply(output,new{jsonrpc="2.0",id=responseId,result=answer});}catch(Exception ex){Reply(output,new{jsonrpc="2.0",id=responseId,error=new{code=-32603,message=ex.Message}});}}))Reply(output,new{jsonrpc="2.0",id=id,error=new{code=-32000,message="Tool queue is full; wait for existing calls."}});continue;}
                }
                else {Reply(output,new{jsonrpc="2.0",id=id,error=new{code=-32601,message="Method not found"}});continue;}
                Reply(output,new{jsonrpc="2.0",id=id,result=result});
            }catch(Exception ex){Reply(output,new{jsonrpc="2.0",id=id,error=new{code=-32600,message=ex.Message}});}}}finally{Work.CompleteAdding();worker.Wait();foreach(var c in Commands.Values)c.Dispose();}
        }
    }
}
