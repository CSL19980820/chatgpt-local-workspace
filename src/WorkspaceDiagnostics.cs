using System;
using System.IO;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;

static class WorkspaceDiagnostics
{
    static readonly object Gate=new object();
    static string initialized,discovered,lastSuccess,lastFailure,lastTool;
    public static bool HostSessionObserved;
    public static void Initialize(){lock(Gate)initialized=DateTime.UtcNow.ToString("o");}
    public static void Discover(){lock(Gate)discovered=DateTime.UtcNow.ToString("o");}
    public static void Completed(string tool,bool failed){lock(Gate){lastTool=tool;if(failed)lastFailure=DateTime.UtcNow.ToString("o");else lastSuccess=DateTime.UtcNow.ToString("o");}}
    static object Check(string label,string status,string detail){return new{label=label,status=status,detail=detail};}
    static bool Probe(string url){try{var req=(HttpWebRequest)WebRequest.Create(url);req.Proxy=null;req.AllowAutoRedirect=false;req.Timeout=1500;using(var response=(HttpWebResponse)req.GetResponse())return response.StatusCode==HttpStatusCode.OK;}catch{return false;}}
    public static object[] Doctor(string tunnel,string key)
    {
        string exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tunnel-client.exe");
        if(!File.Exists(exe))return new[]{Check("配置自检","unavailable","未找到同目录的 tunnel-client.exe。")};
        var result=new List<object>();
        try{
            var info=new ProcessStartInfo(exe,"doctor --json --explain"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8};
            info.EnvironmentVariables["CONTROL_PLANE_TUNNEL_ID"]=tunnel??"";info.EnvironmentVariables["CONTROL_PLANE_API_KEY"]=key??"";
            info.EnvironmentVariables["MCP_COMMAND"]="\""+System.Windows.Forms.Application.ExecutablePath.Replace('\\','/')+"\" --mcp";
            foreach(string name in new[]{"TUNNEL_CLIENT_CONFIG","TUNNEL_CLIENT_PROFILE","TUNNEL_CLIENT_PROFILE_FILE","MCP_SERVER_URL"})info.EnvironmentVariables.Remove(name);
            using(var process=Process.Start(info)){
                var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(8000)){process.Kill();return new[]{Check("配置自检","fail","自检超时，请检查 Tunnel Client。")};}
                var json=new JavaScriptSerializer{MaxJsonLength=1024*1024};var report=json.Deserialize<Dictionary<string,object>>(stdout.Result);object checks;
                if(report.TryGetValue("checks",out checks))foreach(var item in (IEnumerable)checks){
                    var row=item as Dictionary<string,object>;if(row==null)continue;
                    string id=Convert.ToString(row["id"]),status=Convert.ToString(row["status"]);
                    // Display only known check IDs and statuses, never raw doctor output or URLs.
                    var labels=new Dictionary<string,string>{{"config_source","配置来源"},{"profile_load","配置读取"},{"tunnel_id","Tunnel ID"},{"control_plane_api_key","运行密钥"},{"api_key","运行密钥"},{"mcp_transport","MCP 传输"},{"mcp_command","本地程序"}};
                    string label;if(labels.TryGetValue(id,out label))result.Add(Check(label,status=="PASS"?"pass":status=="FAIL"?"fail":"unavailable",status=="PASS"?"配置检查通过。":status=="FAIL"?"请检查连接配置中的对应项目。":"当前配置未检查该项目。"));
                }
                result.Insert(0,Check("配置自检",Convert.ToString(report["result"])=="pass"?"pass":"fail","由官方 Tunnel Client 检查；不代表宿主已成功调用工具。"));
            }
        }catch{result.Add(Check("配置自检","fail","无法完成自检，请确认 Tunnel Client 版本及文件完整性。"));}
        return result.ToArray();
    }
    public static object Read()
    {
        var checks=new List<object>();
        string tunnel=Environment.GetEnvironmentVariable("WORKSPACE_TUNNEL_ID"),key=Environment.GetEnvironmentVariable("CONTROL_PLANE_API_KEY");
        if(string.IsNullOrEmpty(tunnel))checks.Add(Check("配置自检","unavailable","当前为独立 MCP 进程；请从桌面程序启动连接或使用“更多 → 诊断连接”。"));
        else checks.AddRange(Doctor(tunnel,key));
        string healthFile=Environment.GetEnvironmentVariable("WORKSPACE_TUNNEL_HEALTH_FILE");Uri health=null;
        try{Uri parsed;if(!string.IsNullOrEmpty(healthFile)&&File.Exists(healthFile)&&Uri.TryCreate(File.ReadAllText(healthFile).Trim(),UriKind.Absolute,out parsed)&&parsed.Scheme=="http"&&parsed.Host=="127.0.0.1")health=parsed;}catch{}
        checks.Add(Check("隧道存活",health==null?"unavailable":Probe(new Uri(health,"/healthz").AbsoluteUri)?"pass":"fail",health==null?"没有关联的运行中隧道。":"检查官方 /healthz。"));
        checks.Add(Check("隧道就绪",health==null?"unavailable":Probe(new Uri(health,"/readyz").AbsoluteUri)?"pass":"fail",health==null?"没有关联的运行中隧道。":"检查官方 /readyz；就绪不等于工具调用成功。"));
        lock(Gate){
            checks.Add(Check("MCP 握手",initialized==null?"pending":"pass",initialized??"尚未收到 initialize 或 server/discover。"));
            checks.Add(Check("工具发现",discovered==null?"pending":"pass",discovered==null?"尚未收到 tools/list，请在宿主刷新工具。":WorkspaceServer.ToolCount+" 个工具 · "+discovered));
            checks.Add(Check("实际工具调用",lastSuccess==null?"pending":"pass",lastSuccess==null?"尚无成功调用，请从宿主调用 get_workspace_status。":"最近成功："+lastSuccess));
            checks.Add(Check("自动对话归属",HostSessionObserved?"pass":"pending",HostSessionObserved?"已收到官方匿名会话标识。":"尚未收到 openai/session；继续支持手动登记。"));
            return new{version=WorkspaceServer.Version,checked_at=DateTime.UtcNow.ToString("o"),checks=checks,last_tool=lastTool,last_failure=lastFailure,scope="仅当前进程实际观察到的请求；本地测试请求也会计入，不代表所有 ChatGPT 能力均已验收。"};
        }
    }
}
