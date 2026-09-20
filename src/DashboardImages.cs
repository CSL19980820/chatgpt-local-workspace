using System;
using System.Collections.Generic;

// Keep the exact bytes returned to MCP, not a later version of the file. Images are
// fetched only when selected; the one-second activity snapshot contains URLs only.
static class DashboardImages
{
    internal sealed class Image { public string Id,Mime; public byte[] Bytes; }
    static readonly object Gate=new object();
    static readonly List<Image> Items=new List<Image>();
    const int Budget=32*1024*1024;
    static int used;
    public static string Add(byte[] bytes,string mime)
    {
        lock(Gate)
        {
            while(Items.Count>0&&(used+bytes.Length>Budget||Items.Count>=100)){used-=Items[0].Bytes.Length;Items.RemoveAt(0);}
            var item=new Image{Id=Guid.NewGuid().ToString("N"),Mime=mime,Bytes=bytes};Items.Add(item);used+=bytes.Length;
            return "/api/images/"+item.Id;
        }
    }
    public static Image Get(string id){lock(Gate)return Items.Find(item=>item.Id==id);}
}
