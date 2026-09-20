using System;
using System.Linq;
using System.Collections.Generic;

// Local grouping from host session metadata or explicit registration, never the last caller.
static class WorkspaceThreads
{
    sealed class Conversation { public string Id,Title,Path,ChatId,HostKey; public bool Named; public DateTime Created; }
    static readonly object Gate=new object();
    static readonly List<Conversation> Items=new List<Conversation>();
    static string Meta(Dictionary<string,object> meta,string name){object value;if(meta==null||!meta.TryGetValue(name,out value))return "";var text=value as string;if(text==null||text.Length>512)throw new ArgumentException("Invalid host conversation metadata");return text;}
    public static string HostKey(Dictionary<string,object> meta)
    {
        string session=Meta(meta,"openai/session");if(session.Length==0)return "";
        // These opaque hints correlate calls, not authorize access or reveal a /c/ URL.
        string[] parts={Meta(meta,"openai/organization"),Meta(meta,"openai/subject"),session};
        using(var hash=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(String.Join("",parts.Select(p=>p.Length+":"+p)))));
    }
    public static string Resolve(string key,string explicitId,string path)
    {
        if(key.Length==0)return Validate(explicitId);
        lock(Gate){
            Conversation bound=Items.Find(x=>x.HostKey==key),selected=null;
            if(!string.IsNullOrEmpty(explicitId)&&explicitId!="unassigned"){
                selected=Items.Find(x=>x.Id==explicitId);if(selected==null)throw new ArgumentException("Unknown thread_id");
                if((selected.HostKey!=null&&selected.HostKey!=key)||(bound!=null&&bound!=selected))throw new ArgumentException("THREAD_MISMATCH: thread_id belongs to a different host conversation");
                selected.HostKey=key;bound=selected;
            }
            if(bound==null){if(Items.Count>=200)throw new ArgumentException("Conversation limit reached (200)");bound=new Conversation{Id="thread-"+Guid.NewGuid().ToString("N"),Title=path.Length>0?DeriveTitle(path):"ChatGPT 对话",Path=path,ChatId="",HostKey=key,Created=DateTime.UtcNow};Items.Add(bound);}
            if(path.Length>0){bound.Path=path;if(!bound.Named)bound.Title=DeriveTitle(path);}
            return bound.Id;
        }
    }
    public static string Validate(string id)
    {
        if(string.IsNullOrEmpty(id)||id=="unassigned")return "unassigned";
        lock(Gate){if(!Items.Any(x=>x.Id==id))throw new ArgumentException("Unknown thread_id. Call register_conversation first; never reuse another conversation's ID.");}return id;
    }
    public static object Register(string title,string path,string chatId,string existing)
    {
        title=(title??"").Trim();
        if(title.Length==0)title=DeriveTitle(path);
        if(title.Length>120)throw new ArgumentException("title must contain 1..120 characters");
        Guid parsed;if(chatId.Length>0&&!Guid.TryParseExact(chatId,"D",out parsed))throw new ArgumentException("chat_id must be the actual UUID from a known ChatGPT /c/ URL; omit it if unknown");
        lock(Gate){
            Conversation c=null;
            if(!string.IsNullOrEmpty(existing)&&existing!="unassigned"){c=Items.Find(x=>x.Id==existing);if(c==null)throw new ArgumentException("Unknown thread_id");if(c.ChatId.Length>0&&chatId.Length>0&&c.ChatId!=chatId)throw new ArgumentException("Existing thread is bound to a different chat_id");}
            if(c==null&&chatId.Length>0)c=Items.Find(x=>x.ChatId==chatId);
            if(c==null){if(Items.Count>=200)throw new ArgumentException("Conversation limit reached for this instance (200)");c=new Conversation{Id="thread-"+Guid.NewGuid().ToString("N"),Created=DateTime.UtcNow,ChatId=""};Items.Add(c);}
            c.Title=title;c.Named=true;c.Path=path;if(chatId.Length>0)c.ChatId=chatId;
            return new{thread_id=c.Id,title=c.Title,path=c.Path,chat_id=c.ChatId,chat_url=c.ChatId.Length==0?null:"https://chatgpt.com/c/"+c.ChatId,dashboard_url=LocalDashboard.Url+"#thread="+c.Id,instruction="Tell the user this conversation title and dashboard URL BEFORE starting work. Pass thread_id on EVERY subsequent tool call. This is a local grouping ID, not an automatically discovered ChatGPT chat ID."};
        }
    }
    // Title is optional; derive a readable default from the workspace directory so registration only needs a path.
    static string DeriveTitle(string path)
    {
        string trimmed=(path??"").Trim().TrimEnd('/');
        int slash=trimmed.LastIndexOf('/');
        string name=slash>=0?trimmed.Substring(slash+1):trimmed;
        if(name.Length==0||name.EndsWith(":"))name=trimmed;
        if(name.Length==0)name="工作区 "+DateTime.UtcNow.ToString("HH:mm");
        return name.Length>120?name.Substring(0,120):name;
    }
    public static object[] List(){lock(Gate)return Items.Select(c=>new{thread_id=c.Id,title=c.Title,path=c.Path,chat_id=c.ChatId,chat_url=c.ChatId.Length==0?null:"https://chatgpt.com/c/"+c.ChatId,association=c.HostKey==null?"manual":"host_session",created_at=c.Created.ToString("o")}).ToArray();}
}
