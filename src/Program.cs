using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

class Settings { public string Tunnel="",Key=""; }
class MainForm:Form
{
    readonly string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LocalWorkspacePlugin");
    readonly JavaScriptSerializer json=new JavaScriptSerializer();
    TextBox tunnel=new TextBox(),key=new TextBox(),logs=new TextBox();Label status=new Label(),address=new Label();Button start=new Button(),stop=new Button(),save=new Button();
    ListView activity=new ListView();ComboBox threadFilter=new ComboBox();readonly List<ListViewItem> activityRows=new List<ListViewItem>();Button dashboardButton=new Button();string dashboardUrl;readonly Dictionary<string,string> threadNames=new Dictionary<string,string>();Label counter=new Label();readonly ConcurrentQueue<string> logQueue=new ConcurrentQueue<string>();System.Windows.Forms.Timer logTimer=new System.Windows.Forms.Timer();int queued,discarded;string healthUrl;bool checking;readonly bool preview;
    Settings cfg;Process client;IntPtr job;bool busy,closing;string session;CancellationTokenSource cancel;System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateJobObject(IntPtr a,string n);
    [DllImport("kernel32.dll")]static extern bool SetInformationJobObject(IntPtr h,int c,IntPtr p,uint s);
    [DllImport("kernel32.dll")]static extern bool AssignProcessToJobObject(IntPtr h,IntPtr p);
    [DllImport("kernel32.dll")]static extern bool TerminateJobObject(IntPtr h,uint c);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
    [StructLayout(LayoutKind.Sequential)]struct Basic {public long a,b;public uint flags;public UIntPtr min,max;public uint active;public UIntPtr affinity;public uint priority,scheduling;}
    [StructLayout(LayoutKind.Sequential)]struct Io {public ulong a,b,c,d,e,f;}
    [StructLayout(LayoutKind.Sequential)]struct Extended {public Basic basic;public Io io;public UIntPtr a,b,c,d;}
    public MainForm(bool previewOnly=false)
    {
        preview=previewOnly;if(!preview)Directory.CreateDirectory(data);string settings=Path.Combine(data,"settings.json");cfg=!preview&&File.Exists(settings)?json.Deserialize<Settings>(File.ReadAllText(settings)):new Settings();
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);Text="本地工作区 · "+WorkspaceServer.Version;ClientSize=new Size(940,650);MinimumSize=new Size(760,480);Font=new Font("Microsoft YaHei UI",9);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(245,247,250);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=1,RowCount=6};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(layout);
        var title=new Label{Text="本地工作区",Font=new Font(Font.FontFamily,18,FontStyle.Bold),Dock=DockStyle.Fill,ForeColor=Color.FromArgb(27,44,60)};layout.Controls.Add(title,0,0);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill};start.Text="启动连接";start.BackColor=Color.FromArgb(22,112,85);start.ForeColor=Color.White;stop.Text="停止";save.Text="保存配置";stop.Enabled=false;status.Text="● 已停止";status.AutoSize=true;status.Padding=new Padding(10,7,0,0);foreach(var b in new[]{start,stop,save}){b.AutoSize=true;b.Height=32;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;}var configButton=new Button{Text="连接配置",AutoSize=true,Height=32};dashboardButton.Text="打开实时工作台";dashboardButton.AutoSize=true;dashboardButton.Enabled=false;dashboardButton.Click+=(s,e)=>{try{if(dashboardUrl!=null)Process.Start(new ProcessStartInfo(dashboardUrl){UseShellExecute=true});}catch(Exception ex){Log("浏览器打开失败："+ex.Message+"，可复制工作台链接手动打开。");}};buttons.Controls.AddRange(new Control[]{start,stop,configButton,dashboardButton,status});layout.Controls.Add(buttons,0,1);
        var config=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,BackColor=Color.White,Padding=new Padding(10)};config.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));config.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Row(config,0,"Tunnel ID",tunnel);Row(config,1,"API Key",key);config.Controls.Add(save,1,2);key.UseSystemPasswordChar=true;tunnel.Text=cfg.Tunnel;key.Text=cfg.Key;layout.Controls.Add(config,0,2);
        address.Text="允许目录：全部本地磁盘    ·    OpenAI Tunnel → 本机工具";address.Dock=DockStyle.Fill;address.ForeColor=Color.FromArgb(90,106,122);layout.Controls.Add(address,0,3);
        var tabs=new TabControl{Dock=DockStyle.Fill};var actionsPage=new TabPage("操作记录");var rawPage=new TabPage("原始日志");tabs.TabPages.AddRange(new[]{actionsPage,rawPage});layout.Controls.Add(tabs,0,4);
        activity.Dock=DockStyle.Fill;activity.View=View.Details;activity.FullRowSelect=true;activity.GridLines=false;activity.Columns.Add("时间",90);activity.Columns.Add("对话线程",190);activity.Columns.Add("操作",120);activity.Columns.Add("内容 / 状态",600);activity.BackColor=Color.White;actionsPage.Controls.Add(activity);threadFilter.Dock=DockStyle.Top;threadFilter.DropDownStyle=ComboBoxStyle.DropDownList;threadFilter.Items.Add("全部对话");threadFilter.SelectedIndex=0;threadFilter.SelectedIndexChanged+=(s,e)=>RefreshActivity();actionsPage.Controls.Add(threadFilter);
        logs.Multiline=true;logs.ReadOnly=true;logs.ScrollBars=ScrollBars.Both;logs.WordWrap=false;logs.Font=new Font("Consolas",9);logs.Dock=DockStyle.Fill;logs.BackColor=Color.FromArgb(25,32,43);logs.ForeColor=Color.FromArgb(216,225,236);rawPage.Controls.Add(logs);
        var footer=new FlowLayoutPanel{Dock=DockStyle.Fill};counter.AutoSize=true;counter.Padding=new Padding(0,6,12,0);counter.Text="日志仅保存在当前窗口，退出后清空";var clear=new Button{Text="清空日志",AutoSize=true};var copy=new Button{Text="复制原始日志",AutoSize=true};clear.Click+=(s,e)=>{logs.Clear();activityRows.Clear();activity.Items.Clear();};copy.Click+=(s,e)=>{try{Clipboard.SetText(logs.Text.Length==0?"暂无日志":logs.Text);}catch(Exception ex){Log(ex.Message);}};var copyDashboard=new Button{Text="复制工作台链接",AutoSize=true};copyDashboard.Click+=(s,e)=>{try{if(dashboardUrl!=null)Clipboard.SetText(dashboardUrl);else Log("请先启动连接，等待工作台就绪。");}catch(Exception ex){Log(ex.Message);}};footer.Controls.AddRange(new Control[]{counter,clear,copy,copyDashboard});layout.Controls.Add(footer,0,5);
        foreach(float height in new[]{48f,42f,118f,34f})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));configButton.Click+=(s,e)=>{config.Visible=!config.Visible;layout.RowStyles[2].Height=config.Visible?118:0;};
        save.Click+=(s,e)=>{try{Save();Log("配置已保存。");}catch(Exception ex){Log(ex.Message);}};start.Click+=async(s,e)=>await StartRun();stop.Click+=(s,e)=>{if(cancel!=null)cancel.Cancel();StopRun();};
        timer.Interval=4000;timer.Tick+=async(s,e)=>{if(!busy&&client!=null&&client.HasExited){Log("隧道进程已退出，请查看日志。");StopRun();}else if(!busy&&!checking&&client!=null&&healthUrl!=null){checking=true;bool ready=await Probe(healthUrl+"/readyz");if(client!=null)status.Text=ready?"● 已连接":"● 连接异常，查看日志";checking=false;}};if(!preview)timer.Start();
        logTimer.Interval=150;logTimer.Tick+=(s,e)=>FlushLogs();logTimer.Start();
        FormClosing+=(s,e)=>{if(busy){closing=true;e.Cancel=true;cancel.Cancel();return;}StopRun();logs.Clear();};
    }
    void Row(TableLayoutPanel p,int i,string label,Control c){p.Controls.Add(new Label{Text=label,AutoSize=true,Anchor=AnchorStyles.Left},0,i);c.Dock=DockStyle.Fill;p.Controls.Add(c,1,i);}
    void Save(){if(preview)return;cfg.Tunnel=tunnel.Text.Trim();cfg.Key=key.Text.Trim();File.WriteAllText(Path.Combine(data,"settings.json"),json.Serialize(cfg));}
    void Log(string text){if(IsDisposed)return;if(cfg.Key.Length>0)text=text.Replace(cfg.Key,"[key]");if(Interlocked.Increment(ref queued)>5000){Interlocked.Decrement(ref queued);Interlocked.Increment(ref discarded);return;}logQueue.Enqueue(text);}
    void FlushLogs(){var batch=new StringBuilder();string line;for(int i=0;i<300&&logQueue.TryDequeue(out line);i++){Interlocked.Decrement(ref queued);string time=DateTime.Now.ToString("HH:mm:ss");batch.AppendLine(time+" "+line);string readable=line;if(line.StartsWith("[Dashboard] ")){Uri url;if(Uri.TryCreate(line.Substring(12).Trim(),UriKind.Absolute,out url)&&url.Scheme=="http"&&url.Host=="127.0.0.1"){dashboardUrl=url.AbsoluteUri;dashboardButton.Enabled=true;}}try{if(line.StartsWith("{")){var obj=json.Deserialize<Dictionary<string,object>>(line);object msg;if(obj.TryGetValue("msg",out msg))readable=Convert.ToString(msg);}}catch{}if(line.Contains("[Workspace]")||!line.StartsWith("{")||line.Contains("ERROR")||line.Contains("WARN")){string kind="状态";int index=readable.IndexOf("[Workspace]");if(index>=0){readable=readable.Substring(index+11);kind="工具调用";}string thread="未归属";var match=Regex.Match(readable,@"\[thread=([^\]]+)\]");if(match.Success){thread=match.Groups[1].Value;readable=readable.Replace(match.Value,"").Trim();}if(readable.StartsWith("对话登记 | ")){var pieces=readable.Split('|');if(pieces.Length>1)threadNames[thread]=pieces[1].Trim();}string threadLabel;threadLabel=threadNames.TryGetValue(thread,out threadLabel)?threadLabel+" · "+thread:thread;if(!threadFilter.Items.Contains(threadLabel))threadFilter.Items.Add(threadLabel);var item=new ListViewItem(time);item.Tag=thread;item.SubItems.Add(threadLabel);item.SubItems.Add(kind);item.SubItems.Add(readable);activityRows.Add(item);if(activityRows.Count>500)activityRows.RemoveAt(0);}}
        if(logs.TextLength>250000)logs.Text=logs.Text.Substring(logs.TextLength-120000);if(batch.Length>0){logs.AppendText(batch.ToString());RefreshActivity();}if(activity.Items.Count>0)activity.EnsureVisible(activity.Items.Count-1);if(discarded>0)counter.Text="日志过快，已丢弃 "+discarded+" 条缓冲记录；原始日志有容量限制";
    }
    void RefreshActivity(){string selected=Convert.ToString(threadFilter.SelectedItem);activity.BeginUpdate();activity.Items.Clear();foreach(var row in activityRows)if(selected=="全部对话"||row.SubItems[1].Text==selected)activity.Items.Add((ListViewItem)row.Clone());activity.EndUpdate();}
    void CreateJob(){job=CreateJobObject(IntPtr.Zero,null);var x=new Extended();x.basic.flags=0x2000;int size=Marshal.SizeOf(x);IntPtr p=Marshal.AllocHGlobal(size);try{Marshal.StructureToPtr(x,p,false);if(job==IntPtr.Zero||!SetInformationJobObject(job,9,p,(uint)size))throw new Exception("无法托管子进程。");}finally{Marshal.FreeHGlobal(p);}}
    async Task StartRun()
    {
        if(preview)return;busy=true;start.Enabled=save.Enabled=false;tunnel.ReadOnly=key.ReadOnly=true;stop.Enabled=true;cancel=new CancellationTokenSource();var ct=cancel.Token;
        try {
            Save();if(!Regex.IsMatch(cfg.Tunnel,"^tunnel_[a-zA-Z0-9]+$")||cfg.Key.Length<10)throw new Exception("请填写有效的 Tunnel ID 和 API Key。");
            string exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tunnel-client.exe");if(!File.Exists(exe))throw new Exception("请将 tunnel-client.exe 放在本程序旁边。");
            session=Path.Combine(data,"sessions",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(session);string health=Path.Combine(session,"health.url");CreateJob();
            var pi=new ProcessStartInfo(exe,"run --control-plane.tunnel-id "+cfg.Tunnel+" --health.listen-addr 127.0.0.1:0 --health.url-file \""+health+"\" --log.format json --log.level info"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=session,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
            pi.EnvironmentVariables["CONTROL_PLANE_API_KEY"]=cfg.Key;pi.EnvironmentVariables["MCP_COMMAND"]="\""+Application.ExecutablePath.Replace('\\','/')+"\" --mcp";
            foreach(string name in new[]{"MCP_SERVER_URL","TUNNEL_CLIENT_CONFIG","TUNNEL_CLIENT_PROFILE","TUNNEL_CLIENT_PROFILE_FILE","CLOUDFLARED_MANAGED","CLOUDFLARED_TUNNEL_TOKEN"})pi.EnvironmentVariables.Remove(name);
            client=new Process{StartInfo=pi};client.OutputDataReceived+=(s,e)=>{if(e.Data!=null)Log(e.Data);};client.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)Log(e.Data);};client.Start();if(!AssignProcessToJobObject(job,client.Handle)){client.Kill();throw new Exception("无法管理隧道进程。");}client.BeginOutputReadLine();client.BeginErrorReadLine();status.Text="正在连接…";Log("启动本地工作区工具和 OpenAI Tunnel。");
            for(int i=0;;i++) {ct.ThrowIfCancellationRequested();if(client.HasExited)throw new Exception("Tunnel 启动失败。");if(File.Exists(health)){string url=File.ReadAllText(health).Trim();healthUrl=url;address.Text="全部本地磁盘    ·    状态页："+url+"/ui";if(await Probe(url+"/readyz"))break;}if(i>90)throw new Exception("连接超时，请查看日志。");await Task.Delay(500,ct);}
            status.Text="● 隧道已连接";Log("本地工具 "+WorkspaceServer.Version+" 就绪，共 "+WorkspaceServer.ToolCount+" 个工具。隧道就绪不代表 ChatGPT 已刷新工具；在网页版 设置 → 插件 → 本地工作区 → 信息 中刷新后，调用 get_workspace_status 核对版本。查看 initialize、tools/list 和工具调用日志确认实际连接。");
        }catch(OperationCanceledException){StopRun();}catch(Exception ex){Log(ex.Message);StopRun();}finally{busy=false;start.Enabled=save.Enabled=client==null;tunnel.ReadOnly=key.ReadOnly=client!=null;stop.Enabled=client!=null;if(closing)Close();}
    }
    async Task<bool> Probe(string url){return await Task.Run(()=>{try{var req=(HttpWebRequest)WebRequest.Create(url);req.Timeout=1200;using(var r=(HttpWebResponse)req.GetResponse())return r.StatusCode==HttpStatusCode.OK;}catch{return false;}});}
    void StopRun(){healthUrl=null;dashboardUrl=null;dashboardButton.Enabled=false;if(job!=IntPtr.Zero){TerminateJobObject(job,0);CloseHandle(job);job=IntPtr.Zero;}if(client!=null){try{client.WaitForExit(3000);client.Dispose();}catch{}client=null;}if(session!=null){try{Directory.Delete(session,true);session=null;}catch(Exception ex){Log("临时目录清理失败："+ex.Message);}}status.Text="● 已停止";if(!busy){start.Enabled=save.Enabled=true;tunnel.ReadOnly=key.ReadOnly=false;stop.Enabled=false;}}
    [STAThread]static void Main(string[] args)
    {
        AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling",false);AppContext.SetSwitch("Switch.System.IO.BlockLongPaths",false);
        if(args.Length>0&&args[0]=="--mcp"){WorkspaceServer.Run();return;}
        if(args.Length>1&&args[0]=="--preview"){Application.EnableVisualStyles();using(var f=new MainForm(true)){f.Show();string demo=Path.Combine(Path.GetTempPath(),"workspace-preview").Replace('\\','/');f.Log("预览模式：未启动隧道，未读取或修改现有配置。");f.Log("[Workspace] read_file | "+demo+"/README.md | OK · 200 行");f.Log("[Workspace] edit_file | "+demo+"/example.ts | OK · +3 / -1");f.FlushLogs();Application.DoEvents();using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,f.Width,f.Height));bmp.Save(args[1]);}f.Close();}return;}
        bool created;using(var activate=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\LocalWorkspacePlugin.Activate"))using(var mutex=new Mutex(true,"Local\\LocalWorkspacePlugin",out created)){if(!created){activate.Set();return;}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try{var f=new MainForm();var activationTimer=new System.Windows.Forms.Timer{Interval=200};activationTimer.Tick+=(s,e)=>{if(activate.WaitOne(0)){f.Show();if(f.WindowState==FormWindowState.Minimized)f.WindowState=FormWindowState.Normal;f.Activate();}};activationTimer.Start();f.FormClosed+=(s,e)=>activationTimer.Dispose();if(args.Length>0&&args[0]=="--start")f.Shown+=async(s,e)=>await f.StartRun();if(args.Length>1&&args[0]=="--smoke"){f.Shown+=async(s,e)=>{await f.StartRun();f.Log("SMOKE running="+(f.client!=null));using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,f.Width,f.Height));bmp.Save(args[1]+".png");}f.StopRun();f.Log("SMOKE stopped="+(f.client==null)+" sessionClean="+(f.session==null));File.WriteAllText(args[1],f.logs.Text);f.Close();};}Application.Run(f);}catch(Exception ex){if(args.Length>1){File.WriteAllText(args[1]+".error",ex.ToString());Environment.ExitCode=1;}else MessageBox.Show(ex.Message,"本地工作区");}}
    }
}
