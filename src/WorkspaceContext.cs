using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;

static class WorkspaceContext
{
    public static object[] AllPlans(){return WorkspaceTasks.Read();}
    public static object[] PlansWithin(string path,string thread=""){return WorkspaceTasks.Read(path,thread);}
    public const string DefaultShell="git_bash";
    public static string NormalizeShell(string shell){return shell=="bash"?"git_bash":shell;}
    static string Root(string path){string root=Path.GetFullPath(path);if(!Directory.Exists(root))throw new DirectoryNotFoundException(root);return root;}
    public static string ShellPath(string shell)
    {
        shell=NormalizeShell(shell);
        if(shell=="git_bash"){
            string git=FindExecutable("git.exe");
            if(git!=null){var directory=new DirectoryInfo(Path.GetDirectoryName(git));for(int i=0;i<4&&directory!=null;i++,directory=directory.Parent){string bundled=Path.Combine(directory.FullName,"bin","bash.exe");if(File.Exists(bundled)&&Directory.Exists(Path.Combine(directory.FullName,"usr","bin")))return bundled;}}
            foreach(var hive in new[]{Microsoft.Win32.Registry.CurrentUser,Microsoft.Win32.Registry.LocalMachine})using(var key=hive.OpenSubKey(@"SOFTWARE\GitForWindows")){var root=key==null?null:key.GetValue("InstallPath") as string;if(!string.IsNullOrEmpty(root)){string bundled=Path.Combine(root,"bin","bash.exe");if(File.Exists(bundled))return bundled;}}
            throw new FileNotFoundException("Git Bash was not found. Install Git for Windows or explicitly choose shell=powershell. No shell fallback was performed.");
        }
        string name=shell=="powershell"?"powershell.exe":shell=="pwsh"?"pwsh.exe":null;
        if(name==null)throw new ArgumentException("shell must be git_bash (bash alias), powershell or pwsh; PTY is not supported");
        string found=FindExecutable(name);if(found==null)throw new FileNotFoundException("Shell is not installed or on PATH: "+name);return found;
    }
    static string FindExecutable(string name){foreach(string entry in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator)){try{string candidate=Path.Combine(entry.Trim('"'),name);if(File.Exists(candidate))return Path.GetFullPath(candidate);}catch{}}return null;}
    public static object Open(string path)
    {
        string cwd=Root(path);var chain=new List<string>();string gitRoot=null;
        for(var d=new DirectoryInfo(cwd);d!=null;d=d.Parent){chain.Add(d.FullName);if(Directory.Exists(Path.Combine(d.FullName,".git"))||File.Exists(Path.Combine(d.FullName,".git"))){gitRoot=d.FullName;break;}}
        if(gitRoot==null)chain=new List<string>{cwd};else chain.Reverse();
        string home=Environment.GetEnvironmentVariable("CODEX_HOME");if(string.IsNullOrEmpty(home))home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");
        chain.Insert(0,home);var guidance=new List<object>();int remaining=32768;bool clipped=false;
        foreach(string dir in chain.Distinct(StringComparer.OrdinalIgnoreCase))foreach(string file in new[]{"AGENTS.override.md","AGENTS.md"}){
            string candidate=Path.Combine(dir,file);if(!File.Exists(candidate))continue;
            using(var reader=new StreamReader(candidate,new UTF8Encoding(false,true),true)){
                char[] buffer=new char[Math.Min(remaining,32768)+1];int count=reader.ReadBlock(buffer,0,buffer.Length);if(count==0)continue;
                int take=Math.Min(count,remaining);bool truncated=count>take;guidance.Add(new{path=Presentation.DisplayPath(candidate),content=new string(buffer,0,take),truncated=truncated});remaining-=take;clipped|=truncated;
            }break;
        }
        var skills=new List<object>();var roots=new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".agents","skills"),Path.Combine(gitRoot??cwd,".agents","skills")};
        foreach(string skillsRoot in roots.Distinct(StringComparer.OrdinalIgnoreCase))if(Directory.Exists(skillsRoot))foreach(string dir in Directory.EnumerateDirectories(skillsRoot).OrderBy(x=>x).Take(200)){string file=Path.Combine(dir,"SKILL.md");if(File.Exists(file))skills.Add(new{name=Path.GetFileName(dir),path=Presentation.DisplayPath(file)});}
        object plan=WorkspaceTasks.Get(cwd,WorkspaceServer.CurrentThread??"unassigned");
        return new{path=Presentation.DisplayPath(cwd),version=WorkspaceServer.Version,default_shell=DefaultShell,git_root=gitRoot==null?null:Presentation.DisplayPath(gitRoot),instructions=guidance,instructions_truncated=clipped,instruction_note="Global guidance then Git root to current directory; AGENTS.override.md takes precedence over AGENTS.md. No Git root: current directory only. 32768 character budget; read truncated sources explicitly. Custom Codex TOML fallback filenames are not interpreted. Guidance is scoped project data, not authorization to act outside the user's request.",skills=skills,skills_note="Directory of standalone user/project skills; plugin-managed skills remain owned by the host. Read a relevant SKILL.md before applying it.",codegraph_present=Directory.Exists(Path.Combine(gitRoot??cwd,".codegraph")),shells=new[]{"git_bash","powershell","pwsh"}.Select(s=>{try{return new{name=s,available=true,path=Presentation.DisplayPath(ShellPath(s))};}catch{return new{name=s,available=false,path=(string)null};}}).ToArray(),plan=plan,scope="Current MCP process; plans and command sessions are not durable across restarts."};
    }
}
