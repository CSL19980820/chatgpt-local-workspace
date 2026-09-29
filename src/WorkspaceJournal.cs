using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;

// Only direct file-tool changes are undoable. Shell side effects are not captured.
static class WorkspaceJournal
{
    public sealed class FileState {
        public string Path;
        [System.Web.Script.Serialization.ScriptIgnore] public byte[] Before,After;
        public string BeforeData { get { return Before==null?null:Convert.ToBase64String(Before); } set { Before=value==null?null:Convert.FromBase64String(value); } }
        public string AfterData { get { return After==null?null:Convert.ToBase64String(After); } set { After=value==null?null:Convert.FromBase64String(value); } }
    }
    public sealed class Change { public string Id,Thread,Tool,Status; public DateTime At; public List<FileState> Files; public bool Undone; }
    static readonly object Gate=new object();
    static readonly List<Change> Changes=WorkspaceStore.Load("changes",()=>new List<Change>());
    const int FileLimit=16*1024*1024;
    const long TotalLimit=48L*1024*1024;
    public static string Hash(byte[] data) { if(data==null)return "missing";using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant(); }
    public static void SafePath(string path)
    {
        for(string p=Path.GetFullPath(path);!string.IsNullOrEmpty(p);p=Path.GetDirectoryName(p)) {
            if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("File operation refuses reparse points: "+p);
        }
    }
    public static byte[] Read(string path) { SafePath(path);if(Directory.Exists(path))throw new IOException("Expected a file: "+path);if(!File.Exists(path))return null;using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(f.Length>FileLimit)throw new IOException("History file exceeds 16 MiB: "+path);var data=new byte[f.Length];int at=0,n;while(at<data.Length&&(n=f.Read(data,at,data.Length-at))>0)at+=n;if(at!=data.Length)throw new EndOfStreamException();return data;} }
    static bool Same(byte[] a,byte[] b) { return a==null?b==null:b!=null&&a.SequenceEqual(b); }
    static long Size(Change c) { return c.Files.Sum(f=>(long)(f.Before==null?0:f.Before.Length)+(f.After==null?0:f.After.Length)); }
    public static string Begin(string tool,IEnumerable<string> paths)
    {
        lock(Gate) {
            var files=paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(p=>new FileState{Path=Path.GetFullPath(p),Before=Read(p)}).ToList();
            var entry=new Change{Id=Guid.NewGuid().ToString("N"),Thread=WorkspaceServer.CurrentThread??"unassigned",Tool=tool,Status="prepared",At=DateTime.UtcNow,Files=files};
            if(Size(entry)*2>TotalLimit)throw new IOException("Change exceeds 48 MiB history budget; split the operation.");
            while(Changes.Count>0&&(Changes.Count>=100||Changes.Sum(c=>Size(c))+Size(entry)*2>TotalLimit))Changes.RemoveAt(0);
            Changes.Add(entry);WorkspaceStore.Save("changes",Changes);return entry.Id;
        }
    }
    public static bool Finish(string id)
    {
        lock(Gate) {
            var entry=Changes.First(c=>c.Id==id);
            foreach(var file in entry.Files)file.After=Read(file.Path);
            entry.Files=entry.Files.Where(f=>!Same(f.Before,f.After)).ToList();entry.Status="recorded";
            if(entry.Files.Count==0)Changes.Remove(entry);
            while(Changes.Count>1&&Changes.Sum(c=>Size(c))>TotalLimit)Changes.RemoveAt(0);
            WorkspaceStore.Save("changes",Changes);return entry.Files.Count>0;
        }
    }
    public static object History(string path,string thread)
    {
        lock(Gate)return new{path=Presentation.DisplayPath(path),changes=Changes.Where(c=>c.Thread==thread&&c.Files.Any(f=>WorkspaceActivity.Within(Presentation.DisplayPath(f.Path),Presentation.DisplayPath(path)))).Reverse().Select(c=>new{id=c.Id,tool=c.Tool,at=c.At.ToString("o"),status=c.Status,undone=c.Undone,files=c.Files.Select(f=>new{path=Presentation.DisplayPath(f.Path),before_sha256=Hash(f.Before),after_sha256=c.Status=="prepared"?null:Hash(f.After)}).ToArray()}).ToArray(),scope="write_file/edit_file/apply_patch only; newest 100 operations / 48 MiB. Prepared/interrupted changes require manual inspection. Empty directories and shell effects are not restored."};
    }
    public static string Target(string id,string thread)
    {
        lock(Gate) {
            var entry=Changes.FirstOrDefault(c=>c.Id==id&&c.Thread==thread);
            if(entry==null||entry.Files.Count==0)return "";
            string root=Path.GetDirectoryName(entry.Files[0].Path);
            foreach(var file in entry.Files)while(!string.IsNullOrEmpty(root)&&!WorkspaceActivity.Within(Presentation.DisplayPath(file.Path),Presentation.DisplayPath(root)))root=Path.GetDirectoryName(root);
            return root??Path.GetDirectoryName(entry.Files[0].Path);
        }
    }
    public static object Restore(string id,string thread,bool redo,bool apply)
    {
        lock(Gate) {
            var c=Changes.FirstOrDefault(x=>x.Id==id&&x.Thread==thread);if(c==null)throw new ArgumentException("Unknown change_id for this conversation");
            if(c.Status!="recorded")throw new IOException("Interrupted change needs manual inspection; automatic restore refused");
            if(c.Undone!=redo)throw new IOException(redo?"Change is not undone":"Change was already undone");
            foreach(var f in c.Files)if(!Same(Read(f.Path),redo?f.Before:f.After))throw new IOException("UNDO_CONFLICT: file changed since recorded operation: "+f.Path);
            if(apply) {
                c.Status="restoring";WorkspaceStore.Save("changes",Changes);
                var written=new List<FileState>();
                try { foreach(var f in c.Files) { if(!Same(Read(f.Path),redo?f.Before:f.After))throw new IOException("UNDO_CONFLICT: "+f.Path);Write(f.Path,redo?f.After:f.Before);written.Add(f); } }
                catch(Exception ex) {
                    var failures=new List<string>();foreach(var f in written.AsEnumerable().Reverse())try{Write(f.Path,redo?f.Before:f.After);}catch{failures.Add(f.Path);}
                    c.Status=failures.Count==0?"recorded":"interrupted";WorkspaceStore.Save("changes",Changes);
                    throw new IOException("RESTORE_FAILED: "+ex.Message+(failures.Count==0?"; applied files rolled back":"; rollback failed: "+string.Join(", ",failures)));
                }
                c.Undone=!redo;c.Status="recorded";WorkspaceStore.Save("changes",Changes);
                foreach(var f in c.Files)Presentation.Record(f.Path,Decode(redo?f.Before:f.After),Decode(redo?f.After:f.Before));
            }
            return new{change_id=id,applied=apply,action=redo?"redo":"undo",files=c.Files.Select(f=>new{path=Presentation.DisplayPath(f.Path),diff=Presentation.Diff(Decode(redo?f.Before:f.After),Decode(redo?f.After:f.Before))}).ToArray(),count=c.Files.Count};
        }
    }
    public static void Write(string path,byte[] data) { SafePath(path);if(data==null){if(File.Exists(path))File.Delete(path);}else{Directory.CreateDirectory(Path.GetDirectoryName(path));WorkspaceStore.AtomicWrite(path,data);} }
    static string Decode(byte[] bytes) { if(bytes==null)return "";using(var stream=new MemoryStream(bytes))using(var reader=new StreamReader(stream,Encoding.UTF8,true))return reader.ReadToEnd(); }
}
