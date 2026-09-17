using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

static class FileSearch
{
    public static object Info(string path)
    {
        if(!File.Exists(path)&&!Directory.Exists(path))throw new FileNotFoundException("Path not found: "+Presentation.DisplayPath(path));
        FileSystemInfo f=Directory.Exists(path)?(FileSystemInfo)new DirectoryInfo(path):new FileInfo(path);
        return new{path=Presentation.DisplayPath(f.FullName),name=f.Name,directory=f is DirectoryInfo,size_bytes=f is FileInfo?(long?)((FileInfo)f).Length:null,last_modified_utc=f.LastWriteTimeUtc.ToString("o"),created_utc=f.CreationTimeUtc.ToString("o"),attributes=f.Attributes.ToString()};
    }
    public static object Find(string root,string pattern,string query,bool content,bool recursive,bool caseSensitive,int offset,int limit)
    {
        if(!Directory.Exists(root))throw new DirectoryNotFoundException("Directory not found: "+Presentation.DisplayPath(root));
        if(content&&query.Length==0)throw new ArgumentException("query must not be empty");
        var glob=new Regex("^"+Regex.Escape(pattern).Replace("\\*",".*").Replace("\\?",".")+"$",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(200));
        var stack=new Stack<string>();stack.Push(root);var results=new List<object>();int found=0,scanned=0,skipped=0;bool more=false,budget=false;var watch=Stopwatch.StartNew();
        while(stack.Count>0&&!more&&!budget){string dir=stack.Pop();FileSystemInfo[] entries;try{entries=new DirectoryInfo(dir).GetFileSystemInfos().OrderBy(f=>f.Name,StringComparer.OrdinalIgnoreCase).ToArray();}catch(IOException){skipped++;continue;}catch(UnauthorizedAccessException){skipped++;continue;}
            foreach(var f in entries){if(scanned>=10000||watch.ElapsedMilliseconds>5000){budget=true;break;}if((f.Attributes&FileAttributes.ReparsePoint)!=0){skipped++;continue;}if((f.Attributes&FileAttributes.Directory)!=0){if(recursive)stack.Push(f.FullName);continue;}scanned++;if(!glob.IsMatch(f.Name))continue;
                if(!content){if(found++<offset)continue;if(results.Count>=limit){more=true;break;}results.Add(new{path=Presentation.DisplayPath(f.FullName),name=f.Name});continue;}
                try{if(((FileInfo)f).Length>2*1024*1024){skipped++;continue;}using(var r=new StreamReader(f.FullName,true)){string line;int n=0;while((line=r.ReadLine())!=null){n++;if(watch.ElapsedMilliseconds>5000){budget=true;break;}if(line.IndexOf('\0')>=0){skipped++;break;}int at=line.IndexOf(query,caseSensitive?StringComparison.Ordinal:StringComparison.OrdinalIgnoreCase);if(at<0)continue;if(found++<offset)continue;if(results.Count>=limit){more=true;break;}int start=Math.Max(0,at-100);results.Add(new{path=Presentation.DisplayPath(f.FullName),line=n,column=at+1,text=line.Substring(start,Math.Min(600,line.Length-start)),excerpt_truncated=start>0||line.Length>600});}}}catch(IOException){skipped++;}catch(UnauthorizedAccessException){skipped++;}if(more||budget)break;
            }
        }
        return new{path=Presentation.DisplayPath(root),pattern=pattern,query=content?query:null,recursive=recursive,case_sensitive=caseSensitive,matches=results,returned_count=results.Count,scanned_files=scanned,skipped_paths=skipped,complete=!more&&!budget,truncated=budget,next_offset=more?(int?)(offset+results.Count):null,note=budget?"Search time/file budget reached. Narrow the directory or filename pattern; this is a partial result.":skipped>0?"Reparse points, inaccessible paths, binary text and files over 2 MiB may be skipped.":""};
    }
}
