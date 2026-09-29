using System;
using System.IO;
using System.Text;
using System.Linq;

static class WorkspaceFiles
{
    public static void CheckHash(byte[] bytes,string expected)
    {
        if(expected.Length>0&&!string.Equals(WorkspaceJournal.Hash(bytes),expected,StringComparison.OrdinalIgnoreCase))throw new IOException("FILE_CONFLICT: expected_sha256 does not match; read the current file before editing.");
    }
    public static object Write(string path,string text,bool overwrite,string expected,bool dry)
    {
        byte[] before=WorkspaceJournal.Read(path);if(before!=null&&!overwrite)throw new IOException("File exists; overwrite=true is required");CheckHash(before,expected);
        byte[] after=new UTF8Encoding(false).GetBytes(text);return Apply(path,before,after,dry,false);
    }
    public static object Edit(string path,string old,string replacement,string expected,bool dry)
    {
        if(old.Length==0)throw new ArgumentException("old_text must not be empty");byte[] before=WorkspaceJournal.Read(path);if(before==null)throw new FileNotFoundException(path);CheckHash(before,expected);
        string content;Encoding encoding;using(var mem=new MemoryStream(before))using(var reader=new StreamReader(mem,new UTF8Encoding(false,true),true)){content=reader.ReadToEnd();encoding=reader.CurrentEncoding;}
        int at=content.IndexOf(old,StringComparison.Ordinal);
        if(at<0)throw new ArgumentException(Diagnose(content,old));
        if(content.IndexOf(old,at+1,StringComparison.Ordinal)>=0)throw new ArgumentException("EDIT_AMBIGUOUS: old_text has multiple (possibly overlapping) matches; include more surrounding lines.");
        string updated=content.Substring(0,at)+replacement+content.Substring(at+old.Length);byte[] pre=encoding.GetPreamble();bool bom=pre.Length>0&&before.Take(pre.Length).SequenceEqual(pre);
        return Apply(path,before,(bom?pre:new byte[0]).Concat(encoding.GetBytes(updated)).ToArray(),dry,true);
    }
    static object Apply(string path,byte[] before,byte[] after,bool dry,bool edit)
    {
        if(after.Length>16*1024*1024)throw new IOException("File exceeds 16 MiB limit");string id=null;
        bool changed=WorkspaceJournal.Hash(before)!=WorkspaceJournal.Hash(after);
        if(!dry&&changed) {
            id=WorkspaceJournal.Begin(edit?"edit_file":"write_file",new[]{path});
            try { CheckHash(WorkspaceJournal.Read(path),WorkspaceJournal.Hash(before));WorkspaceJournal.Write(path,after); }
            finally { WorkspaceJournal.Finish(id); }
            Presentation.Record(path,Decode(before),Decode(after));
        }
        return new{path=Presentation.DisplayPath(path),written=!dry,edited=edit&&!dry,created=before==null&&!dry,applied=!dry,changed=changed,change_id=id,before_sha256=WorkspaceJournal.Hash(before),after_sha256=WorkspaceJournal.Hash(after),diff=Presentation.Diff(Decode(before),Decode(after))};
    }
    static string Decode(byte[] bytes){if(bytes==null)return "";using(var mem=new MemoryStream(bytes))using(var r=new StreamReader(mem,Encoding.UTF8,true))return r.ReadToEnd();}
    static string Diagnose(string content,string expected)
    {
        if(content.Replace("\r\n","\n").Contains(expected.Replace("\r\n","\n")))return "EDIT_NOT_FOUND: line endings differ (CRLF/LF); use the exact current text. No replacement applied.";
        string needle=expected.Split('\n').FirstOrDefault(l=>l.Trim().Length>0)??expected;var lines=content.Split('\n');int index=Array.FindIndex(lines,l=>l.Trim()==needle.Trim());
        if(index<0){string token=needle.Trim();if(token.Length>24)token=token.Substring(0,24);if(token.Length>2)index=Array.FindIndex(lines,l=>l.Contains(token));}
        return "EDIT_NOT_FOUND: exact text is absent; read the file again. No fuzzy replacement applied."+(index<0?"":" Nearby line "+(index+1)+": "+lines[index].Substring(0,Math.Min(240,lines[index].Length)));
    }
}
