using System;
using System.Collections.Generic;

// Shapes what a completed tool call actually did into a small, typed payload for the
// local dashboard inspector: which files were touched, line ranges, commands, matches.
// The one exception is a text file read, which carries a bounded copy of what came back
// so the inspector can show the file itself; images travel through a bounded cache URL. Nothing
// lets a single call enlarge the one-second dashboard snapshot without bound.
static class WorkspaceDetail
{
    const int DiffRowsTotal=200,DiffRowChars=240,DiffFilesMax=6,MatchesMax=30,EntriesMax=40;
    const int TextChars=20000,OutputChars=4000,NoteChars=400,ReadBytes=10*1024;
    sealed class Budget{public int Rows;}

    static object Field(object source,string name)
    {
        if(source==null)return null;
        var map=source as Dictionary<string,object>;
        if(map!=null){object value;return map.TryGetValue(name,out value)?value:null;}
        var property=source.GetType().GetProperty(name);
        return property==null?null:property.GetValue(source,null);
    }
    static string Text(object source,string name){object value=Field(source,name);return value==null?"":Convert.ToString(value);}
    static int Number(object source,string name,int fallback=0){object value=Field(source,name);if(value==null)return fallback;try{return Convert.ToInt32(value);}catch{return fallback;}}
    static long Long(object source,string name){object value=Field(source,name);if(value==null)return 0;try{return Convert.ToInt64(value);}catch{return 0;}}
    static bool Flag(object source,string name,bool fallback=false){object value=Field(source,name);if(value==null)return fallback;try{return Convert.ToBoolean(value);}catch{return fallback;}}
    static object[] Rows(object source,string name)
    {
        object value=Field(source,name);var sequence=value as System.Collections.IEnumerable;
        if(sequence==null||value is string)return new object[0];
        var items=new List<object>();foreach(object item in sequence)items.Add(item);return items.ToArray();
    }
    static string Clip(string text,int limit){if(text==null)return "";return text.Length<=limit?text:text.Substring(0,limit);}
    static string Name(string path){if(string.IsNullOrEmpty(path))return "";int slash=Math.Max(path.LastIndexOf('/'),path.LastIndexOf('\\'));return slash<0?path:path.Substring(slash+1);}
    static string Tail(string text,int limit){if(text==null)return "";return text.Length<=limit?text:text.Substring(text.Length-limit);}
    static string Bytes(long size){if(size<1024)return size+" B";if(size<1024*1024)return Math.Round(size/1024.0,1)+" KB";return Math.Round(size/(1024.0*1024.0),1)+" MB";}
    static string Display(string path){return (path??"").Replace('\\','/');}
    static string Path(object inner){string path=Text(inner,"path");return path.Length>0?Display(path):Display(Text(inner,"requested_path"));}
    static string ShortSession(object inner){string id=Text(inner,"session_id");return id.Length>6?id.Substring(id.Length-6):id;}

    // Shared across every file of one call so a multi-file patch cannot inflate the snapshot.
    static object ShapeDiff(object diff,Budget budget)
    {
        var shaped=new Dictionary<string,object>();
        object[] rows=Rows(diff,"rows");var kept=new List<object>();bool dropped=false;
        foreach(object row in rows)
        {
            if(budget.Rows>=DiffRowsTotal){dropped=true;break;}
            string text=Text(row,"text");
            kept.Add(new Dictionary<string,object>{
                {"kind",Text(row,"kind")},
                {"old_line",Field(row,"old_line")},
                {"new_line",Field(row,"new_line")},
                {"text",Clip(text,DiffRowChars)},
                {"truncated",Flag(row,"text_truncated")||text.Length>DiffRowChars}});
            budget.Rows++;
        }
        shaped["rows"]=kept;shaped["added"]=Number(diff,"added");shaped["removed"]=Number(diff,"removed");
        shaped["identical"]=Flag(diff,"identical");shaped["coarse"]=Flag(diff,"coarse");
        shaped["truncated"]=Flag(diff,"truncated")||dropped;shaped["omitted_rows"]=Math.Max(0,rows.Length-kept.Count);
        return shaped;
    }
    static Dictionary<string,object> File(string path,string operation,object diff,Budget budget,long size,string previous=null)
    {
        string display=Display(path),from=Display(previous==null?"":previous);
        return new Dictionary<string,object>{
            {"path",display},{"name",Name(display)},{"operation",operation},{"size_bytes",size},
            {"previous_path",from.Length==0?null:from},
            {"diff",diff==null?null:ShapeDiff(diff,budget)}};
    }
    // write_file reports the target path; edit_file replaces exactly one occurrence.
    static Dictionary<string,object> WriteFile(Dictionary<string,object> args,object inner,Budget budget)
    {
        bool created=Flag(inner,"created");
        string path=Path(inner),content=Text(args,"content");
        var detail=new Dictionary<string,object>{
            {"kind","write"},{"session_id",null},
            {"files",new object[]{File(path,created?"add":"replace",Field(inner,"diff"),budget,new System.Text.UTF8Encoding(false).GetByteCount(content))}},
            {"count",1},{"overwrite",Flag(args,"overwrite")},{"label",created?"新建文件":"替换文件"}};
        string counts=Counts(Field(inner,"diff"));
        detail["summary"]=Name(Display(path))+" · "+(created?"新建文件":"替换文件")+(counts.Length>0?" · "+counts:"");
        return detail;
    }
    static string Counts(object diff)
    {
        if(diff==null)return "";
        int added=Number(diff,"added"),removed=Number(diff,"removed");
        if(added==0&&removed==0)return Flag(diff,"identical")?"内容未变化":"";
        return "+"+added+" −"+removed;
    }
    static Dictionary<string,object> Split(string text,int limit)
    {
        bool truncated=text!=null&&text.Length>limit;
        var shape=new Dictionary<string,object>{{"text",Clip(text??"",limit)},{"truncated",truncated}};
        return shape;
    }
    static Dictionary<string,object> Edit(Dictionary<string,object> args,object inner,Budget budget)
    {
        string path=Path(inner);
        var replace=new Dictionary<string,object>{
            {"before",Split(Text(args,"old_text"),NoteChars)},{"after",Split(Text(args,"new_text"),NoteChars)},
            {"before_chars",Text(args,"old_text").Length},{"after_chars",Text(args,"new_text").Length}};
        var detail=new Dictionary<string,object>{
            {"kind","write"},{"session_id",null},
            {"files",new object[]{File(path,"edit",Field(inner,"diff"),budget,0)}},
            {"count",1},{"replace",replace}};
        string counts=Counts(Field(inner,"diff"));
        detail["summary"]=Name(Display(path))+" · 精确替换"+(counts.Length>0?" · "+counts:"");
        return detail;
    }
    static Dictionary<string,object> Patch(object inner,Budget budget)
    {
        var files=new List<object>();int added=0,removed=0,omitted=0;
        foreach(object item in Rows(inner,"files"))
        {
            if(files.Count>=DiffFilesMax){omitted++;continue;}
            object diff=Field(item,"diff");
            added+=Number(diff,"added");removed+=Number(diff,"removed");
            files.Add(File(Text(item,"path"),Text(item,"operation"),diff,budget,0,Text(item,"previous_path")));
        }
        var detail=new Dictionary<string,object>{
            {"kind","write"},{"session_id",null},{"files",files},{"count",Number(inner,"count",files.Count)},
            {"omitted_files",omitted},{"added",added},{"removed",removed},
            {"error",Clip(Text(inner,"error"),NoteChars)},{"partial",Flag(inner,"partial")},{"rollback",Text(inner,"rollback")},
            {"root",Display(Text(inner,"path"))}};
        detail["summary"]="补丁 · "+Number(inner,"count",files.Count)+" 个文件 · +"+added+" −"+removed;
        return detail;
    }
    static Dictionary<string,object> Review(object inner,Budget budget)
    {
        var files=new List<object>();int omitted=0;
        foreach(object item in Rows(inner,"files"))
        {
            if(files.Count>=DiffFilesMax){omitted++;continue;}
            files.Add(File(Text(item,"path"),"update",Field(item,"diff"),budget,0));
        }
        var detail=new Dictionary<string,object>{
            {"kind","write"},{"session_id",null},{"files",files},{"count",Number(inner,"count",files.Count)},
            {"omitted_files",omitted+Number(inner,"omitted_files")},{"scope",Text(inner,"scope")},
            {"untracked_changes",Number(inner,"untracked_changes")},{"root",Display(Text(inner,"path"))}};
        detail["summary"]=Number(inner,"count",files.Count)+" 个文件有工具改动";
        return detail;
    }
    // Text or not is decided by the bytes themselves: a NUL, or a noticeable share of
    // control and replacement characters, means showing the content would be noise.
    static bool LooksBinary(string text)
    {
        if(text.Length==0)return false;
        int odd=0;
        foreach(char c in text)
        {
            if(c=='\0')return true;
            if(c<32&&c!='\n'&&c!='\r'&&c!='\t')odd++;
            else if(c=='\uFFFD')odd++;
        }
        return odd*50>text.Length;
    }
    // read_file answers with "N: text" lines: the number becomes the gutter, the text the content.
    static void ReadContent(object inner,List<object> body,out string raw,out bool truncated,out int omitted,out int used)
    {
        var encoding=new System.Text.UTF8Encoding(false);
        var text=new System.Text.StringBuilder();
        used=0;omitted=0;truncated=false;
        foreach(object item in Rows(inner,"lines"))
        {
            string line=Convert.ToString(item)??"";
            int colon=line.IndexOf(": "),parsed=0;
            bool numbered=colon>0&&int.TryParse(line.Substring(0,colon),out parsed);
            string value=numbered?line.Substring(colon+2):line;
            int size=encoding.GetByteCount(value);
            if(used+size>ReadBytes)
            {
                // Keep the first line even when it alone exceeds the budget, then stop.
                if(body.Count==0&&used<ReadBytes)
                {
                    value=Clip(value,ReadBytes-used);text.Append(value);
                    body.Add(new Dictionary<string,object>{{"n",numbered?(int?)parsed:null},{"text",value}});
                }
                truncated=true;omitted++;continue;
            }
            used+=size;text.Append(value).Append('\n');
            body.Add(new Dictionary<string,object>{{"n",numbered?(int?)parsed:null},{"text",value}});
        }
        raw=text.ToString();
    }
    static long FileBytes(object inner)
    {
        string file=Text(inner,"path");if(file.Length==0)return -1;
        try{var info=new System.IO.FileInfo(file);return info.Exists?info.Length:-1;}catch{return -1;}
    }
    static Dictionary<string,object> Read(Dictionary<string,object> args,object inner)
    {
        string path=Display(Path(inner));
        // A rejected read carries no result payload: the requested path is still worth showing.
        if(path.Length==0)path=Display(Text(args,"path"));
        int start=Number(inner,"start_line",1),count=Number(inner,"returned_count");
        var reads=new Dictionary<string,object>{
            {"path",path},{"name",Name(path)},{"start_line",start},{"returned_count",count},
            {"end_line",count>0?start+count-1:start},{"empty",count==0},{"next_line",Field(inner,"next_line")}};
        var notes=new List<object>();
        if(Flag(inner,"path_corrected"))notes.Add("请求路径与实际路径不同，已按实际文件读取。");

        var body=new List<object>();string raw;bool truncated;int omitted,used;
        ReadContent(inner,body,out raw,out truncated,out omitted,out used);
        bool binary=LooksBinary(raw);long size=FileBytes(inner);
        // Nothing was actually read for an empty or failed call, so it cannot claim the end.
        bool atEnd=count>0&&Field(inner,"next_line")==null;
        if(binary){body=new List<object>();truncated=false;omitted=0;used=0;notes.Add("这个文件不是文本文件，没有在面板里展开内容。");}
        else if(truncated)notes.Add("内容超过 10 KB，这里只展开了一部分。");

        var detail=new Dictionary<string,object>{
            {"kind","read"},{"session_id",null},{"reads",new object[]{reads}},{"notes",notes},
            {"path",path},{"name",Name(path)},{"start_line",start},{"returned_count",count},
            {"end_line",count>0?start+count-1:start},{"next_line",Field(inner,"next_line")},
            {"content",body},{"binary",binary},{"at_end",atEnd},
            {"truncated",truncated},{"omitted_lines",omitted},{"content_bytes",used},
            {"file_bytes",size<0?null:(long?)size}};
        detail["summary"]=Name(path)+(count>0
            ?(binary?" · 不是文本":" · 第 "+start+"–"+(start+count-1)+" 行")
            :" · 未读到内容");
        return detail;
    }
    static Dictionary<string,object> Search(string tool,object inner)
    {
        string query=Text(inner,"query"),pattern=Text(inner,"pattern");bool content=tool=="search_text";
        var kept=new List<object>();
        foreach(object item in Rows(inner,"matches"))
        {
            if(kept.Count>=MatchesMax)break;
            string path=Display(Text(item,"path"));
            kept.Add(new Dictionary<string,object>{
                {"path",path},{"name",Name(path)},{"line",Field(item,"line")},{"column",Field(item,"column")},
                {"text",Clip(Text(item,"text"),300)}});
        }
        int returned=Number(inner,"returned_count",kept.Count);bool complete=Flag(inner,"complete",true);
        var detail=new Dictionary<string,object>{
            {"kind","search"},{"session_id",null},{"root",Display(Text(inner,"path"))},
            {"query",content?query:null},{"pattern",pattern},{"matches",kept},{"returned_count",returned},
            {"omitted",Math.Max(0,returned-kept.Count)},{"complete",complete},{"truncated",Flag(inner,"truncated")},
            {"skipped_paths",Number(inner,"skipped_paths")},{"next_offset",Field(inner,"next_offset")},
            {"scanned_files",Number(inner,"scanned_files")},{"note",Clip(Text(inner,"note"),NoteChars)}};
        detail["summary"]=content?("\""+Clip(query,40)+"\" · "+returned+" 处"+(complete?"":" · 部分结果"))
                                 :(Clip(pattern,40)+" · "+returned+" 个文件"+(complete?"":" · 部分结果"));
        return detail;
    }
    static Dictionary<string,object> Command(string tool,Dictionary<string,object> args,object inner)
    {
        string command=Text(inner,"command"),first=command.Length>0?command.Split('\n')[0]:Title(tool);
        string input=args==null?"":Text(args,"chars");
        var detail=new Dictionary<string,object>{
            {"kind","command"},{"session_id",Text(inner,"session_id")},{"command",command},
            {"shell",Text(inner,"shell")},{"shell_executable",Text(inner,"shell_executable")},
            {"cwd",Display(Text(inner,"cwd"))},{"output_tail",Tail(Text(inner,"output"),OutputChars)},
            {"output_chars",Text(inner,"full_output").Length},
            {"truncated",Flag(inner,"truncated")},{"running",Flag(inner,"running")},
            {"exit_code",Field(inner,"exit_code")},{"timed_out",Flag(inner,"timed_out")},{"stopped",Flag(inner,"stopped")},
            {"elapsed_seconds",Field(inner,"elapsed_seconds")},
            {"output_mode",Text(inner,"output_mode")},
            {"input",input.Length>0?Clip(input,NoteChars):null},{"sent_chars",input.Length}};
        if(tool=="exec_command")detail["summary"]=Clip(first,80);
        else if(tool=="stop_command")detail["summary"]="会话 "+ShortSession(inner)+" · 停止命令";
        else if(tool=="write_stdin")detail["summary"]="会话 "+ShortSession(inner)+(input.Length>0?" · 发送 "+input.Length+" 字符":" · 续读输出");
        else detail["summary"]="会话 "+ShortSession(inner)+" · 读取输出";
        return detail;
    }
    static string Title(string tool)
    {
        if(tool=="stop_command")return "停止命令";
        if(tool=="write_stdin")return "发送命令输入";
        if(tool=="exec_command")return "执行命令";
        return "读取命令输出";
    }
    static Dictionary<string,object> Listing(object inner)
    {
        var kept=new List<object>();
        foreach(object item in Rows(inner,"entries"))
        {
            if(kept.Count>=EntriesMax)break;
            string entryPath=Display(Text(item,"path"));
            kept.Add(new Dictionary<string,object>{{"name",Text(item,"name")},{"path",entryPath},{"directory",Flag(item,"directory")}});
        }
        string path=Display(Text(inner,"path"));int total=Number(inner,"total_entries",kept.Count);
        string label=path.Length==0?"磁盘根目录":(Name(path).Length>0?Name(path):path);
        var detail=new Dictionary<string,object>{
            {"kind","list"},{"session_id",null},{"path",path},{"entries",kept},{"total_entries",total},
            {"returned_count",Number(inner,"returned_count",kept.Count)},{"omitted",Math.Max(0,total-kept.Count)},
            {"next_offset",Field(inner,"next_offset")},{"empty",Flag(inner,"empty")}};
        detail["summary"]=label+" · "+total+" 项";
        return detail;
    }
    static Dictionary<string,object> Git(string tool,object inner)
    {
        string output=Text(inner,"output");int added=0,removed=0;
        if(tool=="git_diff")foreach(string line in output.Replace("\r\n","\n").Split('\n')){
            if(line.StartsWith("+")&&!line.StartsWith("+++"))added++;
            else if(line.StartsWith("-")&&!line.StartsWith("---"))removed++;}
        else foreach(string line in output.Replace("\r\n","\n").Split('\n'))if(line.Trim().Length>0)added++;
        string label=tool=="git_diff"?"Git 差异":"Git 状态";object exit=Field(inner,"exit_code");
        var detail=new Dictionary<string,object>{
            {"kind","text"},{"session_id",null},{"text_label",label},
            {"path",Display(Text(inner,"path"))},{"body",Clip(output,TextChars)},{"exit_code",exit},
            {"truncated",Flag(inner,"truncated")||output.Length>TextChars},{"timed_out",Flag(inner,"timed_out")},
            {"added",tool=="git_diff"?added:0},{"removed",tool=="git_diff"?removed:0}};
        if(exit!=null&&Convert.ToInt32(exit)!=0)detail["summary"]=label+" · 失败（退出码 "+Convert.ToInt32(exit)+"）";
        else if(tool=="git_diff")detail["summary"]=added==0&&removed==0?label+" · 无改动":label+" · +"+added+" −"+removed;
        else detail["summary"]=label+" · "+added+" 行";
        return detail;
    }
    static Dictionary<string,object> Plan(object inner)
    {
        var steps=new List<object>();int done=0,total=0;
        foreach(object item in Rows(inner,"plan"))
        {
            string status=Text(item,"status");total++;if(status=="completed")done++;
            steps.Add(new Dictionary<string,object>{{"step",Clip(Text(item,"step"),240)},{"status",status}});
        }
        var detail=new Dictionary<string,object>{
            {"kind","plan"},{"session_id",null},{"steps",steps},{"done",done},{"total",total},
            {"explanation",Clip(Text(inner,"explanation"),NoteChars)},{"path",Display(Text(inner,"path"))},
            {"updated_at",Text(inner,"updated_at")}};
        detail["summary"]="执行计划 · "+done+"/"+total;
        return detail;
    }
    static Dictionary<string,object> InfoRows(IList<object> rows,string summary)
    {
        var detail=new Dictionary<string,object>{{"kind","info"},{"session_id",null},{"info",rows}};
        detail["summary"]=summary;
        return detail;
    }
    static Dictionary<string,object> Row(string label,string value,bool mono=false)
    {
        return new Dictionary<string,object>{{"label",label},{"value",value},{"mono",mono}};
    }
    static Dictionary<string,object> Info(string tool,object inner)
    {
        if(tool=="import_file")return Flag(inner,"created")?InfoRows(new List<object>{Row("保存位置",Display(Text(inner,"path")),true),Row("文件大小",Bytes(Long(inner,"size_bytes"))),Row("文件类型",Text(inner,"mime_type")),Row("SHA256",Text(inner,"sha256"),true),Row("结果","已创建新文件，未覆盖已有内容")},"已接收聊天附件"):InfoRows(new List<object>{Row("结果","未导入附件"),Row("原因",Text(inner,"message"))},"附件导入失败");
        if(tool=="file_info")
        {
            string path=Display(Text(inner,"path"));bool directory=Flag(inner,"directory");
            var rows=new List<object>{Row("路径",path,true),Row("类型",directory?"目录":"文件")};
            if(!directory)rows.Add(Row("大小",Bytes(Long(inner,"size_bytes"))));
            rows.Add(Row("修改时间",Text(inner,"last_modified_utc")));
            rows.Add(Row("创建时间",Text(inner,"created_utc")));
            rows.Add(Row("属性",Text(inner,"attributes")));
            return InfoRows(rows,Name(path)+(directory?"/":""));
        }
        if(tool=="create_directory")
        {
            string path=Display(Text(inner,"path"));
            return InfoRows(new List<object>{Row("路径",path,true),Row("结果",Flag(inner,"created")?"已新建目录":"目录已存在")},Name(path)+"/");
        }
        if(tool=="read_image")
        {
            string path=Display(Text(inner,"path"));
            return new Dictionary<string,object>{{"kind","image"},{"session_id",null},{"path",path},{"name",Name(path)},
                {"mime_type",Text(inner,"mime_type")},{"size_bytes",Long(inner,"size_bytes")},
                {"preview_url",Text(inner,"preview_url")},{"summary",Name(path)}};
        }
        if(tool=="open_workspace")
        {
            var rows=new List<object>{Row("工作目录",Display(Text(inner,"path")),true)};
            string gitRoot=Text(inner,"git_root");rows.Add(Row("Git 根",gitRoot.Length>0?Display(gitRoot):"无",gitRoot.Length>0));
            var guidance=new List<string>();foreach(object item in Rows(inner,"instructions"))guidance.Add(Display(Text(item,"path")));
            rows.Add(Row("约定文件",guidance.Count==0?"未发现 AGENTS.md":String.Join("\n",guidance.ToArray()),true));
            var shells=new List<string>();foreach(object item in Rows(inner,"shells"))if(Flag(item,"available"))shells.Add(Text(item,"name"));
            rows.Add(Row("可用 Shell",String.Join("、",shells.ToArray())));
            rows.Add(Row("本地技能",Rows(inner,"skills").Length+" 个入口"));
            rows.Add(Row("默认 Shell",Text(inner,"default_shell")));
            object plan=Field(inner,"plan");if(plan!=null)rows.Add(Row("当前计划",Rows(plan,"plan").Length+" 步"));
            rows.Add(Row("CodeGraph",Flag(inner,"codegraph_present")?"存在":"无"));
            return InfoRows(rows,"工作区约定 · "+guidance.Count+" 份指导文件");
        }
        if(tool=="register_conversation")
        {
            var rows=new List<object>{Row("对话",Text(inner,"title")),Row("工作目录",Display(Text(inner,"path")),true),Row("线程",Text(inner,"thread_id"),true)};
            string chat=Text(inner,"chat_id");rows.Add(Row("ChatGPT 对话",chat.Length>0?chat:"未绑定（本地线程）",chat.Length>0));
            return InfoRows(rows,Clip(Text(inner,"title"),60));
        }
        if(tool=="check_task_completion")return InfoRows(new List<object>{Row("工作目录",Display(Text(inner,"path")),true),Row("完成检查",Flag(inner,"can_finish")?"登记证据与执行状态检查通过":"尚未满足交付条件"),Row("未完成步骤",String.Join("\n",Array.ConvertAll(Rows(inner,"unfinished_steps"),Convert.ToString))),Row("缺少验收证据",String.Join("\n",Array.ConvertAll(Rows(inner,"missing_evidence"),Convert.ToString))),Row("阻塞或暂停原因",Text(inner,"reason")),Row("最近执行问题",Text(inner,"last_issue")),Row("下一步",Text(inner,"next_action")),Row("证据范围",Text(inner,"evidence_scope"))},Flag(inner,"can_finish")?"任务完成检查通过":"任务尚未完成");
        if(tool=="get_workspace_status")
        {
            var rows=new List<object>{Row("版本",Text(inner,"version")),Row("实例",Text(inner,"instance_id"),true),Row("程序",Display(Text(inner,"executable")),true)};
            rows.Add(Row("工具数",Text(inner,"tool_count")));
            rows.Add(Row("运行中命令",Text(inner,"running_commands")));
            rows.Add(Row("面板地址",Display(Text(inner,"dashboard_url")),true));
            rows.Add(Row("默认 Shell",Text(inner,"default_shell")));
            var protocols=new List<string>();foreach(object item in Rows(inner,"protocol_versions"))protocols.Add(Convert.ToString(item));
            rows.Add(Row("协议版本",String.Join("\n",protocols.ToArray())));
            rows.Add(Row("可见范围",Text(inner,"scope")));
            var detail=InfoRows(rows,"工作区状态 · v"+Text(inner,"version"));
            detail["kind"]="workspace";
            detail["tools"]=Rows(inner,"tools");
            var workspaces=new List<object>();foreach(object item in Rows(inner,"conversations"))workspaces.Add(new{title=Text(item,"title"),path=Display(Text(item,"path"))});
            detail["workspaces"]=workspaces;
            return detail;
        }
        return null;
    }

    public static Dictionary<string,object> Build(string tool,Dictionary<string,object> args,object receipt)
    {
        object structured=Field(receipt,"structuredContent"),inner=Field(structured,"result");
        bool isError=Flag(structured,"isError");
        var budget=new Budget();
        Dictionary<string,object> detail=null;
        if(tool=="write_file")detail=WriteFile(args??new Dictionary<string,object>(),inner,budget);
        else if(tool=="edit_file")detail=Edit(args??new Dictionary<string,object>(),inner,budget);
        else if(tool=="apply_patch")detail=Patch(inner,budget);
        else if(tool=="show_changes")detail=Review(inner,budget);
        else if(tool=="read_file")detail=Read(args??new Dictionary<string,object>(),inner);
        else if(tool=="search_text"||tool=="search_files")detail=Search(tool,inner);
        else if(tool=="exec_command"||tool=="poll_command"||tool=="read_command"||tool=="stop_command"||tool=="write_stdin")detail=Command(tool,args,inner);
        else if(tool=="list_directory")detail=Listing(inner);
        else if(tool=="git_status"||tool=="git_diff")detail=Git(tool,inner);
        else if(tool=="update_plan")detail=Plan(inner);
        else detail=Info(tool,inner);
        if(detail==null)detail=new Dictionary<string,object>{{"kind","none"},{"session_id",null}};
        if(tool=="read_image"&&Text(detail,"path").Length==0){string requested=Display(Text(args,"path"));detail["path"]=requested;detail["name"]=Name(requested);}
        string target=Text(inner,"path");if(target.Length==0)target=Text(inner,"requested_path");if(target.Length==0)target=Text(inner,"cwd");
        if(target.Length==0)target=Text(detail,"path");
        detail["tool"]=tool;detail["target"]=Display(target);
        if(isError)
        {
            detail["is_error"]=true;
            string message=Text(inner,"message");if(message.Length==0)message=Text(inner,"error");
            detail["error"]=Clip(message,NoteChars);
            // Keep the typed summary when the tool reported a status instead of a message.
            if(message.Length>0)detail["summary"]=Clip(message,80);
            // A rejected write never reached the disk, so it must not read as a completed
            // replacement: keep the path, drop the operation, size and diff. A patch that
            // failed after writing something keeps its files, because patch results are
            // read back from disk and already mean "this file really changed".
            if(Text(detail,"kind")=="write"&&!Flag(detail,"partial"))
            {
                detail["applied"]=false;
                detail.Remove("replace");
                foreach(object entry in Rows(detail,"files"))
                {
                    var file=entry as Dictionary<string,object>;if(file==null)continue;
                    file["operation"]=null;file["diff"]=null;file["size_bytes"]=0;file["previous_path"]=null;
                }
            }
        }
        return detail;
    }
}
