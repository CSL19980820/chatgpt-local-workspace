using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

static class Presentation
{
    public static string DisplayPath(string path){return path.Replace('\\','/');}
    public static object Diff(string before,string after)
    {
        var a=before.Length==0?new string[0]:before.Replace("\r\n","\n").Split('\n');var b=after.Length==0?new string[0]:after.Replace("\r\n","\n").Split('\n');var rows=new List<object>();int prefix=0;
        while(prefix<a.Length&&prefix<b.Length&&a[prefix]==b[prefix])prefix++;
        int endA=a.Length,endB=b.Length;while(endA>prefix&&endB>prefix&&a[endA-1]==b[endB-1]){endA--;endB--;}
        int n=endA-prefix,m=endB-prefix,added=0,removed=0;bool coarse=(long)n*m>1000000;
        int previewChars=0;bool clipped=false;
        Action<string,int?,int?,string> add=(kind,oldLine,newLine,text)=>{if(rows.Count>=3000||previewChars>=80000){clipped=true;return;}int keep=Math.Min(text.Length,Math.Min(16000,80000-previewChars));if(keep<text.Length)clipped=true;rows.Add(new{kind=kind,old_line=oldLine,new_line=newLine,text=text.Substring(0,keep),text_truncated=keep<text.Length});previewChars+=keep;};
        for(int i=Math.Max(0,prefix-3);i<prefix;i++)add("context",i+1,i+1,a[i]);
        if(coarse){for(int i=prefix;i<endA;i++){add("remove",i+1,null,a[i]);removed++;}for(int j=prefix;j<endB;j++){add("add",null,j+1,b[j]);added++;}}
        else {
            var lcs=new int[n+1,m+1];for(int i=n-1;i>=0;i--)for(int j=m-1;j>=0;j--)lcs[i,j]=a[prefix+i]==b[prefix+j]?lcs[i+1,j+1]+1:Math.Max(lcs[i+1,j],lcs[i,j+1]);
            int x=0,y=0;while(x<n||y<m){if(x<n&&y<m&&a[prefix+x]==b[prefix+y]){add("context",prefix+x+1,prefix+y+1,a[prefix+x]);x++;y++;}else if(y<m&&(x==n||lcs[x,y+1]>lcs[x+1,y])){add("add",null,prefix+y+1,b[prefix+y]);added++;y++;}else{add("remove",prefix+x+1,null,a[prefix+x]);removed++;x++;}}
        }
        for(int i=0;i<3&&endA+i<a.Length&&endB+i<b.Length;i++)add("context",endA+i+1,endB+i+1,a[endA+i]);
        return new{rows=rows,added=added,removed=removed,coarse=coarse,truncated=clipped,identical=before==after};
    }
    sealed class Change {public string Path,Before,After,First,Last;}
    static readonly Dictionary<string,Change> Changes=new Dictionary<string,Change>(StringComparer.OrdinalIgnoreCase);
    static long budget;static int untracked;
    public static void Record(string path,string before,string after){Change old;Changes.TryGetValue(path,out old);long next=budget-(old==null?0:(old.Before.Length+old.After.Length)*2L)+((old==null?before:old.Before).Length+after.Length)*2L;if(next>16*1024*1024){untracked++;if(old!=null){budget-=(old.Before.Length+old.After.Length)*2L;Changes.Remove(path);}return;}var c=old??new Change{Path=path,Before=before,First=DateTime.UtcNow.ToString("o")};c.After=after;c.Last=DateTime.UtcNow.ToString("o");Changes[path]=c;budget=next;}
    public static object Review(string root){string full=Path.GetFullPath(root).TrimEnd('\\','/');var list=Changes.Values.Where(c=>c.Path.Equals(full,StringComparison.OrdinalIgnoreCase)||c.Path.StartsWith(full+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)).OrderBy(c=>c.Path).ToArray();return new{path=DisplayPath(full),scope="当前插件进程中通过 write_file/edit_file 记录的改动；不包含 shell 或其他程序的修改。",files=list.Take(25).Select(c=>new{path=DisplayPath(c.Path),first_changed=c.First,last_changed=c.Last,diff=Diff(c.Before,c.After)}).ToArray(),count=list.Length,omitted_files=Math.Max(0,list.Length-25),untracked_changes=untracked};}
}
