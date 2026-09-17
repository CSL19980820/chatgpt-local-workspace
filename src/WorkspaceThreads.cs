using System;
using System.Linq;
using System.Collections.Generic;

// An explicit local grouping, never inferred from a process or the last caller.
static class WorkspaceThreads
{
    sealed class Conversation { public string Id,Title,Path,ChatId; public DateTime Created; }
    static readonly object Gate=new object();
    static readonly List<Conversation> Items=new List<Conversation>();
    public static string Validate(string id)
    {
        if(string.IsNullOrEmpty(id)||id=="unassigned")return "unassigned";
        lock(Gate){if(!Items.Any(x=>x.Id==id))throw new ArgumentException("Unknown thread_id. Call register_conversation first; never reuse another conversation's ID.");}return id;
    }
    public static object Register(string title,string path,string chatId,string existing)
    {
        if(string.IsNullOrWhiteSpace(title)||title.Length>120)throw new ArgumentException("title must contain 1..120 characters");
        Guid parsed;if(chatId.Length>0&&!Guid.TryParseExact(chatId,"D",out parsed))throw new ArgumentException("chat_id must be the actual UUID from a known ChatGPT /c/ URL; omit it if unknown");
        lock(Gate){
            Conversation c=null;
            if(!string.IsNullOrEmpty(existing)&&existing!="unassigned"){c=Items.Find(x=>x.Id==existing);if(c==null)throw new ArgumentException("Unknown thread_id");if(c.ChatId.Length>0&&chatId.Length>0&&c.ChatId!=chatId)throw new ArgumentException("Existing thread is bound to a different chat_id");}
            if(c==null&&chatId.Length>0)c=Items.Find(x=>x.ChatId==chatId);
            if(c==null){if(Items.Count>=200)throw new ArgumentException("Conversation limit reached for this instance (200)");c=new Conversation{Id="thread-"+Guid.NewGuid().ToString("N"),Created=DateTime.UtcNow,ChatId=""};Items.Add(c);}
            c.Title=title;c.Path=path;if(chatId.Length>0)c.ChatId=chatId;
            return new{thread_id=c.Id,title=c.Title,path=c.Path,chat_id=c.ChatId,chat_url=c.ChatId.Length==0?null:"https://chatgpt.com/c/"+c.ChatId,dashboard_url=LocalDashboard.Url+"#thread="+c.Id,instruction="Tell the user this conversation title and dashboard URL BEFORE starting work. Pass thread_id on EVERY subsequent tool call. This is a local grouping ID, not an automatically discovered ChatGPT chat ID."};
        }
    }
    public static object[] List(){lock(Gate)return Items.Select(c=>new{thread_id=c.Id,title=c.Title,path=c.Path,chat_id=c.ChatId,chat_url=c.ChatId.Length==0?null:"https://chatgpt.com/c/"+c.ChatId,created_at=c.Created.ToString("o")}).ToArray();}
}
