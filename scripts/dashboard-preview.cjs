// Local preview server for the embedded dashboard. Never touches a workspace or runs MCP tools.
// Usage:
//   node scripts/dashboard-preview.cjs http://127.0.0.1:<port>/    live, read-only proxy of a running instance
//   node scripts/dashboard-preview.cjs --sample                    built-in classic sample (19 calls, 1 conversation)
//   node scripts/dashboard-preview.cjs --sample=rich               4 conversations, 8-file patch, long output, failures, timeout
// Sample-only options:
//   --diag=slow     /api/diagnostics answers after 3 s        --diag=error   /api/diagnostics answers 500
//   --clear=fail    /api/clear-logs answers 503 like a standalone MCP process
// Sample mode mocks GET /api/snapshot, GET /api/diagnostics, GET /api/local-actions,
// POST /api/clear-logs (same Origin + X-Workspace-Token checks as LocalDashboard) and
// POST /api/open (accepted, but never opens anything). Live mode stays read-only.
const http=require('node:http'),fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const args=process.argv.slice(2),option=name=>{const hit=args.find(a=>a===name||a.startsWith(name+'='));return hit===undefined?undefined:(hit.split('=')[1]||'');};
const sampleMode=option('--sample'),sample=sampleMode!==undefined,scenario=sampleMode||'classic';
const diagMode=option('--diag')||'',clearMode=option('--clear')||'';
if(sample&&!['classic','rich'].includes(scenario))throw Error('Unknown sample scenario: '+scenario+' (use classic or rich)');
const upstream=sample?null:new URL(args.find(a=>!a.startsWith('--'))||'');
if(!sample&&(upstream.protocol!=='http:'||upstream.hostname!=='127.0.0.1'||upstream.username||upstream.password))throw Error('Expected a loopback dashboard URL or --sample');
const html=path.join(__dirname,'../src/dashboard.html');
const samples=sample?require('./sample-snapshot.cjs'):null;
const policy="default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
const token='sample-'+crypto.randomBytes(8).toString('hex');
// Clearing is remembered for the lifetime of this preview server, like the real process.
let cleared=null;
function snapshot(){
  const value=scenario==='rich'?samples.rich():samples();
  if(cleared){
    value.activity=value.activity.filter(call=>call.status==='running');
    value.commands=value.commands.filter(command=>command.running);
    for(const conversation of value.conversations)delete conversation.stats;
    samples.finalize(value);
  }
  return value;
}
function clearLogs(){
  const before=snapshot();
  const runningCalls=before.activity.filter(call=>call.status==='running').length,runningCommands=before.commands.filter(c=>c.running).length;
  const result={
    cleared_activity:before.activity.length-runningCalls,
    cleared_commands:before.commands.length-runningCommands,
    scope:'已完成调用与命令日志已清空；运行中命令、当前任务证据、计划和对话保留。',
    scope_code:'completed_logs',
    cleared_at:new Date().toISOString(),
    kept:{running_calls:runningCalls,running_commands:runningCommands,plans:before.plans.length,conversations:before.conversations.length}
  };
  cleared=result.cleared_at;
  return result;
}
let origin;
const server=http.createServer(async(req,res)=>{
  const send=(code,type,body)=>{res.writeHead(code,{'Content-Type':type+'; charset=utf-8','Cache-Control':'no-store','Content-Security-Policy':policy,'X-Content-Type-Options':'nosniff'});res.end(body)};
  const json=(code,value)=>send(code,'application/json',JSON.stringify(value));
  if(req.headers.host!==origin.host||(req.headers.origin&&req.headers.origin!==origin.origin)||!['same-origin','none',undefined].includes(req.headers['sec-fetch-site']))return send(403,'text/plain','Local same-origin access only');
  const url=new URL(req.url,origin);
  if(url.origin!==origin.origin)return send(400,'text/plain','Invalid target');
  req.resume();
  const actions=['/api/clear-logs','/api/open'];
  if(actions.includes(url.pathname)){
    if(!sample)return send(404,'text/plain','Not found');
    if(req.method!=='POST')return send(405,'text/plain','POST only');
    if(req.headers.origin!==origin.origin||req.headers['x-workspace-token']!==token)return json(403,{error:'请从本地工作台操作。'});
    if(url.pathname==='/api/open')return json(200,{opened:true,sample:true});
    if(clearMode==='fail')return json(503,{error:'当前进程无法清空日志。'});
    return json(200,clearLogs());
  }
  if(req.method!=='GET')return send(405,'text/plain','GET only');
  try{
    if(url.pathname==='/')return send(200,'text/html',await fs.promises.readFile(html,'utf8'));
    if(sample&&url.pathname==='/sample-image')return send(200,'image/png',await fs.promises.readFile(path.join(__dirname,'../docs/images/dashboard-patch-review.png')));
    if(sample&&url.pathname==='/api/local-actions')return json(200,{token});
    if(sample&&url.pathname==='/api/diagnostics'){
      if(diagMode==='error')return json(500,{error:'诊断失败（示例）'});
      if(diagMode==='slow')await new Promise(resolve=>setTimeout(resolve,3000));
      return json(200,samples.diagnostics());
    }
    if(!sample&&(/^\/api\/images\/[a-f0-9]{32}$/.test(url.pathname)||url.pathname==='/api/diagnostics')){
      const response=await fetch(new URL(url.pathname,upstream),{signal:AbortSignal.timeout(15000),redirect:'error'});
      return send(response.status,response.headers.get('content-type')||'application/octet-stream',Buffer.from(await response.arrayBuffer()));
    }
    if(url.pathname!=='/api/snapshot')return send(404,'text/plain','Not found');
    if(sample){
      const value=snapshot(),thread=url.searchParams.get('thread');
      // Like LiveSnapshot: ?thread= narrows activity, plans and commands; the sidebar still lists every conversation.
      if(thread){
        value.thread_id=thread;
        value.activity=value.activity.filter(call=>call.thread_id===thread);
        value.plans=value.plans.filter(plan=>plan.thread_id===thread);
        value.commands=value.commands.filter(command=>command.thread_id===thread);
      }
      return json(200,value);
    }
    const target=new URL('/api/snapshot',upstream);
    if(url.searchParams.has('thread'))target.searchParams.set('thread',url.searchParams.get('thread'));
    const response=await fetch(target,{signal:AbortSignal.timeout(5000),redirect:'error'});
    send(response.status,'application/json',await response.text());
  }catch{return json(502,{error:'原工作台连接不可用'})}
});
server.headersTimeout=5000;server.requestTimeout=6000;server.maxConnections=16;
server.listen(0,'127.0.0.1',()=>{origin=new URL('http://127.0.0.1:'+server.address().port);console.log(origin.href+(sample?' (sample snapshot: '+scenario+')':' (live proxy)'))});
