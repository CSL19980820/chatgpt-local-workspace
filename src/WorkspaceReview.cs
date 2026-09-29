using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;

// Private Git references and a temporary index never stage or commit on the user's branch.
static class WorkspaceReview
{
    sealed class Reply { public string Output,Error; public int Code; public bool Truncated; }
    static Reply Git(string root,string args,string index=null)
    {
        var info=new ProcessStartInfo("git.exe","--no-optional-locks -c core.fsmonitor=false "+args){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        foreach(string key in new[]{"GIT_DIR","GIT_WORK_TREE","GIT_INDEX_FILE"})info.EnvironmentVariables.Remove(key);
        if(index!=null)info.EnvironmentVariables["GIT_INDEX_FILE"]=index;
        info.EnvironmentVariables["GIT_TERMINAL_PROMPT"]="0";
        info.EnvironmentVariables["GIT_AUTHOR_NAME"]="Local Workspace";info.EnvironmentVariables["GIT_COMMITTER_NAME"]="Local Workspace";
        info.EnvironmentVariables["GIT_AUTHOR_EMAIL"]="workspace@example.invalid";info.EnvironmentVariables["GIT_COMMITTER_EMAIL"]="workspace@example.invalid";
        using(var process=new Process{StartInfo=info}) {
            process.Start();process.StandardInput.Close();var output=new StringBuilder();var error=new StringBuilder();bool clipped=false;
            Func<StreamReader,StringBuilder,Task> pump=async(reader,text)=>{char[] buffer=new char[4096];int n;while((n=await reader.ReadAsync(buffer,0,buffer.Length))>0){int take=Math.Min(n,2*1024*1024-text.Length);if(take>0)text.Append(buffer,0,take);if(take<n)clipped=true;}};
            var tasks=new[]{pump(process.StandardOutput,output),pump(process.StandardError,error)};
            if(!process.WaitForExit(30000)){using(var kill=Process.Start(new ProcessStartInfo("taskkill.exe","/PID "+process.Id+" /T /F"){UseShellExecute=false,CreateNoWindow=true}))kill.WaitForExit(5000);throw new IOException("Review snapshot timed out; no baseline advanced");}
            Task.WaitAll(tasks);return new Reply{Code=process.ExitCode,Output=output.ToString(),Error=error.ToString(),Truncated=clipped};
        }
    }
    static string Require(string root,string args,string index=null){var r=Git(root,args,index);if(r.Code!=0||r.Truncated)throw new IOException("Review Git operation failed: "+r.Error+(r.Truncated?" (output limit exceeded)":""));return r.Output.Trim();}
    static string Ref(string root,string thread){return "refs/local-workspace/review/"+WorkspaceJournal.Hash(Encoding.UTF8.GetBytes(root.ToUpperInvariant()+"|"+thread));}
    static string Snapshot(string root)
    {
        string index=Path.Combine(WorkspaceStore.Root,"review-"+Guid.NewGuid().ToString("N")+".index");
        try {
            Require(root,"read-tree --empty",index);
            Require(root,"-c core.autocrlf=false -c core.safecrlf=false add -A -- .",index);
            string tree=Require(root,"write-tree",index);
            return Require(root,"commit-tree "+tree+" -m \"Local Workspace private review snapshot\"",index);
        } finally { if(File.Exists(index))File.Delete(index);if(File.Exists(index+".lock"))File.Delete(index+".lock"); }
    }
    public static object Open(string path,string thread)
    {
        try { string root=Require(path,"rev-parse --show-toplevel"),prefix=Ref(root,thread);var current=Git(root,"rev-parse --verify "+prefix+"/open");
            if(current.Code!=0){string commit=Snapshot(root);Require(root,"update-ref "+prefix+"/open "+commit);Require(root,"update-ref "+prefix+"/last "+commit);}
            return new{available=true,root=Presentation.DisplayPath(root),scope="Repository-wide private Git review baseline; includes tracked and non-ignored files, including external edits. No normal index, branch or remote is changed."};
        }catch(Exception ex){return new{available=false,reason=ex.Message};}
    }
    public static object Read(string path,string thread,string since,bool mark)
    {
        if(since!="workspace_open"&&since!="last_shown")throw new ArgumentException("since must be recorded, workspace_open or last_shown");
        string root=Require(path,"rev-parse --show-toplevel"),prefix=Ref(root,thread);
        var reference=Git(root,"rev-parse --verify "+prefix+(since=="workspace_open"?"/open":"/last"));
        if(reference.Code!=0)throw new IOException("Review baseline is unavailable; call open_workspace before editing. Existing history cannot be reconstructed.");
        string before=reference.Output.Trim(),after=Snapshot(root);
        var names=Git(root,"diff --no-ext-diff --no-textconv --no-renames --name-only -z "+before+" "+after+" --");
        if(names.Code!=0||names.Truncated)throw new IOException("Review path list exceeded its limit or could not be read; no baseline advanced");
        var paths=names.Output.Split(new[]{'\0'},StringSplitOptions.RemoveEmptyEntries);var diff=Git(root,"diff --no-ext-diff --no-textconv --no-color --no-renames "+before+" "+after+" --");
        if(diff.Code!=0)throw new IOException(diff.Error);
        if(mark&&(diff.Truncated||paths.Length>200))throw new IOException("Review is truncated; last_shown baseline was not advanced. Review a smaller change set.");
        if(mark)Require(root,"update-ref "+prefix+"/last "+after);
        return new{path=Presentation.DisplayPath(root),since=since,marked_reviewed=mark,baseline=before,snapshot=after,files=paths.Take(200).Select(p=>new{path=Presentation.DisplayPath(Path.Combine(root,p))}).ToArray(),count=paths.Length,output=diff.Output,truncated=diff.Truncated||paths.Length>200,scope="Git repository changes since the selected baseline; attribution to this conversation is not implied. Ignored files are omitted."};
    }
}
