using System;
using System.Collections;
using System.Collections.Generic;

// Model-facing receipts keep data once, with a short human-readable summary beside it.
static class WorkspaceContracts
{
    static object Get(object value,string key){if(value==null)return null;var map=value as IDictionary<string,object>;object found;if(map!=null)return map.TryGetValue(key,out found)?found:null;var field=value.GetType().GetProperty(key);return field==null?null:field.GetValue(value,null);}
    static string Text(object value,string key){return Convert.ToString(Get(value,key))??"";}
    public static string Summary(string tool,object value,bool error)
    {
        if(error){string message=Text(value,"message");if(message.Length==0)message=Text(value,"error");if(message.Length==0)message="exit_code="+Text(value,"exit_code")+", timed_out="+Text(value,"timed_out");return tool+" failed: "+Clip(message);}
        string path=Text(value,"path"),session=Text(value,"session_id");
        if(session.Length>0)return tool+": session "+session+", running="+Text(value,"running")+", exit_code="+Text(value,"exit_code")+". Output is in structuredContent.result.";
        if(tool=="read_file")return "Read "+Text(value,"returned_count")+" lines from "+path+". next_line="+Text(value,"next_line")+". Content is in structuredContent.result.lines.";
        if(tool=="get_workspace_status")return "Local Workspace "+Text(value,"version")+": "+Text(value,"tool_count")+" tools; "+Text(value,"running_commands")+" running commands.";
        if(tool=="import_file")return "Saved attachment to "+path+" ("+Text(value,"size_bytes")+" bytes). Existing files were not overwritten.";
        return tool+" completed"+(path.Length>0?": "+path:"")+". See structuredContent.result for details.";
    }
    static string Clip(string text){return text.Length>500?text.Substring(0,500)+"…":text;}
    static object Type(string type){return new{type=type};}
    static object ArrayOf(object item){return new{type="array",items=item};}
    static object Nullable(string type){return new{type=new[]{type,"null"}};}
    static object Shape(Dictionary<string,object> properties,params string[] required){return new{type="object",properties=properties,required=required,additionalProperties=true};}
    public static object Output(string tool)
    {
        var p=new Dictionary<string,object>();
        var str=Type("string");var number=Type("number");var integer=Type("integer");var boolean=Type("boolean");
        // Common result facts, including the explicit failure envelope.
        p["path"]=str;p["error_code"]=str;p["message"]=str;
        string[] required=new string[0];
        if(new List<string>{"exec_command","read_command","poll_command","write_stdin","stop_command"}.Contains(tool)){
            p["session_id"]=str;p["running"]=boolean;p["exit_code"]=Nullable("integer");p["output"]=str;p["full_output"]=str;p["output_mode"]=new{@enum=new[]{"delta","snapshot"}};p["elapsed_seconds"]=number;p["truncated"]=boolean;p["timed_out"]=boolean;p["stopped"]=boolean;required=new[]{"session_id","running","output","exit_code"};
        }else if(tool=="read_file"){
            p["lines"]=ArrayOf(str);p["start_line"]=integer;p["returned_count"]=integer;p["next_line"]=Nullable("integer");required=new[]{"path","lines","returned_count","next_line"};
        }else if(tool=="read_image"||tool=="import_file"){
            p["mime_type"]=str;p["size_bytes"]=integer;p["preview_url"]=str;p["is_image"]=boolean;p["sha256"]=str;p["file_id"]=str;p["created"]=boolean;required=new[]{"path","mime_type","size_bytes"};
        }else if(tool=="list_directory"){
            p["entries"]=ArrayOf(Shape(new Dictionary<string,object>{{"name",str},{"path",str},{"directory",boolean}},"name","path","directory"));p["next_offset"]=Nullable("integer");p["returned_count"]=integer;p["total_entries"]=integer;p["empty"]=boolean;required=new[]{"path","entries","next_offset"};
        }else if(tool=="search_files"||tool=="search_text"){
            p["matches"]=ArrayOf(Shape(new Dictionary<string,object>{{"path",str},{"line",Nullable("integer")},{"column",Nullable("integer")},{"text",str}},"path"));p["next_offset"]=Nullable("integer");p["returned_count"]=integer;p["complete"]=boolean;p["truncated"]=boolean;p["skipped_paths"]=integer;required=new[]{"path","matches","returned_count"};
        }else if(tool=="get_workspace_status"){
            p["version"]=str;p["instance_id"]=str;p["tool_count"]=integer;p["tools"]=ArrayOf(str);p["running_commands"]=integer;p["dashboard_url"]=str;p["host_session_observed"]=boolean;required=new[]{"version","instance_id","tools","tool_count"};
        }else if(tool=="register_conversation"){
            p["thread_id"]=str;p["title"]=str;p["chat_id"]=str;p["chat_url"]=Nullable("string");p["dashboard_url"]=str;required=new[]{"thread_id","title","path"};
        }else if(tool=="git_status"||tool=="git_diff"){
            p["output"]=str;p["exit_code"]=integer;p["truncated"]=boolean;p["timed_out"]=boolean;required=new[]{"path","output","exit_code"};
        }else if(tool=="list_commands"){
            p["commands"]=ArrayOf(Shape(new Dictionary<string,object>{{"session_id",str},{"running",boolean},{"exit_code",Nullable("integer")}},"session_id","running"));p["count"]=integer;required=new[]{"commands","count"};
        }else if(tool=="update_plan"){
            p["plan"]=ArrayOf(Shape(new Dictionary<string,object>{{"step",str},{"status",new{@enum=new[]{"pending","in_progress","completed"}}}},"step","status"));p["thread_id"]=str;p["explanation"]=str;p["updated_at"]=str;required=new[]{"path","plan"};
        }else if(tool=="open_workspace"){
            p["instructions"]=ArrayOf(Shape(new Dictionary<string,object>{{"path",str},{"content",str},{"truncated",boolean}},"path","content"));p["skills"]=ArrayOf(Shape(new Dictionary<string,object>{{"name",str},{"path",str}},"name","path"));p["default_shell"]=str;required=new[]{"path","instructions","skills"};
        }else if(tool=="read_workspace_activity"){
            p["activity"]=ArrayOf(Type("object"));p["commands"]=ArrayOf(Type("object"));p["plans"]=ArrayOf(Type("object"));p["instance_id"]=str;required=new[]{"activity","commands","plans"};
        }else if(tool=="create_directory"){p["created"]=boolean;p["exists"]=boolean;required=new[]{"path","created","exists"};}
        else if(tool=="file_info"){p["directory"]=boolean;p["size_bytes"]=Nullable("integer");required=new[]{"path","directory"};}
        else if(tool=="write_file"||tool=="edit_file"){p["diff"]=Type("object");p["created"]=boolean;required=new[]{"path","diff"};}
        else if(tool=="apply_patch"||tool=="show_changes"){p["files"]=ArrayOf(Type("object"));p["count"]=integer;p["partial"]=boolean;required=new[]{"files","count"};}
        var error=Shape(new Dictionary<string,object>{{"error_code",str},{"message",str}},"error_code","message");
        return new{type="object",properties=new{tool=new{@enum=new[]{tool}},result=new{anyOf=new[]{Shape(p,required),error}},isError=boolean,thread_id=str},required=new[]{"tool","result","isError"},additionalProperties=false};
    }
}
