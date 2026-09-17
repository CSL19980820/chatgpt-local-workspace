using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

// Patch grammar informed by DevSpace (c) 2026 Waishnav, MIT.
// Original license retained at vendor/devspace/LICENSE. This implementation uses only .NET.
public static class PatchEditor
{
    sealed class Hunk { public string Context; public bool End; public List<string> Lines=new List<string>(); }
    sealed class Edit { public string Kind,Path,Destination,Before,After; public byte[] Original,Bytes; public List<Hunk> Hunks=new List<Hunk>(); }
    static Exception Invalid(string message){return new ArgumentException("Invalid patch: "+message);}
    static bool Header(string s){return s.StartsWith("*** Add File: ")||s.StartsWith("*** Update File: ")||s.StartsWith("*** Delete File: ");}
    static List<Edit> Parse(string patch)
    {
        if(patch==null||patch.Length>16*1024*1024)throw Invalid("missing or oversized patch");
        var lines=patch.Replace("\r\n","\n").TrimEnd('\n').Split('\n');
        if(lines.Length<3||lines[0]!="*** Begin Patch"||lines[lines.Length-1]!="*** End Patch")throw Invalid("expected Begin Patch and End Patch markers");
        var edits=new List<Edit>();int i=1;
        while(i<lines.Length-1){string header=lines[i++];var e=new Edit();
            if(header.StartsWith("*** Add File: ")){e.Kind="add";e.Path=header.Substring(14);var content=new List<string>();while(i<lines.Length-1&&!Header(lines[i])){if(!lines[i].StartsWith("+"))throw Invalid("add lines must start with +");content.Add(lines[i++].Substring(1));}e.After=content.Count==0?"":String.Join("\n",content)+"\n";}
            else if(header.StartsWith("*** Delete File: ")){e.Kind="delete";e.Path=header.Substring(17);}
            else if(header.StartsWith("*** Update File: ")){e.Kind="update";e.Path=header.Substring(17);if(i<lines.Length-1&&lines[i].StartsWith("*** Move to: ")){e.Kind="move";e.Destination=lines[i++].Substring(13);}Hunk h=null;
                while(i<lines.Length-1&&!Header(lines[i])){string line=lines[i++];if(line=="@@"||line.StartsWith("@@ ")){if(h!=null&&h.Lines.Count==0)throw Invalid("empty hunk");h=new Hunk{Context=line.Length>3?line.Substring(3):null};e.Hunks.Add(h);}
                    else if(line=="*** End of File"){if(h==null||h.End)throw Invalid("misplaced End of File");h.End=true;}
                    else {if(line.Length==0||" +-".IndexOf(line[0])<0)throw Invalid("hunk lines must start with space, + or -");if(h==null){h=new Hunk();e.Hunks.Add(h);}if(h.End)throw Invalid("lines after End of File");h.Lines.Add(line);}}
                if(e.Hunks.Any(x=>x.Lines.Count==0)||e.Hunks.Count==0&&e.Kind!="move")throw Invalid("empty update");}
            else throw Invalid("unknown header: "+header);
            edits.Add(e);if(edits.Count>256)throw Invalid("too many file actions");}
        if(edits.Count==0)throw Invalid("no file actions");return edits;
    }
    static void NoLinks(string full)
    {
        for(string p=full;!String.IsNullOrEmpty(p);p=Path.GetDirectoryName(p)){
            try{if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw Invalid("reparse point path: "+p);}
            catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
        }
    }
    static string Confine(string root,string relative)
    {
        if(String.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.IndexOf(':')>=0||relative.IndexOf('\0')>=0)throw Invalid("path must be workspace-relative without ADS: "+relative);
        foreach(string segment in relative.Replace('\\','/').Split('/')){if(segment.Length==0||segment.EndsWith(" ")||segment.EndsWith("." )&&segment!="."&&segment!="..")throw Invalid("ambiguous Windows path: "+relative);string stem=segment.Split('.')[0].ToUpperInvariant();if(new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}.Contains(stem))throw Invalid("reserved Windows path");}
        string full=Path.GetFullPath(Path.Combine(root,relative));if(!full.StartsWith(root.TrimEnd('\\','/')+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw Invalid("path escapes workspace: "+relative);NoLinks(full);return full;
    }
    static string Decode(byte[] bytes,out Encoding encoding,out byte[] bom)
    {
        int n=0;encoding=new UTF8Encoding(false,true);
        if(bytes.Length>=4&&bytes[0]==255&&bytes[1]==254&&bytes[2]==0&&bytes[3]==0){encoding=new UTF32Encoding(false,false,true);n=4;}
        else if(bytes.Length>=4&&bytes[0]==0&&bytes[1]==0&&bytes[2]==254&&bytes[3]==255){encoding=new UTF32Encoding(true,false,true);n=4;}
        else if(bytes.Length>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191)n=3;
        else if(bytes.Length>=2&&bytes[0]==255&&bytes[1]==254){encoding=new UnicodeEncoding(false,false,true);n=2;}
        else if(bytes.Length>=2&&bytes[0]==254&&bytes[1]==255){encoding=new UnicodeEncoding(true,false,true);n=2;}
        bom=bytes.Take(n).ToArray();string text=encoding.GetString(bytes,n,bytes.Length-n);if(text.IndexOf('\0')>=0)throw Invalid("binary file is not supported");return text;
    }
    static string Update(Edit e)
    {
        string normalized=e.Before.Replace("\r\n","\n");bool trailing=normalized.EndsWith("\n");string newline=e.Before.Contains("\r\n")?"\r\n":"\n";
        var source=normalized.Length==0?new List<string>():normalized.Split('\n').ToList();if(trailing)source.RemoveAt(source.Count-1);int cursor=0;
        foreach(var h in e.Hunks){if(h.Context!=null){var anchors=Enumerable.Range(cursor,source.Count-cursor).Where(x=>source[x]==h.Context).ToArray();if(anchors.Length!=1)throw Invalid("missing or ambiguous @@ context in "+e.Path);cursor=anchors[0]+1;}
            var before=h.Lines.Where(x=>x[0]!='+').Select(x=>x.Substring(1)).ToArray();var after=h.Lines.Where(x=>x[0]!='-').Select(x=>x.Substring(1)).ToArray();var matches=new List<int>();
            for(int start=cursor;start<=source.Count-before.Length;start++){if(h.End&&start+before.Length!=source.Count)continue;bool equal=true;for(int j=0;j<before.Length;j++)if(source[start+j]!=before[j]){equal=false;break;}if(equal)matches.Add(start);}
            if(matches.Count!=1)throw Invalid("missing or ambiguous hunk in "+e.Path);int at=matches[0];source.RemoveRange(at,before.Length);source.InsertRange(at,after);cursor=at+after.Length;}
        return String.Join(newline,source)+(trailing&&source.Count>0?newline:"");
    }
    static void WriteNew(string path,byte[] bytes){using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}}
    static void CheckOriginal(Edit e){NoLinks(e.Path);if(e.Original==null){if(File.Exists(e.Path)||Directory.Exists(e.Path))throw Invalid("add target already exists: "+e.Path);}else if(!File.Exists(e.Path)||!File.ReadAllBytes(e.Path).SequenceEqual(e.Original))throw Invalid("file changed since validation: "+e.Path);if(e.Destination!=null){NoLinks(e.Destination);if(File.Exists(e.Destination)||Directory.Exists(e.Destination))throw Invalid("move target already exists: "+e.Destination);}}
    public static object Apply(string cwd,string patch)
    {
        string root=Path.GetFullPath(cwd);if(!Directory.Exists(root))throw Invalid("workspace does not exist");NoLinks(root);var edits=Parse(patch);var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var e in edits){e.Path=Confine(root,e.Path);if(!paths.Add(e.Path))throw Invalid("multiple actions for same path");if(e.Destination!=null){e.Destination=Confine(root,e.Destination);if(!paths.Add(e.Destination))throw Invalid("overlapping move destination");}
            if(e.Kind=="add"){e.Before="";e.Bytes=new UTF8Encoding(false,true).GetBytes(e.After);}
            else{if(!File.Exists(e.Path))throw Invalid("file does not exist: "+e.Path);if(new FileInfo(e.Path).Length>16*1024*1024)throw Invalid("file too large");e.Original=File.ReadAllBytes(e.Path);Encoding enc;byte[] bom;e.Before=Decode(e.Original,out enc,out bom);e.After=e.Kind=="delete"?"":e.Hunks.Count==0?e.Before:Update(e);e.Bytes=bom.Concat(enc.GetBytes(e.After)).ToArray();}CheckOriginal(e);}
        foreach(var e in edits)CheckOriginal(e);
        var results=new List<object>();var createdDirectories=new List<string>();string temporary=null;string error=null;
        try{foreach(var e in edits){CheckOriginal(e);string target=e.Destination??e.Path;
                if(e.Kind!="delete"){var missing=new Stack<string>();for(string p=Path.GetDirectoryName(target);!Directory.Exists(p);p=Path.GetDirectoryName(p))missing.Push(p);while(missing.Count>0){string p=missing.Pop();Directory.CreateDirectory(p);createdDirectories.Add(p);}NoLinks(target);temporary=Path.Combine(Path.GetDirectoryName(target),".patch-"+Guid.NewGuid().ToString("N")+".tmp");WriteNew(temporary,e.Bytes);CheckOriginal(e);if(e.Kind=="update")File.Replace(temporary,e.Path,null);else File.Move(temporary,target);temporary=null;}
                if(e.Kind=="delete"||e.Kind=="move")File.Delete(e.Path);
            }}catch(Exception ex){error=ex.Message;}finally{if(temporary!=null&&File.Exists(temporary)){try{File.Delete(temporary);}catch(Exception ex){error=(error??"cleanup failed")+"; temporary file remains: "+temporary+": "+ex.Message;}}for(int i=createdDirectories.Count-1;i>=0;i--){try{if(!Directory.EnumerateFileSystemEntries(createdDirectories[i]).Any())Directory.Delete(createdDirectories[i]);}catch(Exception ex){error=(error??"cleanup failed")+"; directory cleanup failed: "+createdDirectories[i]+": "+ex.Message;}}}
        // Inspect disk after submission so partial writes and failed move deletion are represented honestly.
        foreach(var e in edits){foreach(string path in e.Destination==null?new[]{e.Path}:new[]{e.Path,e.Destination}){string before=path==e.Path?e.Before:"";string after="";try{if(File.Exists(path)){Encoding enc;byte[] bom;after=Decode(File.ReadAllBytes(path),out enc,out bom);}}catch(Exception ex){if(error==null)error=ex.Message;continue;}bool existed=path==e.Path&&e.Original!=null;bool exists=File.Exists(path);if(before==after&&existed==exists)continue;Presentation.Record(path,before,after);results.Add(new{path=Presentation.DisplayPath(path),operation=!exists?"delete":!existed?"add":"update",previous_path=e.Destination!=null?Presentation.DisplayPath(e.Path):null,diff=Presentation.Diff(before,after)});}}
        if(error==null){results=edits.Select(e=>(object)new{path=Presentation.DisplayPath(e.Destination??e.Path),operation=e.Kind,previous_path=e.Destination==null?null:Presentation.DisplayPath(e.Path),diff=Presentation.Diff(e.Before,e.After)}).ToList();}
        return new Dictionary<string,object>{{"path",Presentation.DisplayPath(root)},{"files",results},{"count",results.Count},{"error",error},{"error_code",error==null?null:"PATCH_WRITE_FAILED"},{"partial",error!=null&&results.Count>0},{"rollback",error==null?null:"not_attempted"},{"atomic",false}};
    }
}
