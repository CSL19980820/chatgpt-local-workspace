using System;
using System.Collections.Generic;
using System.Text;

static class WorkspaceRequests
{
    public sealed class Request { public string Fingerprint,Session; }
    static readonly Dictionary<string,Request> Items=WorkspaceStore.Load("requests",()=>new Dictionary<string,Request>());
    static string Key(string thread,string id){return thread+"|"+id;}
    public static string Fingerprint(string cwd,string shell,string command,int timeout)
    { return WorkspaceJournal.Hash(Encoding.UTF8.GetBytes(cwd.Length+":"+cwd+shell.Length+":"+shell+command.Length+":"+command+":"+timeout)); }
    public static string Lookup(string thread,string id,string fingerprint)
    {
        if(id.Length==0)return null;
        if(id.Length>128||!System.Text.RegularExpressions.Regex.IsMatch(id,@"\A[A-Za-z0-9._-]+\z"))throw new ArgumentException("Invalid request_id (1..128 ASCII letters, digits, dot, underscore, hyphen)");
        Request r;if(!Items.TryGetValue(Key(thread,id),out r))return null;
        if(r.Fingerprint!=fingerprint)throw new ArgumentException("REQUEST_CONFLICT: request_id was already used with different execution arguments");
        return r.Session;
    }
    public static void Reserve(string thread,string id,string fingerprint,string session)
    {
        if(id.Length==0)return;
        if(Items.Count>=10000)throw new InvalidOperationException("Request retry ledger is full; inspect and archive the stopped runtime's state before resetting it. No command started.");
        Items.Add(Key(thread,id),new Request{Fingerprint=fingerprint,Session=session});
        try{WorkspaceStore.Save("requests",Items);}catch{Items.Remove(Key(thread,id));throw;}
    }
}
