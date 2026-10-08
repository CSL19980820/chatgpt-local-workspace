// Explicit opt-in: WORKSPACE_TUNNEL_SMOKE=1 plus WORKSPACE_TUNNEL_SMOKE_KEY (and optionally WORKSPACE_TUNNEL_SMOKE_ID); never prints credentials.
const fs=require('node:fs'),path=require('node:path'),os=require('node:os'),assert=require('node:assert/strict');
const {spawn,execFileSync}=require('node:child_process');
if(process.env.WORKSPACE_TUNNEL_SMOKE!=='1'){console.log('SKIP authenticated tunnel smoke (set WORKSPACE_TUNNEL_SMOKE=1)');process.exit(0)}
// The key must be supplied explicitly; tests never read the stored credential.
if(!process.env.WORKSPACE_TUNNEL_SMOKE_KEY){console.log('SKIP authenticated tunnel smoke (set WORKSPACE_TUNNEL_SMOKE_KEY; the stored credential is never read)');process.exit(0)}
const repo=path.resolve(__dirname,'..');
const exe=process.env.WORKSPACE_TEST_EXE||path.join(repo,'dist','LocalWorkspace.exe');
const tunnel=path.join(repo,'dist','tunnel-client.exe');
// Refuse to take over a user's live Tunnel. This smoke uses isolated backend state.
const active=execFileSync('powershell.exe',['-NoProfile','-NonInteractive','-Command',"@(Get-CimInstance Win32_Process -Filter \"Name='tunnel-client.exe'\").Count"],{windowsHide:true,encoding:'utf8'}).trim();
assert.equal(active,'0','Close the live Tunnel before enabling this authenticated smoke; no process was stopped.');
const settings={Tunnel:process.env.WORKSPACE_TUNNEL_SMOKE_ID||JSON.parse(fs.readFileSync(path.join(process.env.LOCALAPPDATA,'LocalWorkspacePlugin','settings.json'),'utf8')).Tunnel,Key:process.env.WORKSPACE_TUNNEL_SMOKE_KEY};
assert.match(settings.Tunnel,/^tunnel_[a-zA-Z0-9]+$/);assert(settings.Key.length>=10);
const root=fs.mkdtempSync(path.join(os.tmpdir(),'workspace-tunnel-test-')),health=path.join(root,'health.url');
const env={...process.env,CONTROL_PLANE_API_KEY:settings.Key,MCP_COMMAND:'"'+exe.replaceAll('\\','/')+'" --mcp',WORKSPACE_STATE_DIR:path.join(root,'.state'),MCP_FORWARD_TRACE_CONTEXT:'true',WORKSPACE_DESKTOP_VERSION:'',WORKSPACE_TUNNEL_VERSION:'0.0.16'};
for(const key of ['MCP_SERVER_URL','TUNNEL_CLIENT_CONFIG','TUNNEL_CLIENT_PROFILE','TUNNEL_CLIENT_PROFILE_FILE','CLOUDFLARED_MANAGED','CLOUDFLARED_TUNNEL_TOKEN'])delete env[key];
const child=spawn(tunnel,['run','--control-plane.tunnel-id',settings.Tunnel,'--health.listen-addr','127.0.0.1:0','--health.url-file',health,'--log.format','json','--log.level','info'],{cwd:root,env,windowsHide:true,stdio:['ignore','pipe','pipe']});
const closed=new Promise(resolve=>child.once('close',resolve));
let initialized=false,discovered=false;let startError;
child.on('error',e=>startError=e);
for(const stream of [child.stdout,child.stderr])stream.on('data',chunk=>{const text=chunk.toString();initialized ||=text.includes('[Workspace] initialize | 2.4.0');discovered ||=text.includes('[Workspace] tools/list | 23 tools');});
async function run(){
 try{
  const deadline=Date.now()+45000;let ready=false;
  while(Date.now()<deadline){
   if(startError)throw new Error('Tunnel executable failed to start');
   if(child.exitCode!==null)throw new Error('Tunnel exited before becoming ready (code '+child.exitCode+')');
   if(fs.existsSync(health)){
    const url=fs.readFileSync(health,'utf8').trim();
    assert.match(url,/^http:\/\/127\.0\.0\.1:\d+$/);
    try{const response=await fetch(url+'/readyz',{signal:AbortSignal.timeout(1500)});if(response.ok){ready=true;break}}catch{}
   }
   await new Promise(r=>setTimeout(r,500));
  }
  assert(ready,'Tunnel did not become ready within 45 seconds');
  console.log(JSON.stringify({ready:true,tunnel_version:'0.0.16',observed_initialize_2_4:initialized,observed_tools_list_23:discovered,chatgpt_call_verified:false}));
 }finally{
  if(child.pid&&child.exitCode===null){try{execFileSync('taskkill.exe',['/PID',String(child.pid),'/T','/F'],{windowsHide:true,stdio:'ignore'})}catch{}}
  await Promise.race([closed,new Promise(r=>setTimeout(r,5000))]);
  fs.rmSync(root,{recursive:true,force:true,maxRetries:20,retryDelay:100});assert(!fs.existsSync(root));console.log('PASS tunnel process tree stopped and temporary directory removed');
 }
}
run().catch(e=>{console.error(e.message.replaceAll(settings.Key,'[redacted]'));process.exitCode=1});
