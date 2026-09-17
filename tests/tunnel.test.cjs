// Explicit opt-in: uses the existing local Tunnel settings, without printing credentials.
const fs=require('node:fs'),path=require('node:path'),os=require('node:os'),assert=require('node:assert/strict');
const {spawn,execFileSync}=require('node:child_process');
if(process.env.WORKSPACE_TUNNEL_SMOKE!=='1'){console.log('SKIP authenticated tunnel smoke (set WORKSPACE_TUNNEL_SMOKE=1)');process.exit(0)}
const repo=path.resolve(__dirname,'..');
const exe=process.env.WORKSPACE_TEST_EXE||path.join(repo,'dist','LocalWorkspace.exe');
const tunnel=path.join(repo,'dist','tunnel-client.exe');
const settings=JSON.parse(fs.readFileSync(path.join(process.env.LOCALAPPDATA,'LocalWorkspacePlugin','settings.json'),'utf8'));
assert.match(settings.Tunnel,/^tunnel_[a-zA-Z0-9]+$/);assert(settings.Key.length>=10);
const root=fs.mkdtempSync(path.join(os.tmpdir(),'workspace-tunnel-test-')),health=path.join(root,'health.url');
const env={...process.env,CONTROL_PLANE_API_KEY:settings.Key,MCP_COMMAND:'"'+exe.replaceAll('\\','/')+'" --mcp'};
for(const key of ['MCP_SERVER_URL','TUNNEL_CLIENT_CONFIG','TUNNEL_CLIENT_PROFILE','TUNNEL_CLIENT_PROFILE_FILE','CLOUDFLARED_MANAGED','CLOUDFLARED_TUNNEL_TOKEN'])delete env[key];
const child=spawn(tunnel,['run','--control-plane.tunnel-id',settings.Tunnel,'--health.listen-addr','127.0.0.1:0','--health.url-file',health,'--log.format','json','--log.level','info'],{cwd:root,env,windowsHide:true,stdio:['ignore','pipe','pipe']});
let initialized=false,discovered=false;let startError;
child.on('error',e=>startError=e);
for(const stream of [child.stdout,child.stderr])stream.on('data',chunk=>{const text=chunk.toString();initialized ||=text.includes('[Workspace] initialize | 1.3.0');discovered ||=text.includes('[Workspace] tools/list | 22 tools');});
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
  console.log(JSON.stringify({ready:true,observed_initialize_1_3:initialized,observed_tools_list_22:discovered,chatgpt_call_verified:false}));
 }finally{
  if(child.pid&&child.exitCode===null){try{execFileSync('taskkill.exe',['/PID',String(child.pid),'/T','/F'],{windowsHide:true,stdio:'ignore'})}catch{}}
  if(child.exitCode===null&&child.pid)await Promise.race([new Promise(r=>child.once('exit',r)),new Promise(r=>setTimeout(r,5000))]);
  fs.rmSync(root,{recursive:true,force:true});assert(!fs.existsSync(root));console.log('PASS tunnel process tree stopped and temporary directory removed');
 }
}
run().catch(e=>{console.error(e.message.replaceAll(settings.Key,'[redacted]'));process.exitCode=1});
