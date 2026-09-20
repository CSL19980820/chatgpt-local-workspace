const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs'), os = require('node:os'), path = require('node:path');
const { spawn } = require('node:child_process');
const { createHash } = require('node:crypto');
const { launchDashboardBrowser } = require('../scripts/browser-launch.cjs');

test('2.1 host sessions, attachment contract and actual diagnostic dashboard', { timeout: 150000 }, async t => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'workspace-21-'));
  const child = spawn(process.env.WORKSPACE_TEST_EXE || path.join(__dirname, '../dist-next/LocalWorkspace.exe'), ['--mcp'], { windowsHide: true, stdio: ['pipe','pipe','pipe'] });
  const exited = new Promise(resolve => child.once('exit', resolve));
  let seq=0, buffer='', logs='', base='', browser;
  const pending=new Map();
  child.stderr.on('data', d => { logs+=d; const m=logs.match(/\[Dashboard\] (http:\/\/127\.0\.0\.1:\d+\/)/); if(m)base=m[1]; });
  child.stdout.on('data', d => { buffer+=d; let i; while((i=buffer.indexOf('\n'))>=0){const r=JSON.parse(buffer.slice(0,i));buffer=buffer.slice(i+1);const p=pending.get(r.id);if(p){clearTimeout(p.timer);pending.delete(r.id);p.resolve(r);}} });
  const rpc=(method,params={})=>new Promise((resolve,reject)=>{const id=++seq;pending.set(id,{resolve,timer:setTimeout(()=>reject(Error('MCP timeout: '+method)),60000)});child.stdin.write(JSON.stringify({jsonrpc:'2.0',id,method,params})+'\n');});
  const validators=new Map(),ajv=new(require('ajv'))({strict:false});
  const call=async(name,args={},meta={})=>{const r=await rpc('tools/call',{name,arguments:args,_meta:meta});assert(!r.error,JSON.stringify(r));const v=validators.get(name);if(v)assert(v(r.result.structuredContent),name+': '+JSON.stringify(v.errors));return r.result;};
  const A={'openai/session':'session-secret-A','openai/subject':'subject-secret','openai/organization':'org-secret'},B={...A,'openai/session':'session-secret-B'};
  const modern=extra=>({'io.modelcontextprotocol/protocolVersion':'2026-07-28','io.modelcontextprotocol/clientCapabilities':{elicitation:{},extensions:{'io.modelcontextprotocol/tasks':{}}},...extra});
  try {
    await rpc('initialize');
    const before=await(await fetch(new URL('/api/diagnostics',base))).json();
    assert.equal(before.checks.find(c=>c.label==='实际工具调用').status,'pending');
    const tools=(await rpc('tools/list')).result.tools;
    for(const tool of tools)validators.set(tool.name,ajv.compile(tool.outputSchema));
    const descriptor=tools.find(tool=>tool.name==='import_file');
    assert.deepEqual(descriptor._meta['openai/fileParams'],['file']);
    assert.deepEqual(Object.keys(descriptor.inputSchema.properties.file.properties).sort(),['download_url','file_id','file_name','mime_type']);
    assert.deepEqual(descriptor.inputSchema.properties.file.required,['download_url','file_id']);
    assert.equal(descriptor.annotations.openWorldHint,true);assert.equal(descriptor.annotations.destructiveHint,false);
    const first=await call('open_workspace',{path:root},A),id=first.structuredContent.thread_id;
    assert.match(id,/^thread-/);
    assert.equal((await call('get_workspace_status',{},A)).structuredContent.thread_id,id);
    const other=(await call('open_workspace',{path:root},B)).structuredContent.thread_id;
    assert.notEqual(id,other);
    for(const meta of [{...A,'openai/subject':'different'},{...A,'openai/organization':'different'}])assert.notEqual((await call('get_workspace_status',{},meta)).structuredContent.thread_id,id);
    assert.equal((await call('get_workspace_status')).structuredContent.thread_id,'unassigned');
    const bad=await rpc('tools/call',{name:'get_workspace_status',arguments:{thread_id:id},_meta:B});
    assert(bad.error || bad.result.isError,'explicit ID must not rebind another host session');
    const named=await call('register_conversation',{path:root,title:'2.1 验收对话'},A);assert.equal(named.structuredContent.result.thread_id,id);
    const nested=path.join(root,'nested');fs.mkdirSync(nested);await call('open_workspace',{path:nested},A);
    const status=(await call('get_workspace_status',{},A)).structuredContent.result;
    assert.equal(status.conversations.find(c=>c.thread_id===id).title,'2.1 验收对话');
    assert.equal(status.conversations.find(c=>c.thread_id===id).association,'host_session');
    const manual=(await call('register_conversation',{path:root,title:'手动兼容'})).structuredContent.result.thread_id;
    assert.equal((await call('file_info',{path:root,thread_id:manual})).structuredContent.thread_id,manual);

    const target=path.join(root,'confirmation.txt');fs.writeFileSync(target,'before');
    const gate=(await rpc('tools/call',{name:'write_file',arguments:{path:target,content:'after',overwrite:true},_meta:modern(A)})).result;
    assert.equal(gate.resultType,'input_required');
    const accepted=(await rpc('tools/call',{name:'write_file',arguments:{path:target,content:'after',overwrite:true},requestState:gate.requestState,inputResponses:{confirm:{action:'accept'}},_meta:modern(A)})).result;
    assert.equal(accepted.isError,false);assert.equal(fs.readFileSync(target,'utf8'),'after');assert.equal(accepted.structuredContent.thread_id,id);
    const task=(await rpc('tools/call',{name:'exec_command',arguments:{shell:'powershell',cmd:'Start-Sleep -Seconds 30',cwd:root,yield_time_ms:0},_meta:modern(A)})).result;
    assert.equal(task.resultType,'task');
    assert((await rpc('tasks/cancel',{taskId:task.taskId,_meta:modern(B)})).error);
    assert((await call('stop_command',{session_id:task.taskId},B)).isError);
    assert(!(await rpc('tasks/cancel',{taskId:task.taskId,_meta:modern(A)})).error);
    const text=(await call('read_file',{path:target},A));assert.equal(text.structuredContent.result.lines[0],'1: after');assert(!text.content[0].text.includes('after'));

    for(const url of ['http://example.com/file?token=do-not-log','https://127.0.0.1/file?token=do-not-log','https://[::1]/file','https://192.168.1.1/file','https://198.18.0.1/file']){
      const result=await call('import_file',{path:path.join(root,'refused.txt'),file:{file_id:'fixture',download_url:url}},A);
      assert(result.isError);assert(!JSON.stringify(result).includes('do-not-log'));assert(!fs.existsSync(path.join(root,'refused.txt')));
    }
    assert((await call('import_file',{path:target,file:{file_id:'fixture',download_url:'https://example.com/file'}},A)).isError);
    assert.equal(fs.readFileSync(target,'utf8'),'after');
    assert(!fs.readdirSync(root).some(n=>n.startsWith('.attachment-')));
    if(process.env.WORKSPACE_ATTACHMENT_SMOKE==='1'){
      const destination=path.join(root,'附件 LICENSE.txt');
      const result=await call('import_file',{path:destination,file:{file_id:'public-fixture',file_name:'LICENSE',mime_type:'text/plain',download_url:'https://raw.githubusercontent.com/CSL19980820/chatgpt-local-workspace/7b84a8074620c476a89e1b1f4189b543ddcaa892/LICENSE'}},A);
      assert.equal(result.isError,false,JSON.stringify(result));
      const expected=fs.readFileSync(path.join(__dirname,'../LICENSE'));
      assert(fs.readFileSync(destination).equals(expected));assert.equal(result.structuredContent.result.sha256,createHash('sha256').update(expected).digest('hex'));
      const expired=await call('import_file',{path:path.join(root,'404.txt'),file:{file_id:'expired',download_url:'https://raw.githubusercontent.com/CSL19980820/chatgpt-local-workspace/7b84a8074620c476a89e1b1f4189b543ddcaa892/no-such-attachment?token=do-not-log'}},A);
      assert(expired.isError);assert(!fs.existsSync(path.join(root,'404.txt')));assert(!fs.readdirSync(root).some(n=>n.startsWith('.attachment-')));
      t.diagnostic('Actual public HTTPS attachment downloaded; exact bytes and SHA256 verified. ChatGPT file picker not exercised.');
    } else t.diagnostic('Public network attachment smoke disabled; set WORKSPACE_ATTACHMENT_SMOKE=1.');
    const snapshot=await(await fetch(new URL('/api/snapshot',base))).json();
    assert(snapshot.activity.filter(a=>a.tool==='import_file'&&a.status==='failed').every(a=>!JSON.stringify(a.detail).includes('已接收聊天附件')));
    for(const secret of [...Object.values(A),'do-not-log']){assert(!JSON.stringify(snapshot).includes(secret));assert(!logs.includes(secret));}
    const report=await(await fetch(new URL('/api/diagnostics',base))).json();
    for(const label of ['MCP 握手','工具发现','实际工具调用','自动对话归属'])assert.equal(report.checks.find(c=>c.label===label).status,'pass');
    assert.equal(report.checks.find(c=>c.label==='隧道就绪').status,'unavailable');
    const launched=await launchDashboardBrowser();browser=launched.browser;
    const page=await browser.newPage({viewport:{width:1400,height:900}}),errors=[];page.on('pageerror',e=>errors.push(String(e)));
    await page.goto(base+'#diagnostics');await page.getByRole('dialog').waitFor();await page.getByText('自动对话归属',{exact:true}).waitFor();
    assert.equal(await page.locator('.diagnostic-status.pass').count(),4);
    await page.getByRole('button',{name:'重新检查',exact:true}).click();await page.getByRole('button',{name:'重新检查',exact:true}).waitFor();
    if(process.env.WORKSPACE_CAPTURE_DIAGNOSTICS==='1')await page.getByRole('dialog').screenshot({path:path.join(__dirname,'../docs/images/dashboard-diagnostics.png')});
    await page.keyboard.press('Escape');assert.equal(new URL(page.url()).hash,'');
    await page.evaluate(()=>location.hash='diagnostics');await page.getByRole('dialog').waitFor();
    for(const width of [1000,640]){await page.setViewportSize({width,height:720});assert(await page.getByRole('dialog').evaluate(e=>e.getBoundingClientRect().right<=innerWidth&&e.scrollHeight<=e.clientHeight+1));}
    await page.emulateMedia({colorScheme:'dark'});await page.setViewportSize({width:1000,height:800});
    await page.waitForTimeout(250);
    if(process.env.WORKSPACE_CAPTURE_DIAGNOSTICS==='1')await page.getByRole('dialog').screenshot({path:path.join(__dirname,'../work/diagnostics-dark.png')});
    assert.deepEqual(errors,[]);
  } finally {
    if(browser)await browser.close();for(const p of pending.values())clearTimeout(p.timer);
    child.stdin.end();await exited;fs.rmSync(root,{recursive:true,force:true});
  }
});

test('diagnostics runs official doctor and keeps readiness separate from tool success', { timeout: 25000 }, async t => {
  const exe=process.env.WORKSPACE_TEST_EXE||path.join(__dirname,'../dist-next/LocalWorkspace.exe');
  if(!fs.existsSync(path.join(path.dirname(exe),'tunnel-client.exe'))){t.skip('Requires release directory with official Tunnel Client');return;}
  const root=fs.mkdtempSync(path.join(os.tmpdir(),'workspace-doctor-'));
  const health=require('node:http').createServer((req,res)=>{res.writeHead(req.url==='/healthz'?200:503);res.end();});
  await new Promise(resolve=>health.listen(0,'127.0.0.1',resolve));
  const healthFile=path.join(root,'health.url');fs.writeFileSync(healthFile,'http://127.0.0.1:'+health.address().port);
  const child=spawn(exe,['--mcp'],{windowsHide:true,stdio:['pipe','pipe','pipe'],env:{...process.env,WORKSPACE_TUNNEL_ID:'invalid-fixture',CONTROL_PLANE_API_KEY:'',WORKSPACE_TUNNEL_HEALTH_FILE:healthFile}});
  const exited=new Promise(resolve=>child.once('exit',resolve));child.stdout.resume();
  try{
    const base=await new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(Error('No dashboard')),5000);child.stderr.on('data',d=>{const m=String(d).match(/\[Dashboard\] (http:\/\/127\.0\.0\.1:\d+\/)/);if(m){clearTimeout(timer);resolve(m[1]);}});});
    const report=await(await fetch(new URL('/api/diagnostics',base),{signal:AbortSignal.timeout(15000)})).json();
    const status=label=>report.checks.find(c=>c.label===label)?.status;
    assert.equal(status('配置自检'),'fail');assert.equal(status('隧道存活'),'pass');assert.equal(status('隧道就绪'),'fail');
    assert.equal(status('实际工具调用'),'pending');assert.equal(status('MCP 握手'),'pending');
    assert(!JSON.stringify(report).includes('invalid-fixture'));
  }finally{child.stdin.end();await exited;await new Promise(resolve=>health.close(resolve));fs.rmSync(root,{recursive:true,force:true});}
});
