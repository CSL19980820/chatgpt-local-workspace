using System;
using System.Linq;
using System.Collections.Generic;

// Local grouping from host session metadata or explicit registration, never the last caller.
static class WorkspaceThreads
{
    public sealed class Conversation { public string Id,Title,Path,ChatId,HostKey; public bool Named; public DateTime Created; }
    public sealed class Binding { public string Id,ChatId,HostKey; }
    static readonly object Gate=new object();
    // Preserve only identity for command deduplication and file-undo ownership.
    // Titles, workspace paths and the visible conversation list never survive a restart.
    static readonly List<Binding> Bindings=LoadBindings();
    static readonly List<Conversation> Items=new List<Conversation>();
    static bool pendingSave;
    static void SaveBindings(bool changed)
    {
        if(!changed&&!pendingSave)return;
        pendingSave=true;WorkspaceStore.Save("thread-bindings",Bindings);pendingSave=false;
    }
    static List<Binding> LoadBindings()
    {
        var bindings=WorkspaceStore.Load("thread-bindings",()=>new List<Binding>());
        var legacy=WorkspaceStore.Load("threads",()=>new List<Conversation>());
        foreach(var old in legacy) {
            bindings.RemoveAll(b=>b.Id==old.Id);bindings.Add(new Binding{Id=old.Id,ChatId=old.ChatId??"",HostKey=old.HostKey});
        }
        if(legacy.Count>0)WorkspaceStore.Save("thread-bindings",bindings);
        WorkspaceStore.Delete("threads");
        return bindings;
    }
    public static void Initialize(){lock(Gate){}}
    static void MakeRoom()
    {
        if(Bindings.Count<200)return;
        // Never discard identities still owning retry receipts, file history or current work.
        var protectedIds=new HashSet<string>(Items.Select(c=>c.Id).Concat(WorkspaceRequests.ThreadReferences()).Concat(WorkspaceJournal.ThreadReferences()));
        Bindings.RemoveAll(b=>!protectedIds.Contains(b.Id));
        if(Bindings.Count>=200)throw new ArgumentException("Conversation limit reached (200 active or protected identities)");
    }
    static Conversation Activate(Binding binding,string path)
    {
        var current=Items.Find(c=>c.Id==binding.Id);
        if(current==null){current=new Conversation{Id=binding.Id,ChatId=binding.ChatId??"",HostKey=binding.HostKey,Path=path,Title=DeriveTitle(path),Created=DateTime.UtcNow};Items.Add(current);}
        current.HostKey=binding.HostKey;current.ChatId=binding.ChatId??"";
        if(path.Length>0){current.Path=path;if(!current.Named)current.Title=DeriveTitle(path);}
        return current;
    }
    static string Meta(Dictionary<string,object> meta,string name){object value;if(meta==null||!meta.TryGetValue(name,out value))return "";var text=value as string;if(text==null||text.Length>512)throw new ArgumentException("Invalid host conversation metadata");return text;}
    // Lenient read for other hosts' metadata: an unexpected shape is ignored, never fatal.
    static string Optional(Dictionary<string,object> meta,string name){object value;if(meta==null||!meta.TryGetValue(name,out value))return "";var text=value as string;return text==null||text.Length>512?"":text.Trim();}
    static Dictionary<string,object> CodexTurn(Dictionary<string,object> meta)
    {
        object value;if(meta==null||!meta.TryGetValue("x-codex-turn-metadata",out value)||value==null)return null;
        var map=value as Dictionary<string,object>;if(map!=null)return map;
        var text=value as string;if(text==null||text.Length>8192)return null;
        try{return new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,object>>(text);}catch{return null;}
    }
    // Codex sends x-codex-turn-metadata.turn_id per model turn; other hosts have no turn identifier.
    public static string TurnId(Dictionary<string,object> meta)
    {
        var turn=CodexTurn(meta);if(turn==null)return null;
        string id=Optional(turn,"turn_id");return id.Length==0||id.Length>128?null:id;
    }
    const string CodexKeyPrefix="codex:";
    // conversations[].source: manual (registered, no host signal), codex (Codex threadId) or chatgpt (openai/session).
    public static string Source(string hostKey){return hostKey==null?"manual":hostKey.StartsWith(CodexKeyPrefix,StringComparison.Ordinal)?"codex":"chatgpt";}
    // Codex sends threadId (stable across resume) instead of openai/session.
    static string CodexThread(Dictionary<string,object> meta)
    {
        string thread=Optional(meta,"threadId");if(thread.Length>0)return thread;
        var turn=CodexTurn(meta);return turn==null?"":Optional(turn,"thread_id");
    }
    public static string HostKey(Dictionary<string,object> meta)
    {
        string session=Meta(meta,"openai/session");
        if(session.Length==0){
            string codex=CodexThread(meta);if(codex.Length==0)return "";
            // The "codex:" prefix only labels the source; ChatGPT keys keep their pre-2.4 format.
            using(var hash=System.Security.Cryptography.SHA256.Create())return CodexKeyPrefix+Convert.ToBase64String(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes("codex:"+codex.Length+":"+codex)));
        }
        // These opaque hints correlate calls, not authorize access or reveal a /c/ URL.
        string[] parts={Meta(meta,"openai/organization"),Meta(meta,"openai/subject"),session};
        using(var hash=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(String.Join("",parts.Select(p=>p.Length+":"+p)))));
    }
    public static string Resolve(string key,string explicitId,string path)
    {
        lock(Gate){
            if(key.Length==0){string id=Validate(explicitId);if(id!="unassigned"){Activate(Bindings.Find(b=>b.Id==id),path);SaveBindings(false);}return id;}
            Binding bound=Bindings.Find(x=>x.HostKey==key),selected=null;bool changed=false;
            if(!string.IsNullOrEmpty(explicitId)&&explicitId!="unassigned"){
                selected=Bindings.Find(x=>x.Id==explicitId);if(selected==null)throw new ArgumentException("Unknown thread_id");
                if((selected.HostKey!=null&&selected.HostKey!=key)||(bound!=null&&bound!=selected))throw new ArgumentException("THREAD_MISMATCH: thread_id belongs to a different host conversation");
                changed=selected.HostKey!=key;selected.HostKey=key;bound=selected;
            }
            if(bound==null){MakeRoom();bound=new Binding{Id="thread-"+Guid.NewGuid().ToString("N"),ChatId="",HostKey=key};Bindings.Add(bound);changed=true;}
            Activate(bound,path);SaveBindings(changed);return bound.Id;
        }
    }
    public static string Validate(string id)
    {
        if(string.IsNullOrEmpty(id)||id=="unassigned")return "unassigned";
        lock(Gate){if(!Bindings.Any(x=>x.Id==id))throw new ArgumentException("Unknown thread_id. Call register_conversation first; never reuse another conversation's ID.");}return id;
    }
    public static object Register(string title,string path,string chatId,string existing)
    {
        title=(title??"").Trim();
        if(title.Length==0)title=DeriveTitle(path);
        if(title.Length>120)throw new ArgumentException("title must contain 1..120 characters");
        Guid parsed;if(chatId.Length>0&&!Guid.TryParseExact(chatId,"D",out parsed))throw new ArgumentException("chat_id must be the actual UUID from a known ChatGPT /c/ URL; omit it if unknown");
        lock(Gate){
            Binding binding=null;bool changed=false;
            if(!string.IsNullOrEmpty(existing)&&existing!="unassigned"){binding=Bindings.Find(x=>x.Id==existing);if(binding==null)throw new ArgumentException("Unknown thread_id");if((binding.ChatId??"").Length>0&&chatId.Length>0&&binding.ChatId!=chatId)throw new ArgumentException("Existing thread is bound to a different chat_id");}
            if(binding==null&&chatId.Length>0)binding=Bindings.Find(x=>x.ChatId==chatId);
            if(binding==null){MakeRoom();binding=new Binding{Id="thread-"+Guid.NewGuid().ToString("N"),ChatId=""};Bindings.Add(binding);changed=true;}
            var c=Activate(binding,path);
            c.Title=title;c.Named=true;c.Path=path;if(chatId.Length>0)c.ChatId=chatId;
            changed=changed||binding.ChatId!=c.ChatId;binding.ChatId=c.ChatId;SaveBindings(changed);return new{thread_id=c.Id,title=c.Title,path=c.Path,chat_id=c.ChatId,chat_url=c.ChatId.Length==0?null:"https://chatgpt.com/c/"+c.ChatId,dashboard_url=LocalDashboard.Url+"#thread="+c.Id,instruction="Tell the user this conversation title and dashboard URL BEFORE starting work. Pass thread_id on EVERY subsequent tool call. This is a local grouping ID, not an automatically discovered ChatGPT chat ID."};
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
    public static object[] List(Func<string,int> runningCommands=null)
    {
        Conversation[] items;lock(Gate)items=Items.ToArray();
        return items.Select(c=>new{thread_id=c.Id,title=c.Title,path=c.Path,chat_id=c.ChatId,chat_url=c.ChatId.Length==0?null:"https://chatgpt.com/c/"+c.ChatId,association=c.HostKey==null?"manual":"host_session",source=Source(c.HostKey),created_at=c.Created.ToString("o"),stats=WorkspaceActivity.StatsFor(c.Id,runningCommands==null?0:runningCommands(c.Id))}).ToArray();
    }
}
