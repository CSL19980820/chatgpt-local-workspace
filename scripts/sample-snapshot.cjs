// Representative snapshot for design review: it mirrors LiveSnapshot's shape so the
// dashboard can be opened with realistic payloads while no workspace instance runs.
// Never used by the shipped server; the real payload always comes from LocalWorkspace.
// Times are frozen when the preview server starts, exactly like a real server that
// reported them once: 开始 then stays put and a running command keeps counting up.
const minute=60000,origin=Date.now(),iso=offset=>new Date(origin-offset).toISOString();
const diffRow=(kind,oldLine,newLine,text)=>({kind,old_line:oldLine,new_line:newLine,text,truncated:false});
const diff=(rows,added,removed)=>({rows,added,removed,identical:false,coarse:false,truncated:false,omitted_rows:0});

function call(id,tool,target,started,elapsed,detail,extra){
  return Object.assign({thread_id:'thread-local-workspace',id,tool,target,status:'returned',started_at:iso(started),elapsed_ms:elapsed,error_code:null,preview:null,detail:Object.assign({tool,target},detail),session_id:detail.session_id||null},extra||{});
}
module.exports=function sampleSnapshot(){
  const activity=[
    call('c1','open_workspace','E:/workspace/local-workspace',46*minute,18,{kind:'info',session_id:null,summary:'工作区约定 · 2 份指导文件',info:[{label:'工作目录',value:'E:/workspace/local-workspace',mono:true},{label:'Git 根',value:'无',mono:false},{label:'约定文件',value:'E:/workspace/local-workspace/AGENTS.md\nC:/Users/dev/.codex/AGENTS.md',mono:true},{label:'可用 Shell',value:'git_bash、powershell、pwsh',mono:false},{label:'默认 Shell',value:'git_bash',mono:false}]}),
    call('c2','read_file','E:/workspace/local-workspace/src/WorkspaceDetail.cs',45*minute,2,{kind:'read',session_id:null,path:'E:/workspace/local-workspace/src/WorkspaceDetail.cs',name:'WorkspaceDetail.cs',start_line:96,returned_count:22,end_line:117,next_line:118,at_end:false,truncated:false,omitted_lines:0,content_bytes:812,file_bytes:26418,binary:false,summary:'WorkspaceDetail.cs · 第 96–117 行',content:[
      {n:96,text:'    static Dictionary<string,object> Split(string text,int limit)'},
      {n:97,text:'    {'},
      {n:98,text:'        bool truncated=text!=null&&text.Length>limit;'},
      {n:99,text:'        var shape=new Dictionary<string,object>{{"text",Clip(text??"",limit)},{"truncated",truncated}};'},
      {n:100,text:'        return shape;'},
      {n:101,text:'    }'},
      {n:102,text:'    static Dictionary<string,object> Edit(Dictionary<string,object> args,object inner,Budget budget)'},
      {n:103,text:'    {'},
      {n:104,text:'        string path=Path(inner);'},
      {n:105,text:'        var replace=new Dictionary<string,object>{'},
      {n:106,text:'            {"before",Split(Text(args,"old_text"),NoteChars)},{"after",Split(Text(args,"new_text"),NoteChars)},'},
      {n:107,text:'            {"before_chars",Text(args,"old_text").Length},{"after_chars",Text(args,"new_text").Length}};'},
      {n:108,text:'        var detail=new Dictionary<string,object>{'},
      {n:109,text:'            {"kind","write"},{"session_id",null},'},
      {n:110,text:'            {"files",new object[]{File(path,"edit",Field(inner,"diff"),budget,0)}},'},
      {n:111,text:'            {"count",1},{"replace",replace}};'},
      {n:112,text:'        string counts=Counts(Field(inner,"diff"));'},
      {n:113,text:'        detail["summary"]=Name(Display(path))+" · 精确替换"+(counts.Length>0?" · "+counts:"");'},
      {n:114,text:'        return detail;'},
      {n:115,text:'    }'},
      {n:116,text:''},
      {n:117,text:'    // read_file 的每一行都带行号，面板把它渲染成内容本身。'}],reads:[{path:'E:/workspace/local-workspace/src/WorkspaceDetail.cs',name:'WorkspaceDetail.cs',start_line:96,returned_count:22,end_line:117,empty:false,next_line:118}],notes:[]}),
    call('c3','search_text','E:/workspace/local-workspace',44*minute,11,{kind:'search',session_id:null,root:'E:/workspace/local-workspace',query:'command-output',pattern:'*',returned_count:4,omitted:0,complete:true,truncated:false,skipped_paths:0,next_offset:null,scanned_files:37,note:'',summary:'"command-output" · 4 处',matches:[{path:'E:/workspace/local-workspace/src/dashboard.template.html',name:'dashboard.template.html',line:212,column:31,text:'const body=h(\'pre\',{class:\'output\'+(output?\'\':\' empty-output\'),text:output||\'等待输出…\'});'},{path:'E:/workspace/local-workspace/src/dashboard.template.html',name:'dashboard.template.html',line:213,column:15,text:'const status=h(\'div\',{class:\'status-line\'},…);'},{path:'E:/workspace/local-workspace/src/dashboard.css',name:'dashboard.css',line:196,column:1,text:'.output{margin:0;padding:10px 12px;border-top:1px solid var(--border);}'},{path:'E:/workspace/local-workspace/tests/dashboard-ui.test.cjs',name:'dashboard-ui.test.cjs',line:44,column:9,text:'assert(text(h.get(\'detail-body\')).includes(\'退出码 0\'));'}]}),
    call('c4','list_directory','E:/workspace/local-workspace',42*minute,1,{kind:'list',session_id:null,path:'E:/workspace/local-workspace/src',total_entries:6,returned_count:6,omitted:0,next_offset:null,empty:false,summary:'src · 6 项',entries:[{name:'dashboard.css',path:'E:/workspace/local-workspace/src/dashboard.css',directory:false},{name:'dashboard.template.html',path:'E:/workspace/local-workspace/src/dashboard.template.html',directory:false},{name:'LocalDashboard.cs',path:'E:/workspace/local-workspace/src/LocalDashboard.cs',directory:false},{name:'WorkspaceActivity.cs',path:'E:/workspace/local-workspace/src/WorkspaceActivity.cs',directory:false},{name:'WorkspaceDetail.cs',path:'E:/workspace/local-workspace/src/WorkspaceDetail.cs',directory:false},{name:'workspace-card.html',path:'E:/workspace/local-workspace/src/workspace-card.html',directory:false}]}),
    call('c5','write_file','E:/workspace/local-workspace/src/dashboard.css',40*minute,4,{kind:'write',session_id:null,count:1,overwrite:false,label:'替换文件',summary:'dashboard.css · 替换文件 · +3 −2',files:[{path:'E:/workspace/local-workspace/src/dashboard.css',name:'dashboard.css',operation:'replace',size_bytes:19042,previous_path:null,diff:diff([diffRow('context',196,null,'.output{margin:0;padding:10px 12px;border-top:1px solid var(--border);}'),diffRow('remove',197,null,'.command-output{font:12px/1.75 Consolas,"Microsoft YaHei UI",monospace;}'),diffRow('add',null,197,'.output{font:12px/1.75 var(--font-mono);white-space:pre-wrap;}'),diffRow('add',null,198,'.status-line .live{color:var(--primary)}'),diffRow('context',199,null,'.match{display:grid;gap:3px;padding:8px 12px;}'),diffRow('remove',200,null,'.match-row{display:grid;grid-template-columns:minmax(0,1fr);}'),diffRow('add',null,200,'.match-place{display:flex;gap:6px;}')],3,2)}]}),
    call('c6','edit_file','E:/workspace/local-workspace/src/WorkspaceActivity.cs',34*minute,3,{kind:'write',session_id:null,count:1,summary:'WorkspaceActivity.cs · 精确替换 · +1 −1',files:[{path:'E:/workspace/local-workspace/src/WorkspaceActivity.cs',name:'WorkspaceActivity.cs',operation:'edit',size_bytes:0,previous_path:null,diff:diff([diffRow('remove',23,null,'preview=receipts?x.Preview:null,detail=x.Detail);'),diffRow('add',null,23,'preview=receipts?x.Preview:null,detail=x.Detail,session_id=Session(x.Detail));')],1,1)}],replace:{before:{text:'detail=x.Detail);',truncated:false},after:{text:'detail=x.Detail,session_id=Session(x.Detail));',truncated:false},before_chars:17,after_chars:45}}),
    call('c7','apply_patch','E:/workspace/local-workspace',26*minute,32,{kind:'write',session_id:null,count:2,added:9,removed:1,omitted_files:0,error:'',partial:false,rollback:'',root:'E:/workspace/local-workspace',summary:'补丁 · 2 个文件 · +9 −1',files:[{path:'E:/workspace/local-workspace/scripts/sample-snapshot.cjs',name:'sample-snapshot.cjs',operation:'add',size_bytes:0,previous_path:null,diff:diff([diffRow('add',null,1,'// Representative snapshot for design review.'),diffRow('add',null,2,'const minute=60000;'),diffRow('add',null,3,'module.exports=function sampleSnapshot(){'),diffRow('add',null,4,'  return {version:"2.2.1"};'),diffRow('add',null,5,'};')],5,0)},{path:'E:/workspace/local-workspace/scripts/dashboard-preview.cjs',name:'dashboard-preview.cjs',operation:'update',size_bytes:0,previous_path:null,diff:diff([diffRow('remove',12,null,'if(upstream.protocol!==\'http:\')throw Error(\'Expected a loopback dashboard URL\');'),diffRow('add',null,12,'const sample=process.argv.includes(\'--sample\');'),diffRow('add',null,13,'if(!sample&&upstream.protocol!==\'http:\')throw Error(\'Expected a loopback dashboard URL\');'),diffRow('context',14,null,'const policy="default-src \'none\'; script-src \'unsafe-inline\';";')],4,1)}]}),
    call('c8','exec_command','E:/workspace/local-workspace',22*minute,253,{kind:'command',session_id:'7f31c02a91b64a2f9a4d1fb2c0d1e5aa',command:'npm run build:ui',shell:'git_bash',shell_executable:'E:/Git/bin/bash.exe',cwd:'E:/workspace/local-workspace',output_tail:'\n> local-workspace-dashboard@0.0.0 build:ui\n> node scripts/build-dashboard.cjs\n\n≈ tailwindcss v4.3.3\n\nDone in 63ms\nDashboard built with local Tailwind CSS.\n',output_chars:168,truncated:false,running:false,exit_code:0,timed_out:false,stopped:false,elapsed_seconds:2.1,output_mode:'delta',input:null,sent_chars:0,summary:'npm run build:ui'}),
    call('c9','read_file','E:/workspace/local-workspace/tests/dashboard-ui.test.cjs',18*minute,1,{kind:'read',session_id:null,path:'E:/workspace/local-workspace/tests/dashboard-ui.test.cjs',name:'dashboard-ui.test.cjs',start_line:1,returned_count:8,end_line:8,next_line:9,at_end:false,truncated:false,omitted_lines:0,content_bytes:402,file_bytes:9807,binary:false,summary:'dashboard-ui.test.cjs · 第 1–8 行',content:[
      {n:1,text:'// The dashboard ships as one React bundle, so the honest test renders the built page'},
      {n:2,text:'// (src/dashboard.html) in a real browser and drives it: start time, running state, the'},
      {n:3,text:'// typed inspector for every call, the plan card and the filters.'},
      {n:4,text:'// Uses the installed Chromium-family browser; skips with a clear message when absent.'},
      {n:5,text:"const test = require('node:test');"},
      {n:6,text:"const assert = require('node:assert/strict');"},
      {n:7,text:"const fs = require('node:fs');"},
      {n:8,text:"const path = require('node:path');"}],reads:[{path:'E:/workspace/local-workspace/tests/dashboard-ui.test.cjs',name:'dashboard-ui.test.cjs',start_line:1,returned_count:8,end_line:8,empty:false,next_line:9}],notes:[]}),
    call('c10','exec_command','E:/workspace/local-workspace',12*minute,148,{kind:'command',session_id:'one',command:'node --test tests/dashboard-ui.test.cjs',shell:'git_bash',shell_executable:'E:/Git/bin/bash.exe',cwd:'E:/workspace/local-workspace',output_tail:'# tests 5\n# pass 4\n# fail 1\n\nnot ok 3 - a running command keeps streaming into the inspector\n',output_chars:96,truncated:false,running:false,exit_code:1,timed_out:false,stopped:false,elapsed_seconds:1.4,output_mode:'delta',input:null,sent_chars:0,summary:'node --test tests/dashboard-ui.test.cjs'},{}),
    call('c11','read_file','E:/workspace/local-workspace/tests/dashboard-ui.test.cjs',9*minute,0,{kind:'read',session_id:null,path:'E:/workspace/local-workspace/tests/missing-fixture.json',name:'missing-fixture.json',start_line:1,returned_count:0,end_line:1,next_line:null,at_end:false,truncated:false,omitted_lines:0,content_bytes:0,file_bytes:null,binary:false,summary:'未能找到文件。',content:[],reads:[{path:'E:/workspace/local-workspace/tests/missing-fixture.json',name:'missing-fixture.json',start_line:1,returned_count:0,end_line:1,empty:true,next_line:null}],notes:[],is_error:true,error:'未能找到文件：E:/workspace/local-workspace/tests/missing-fixture.json'},{}),
    // A binary read is recognized as such and shows no content at all.
    call('c17','read_file','E:/workspace/local-workspace/assets/local-workspace.ico',7*minute,1,{kind:'read',session_id:null,path:'E:/workspace/local-workspace/assets/local-workspace.ico',name:'local-workspace.ico',start_line:1,returned_count:2,end_line:2,next_line:null,at_end:true,truncated:false,omitted_lines:0,content_bytes:0,file_bytes:24322,binary:true,summary:'local-workspace.ico · 不是文本',content:[],reads:[{path:'E:/workspace/local-workspace/assets/local-workspace.ico',name:'local-workspace.ico',start_line:1,returned_count:2,end_line:2,empty:false,next_line:null}],notes:['这个文件不是文本文件，没有在面板里展开内容。']},{}),
    call('c12','git_diff','E:/workspace/local-workspace',6*minute,44,{kind:'text',session_id:null,text_label:'Git 差异',path:'E:/workspace/local-workspace',body:'diff --git a/src/dashboard.css b/src/dashboard.css\nindex 3f9c1ab..b7d2e14 100644\n--- a/src/dashboard.css\n+++ b/src/dashboard.css\n@@ -193,6 +193,8 @@\n-.command-output{font:12px/1.75 Consolas,monospace}\n+.output{font:12px/1.75 var(--font-mono)}\n+.status-line .live{color:var(--primary)}\n',exit_code:0,truncated:false,timed_out:false,added:2,removed:1,summary:'Git 差异 · +2 −1'}),
    // A rejected write shows the path only: no operation, size or diff may be claimed.
    call('c16','write_file','E:/workspace/local-workspace/src/dashboard.css',8*minute,1,{kind:'write',session_id:null,count:1,overwrite:false,label:'替换文件',summary:'文件已经存在，需要 overwrite=true 才能覆盖。',files:[{path:'E:/workspace/local-workspace/src/dashboard.css',name:'dashboard.css',operation:null,size_bytes:0,previous_path:null,diff:null}],applied:false,is_error:true,error:'文件“E:/workspace/local-workspace/src/dashboard.css”已经存在，需要 overwrite=true 才能覆盖。'}),
    call('c13','list_directory','E:/workspace/local-workspace/dist-next/LocalWorkspace.exe',4*minute,1,{kind:'info',session_id:null,summary:'LocalWorkspace.exe',info:[{label:'路径',value:'E:/workspace/local-workspace/dist-next/LocalWorkspace.exe',mono:true},{label:'类型',value:'文件',mono:false},{label:'大小',value:'292.5 KB',mono:false},{label:'修改时间',value:'2026-09-16T09:49:26Z',mono:false}]}),
    call('c14','update_plan','E:/workspace/local-workspace',3*minute,2,{kind:'plan',session_id:null,done:1,total:5,explanation:'先把仪表盘的数据通道接通，再调整展示。',path:'E:/workspace/local-workspace',updated_at:iso(3*minute),summary:'执行计划 · 1/5',steps:[{step:'核对线上与工作区基线、功能依赖和数据边界，确定清理及智能体改造清单',status:'completed'},{step:'把每次调用的类型化详情送进一小时的时间线',status:'in_progress'},{step:'按调用类型重做右侧检查器，替换常驻终端',status:'pending'},{step:'把执行计划移到时间线上方并重做进度展示',status:'pending'},{step:'补齐界面测试并在真实浏览器里复核',status:'pending'}]}),
    call('c15','exec_command','E:/workspace/local-workspace',0.4*minute,26400,{kind:'command',session_id:'two',command:'node scripts/dashboard-preview.cjs --sample',shell:'git_bash',shell_executable:'E:/Git/bin/bash.exe',cwd:'E:/workspace/local-workspace',output_tail:'Dashboard preview: http://127.0.0.1:43117\nwaiting for the first request…\n',output_chars:74,truncated:false,running:true,exit_code:null,timed_out:false,stopped:false,elapsed_seconds:26.4,output_mode:'delta',input:null,sent_chars:0,summary:'node scripts/dashboard-preview.cjs --sample'},{trace:'00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01'})
  ];
  activity.push(call('c18','read_image','E:/workspace/local-workspace/docs/images/dashboard-patch-review.png',1.5*minute,3,{kind:'image',name:'dashboard-patch-review.png',path:'E:/workspace/local-workspace/docs/images/dashboard-patch-review.png',mime_type:'image/png',size_bytes:125000,preview_url:'/sample-image',summary:'dashboard-patch-review.png'}));
  activity.push(call('c19','get_workspace_status','',1.8*minute,5,{kind:'workspace',summary:'工作区状态 · v2.4.0',info:[{label:'版本',value:'2.4.0'},{label:'实例',value:'sample-instance',mono:true},{label:'程序',value:'E:/workspace/local-workspace/dist/LocalWorkspace.exe',mono:true},{label:'工具数',value:'23'},{label:'运行中命令',value:'1'},{label:'命令会话',value:'2 个'},{label:'面板地址',value:'http://127.0.0.1:43117/',mono:true},{label:'默认 Shell',value:'git_bash'},{label:'协议版本',value:'2025-06-18 (legacy initialize)\n2026-07-28 (modern stateless)'},{label:'可见范围',value:'当前 MCP 进程；其他连接、过去进程和模型思考不可见。'}],workspaces:[{title:'本地工作区面板改版',path:'E:/workspace/local-workspace'}],tools:['register_conversation','open_workspace','update_plan','check_task_completion','apply_patch','workspace_history','restore_change','get_workspace_status','read_command','write_stdin','git_status','git_diff','import_file','read_image','create_directory','list_directory','read_file','write_file','edit_file','exec_command','show_changes','search_files','search_text']}));
  return finalize({
    version:'2.4.0',
    instance_id:'sample',
    dashboard_url:'http://127.0.0.1:0/',
    conversations:[{thread_id:'thread-local-workspace',title:'本地工作区面板改版',path:'E:/workspace/local-workspace',chat_id:'',chat_url:null,created_at:iso(50*minute)}],
    thread_id:'',
    path:'',
    checked_at:new Date().toISOString(),
    default_shell:'git_bash',
    activity,
    plans:[{thread_id:'thread-local-workspace',path:'E:/workspace/local-workspace',explanation:'先把仪表盘的数据通道接通，再调整展示。',updated_at:iso(3*minute),plan:[{step:'核对线上与工作区基线、功能依赖和数据边界，确定清理及智能体改造清单',status:'completed'},{step:'把每次调用的类型化详情送进一小时的时间线',status:'in_progress'},{step:'按调用类型重做右侧检查器，替换常驻终端',status:'pending'},{step:'把执行计划移到时间线上方并重做进度展示',status:'pending'},{step:'补齐界面测试并在真实浏览器里复核',status:'pending'}]}],
    commands:[
      {thread_id:'thread-local-workspace',started_at:iso(0.4*minute),session_id:'two',command:'node scripts/dashboard-preview.cjs --sample',cwd:'E:/workspace/local-workspace',shell:'git_bash',running:true,exit_code:null,elapsed_seconds:26.4,output:'Dashboard preview: http://127.0.0.1:43117\nwaiting for the first request…\n',truncated:false,timed_out:false,stopped:false},
      {thread_id:'thread-local-workspace',started_at:iso(22*minute),session_id:'7f31c02a91b64a2f9a4d1fb2c0d1e5aa',command:'npm run build:ui',cwd:'E:/workspace/local-workspace',shell:'git_bash',running:false,exit_code:0,elapsed_seconds:2.1,output:'≈ tailwindcss v4.3.3\n\nDone in 63ms\nDashboard built with local Tailwind CSS.\n',truncated:false,timed_out:false,stopped:false},
      {thread_id:'thread-local-workspace',started_at:iso(12*minute),session_id:'one',command:'node --test tests/dashboard-ui.test.cjs',cwd:'E:/workspace/local-workspace',shell:'git_bash',running:false,exit_code:1,elapsed_seconds:1.4,output:'# tests 5\n# pass 4\n# fail 1\n',truncated:false,timed_out:false,stopped:false}
    ],
    omitted_commands:0,
    queued_calls:1,
    ui:{resource_reads:3,last_resource_read_at:iso(1*minute),viewers:[],note:'示例数据'}
  });
};
// ---- 2.4.0 contract fields ----------------------------------------------------------
// The server adds these fields (see the backend API contract): per-file and per-call
// added/removed for writes, turn_id/turn_source/trace_id on every call, and stats on
// every conversation. finalize() derives them the same way so the UI can rely on them.
const WRITE_TOOLS=['write_file','edit_file','apply_patch','restore_change'];
const TRACE=/^[0-9a-f]{2}-([0-9a-f]{32})-[0-9a-f]{16}-[0-9a-f]{2}$/;
function enrichDetail(detail){
  if(!detail||detail.kind!=='write'||!Array.isArray(detail.files))return detail;
  let added=0,removed=0;
  for(const file of detail.files){
    if(typeof file.added!=='number')file.added=file.diff?file.diff.added||0:0;
    if(typeof file.removed!=='number')file.removed=file.diff?file.diff.removed||0:0;
    added+=file.added;removed+=file.removed;
  }
  // Totals also cover files omitted from the bounded files[] list, so keep explicit values.
  if(typeof detail.added!=='number')detail.added=added;
  if(typeof detail.removed!=='number')detail.removed=removed;
  return detail;
}
// Only writes that reached the disk count: no previews, no rejected writes; a partially
// failed patch counts the files it really changed; restore_change only when applied.
function countsAsWrite(row){
  const d=row.detail||{};
  if(!WRITE_TOOLS.includes(row.tool)||d.is_preview||d.applied===false)return false;
  if(row.tool==='restore_change'&&d.applied!==true)return false;
  return !d.is_error||d.partial===true;
}
function endOf(row){return new Date(new Date(row.started_at).getTime()+(row.status==='running'?0:row.elapsed_ms||0)).toISOString();}
function statsFor(snapshot,thread){
  const rows=snapshot.activity.filter(row=>row.thread_id===thread);
  const stats={calls:rows.length,running:0,failed:0,added:0,removed:0,last_at:null};
  for(const row of rows){
    if(row.status==='running')stats.running++;
    if(row.status==='failed'||(row.detail&&row.detail.is_error))stats.failed++;
    if(countsAsWrite(row)){stats.added+=row.detail.added||0;stats.removed+=row.detail.removed||0;}
    const end=endOf(row);if(!stats.last_at||end>stats.last_at)stats.last_at=end;
  }
  // A returned exec_command whose process still runs is in progress as well.
  stats.running+=(snapshot.commands||[]).filter(c=>c.thread_id===thread&&c.running).length;
  return stats;
}
function finalize(snapshot){
  for(const row of snapshot.activity){
    enrichDetail(row.detail);
    if(row.turn_id===undefined)row.turn_id=null;
    if(row.turn_source===undefined)row.turn_source=row.turn_id?'codex':null;
    const match=TRACE.exec(row.trace||'');
    if(row.trace_id===undefined)row.trace_id=match?match[1]:null;
  }
  for(const conversation of snapshot.conversations){
    if(!conversation.association)conversation.association='host_session';
    if(!conversation.source)conversation.source=conversation.association==='manual'?'manual':/codex/i.test(conversation.thread_id+conversation.title)?'codex':'chatgpt';
    conversation.stats=statsFor(snapshot,conversation.thread_id);
  }
  return snapshot;
}

// ---- Rich scenario: node scripts/dashboard-preview.cjs --sample=rich -----------------
// Four conversations (Codex with turn IDs, ChatGPT without, a manual registration and one
// with only a running command), an 8-file patch with 2 omitted files, a ~60 KB command
// output, failures (non-zero exit, rejected write, partial patch) and a timed-out command.
function richSnapshot(){
  const root='E:/workspace/local-workspace',P=name=>root+'/'+name;
  const turnA='0199c3a4-7f1e-7a2b-9a51-3c0d5e6f7a8b',turnB='0199c3b9-02aa-7c4d-8e11-5f6a7b8c9d0e';
  const row=(thread,id,tool,target,started,elapsed,detail,extra)=>Object.assign(call(id,tool,target,started,elapsed,detail,extra),{thread_id:thread});
  const file=(path,operation,added,removed,rows,previous)=>({path:P(path),name:path.split('/').pop(),operation,size_bytes:0,previous_path:previous?P(previous):null,added,removed,diff:diff(rows||[],added,removed)});
  const longLines=[];for(let i=1;longLines.join('\n').length<61440;i++)longLines.push('[build '+String(i).padStart(4,'0')+'] compiling module '+(i%37)+' of 37 · cache '+(i%3?'hit':'miss')+' · '+(i*13%997)+' ms');
  const longOutput=longLines.join('\n')+'\nBuild finished with 0 errors.\n';
  const patchFiles=[
    file('src/WorkspaceServer.cs','update',18,6,[diffRow('remove',190,null,'    static object ReadPage(string id,int offset,int length)'),diffRow('add',null,190,'    static object Snapshot(string id,int wait,bool stop,bool consume=true,bool full=false,int? offset=null,int length=8000)')]),
    file('src/WorkspaceDetail.cs','update',9,3,[diffRow('add',null,116,'            added+=Number(diff,"added");removed+=Number(diff,"removed");')]),
    file('src/WorkspaceContracts.cs','update',2,1,[diffRow('add',null,36,'p["output_chars"]=integer;p["logs_cleared"]=boolean;')]),
    file('tests/paging.test.cjs','add',24,0,[diffRow('add',null,1,"const test=require('node:test');")]),
    file('docs/PAGING.md','move',3,1,[diffRow('add',null,1,'# 命令输出分页')],'docs/OUTPUT.md'),
    file('scripts/old-pager.cjs','delete',0,14,[diffRow('remove',1,null,'// superseded by read_command offset/length')])
  ];
  const activity=[
    // Codex conversation, turn A: three calls within a few seconds share one turn_id.
    row('thread-codex-paging','r1','open_workspace',root,40*minute,15,{kind:'info',session_id:null,summary:'工作区约定 · 1 份指导文件',info:[{label:'工作目录',value:root,mono:true},{label:'默认 Shell',value:'git_bash'}]},{turn_id:turnA}),
    row('thread-codex-paging','r2','apply_patch',root,39.8*minute,48,{kind:'write',session_id:null,count:8,omitted_files:2,added:71,removed:29,error:'',partial:false,rollback:'',root,summary:'补丁 · 8 个文件 · +71 −29',files:patchFiles},{turn_id:turnA}),
    row('thread-codex-paging','r3','exec_command',root,39.6*minute,5200,{kind:'command',session_id:'c0de0001',command:'node --test tests/paging.test.cjs',shell:'git_bash',shell_executable:'E:/Git/bin/bash.exe',cwd:root,output_tail:'# tests 6\n# pass 5\n# fail 1\nnot ok 4 - negative offset reads the tail\n',output_chars:58,truncated:false,running:false,exit_code:1,timed_out:false,stopped:false,elapsed_seconds:5.2,output_mode:'delta',input:null,sent_chars:0,summary:'node --test tests/paging.test.cjs'},{turn_id:turnA,status:'failed',error_code:'exit 1'}),
    // Codex conversation, turn B: an edit, a partial patch failure and a timed-out command.
    row('thread-codex-paging','r4','edit_file',P('src/WorkspaceServer.cs'),20*minute,4,{kind:'write',session_id:null,count:1,summary:'WorkspaceServer.cs · 精确替换 · +1 −1',files:[file('src/WorkspaceServer.cs','edit',1,1,[diffRow('remove',74,null,'requested=offset.Value;'),diffRow('add',null,74,'requested=offset.Value<0?Math.Max(0,end+(long)offset.Value):offset.Value;')])],replace:{before:{text:'requested=offset.Value;',truncated:false},after:{text:'requested=offset.Value<0?Math.Max(0,end+(long)offset.Value):offset.Value;',truncated:false},before_chars:23,after_chars:71},change_id:'4f2a9c1e7b3d4e5f8a6b0c2d1e9f7a35',before_sha256:'66e1b8242346d0393a46f1ce69cc3df624da6a8d81e0c759b6791f7fe33bf1db',after_sha256:'12a956448991110a07da73566baaace499e56e89903d4aad20478ddb564f47f4'},{turn_id:turnB}),
    row('thread-codex-paging','r5','apply_patch',root,19.9*minute,30,{kind:'write',session_id:null,count:1,omitted_files:0,error:'第 2 个文件写入失败：拒绝访问。已写入的文件保留，请检查后再继续。',partial:true,rollback:'not_attempted',root,summary:'第 2 个文件写入失败：拒绝访问。已写入的文件保留，请检查后再继续。',files:[file('src/WorkspaceTasks.cs','update',4,2,[diffRow('add',null,67,'        bool recoveryValid=(p.ResolvedIssue==issueId||SameFailure(...));')])],is_error:true},{turn_id:turnB,status:'failed',error_code:'PATCH_WRITE_FAILED'}),
    row('thread-codex-paging','r6','exec_command',root,19.5*minute,300000,{kind:'command',session_id:'c0de0002',command:'npm run test:slow',shell:'git_bash',shell_executable:'E:/Git/bin/bash.exe',cwd:root,output_tail:'waiting for fixture server…\n[timeout] command exceeded 300 s and its process tree was stopped\n',output_chars:96,truncated:false,running:false,exit_code:1,timed_out:true,stopped:false,elapsed_seconds:300,output_mode:'delta',input:null,sent_chars:0,summary:'npm run test:slow'},{turn_id:turnB,status:'failed',error_code:'TIMEOUT'}),
    // ChatGPT conversation: no turn_id; calls spaced so a ~30 s gap heuristic splits them in two groups.
    row('thread-chatgpt-release','r7','get_workspace_status','',33*minute,6,{kind:'workspace',summary:'工作区状态 · v2.4.0',info:[{label:'版本',value:'2.4.0'},{label:'工具数',value:'23'}]},{trace:'00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01'}),
    row('thread-chatgpt-release','r8','write_file',P('README.md'),32.8*minute,2,{kind:'write',session_id:null,count:1,overwrite:false,label:'替换文件',summary:'文件已经存在，需要 overwrite=true 才能覆盖。',files:[{path:P('README.md'),name:'README.md',operation:null,size_bytes:0,previous_path:null,added:0,removed:0,diff:null}],applied:false,is_error:true,error:'文件“'+P('README.md')+'”已经存在，需要 overwrite=true 才能覆盖。'},{status:'failed',error_code:'EXISTS',trace:'00-0af7651916cd43dd8448eb211c80319c-c1d2e3f4a5b6c7d8-01'}),
    row('thread-chatgpt-release','r9','write_file',P('docs/RELEASE-2.4.0.md'),32.6*minute,5,{kind:'write',session_id:null,count:1,overwrite:true,label:'替换文件',summary:'RELEASE-2.4.0.md · 替换文件 · +12 −4',files:[file('docs/RELEASE-2.4.0.md','replace',12,4,[diffRow('add',null,3,'2026-10-08。公开发行包含本地 2.3.1、2.3.2 的修复。')])]}),
    row('thread-chatgpt-release','r10','exec_command',root,14*minute,48000,{kind:'command',session_id:'c0de0003',command:'pwsh -File build.ps1',shell:'powershell',shell_executable:'C:/Program Files/PowerShell/7/pwsh.exe',cwd:root,output_tail:longOutput.slice(-4000),output_chars:longOutput.length,truncated:true,running:false,exit_code:0,timed_out:false,stopped:false,elapsed_seconds:48,output_mode:'delta',input:null,sent_chars:0,summary:'pwsh -File build.ps1'}),
    row('thread-chatgpt-release','r11','search_text',root,13.7*minute,9,{kind:'search',session_id:null,root,query:'2.3.2',pattern:'*.md',returned_count:2,omitted:0,complete:true,truncated:false,skipped_paths:0,next_offset:null,scanned_files:14,note:'',summary:'"2.3.2" · 2 处',matches:[{path:P('README.md'),name:'README.md',line:44,column:4,text:'## 2.3.2：一并清理旧对话和执行计划'},{path:P('UPGRADE-NOTES.md'),name:'UPGRADE-NOTES.md',line:14,column:4,text:'## 2.3.2 · 2026-10-08'}]}),
    // Manually registered conversation (no host session metadata).
    row('thread-manual-tunnel','r12','list_directory',P('dist'),9*minute,2,{kind:'list',session_id:null,path:P('dist'),total_entries:3,returned_count:3,omitted:0,next_offset:null,empty:false,summary:'dist · 3 项',entries:[{name:'LocalWorkspace.exe',path:P('dist/LocalWorkspace.exe'),directory:false},{name:'tunnel-client.exe',path:P('dist/tunnel-client.exe'),directory:false},{name:'dashboard.html',path:P('dist/dashboard.html'),directory:false}]}),
    row('thread-manual-tunnel','r13','read_command',root,8.5*minute,1,{kind:'command',session_id:'c0de0003',command:'pwsh -File build.ps1',shell:'powershell',shell_executable:'C:/Program Files/PowerShell/7/pwsh.exe',cwd:root,output_tail:longOutput.slice(0,4000),output_chars:longOutput.length,truncated:true,running:false,exit_code:0,timed_out:false,stopped:false,elapsed_seconds:48,output_mode:'page',input:null,sent_chars:0,summary:'会话 c0de0003 · 读取输出'}),
    // Conversation with nothing but a still-running command.
    row('thread-running-build','r14','exec_command',root,1.2*minute,1000,{kind:'command',session_id:'c0de0004',command:'npm run package',shell:'git_bash',shell_executable:'E:/Git/bin/bash.exe',cwd:root,output_tail:'packaging dist-next → work/release …\n',output_chars:38,truncated:false,running:true,exit_code:null,timed_out:false,stopped:false,elapsed_seconds:72,output_mode:'delta',input:null,sent_chars:0,summary:'npm run package'})
  ];
  const command=(thread,session,cmd,shell,started,running,exit,elapsed,output,extra)=>Object.assign({thread_id:thread,started_at:iso(started),session_id:session,command:cmd,cwd:root,shell,running,exit_code:exit,elapsed_seconds:elapsed,output,truncated:false,timed_out:false,stopped:false},extra||{});
  return finalize({
    version:'2.4.0',
    instance_id:'sample-rich',
    dashboard_url:'http://127.0.0.1:0/',
    conversations:[
      {thread_id:'thread-codex-paging',title:'命令输出分页（Codex）',path:root,chat_id:'',chat_url:null,association:'host_session',created_at:iso(41*minute)},
      {thread_id:'thread-chatgpt-release',title:'整理 2.4.0 发行说明',path:root,chat_id:'6f0c2d1e-8a7b-4c3d-9e2f-1a0b9c8d7e6f',chat_url:'https://chatgpt.com/c/6f0c2d1e-8a7b-4c3d-9e2f-1a0b9c8d7e6f',association:'host_session',created_at:iso(34*minute)},
      {thread_id:'thread-manual-tunnel',title:'手动登记：排查 Tunnel 连接',path:root+'/dist',chat_id:'',chat_url:null,association:'manual',created_at:iso(10*minute)},
      {thread_id:'thread-running-build',title:'打包发行版',path:root,chat_id:'',chat_url:null,association:'host_session',created_at:iso(1.5*minute)}
    ],
    thread_id:'',
    path:'',
    checked_at:new Date().toISOString(),
    default_shell:'git_bash',
    activity,
    plans:[{thread_id:'thread-codex-paging',path:root,explanation:'先统一分页，再补测试和文档。',updated_at:iso(19*minute),task_state:'active',reason:'',next_action:'',recovery_note:'',plan:[{step:'合并 read_command 分页到 Snapshot',status:'completed',evidence:'r2 补丁已写入'},{step:'修复负偏移读取尾部',status:'in_progress'},{step:'补齐分页测试并全部通过',status:'pending'},{step:'更新 TOOLS.md',status:'pending'}],
      // Same shape as WorkspaceTasks.Assess: the r6 timeout is the open issue, so the task needs attention.
      task:{path:root,state:'needs_attention',can_finish:false,unfinished_steps:['修复负偏移读取尾部','补齐分页测试并全部通过','更新 TOOLS.md'],missing_evidence:[],running:false,reason:'',next_action:'检查最近失败，修复或记录具体阻塞，再更新计划。',last_activity_at:iso(14.5*minute),last_issue:'COMMAND_TIMEOUT',last_issue_id:'command:c0de0002',recovery_note:'',recovery_verified:false,last_issue_at:iso(14.5*minute),resume_prompt:'继续完成 '+root+' 的原任务。先调用 open_workspace 和 check_task_completion，核对当前文件与运行中的命令，避免重复执行。尚未完成：修复负偏移读取尾部；补齐分页测试并全部通过；更新 TOOLS.md。下一步：检查最近失败，修复或记录具体阻塞，再更新计划。仅在全部验收完成，或明确记录必要阻塞及下一步后结束回复。 已记录原因：无；无调用不代表模型已停止。 此提示不新增部署、删除或对外操作授权。',evidence_scope:'依据模型登记的步骤证据与本进程执行状态；不是独立验收，也不能强制宿主续跑。'}}],
    commands:[
      command('thread-running-build','c0de0004','npm run package','git_bash',1.2*minute,true,null,72,'packaging dist-next → work/release …\n'),
      command('thread-chatgpt-release','c0de0003','pwsh -File build.ps1','powershell',14*minute,false,0,48,longOutput.slice(-8000),{truncated:true}),
      command('thread-codex-paging','c0de0002','npm run test:slow','git_bash',19.5*minute,false,1,300,'waiting for fixture server…\n[timeout] command exceeded 300 s and its process tree was stopped\n',{timed_out:true}),
      command('thread-codex-paging','c0de0001','node --test tests/paging.test.cjs','git_bash',39.6*minute,false,1,5.2,'# tests 6\n# pass 5\n# fail 1\nnot ok 4 - negative offset reads the tail\n')
    ],
    omitted_commands:0,
    queued_calls:0,
    ui:{resource_reads:0,viewers:[],note:'示例数据（rich）'}
  });
}

// ---- /api/diagnostics sample (same shape as WorkspaceDiagnostics.Read) ----------------
function diagnosticsSample(){
  const component=(id,label,running,installed,detail)=>{
    const restart=!!(running&&installed&&running!==installed);
    return {id,label,running,installed,restart_required:restart,status:restart?'restart_required':!installed?'missing':!running?'unknown_running':'ok',detail};
  };
  return {
    version:'2.4.0',
    versions:[
      component('mcp','本地 MCP','2.4.0','2.4.0','运行版本来自当前进程，文件版本来自当前磁盘程序。'),
      component('desktop','桌面程序','2.4.0','2.4.1','桌面启动时记录；独立 MCP 进程不关联桌面。'),
      component('tunnel','Tunnel Client','0.0.16','0.0.16','运行版本在启动时记录；文件已更新时需要下次启动生效。'),
      component('webview2_sdk','WebView2 SDK','1.0.4258.31','1.0.4258.31','随本地程序嵌入；加载器按 SDK 版本隔离缓存。'),
      component('webview2_runtime','WebView2 Runtime',null,'147.0.3912.86','Windows 当前可用的 Evergreen 运行时；实际嵌入视图在桌面启动后加载。')
    ],
    checked_at:new Date().toISOString(),
    checks:[
      {label:'配置自检',status:'pass',detail:'由官方 Tunnel Client 检查；不代表宿主已成功调用工具。'},
      {label:'Tunnel ID',status:'pass',detail:'配置检查通过。'},
      {label:'运行密钥',status:'pass',detail:'配置检查通过。'},
      {label:'隧道存活',status:'pass',detail:'检查官方 /healthz。'},
      {label:'隧道就绪',status:'fail',detail:'检查官方 /readyz；就绪不等于工具调用成功。'},
      {label:'MCP 握手',status:'pass',detail:iso(30*minute)},
      {label:'工具发现',status:'pass',detail:'23 个工具 · '+iso(30*minute)},
      {label:'实际工具调用',status:'pass',detail:'最近成功：'+iso(1*minute)},
      {label:'自动对话归属',status:'pending',detail:'尚未收到宿主会话标识（ChatGPT openai/session 或 Codex threadId）；继续支持手动登记。'}
    ],
    last_tool:'exec_command',
    last_failure:iso(19*minute),
    scope:'仅当前进程实际观察到的请求；本地测试请求也会计入，不代表所有 ChatGPT 能力均已验收。（示例数据）'
  };
}
module.exports.classic=module.exports;
module.exports.rich=richSnapshot;
module.exports.diagnostics=diagnosticsSample;
module.exports.finalize=finalize;
