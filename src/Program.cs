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

[assembly:System.Reflection.AssemblyVersion(WorkspaceServer.Version+".0")]
[assembly:System.Reflection.AssemblyFileVersion(WorkspaceServer.Version+".0")]

class Settings { public string Tunnel="",Key=""; public double Zoom=1.0; }
// Loads exact-size frames of the EXE's application icon (embedded by csc /win32icon).
static class AppIcons
{
    public const int SmallIconMetric=49,IconMetric=11,IconSmall=0,IconBig=1,SetIconMessage=0x0080,DpiChangedMessage=0x02E0;
    const uint ImageIcon=1;const int GroupIconType=14;
    [DllImport("user32.dll",SetLastError=true)]static extern IntPtr LoadImage(IntPtr instance,IntPtr name,uint type,int cx,int cy,uint flags);
    [DllImport("user32.dll",SetLastError=true,CharSet=CharSet.Unicode)]static extern IntPtr LoadImage(IntPtr instance,string name,uint type,int cx,int cy,uint flags);
    [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")]static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")]static extern int GetSystemMetricsForDpi(int index,uint dpi);
    [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
    delegate bool EnumNameProc(IntPtr module,IntPtr type,IntPtr name,IntPtr param);
    [DllImport("kernel32.dll")]static extern bool EnumResourceNames(IntPtr module,IntPtr type,EnumNameProc callback,IntPtr param);
    static readonly object gate=new object();static bool resolved;static IntPtr module,groupId;static string groupName;
    static void Resolve()
    {
        lock(gate){if(resolved)return;resolved=true;
            try{module=Marshal.GetHINSTANCE(typeof(AppIcons).Module);if(module==IntPtr.Zero||module==new IntPtr(-1)){module=IntPtr.Zero;return;}
                EnumResourceNames(module,new IntPtr(GroupIconType),(m,t,name,p)=>{if(((long)name>>16)==0)groupId=name;else groupName=Marshal.PtrToStringUni(name);return false;},IntPtr.Zero);
            }catch(Exception){module=IntPtr.Zero;}}
    }
    // Returns an owned HICON of exactly size x size (the closest frame, scaled only if missing), or zero.
    public static IntPtr Load(int size)
    {
        Resolve();if(module==IntPtr.Zero||size<=0)return IntPtr.Zero;
        if(groupName!=null)return LoadImage(module,groupName,ImageIcon,size,size,0);
        return groupId==IntPtr.Zero?IntPtr.Zero:LoadImage(module,groupId,ImageIcon,size,size,0);
    }
    public static int WindowDpi(IntPtr window)
    {
        try{uint dpi=GetDpiForWindow(window);if(dpi>0)return (int)dpi;}catch(EntryPointNotFoundException){}
        return 0;
    }
    // dpi 0 means "unknown": fall back to the process-level metric, which is already in the
    // process's own DPI context. A DPI-unaware window reports 96 and gets 16 / 32 px.
    public static int Metric(int index,int dpi)
    {
        if(dpi>0){try{int v=GetSystemMetricsForDpi(index,(uint)dpi);if(v>0)return v;}catch(EntryPointNotFoundException){}}
        return GetSystemMetrics(index);
    }
    public static void SendIcon(IntPtr window,int kind,IntPtr icon){SendMessage(window,SetIconMessage,new IntPtr(kind),icon);}
    public static void Destroy(ref IntPtr icon){if(icon!=IntPtr.Zero){DestroyIcon(icon);icon=IntPtr.Zero;}}
}
class MainForm:Form
{
    readonly string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LocalWorkspacePlugin");
    readonly JavaScriptSerializer json=new JavaScriptSerializer();
    UiInput tunnelInput=new UiInput(),keyInput=new UiInput();UiButton runToggle=new UiButton(),browserBtn=new UiButton(),moreBtn=new UiButton(),save=new UiButton();
    UiStatusPill statusPill=new UiStatusPill();Panel toolbar=new Panel();UiCard configCard=new UiCard();UiContext contextLabel=new UiContext();
    UiTabs tabs=new UiTabs();Panel pages=new Panel(),pageWork=new Panel(),pageAct=new Panel(),pageLog=new Panel(),pageCfg=new Panel();
    Panel wbHost=new Panel(),wbEmpty=new Panel();UiButton emptyStart=new UiButton(),emptyBrowser=new UiButton();
    UiSelect threadFilter=new UiSelect();ActivityGrid grid=new ActivityGrid();LogView logView=new LogView();
    Label configHint=new Label(),filterHint=new Label(),emptyTitle=new Label(),emptySub=new Label(),emptyFail=new Label();
    int lastMenuClose;LogLine foldLine;int foldCount;DateTime foldAt;
    readonly List<ActivityRow> activityRows=new List<ActivityRow>();readonly Dictionary<string,string> threadNames=new Dictionary<string,string>();
    readonly ConcurrentQueue<string> logQueue=new ConcurrentQueue<string>();System.Windows.Forms.Timer logTimer=new System.Windows.Forms.Timer();int queued,discarded;bool discardNoted;string healthUrl;bool checking;readonly bool preview;
    string dashboardUrl,webviewAt,contextText="允许目录：全部本地磁盘    ·    OpenAI Tunnel → 本机工具";
    WorkbenchHost host;bool webviewFailed,webviewCreating,navDone;
    Icon appIcon,formIcon;IntPtr iconForm,iconMark,iconSmall,iconBig;
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
        if(!preview){if(!string.IsNullOrEmpty(cfg.Key)){WorkspaceCredentials.Save(cfg.Key);SaveSettingsFile();}else cfg.Key=WorkspaceCredentials.Read();}
        LoadAppIcons();Text="本地工作区 · "+WorkspaceServer.Version;ClientSize=new Size(1000,640);MinimumSize=new Size(820,480);Font=Theme.Body;StartPosition=FormStartPosition.CenterScreen;BackColor=Theme.Window;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(layout);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));

        toolbar.Dock=DockStyle.Fill;toolbar.BackColor=Theme.Window;layout.Controls.Add(toolbar,0,0);
        var toolbarGrid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=5,RowCount=1};
        toolbarGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));toolbarGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));toolbarGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));toolbarGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));toolbarGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        runToggle.Text="启动连接";runToggle.Kind=UiButton.Variant.Primary;runToggle.Margin=new Padding(12,4,6,4);
        browserBtn.Text="在浏览器打开";browserBtn.Kind=UiButton.Variant.Secondary;browserBtn.Margin=new Padding(0,4,6,4);browserBtn.Enabled=false;
        moreBtn.Text="更多";moreBtn.Kind=UiButton.Variant.Secondary;moreBtn.Margin=new Padding(0,4,6,4);
        contextLabel.Dock=DockStyle.Fill;contextLabel.Text=contextText;contextLabel.Margin=new Padding(8,0,12,0);
        contextLabel.Click+=(s,e)=>{if(!string.IsNullOrEmpty(healthUrl))OpenExternal(healthUrl+"/ui");};
        statusPill.Value=UiStatusPill.State.Stopped;statusPill.Margin=new Padding(0,7,12,7);
        toolbarGrid.Controls.Add(runToggle,0,0);toolbarGrid.Controls.Add(browserBtn,1,0);toolbarGrid.Controls.Add(moreBtn,2,0);toolbarGrid.Controls.Add(contextLabel,3,0);toolbarGrid.Controls.Add(statusPill,4,0);
        toolbar.Controls.Add(toolbarGrid);

        tabs.Dock=DockStyle.Fill;tabs.BackColor=Theme.Window;tabs.AddTab("实时工作台");tabs.AddTab("操作记录");tabs.AddTab("原始日志");tabs.AddTab("连接配置");
        tabs.SelectedChanged+=()=>{pageWork.Visible=tabs.Selected==0;pageAct.Visible=tabs.Selected==1;pageLog.Visible=tabs.Selected==2;pageCfg.Visible=tabs.Selected==3;if(tabs.Selected==0)SyncWorkbench();};
        layout.Controls.Add(tabs,0,1);

        pages.Dock=DockStyle.Fill;layout.Controls.Add(pages,0,2);
        pageWork.Dock=pageAct.Dock=pageLog.Dock=pageCfg.Dock=DockStyle.Fill;pageAct.Visible=pageLog.Visible=pageCfg.Visible=false;
        pages.Controls.AddRange(new Control[]{pageWork,pageAct,pageLog,pageCfg});

        wbHost.BackColor=Theme.Window;
        wbEmpty.BackColor=Theme.Window;wbEmpty.Paint+=PaintEmpty;
        emptyTitle.Text="实时工作台未连接";emptyTitle.Font=Theme.BodyBold;emptyTitle.ForeColor=Theme.Text;emptyTitle.AutoSize=true;
        emptySub.Text="启动连接后，调用时间线、执行计划与命令输出会嵌入显示在这里。";emptySub.Font=Theme.Small;emptySub.ForeColor=Theme.Muted;emptySub.AutoSize=true;
        emptyFail.Font=Theme.Small;emptyFail.ForeColor=Theme.Danger;emptyFail.AutoSize=true;emptyFail.Visible=false;
        emptyStart.Text="启动连接";emptyStart.Kind=UiButton.Variant.Primary;emptyStart.Click+=async(s,e)=>{if(client==null&&!busy)await StartRun();};
        emptyBrowser.Text="在浏览器打开";emptyBrowser.Kind=UiButton.Variant.Secondary;emptyBrowser.Visible=false;emptyBrowser.Click+=(s,e)=>OpenExternal(dashboardUrl);
        wbEmpty.Resize+=(s,e)=>LayoutEmpty();
        wbEmpty.Controls.AddRange(new Control[]{emptyTitle,emptySub,emptyFail,emptyStart,emptyBrowser});
        pageWork.Resize+=(s,e)=>{wbHost.Bounds=new Rectangle(0,0,pageWork.Width,pageWork.Height);wbEmpty.Bounds=wbHost.Bounds;};
        pageWork.Controls.Add(wbEmpty);pageWork.Controls.Add(wbHost);
        {wbHost.Bounds=new Rectangle(0,0,pageWork.Width,pageWork.Height);wbEmpty.Bounds=wbHost.Bounds;}

        var filterBar=new Panel{Height=44,BackColor=Theme.Window};
        var filterGrid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
        filterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,264));filterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        threadFilter.Margin=new Padding(14,7,0,7);threadFilter.Dock=DockStyle.Fill;
        threadFilter.AddItem("全部对话");threadFilter.SelectedChanged+=()=>RefreshActivity();
        filterHint.Text="双击行复制 · Ctrl+C 复制选中行";filterHint.ForeColor=Theme.Faint;filterHint.Font=Theme.Tiny;filterHint.Dock=DockStyle.Fill;filterHint.TextAlign=ContentAlignment.MiddleRight;filterHint.Margin=new Padding(0,0,14,0);
        filterGrid.Controls.Add(threadFilter,0,0);filterGrid.Controls.Add(filterHint,1,0);
        filterBar.Controls.Add(filterGrid);
        pageAct.Resize+=(s,e)=>{int w=pageAct.Width,h=pageAct.Height;filterBar.Bounds=new Rectangle(0,0,w,44);grid.Bounds=new Rectangle(0,44,w,Math.Max(0,h-44));};
        pageAct.Controls.Add(filterBar);pageAct.Controls.Add(grid);
        {int w=pageAct.Width,h=pageAct.Height;filterBar.Bounds=new Rectangle(0,0,w,44);grid.Bounds=new Rectangle(0,44,w,Math.Max(0,h-44));}

        logView.Dock=DockStyle.Fill;pageLog.Controls.Add(logView);

        configCard.Padding=new Padding(12,8,12,10);
        var configGrid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=3,Padding=new Padding(16,10,16,8)};
        configGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,86));configGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));configGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        configGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,40));configGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,40));configGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var tunnelLabel=new Label{Text="Tunnel ID",ForeColor=Theme.Muted,Font=Theme.Small,Dock=DockStyle.Fill,Margin=new Padding(2,11,8,0)};var keyLabel=new Label{Text="API Key",ForeColor=Theme.Muted,Font=Theme.Small,Dock=DockStyle.Fill,Margin=new Padding(2,11,8,0)};
        tunnelInput.Dock=DockStyle.Fill;tunnelInput.Margin=new Padding(0,3,0,3);keyInput.Dock=DockStyle.Fill;keyInput.Margin=new Padding(0,3,0,3);keyInput.Masked=true;
        tunnelInput.Text=cfg.Tunnel;keyInput.Text=cfg.Key;
        configHint.Text="配置仅保存在本机，不会上传";configHint.ForeColor=Theme.Faint;configHint.Font=Theme.Tiny;configHint.Dock=DockStyle.Fill;configHint.Margin=new Padding(2,8,0,0);
        save.Text="保存配置";save.Kind=UiButton.Variant.Secondary;
        var saveCell=new Panel{Dock=DockStyle.Fill};saveCell.Controls.Add(save);
        saveCell.Resize+=(s,e)=>{save.Location=new Point(saveCell.Width-save.Width-2,4);};
        configGrid.Controls.Add(tunnelLabel,0,0);configGrid.Controls.Add(tunnelInput,1,0);configGrid.SetColumnSpan(tunnelInput,2);
        configGrid.Controls.Add(keyLabel,0,1);configGrid.Controls.Add(keyInput,1,1);configGrid.SetColumnSpan(keyInput,2);
        configGrid.Controls.Add(configHint,0,2);configGrid.SetColumnSpan(configHint,2);configGrid.Controls.Add(saveCell,2,2);
        configCard.Controls.Add(configGrid);
        pageCfg.Resize+=(s,e)=>LayoutCfg();
        pageCfg.Controls.Add(configCard);
        LayoutCfg();

        moreBtn.Click+=(s,e)=>{
            if(Environment.TickCount-lastMenuClose<250)return;
            var items=new List<UiMenuItem>{
                new UiMenuItem{Text="刷新工作台",Click=()=>{if(host!=null)host.Reload();}},
                new UiMenuItem{Text="诊断连接",Click=async()=>{if(dashboardUrl!=null){if(host!=null){tabs.Select(0);host.Navigate(dashboardUrl+"#diagnostics");}else OpenExternal(dashboardUrl+"#diagnostics");}else{string tunnel=tunnelInput.Text.Trim(),key=keyInput.Text.Trim();Log("正在检查连接配置…");var checks=await Task.Run(()=>WorkspaceDiagnostics.Doctor(tunnel,key));var report=new StringBuilder();foreach(var row in checks){var type=row.GetType();report.AppendLine(Convert.ToString(type.GetProperty("label").GetValue(row,null))+"："+Convert.ToString(type.GetProperty("status").GetValue(row,null))+" — "+Convert.ToString(type.GetProperty("detail").GetValue(row,null)));}MessageBox.Show(this,report.ToString(),"连接配置诊断",MessageBoxButtons.OK,MessageBoxIcon.Information);}}},
                new UiMenuItem{Text="清空已完成日志",Click=async()=>{try{await ClearLogs();}catch(Exception ex){MessageBox.Show(this,"日志未清空："+ex.Message,"本地工作区",MessageBoxButtons.OK,MessageBoxIcon.Information);}}},
                new UiMenuItem{Text="复制原始日志",Click=()=>{try{Clipboard.SetText(logView.Count==0?"暂无日志":logView.Text);}catch(Exception ex){Log(ex.Message);}}},
                new UiMenuItem{Text="复制工作台链接",Click=()=>{try{if(dashboardUrl!=null)Clipboard.SetText(dashboardUrl);else Log("请先启动连接，等待工作台就绪。");}catch(Exception ex){Log(ex.Message);}}},
                new UiMenuItem{Separator=true},
                new UiMenuItem{Text="调用日志、对话列表和执行计划退出后清空",Enabled=false}};
            var menu=new UiMenuForm(items,moreBtn.PointToScreen(new Point(0,moreBtn.Height-2)));
            menu.FormClosed+=(s2,e2)=>{lastMenuClose=Environment.TickCount;};
            menu.Show(this);
        };
        browserBtn.Click+=(s,e)=>OpenExternal(dashboardUrl);

        save.Click+=(s,e)=>{if(!Regex.IsMatch(tunnelInput.Text.Trim(),"^tunnel_[a-zA-Z0-9]+$")||keyInput.Text.Trim().Length<10){ShowConfigError();Log("配置未保存：请先修正标红的输入。");return;}try{Save();ClearConfigError();Log("配置已保存。");}catch(Exception ex){Log(ex.Message);}};
        tunnelInput.Box.TextChanged+=(s,e)=>{if(tunnelInput.Error)ShowConfigError();};
        keyInput.Box.TextChanged+=(s,e)=>{if(keyInput.Error)ShowConfigError();};
        runToggle.Click+=async(s,e)=>{if(client!=null||busy){if(cancel!=null)cancel.Cancel();StopRun();}else await StartRun();};
        timer.Interval=4000;timer.Tick+=async(s,e)=>{if(!busy&&client!=null&&client.HasExited){Log("隧道进程已退出，请查看日志。");StopRun();}else if(!busy&&!checking&&client!=null&&healthUrl!=null){checking=true;bool ready=await Probe(healthUrl+"/readyz");if(client!=null)statusPill.Value=ready?UiStatusPill.State.Live:UiStatusPill.State.Error;checking=false;}};if(!preview)timer.Start();
        logTimer.Interval=150;logTimer.Tick+=(s,e)=>FlushLogs();logTimer.Start();
        FormClosing+=(s,e)=>{if(busy){closing=true;e.Cancel=true;cancel.Cancel();return;}StopRun();logView.Clear();if(host!=null){host.Dispose();host=null;}};
        LayoutEmpty();SyncWorkbench();
    }
    void LayoutCfg()
    {
        int w=pageCfg.Width,h=pageCfg.Height;int cw=Math.Min(720,Math.Max(320,w-48));int ch=Math.Min(176,Math.Max(120,h-32));
        configCard.Bounds=new Rectangle((w-cw)/2,16,cw,ch);
    }
    void LayoutEmpty()
    {
        int cx=wbEmpty.Width/2,cy=wbEmpty.Height/2;
        emptyTitle.Location=new Point(cx-emptyTitle.Width/2,cy-86);
        emptySub.Location=new Point(cx-emptySub.Width/2,cy-58);
        emptyFail.Location=new Point(cx-emptyFail.Width/2,cy-30);
        emptyStart.Location=new Point(cx-emptyStart.Width/2,cy+4);
        emptyBrowser.Location=new Point(cx-emptyBrowser.Width/2,cy+4+(emptyStart.Visible?40:0));
    }
    void PaintEmpty(object s,PaintEventArgs e)
    {
        var g=e.Graphics;Theme.Smooth(g);
        using(var b=new SolidBrush(Theme.Window))g.FillRectangle(b,0,0,wbEmpty.Width,wbEmpty.Height);
        int cx=wbEmpty.Width/2,cy=wbEmpty.Height/2;
        if(appIcon!=null)g.DrawIcon(appIcon,new Rectangle(cx-20,cy-150,40,40));
    }
    void OpenExternal(string url){if(url==null)return;try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){Log("浏览器打开失败："+ex.Message+"，可复制工作台链接手动打开。");}}
    void Save(){if(preview)return;cfg.Tunnel=tunnelInput.Text.Trim();cfg.Key=keyInput.Text.Trim();WorkspaceCredentials.Save(cfg.Key);SaveSettingsFile();}
    // settings.json only carries non-secret preferences; the API key lives in the credential store.
    void SaveSettingsFile(){if(preview)return;WorkspaceStore.AtomicWrite(Path.Combine(data,"settings.json"),Encoding.UTF8.GetBytes(json.Serialize(new{Tunnel=cfg.Tunnel,Zoom=WorkbenchZoom.Clamp(cfg.Zoom)})));}
    void SetContext(string text){contextText=text;contextLabel.Text=text;contextLabel.Cursor=string.IsNullOrEmpty(healthUrl)?Cursors.Default:Cursors.Hand;}
    void ShowConfigError()
    {
        bool badT=!Regex.IsMatch(tunnelInput.Text.Trim(),"^tunnel_[a-zA-Z0-9]+$");bool badK=keyInput.Text.Trim().Length<10;
        tunnelInput.Error=badT;keyInput.Error=badK;
        if(badT||badK){configHint.Text=badT?"Tunnel ID 无效：应形如 tunnel_ 加字母数字，在连接器创建流程中获取":"API Key 无效：长度至少 10 位，在连接器创建流程中获取";configHint.ForeColor=Theme.Danger;}
        else ClearConfigError();
    }
    void ClearConfigError(){tunnelInput.Error=false;keyInput.Error=false;configHint.Text="配置仅保存在本机，不会上传";configHint.ForeColor=Theme.Faint;}
    void UpdateRunButton(){bool running=client!=null||busy;runToggle.Text=running?"停止":"启动连接";runToggle.Kind=running?UiButton.Variant.Secondary:UiButton.Variant.Primary;}
    void Log(string text){if(IsDisposed)return;if(cfg.Key.Length>0)text=text.Replace(cfg.Key,"[key]");if(Interlocked.Increment(ref queued)>5000){Interlocked.Decrement(ref queued);Interlocked.Increment(ref discarded);return;}logQueue.Enqueue(text);}
    void FlushLogs(){string line;bool any=false;for(int i=0;i<300&&logQueue.TryDequeue(out line);i++){Interlocked.Decrement(ref queued);string time=DateTime.Now.ToString("HH:mm:ss");any=true;if(TunnelLogFold.IsStartupNoise(line)){FoldNoise(time);continue;}string readable=line;if(line.StartsWith("[Dashboard] ")){Uri url;if(Uri.TryCreate(line.Substring(12).Trim(),UriKind.Absolute,out url)&&url.Scheme=="http"&&url.Host=="127.0.0.1"){SetDashboardUrl(url.AbsoluteUri);}}try{if(line.StartsWith("{")){var obj=json.Deserialize<Dictionary<string,object>>(line);object msg;if(obj.TryGetValue("msg",out msg))readable=Convert.ToString(msg);}}catch{}
        var ll=new LogLine{Time=time,Raw=line,Fore=Theme.Text};
        if(line.IndexOf("[Workspace]",StringComparison.Ordinal)>=0){ll.Chip="工具";ll.ChipFore=Theme.Primary;ll.ChipBack=Theme.PrimarySoft;}
        else{string level=null;var lm=Regex.Match(line,"\"level\"\\s*:\\s*\"(error|warn|warning|info|debug)\"");if(lm.Success)level=lm.Groups[1].Value;else if(line.IndexOf("ERROR",StringComparison.Ordinal)>=0)level="error";else if(line.IndexOf("WARN",StringComparison.Ordinal)>=0)level="warn";
            if(level=="error"){ll.Chip="error";ll.ChipFore=Theme.Danger;ll.ChipBack=Theme.DangerSoft;ll.Fore=Theme.Danger;}
            else if(level=="warn"||level=="warning"){ll.Chip="warn";ll.ChipFore=Theme.Warn;ll.ChipBack=Theme.WarnSoft;}
            else if(level==null){ll.Chip="日志";ll.ChipFore=Theme.Muted;ll.ChipBack=Theme.NeutralSoft;}
            else{ll.Chip=level;ll.ChipFore=Theme.Muted;ll.ChipBack=Theme.NeutralSoft;}}
        logView.NoteWidth(90+line.Length*7);logView.Append(ll);
        if(line.Contains("[Workspace]")||!line.StartsWith("{")||line.Contains("ERROR")||line.Contains("WARN")){string kind="状态";int index=readable.IndexOf("[Workspace]");if(index>=0){readable=readable.Substring(index+11);kind="工具调用";}string thread="未归属";var match=Regex.Match(readable,@"\[thread=([^\]]+)\]");if(match.Success){thread=match.Groups[1].Value;readable=readable.Replace(match.Value,"").Trim();}if(readable.StartsWith("对话登记 | ")){var pieces=readable.Split('|');if(pieces.Length>1)threadNames[thread]=pieces[1].Trim();}string threadLabel;threadLabel=threadNames.TryGetValue(thread,out threadLabel)?threadLabel+" · "+thread:thread;threadFilter.AddItem(threadLabel);activityRows.Add(new ActivityRow{Time=time,ThreadTag=thread,ThreadLabel=threadLabel,Kind=kind,Text=readable});if(activityRows.Count>500)activityRows.RemoveAt(0);}}
        if(any){RefreshActivity();tabs.SetCount(1,activityRows.Count>0?activityRows.Count.ToString():"");tabs.SetCount(2,logView.Count>0?logView.Count.ToString():"");}
        if(discarded>0&&!discardNoted){discardNoted=true;Log("日志过快，已丢弃 "+discarded+" 条缓冲记录；原始日志有容量限制");}
    }
    // tunnel-client 0.0.16 prints every fx dependency-injection step at INFO (~240 lines per start).
    // Fold them into one summary row per burst; WARN/ERROR and the useful INFO lines stay untouched.
    void FoldNoise(string time){DateTime now=DateTime.UtcNow;if(foldLine==null||!TunnelLogFold.SameBurst(foldAt,now)){foldCount=0;foldLine=new LogLine{Time=time,Chip="折叠",ChipFore=Theme.Muted,ChipBack=Theme.NeutralSoft,Fore=Theme.Faint};foldLine.Raw=TunnelLogFold.Summary(1);logView.Append(foldLine);}foldCount++;foldAt=now;foldLine.Raw=TunnelLogFold.Summary(foldCount);logView.NoteWidth(90+foldLine.Raw.Length*7);logView.Invalidate();}
    void RefreshActivity(){string selected=Convert.ToString(threadFilter.SelectedItem);var list=new List<ActivityRow>();foreach(var row in activityRows)if(selected=="全部对话"||selected==null||row.ThreadLabel==selected)list.Add(row);grid.SetRows(list);}
    async Task ClearLogs()
    {
        string url=dashboardUrl;
        if(url!=null)await Task.Run(()=>{
            var bootstrap=(HttpWebRequest)WebRequest.Create(new Uri(new Uri(url),"api/local-actions"));bootstrap.Proxy=null;bootstrap.Timeout=3000;
            string token;using(var response=bootstrap.GetResponse())using(var reader=new StreamReader(response.GetResponseStream()))token=Convert.ToString(new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(reader.ReadToEnd())["token"]);
            var request=(HttpWebRequest)WebRequest.Create(new Uri(new Uri(url),"api/clear-logs"));request.Proxy=null;request.Timeout=3000;request.Method="POST";request.ContentLength=0;request.Headers["Origin"]=new Uri(url).GetLeftPart(UriPartial.Authority);request.Headers["X-Workspace-Token"]=token;
            using(var response=request.GetResponse()){}
        });
        ClearDisplayLogs();if(host!=null)host.Reload();
    }
    void ClearDisplayLogs(){string line;while(logQueue.TryDequeue(out line))Interlocked.Decrement(ref queued);Interlocked.Exchange(ref discarded,0);discardNoted=false;foldLine=null;logView.Clear();grid.Clear();activityRows.Clear();RefreshActivity();tabs.SetCount(1,"");tabs.SetCount(2,"");}
    void SetDashboardUrl(string url){if(dashboardUrl==url)return;dashboardUrl=url;tabs.Select(0);SyncWorkbench();}
    void SyncWorkbench()
    {
        bool live=dashboardUrl!=null&&!webviewFailed;
        wbEmpty.Visible=!live;wbHost.Visible=live;
        browserBtn.Enabled=dashboardUrl!=null;
        if(dashboardUrl==null){emptyStart.Visible=client==null&&!busy;emptyBrowser.Visible=false;emptyFail.Visible=false;}
        else if(webviewFailed){emptyStart.Visible=false;emptyBrowser.Visible=true;emptyFail.Visible=true;emptyFail.Text="嵌入视图不可用（缺少 WebView2 运行时），已回退为浏览器打开。";}
        else{emptyStart.Visible=false;emptyBrowser.Visible=false;emptyFail.Visible=false;EnsureWebView();}
        LayoutEmpty();
    }
    async void EnsureWebView()
    {
        if(host!=null||webviewFailed||webviewCreating)return;webviewCreating=true;
        try{
            string folder=preview?Path.Combine(Path.GetTempPath(),"workspace-preview-webview"):Path.Combine(data,"webview2");
            var created=await WorkbenchHost.CreateAsync(folder,wbHost,cfg.Zoom);
            if(IsDisposed){created.Dispose();return;}
            host=created;host.NavigationCompleted+=()=>{navDone=true;};
            host.ZoomChanged+=z=>{cfg.Zoom=z;SaveSettingsFile();};
            webviewCreating=false;
            NavigateWorkbench();SyncWorkbench();
        }catch(Exception ex){webviewCreating=false;webviewFailed=true;Log("嵌入工作台不可用："+ex.Message);SyncWorkbench();}
    }
    // Window icons are loaded per size from the EXE's own icon group, so the title bar gets the
    // hand-tuned 16/20/24 px frame and the taskbar / Alt+Tab the large frame, instead of one
    // 32 px icon that WinForms scales down. Every handle here is owned by this form.
    void LoadAppIcons()
    {
        iconForm=AppIcons.Load(SystemInformation.IconSize.Width);iconMark=AppIcons.Load(40);
        if(iconForm!=IntPtr.Zero){formIcon=Icon.FromHandle(iconForm);Icon=formIcon;}
        else{formIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);Icon=formIcon;}
        appIcon=iconMark!=IntPtr.Zero?Icon.FromHandle(iconMark):formIcon;
    }
    void ApplyWindowIcons()
    {
        int dpi=AppIcons.WindowDpi(Handle);
        IntPtr small=AppIcons.Load(AppIcons.Metric(AppIcons.SmallIconMetric,dpi)),big=AppIcons.Load(AppIcons.Metric(AppIcons.IconMetric,dpi));
        if(small==IntPtr.Zero||big==IntPtr.Zero){AppIcons.Destroy(ref small);AppIcons.Destroy(ref big);return;}
        IntPtr oldSmall=iconSmall,oldBig=iconBig;iconSmall=small;iconBig=big;
        AppIcons.SendIcon(Handle,AppIcons.IconSmall,iconSmall);AppIcons.SendIcon(Handle,AppIcons.IconBig,iconBig);
        AppIcons.Destroy(ref oldSmall);AppIcons.Destroy(ref oldBig);
    }
    protected override void CreateHandle(){base.CreateHandle();ApplyWindowIcons();}
    protected override void WndProc(ref Message m)
    {
        // WinForms re-sends Form.Icon (and a small copy scaled from it) on several property
        // changes; substitute the per-size frames so the title bar never falls back to it.
        if(m.Msg==AppIcons.SetIconMessage){IntPtr mine=m.WParam==(IntPtr)AppIcons.IconSmall?iconSmall:m.WParam==(IntPtr)AppIcons.IconBig?iconBig:IntPtr.Zero;if(mine!=IntPtr.Zero)m.LParam=mine;}
        base.WndProc(ref m);
        if(m.Msg==AppIcons.DpiChangedMessage)ApplyWindowIcons();
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if(disposing){if(formIcon!=null&&iconForm==IntPtr.Zero)formIcon.Dispose();formIcon=null;appIcon=null;}
        AppIcons.Destroy(ref iconSmall);AppIcons.Destroy(ref iconBig);AppIcons.Destroy(ref iconForm);AppIcons.Destroy(ref iconMark);
    }
    // Ctrl+= / Ctrl+- / Ctrl+0 zoom the embedded workbench. The WebView2 control forwards
    // accelerator keys here because the browser's own accelerator keys are disabled.
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        if(host!=null&&tabs.Selected==0&&wbHost.Visible&&HandleZoomKey(keyData))return true;
        return base.ProcessCmdKey(ref msg,keyData);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    bool HandleZoomKey(Keys keyData)
    {
        var mods=keyData&Keys.Modifiers;var key=keyData&Keys.KeyCode;
        if(mods!=Keys.Control&&mods!=(Keys.Control|Keys.Shift))return false;
        if(key==Keys.Oemplus||key==Keys.Add){host.ZoomIn();return true;}
        if(mods==Keys.Control&&(key==Keys.OemMinus||key==Keys.Subtract)){host.ZoomOut();return true;}
        if(mods==Keys.Control&&(key==Keys.D0||key==Keys.NumPad0)){host.ZoomReset();return true;}
        return false;
    }
    void NavigateWorkbench(){string target=dashboardUrl??"about:blank";if(webviewAt==target)return;webviewAt=target;if(host!=null)host.Navigate(target);}
    public void WaitWorkbench(int ms){var sw=Stopwatch.StartNew();while(sw.ElapsedMilliseconds<ms&&!navDone&&!webviewFailed){Application.DoEvents();Thread.Sleep(20);}}
    void CreateJob(){job=CreateJobObject(IntPtr.Zero,null);var x=new Extended();x.basic.flags=0x2000;int size=Marshal.SizeOf(x);IntPtr p=Marshal.AllocHGlobal(size);try{Marshal.StructureToPtr(x,p,false);if(job==IntPtr.Zero||!SetInformationJobObject(job,9,p,(uint)size))throw new Exception("无法托管子进程。");}finally{Marshal.FreeHGlobal(p);}}
    async Task StartRun()
    {
        if(preview)return;
        string tunnelCfg=tunnelInput.Text.Trim(),keyCfg=keyInput.Text.Trim();
        if(!Regex.IsMatch(tunnelCfg,"^tunnel_[a-zA-Z0-9]+$")||keyCfg.Length<10){ShowConfigError();tabs.Select(3);Log("请先填写有效的 Tunnel ID 和 API Key，已切换到“连接配置”页签。");(!Regex.IsMatch(tunnelCfg,"^tunnel_[a-zA-Z0-9]+$")?tunnelInput:keyInput).Box.Focus();return;}
        ClearConfigError();
        busy=true;save.Enabled=false;tunnelInput.ReadOnly=keyInput.ReadOnly=true;cancel=new CancellationTokenSource();var ct=cancel.Token;UpdateRunButton();
        try {
            Save();
            string exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tunnel-client.exe");if(!File.Exists(exe))throw new Exception("请将 tunnel-client.exe 放在本程序旁边。");
            session=Path.Combine(data,"sessions",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(session);string health=Path.Combine(session,"health.url");CreateJob();
            var pi=new ProcessStartInfo(exe,"run --control-plane.tunnel-id "+cfg.Tunnel+" --health.listen-addr 127.0.0.1:0 --health.url-file \""+health+"\" --log.format json --log.level info"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=session,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
            pi.EnvironmentVariables["CONTROL_PLANE_API_KEY"]=cfg.Key;pi.EnvironmentVariables["MCP_COMMAND"]="\""+Application.ExecutablePath.Replace('\\','/')+"\" --mcp";
            pi.EnvironmentVariables["WORKSPACE_TUNNEL_ID"]=cfg.Tunnel;pi.EnvironmentVariables["WORKSPACE_TUNNEL_HEALTH_FILE"]=health;
            pi.EnvironmentVariables["WORKSPACE_DESKTOP_VERSION"]=WorkspaceServer.Version;pi.EnvironmentVariables["WORKSPACE_TUNNEL_VERSION"]=WorkspaceDiagnostics.TunnelVersion(exe);
            pi.EnvironmentVariables["MCP_FORWARD_TRACE_CONTEXT"]="true";
            foreach(string name in new[]{"MCP_SERVER_URL","TUNNEL_CLIENT_CONFIG","TUNNEL_CLIENT_PROFILE","TUNNEL_CLIENT_PROFILE_FILE","CLOUDFLARED_MANAGED","CLOUDFLARED_TUNNEL_TOKEN"})pi.EnvironmentVariables.Remove(name);
            foldLine=null;client=new Process{StartInfo=pi};client.OutputDataReceived+=(s,e)=>{if(e.Data!=null)Log(e.Data);};client.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)Log(e.Data);};client.Start();if(!AssignProcessToJobObject(job,client.Handle)){client.Kill();throw new Exception("无法管理隧道进程。");}client.BeginOutputReadLine();client.BeginErrorReadLine();statusPill.Value=UiStatusPill.State.Connecting;Log("启动本地工作区工具和 OpenAI Tunnel。");
            for(int i=0;;i++){ct.ThrowIfCancellationRequested();if(client.HasExited)throw new Exception("Tunnel 启动失败。");if(File.Exists(health)){string url=File.ReadAllText(health).Trim();healthUrl=url;SetContext("允许目录：全部本地磁盘    ·    状态页："+url+"/ui");if(await Probe(url+"/readyz"))break;}if(i>90)throw new Exception("连接超时，请查看日志。");await Task.Delay(500,ct);}
            statusPill.Value=UiStatusPill.State.Live;Log("本地工具 "+WorkspaceServer.Version+" 就绪，共 "+WorkspaceServer.ToolCount+" 个工具。隧道就绪不代表 ChatGPT 已刷新工具；在网页版 设置 → 插件 → 本地工作区 → 信息 中刷新后，调用 get_workspace_status 核对版本。查看 initialize、tools/list 和工具调用日志确认实际连接。");
        }catch(OperationCanceledException){StopRun();}catch(Exception ex){Log(ex.Message);StopRun();}finally{busy=false;save.Enabled=client==null;tunnelInput.ReadOnly=keyInput.ReadOnly=client!=null;UpdateRunButton();SyncWorkbench();if(closing)Close();}
    }
    async Task<bool> Probe(string url){return await Task.Run(()=>{try{var req=(HttpWebRequest)WebRequest.Create(url);req.Timeout=1200;using(var r=(HttpWebResponse)req.GetResponse())return r.StatusCode==HttpStatusCode.OK;}catch{return false;}});}
    void StopRun(){healthUrl=null;dashboardUrl=null;webviewAt=null;NavigateWorkbench();statusPill.Value=UiStatusPill.State.Stopped;SetContext("允许目录：全部本地磁盘    ·    OpenAI Tunnel → 本机工具");if(job!=IntPtr.Zero){TerminateJobObject(job,0);CloseHandle(job);job=IntPtr.Zero;}if(client!=null){try{client.WaitForExit(3000);client.Dispose();}catch{}client=null;}if(session!=null){try{Directory.Delete(session,true);session=null;}catch(Exception ex){Log("临时目录清理失败："+ex.Message);}}SyncWorkbench();if(!busy){save.Enabled=true;tunnelInput.ReadOnly=keyInput.ReadOnly=false;UpdateRunButton();}}
    [STAThread]static void Main(string[] args)
    {
        AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling",false);AppContext.SetSwitch("Switch.System.IO.BlockLongPaths",false);
        WebviewLoader.Hook();
        if(args.Length>0&&args[0]=="--mcp"){try{WorkspaceServer.Run();}catch(Exception ex){Console.Error.WriteLine("[Workspace] runtime failed: "+ex.GetBaseException().Message);Environment.ExitCode=1;}return;}
        if(args.Length>1&&args[0]=="--preview"){Application.EnableVisualStyles();using(var f=new MainForm(true)){f.Show();string demo=Path.Combine(Path.GetTempPath(),"workspace-preview").Replace('\\','/');f.Log("预览模式：未启动隧道，未读取或修改现有配置。");f.Log("[Workspace] read_file | "+demo+"/README.md | OK · 200 行");f.Log("[Workspace] edit_file | "+demo+"/example.ts | OK · +3 / -1");f.FlushLogs();Application.DoEvents();
            if(args.Length>2){f.SetDashboardUrl(args[2]);f.WaitWorkbench(9000);}
            Application.DoEvents();using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,f.Width,f.Height));bmp.Save(args[1]);}
            if(args.Length>2){Application.Run(f);}else f.Close();}return;}
        bool created;using(var activate=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\LocalWorkspacePlugin.Activate"))using(var mutex=new Mutex(true,"Local\\LocalWorkspacePlugin",out created)){if(!created){activate.Set();return;}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try{var f=new MainForm();var activationTimer=new System.Windows.Forms.Timer{Interval=200};activationTimer.Tick+=(s,e)=>{if(activate.WaitOne(0)){f.Show();if(f.WindowState==FormWindowState.Minimized)f.WindowState=FormWindowState.Normal;f.Activate();}};activationTimer.Start();f.FormClosed+=(s,e)=>activationTimer.Dispose();if(args.Length>0&&args[0]=="--start")f.Shown+=async(s,e)=>await f.StartRun();if(args.Length>1&&args[0]=="--smoke"){f.Shown+=async(s,e)=>{await f.StartRun();f.Log("SMOKE running="+(f.client!=null));using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(0,0,f.Width,f.Height));bmp.Save(args[1]+".png");}f.StopRun();f.Log("SMOKE stopped="+(f.client==null)+" sessionClean="+(f.session==null));File.WriteAllText(args[1],f.logView.Text);f.Close();};}Application.Run(f);}catch(Exception ex){if(args.Length>1){File.WriteAllText(args[1]+".error",ex.ToString());Environment.ExitCode=1;}else MessageBox.Show(ex.Message,"本地工作区");}}
    }
}
// Pure, testable classification for the 原始日志 fold (tests/log-fold.test.cjs drives it via reflection).
static class TunnelLogFold
{
    // Exact fx event messages (go.uber.org/fx fxevent logger) observed in real tunnel-client 0.0.16
    // `--log.format json --log.level info` output, plus the fx success events of the same family.
    // Failure events (OnStart hook failed, start failed, invoke failed, ...) are deliberately absent.
    internal static readonly HashSet<string> Events=new HashSet<string>(StringComparer.Ordinal){
        "provided","replaced","decorated","supplied","invoking","run",
        "OnStart hook executing","OnStart hook executed","OnStop hook executing","OnStop hook executed",
        "initialized custom fxevent.Logger","started"};
    internal const int BurstSeconds=10;
    static readonly Regex Level=new Regex("\"level\"\\s*:\\s*\"([A-Za-z]+)\"",RegexOptions.CultureInvariant);
    static readonly Regex Msg=new Regex("\"msg\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",RegexOptions.CultureInvariant);
    internal static bool IsStartupNoise(string line)
    {
        if(line==null||!line.StartsWith("{",StringComparison.Ordinal)||line.IndexOf("[Workspace]",StringComparison.Ordinal)>=0)return false;
        var level=Level.Match(line);if(!level.Success||!string.Equals(level.Groups[1].Value,"info",StringComparison.OrdinalIgnoreCase))return false;
        var msg=Msg.Match(line);return msg.Success&&Events.Contains(msg.Groups[1].Value);
    }
    internal static bool SameBurst(DateTime last,DateTime now){double gap=(now-last).TotalSeconds;return gap>=0&&gap<=BurstSeconds;}
    internal static string Summary(int count){return "已折叠 "+count+" 条 Tunnel 启动内部日志（fx 依赖注入 INFO；WARN/ERROR 不折叠，完整日志见 Tunnel 状态页 /ui）";}
}
