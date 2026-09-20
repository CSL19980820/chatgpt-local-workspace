const {spawn}=require('node:child_process');
const fs=require('node:fs'),os=require('node:os'),path=require('node:path'),assert=require('node:assert/strict');
const root=fs.mkdtempSync(path.join(os.tmpdir(),'workspace-activity-test-'));
const other=root+'-other';fs.mkdirSync(other);
const child=spawn(process.env.WORKSPACE_TEST_EXE||path.join(__dirname,'../dist-next/LocalWorkspace.exe'),['--mcp'],{windowsHide:true,stdio:['pipe','pipe','pipe']});
let sequence=0,buffer='',log='';const pending=new Map();
child.stderr.on('data',d=>log+=d);
child.stdout.on('data',d=>{buffer+=d;let at;while((at=buffer.indexOf('\n'))>=0){const m=JSON.parse(buffer.slice(0,at));buffer=buffer.slice(at+1);const p=pending.get(m.id);if(p){clearTimeout(p.timer);pending.delete(m.id);p.resolve(m);}}});
function request(method,params={}){return new Promise((resolve,reject)=>{const id=++sequence;const timer=setTimeout(()=>reject(new Error('Timed out: '+method)),15000);pending.set(id,{resolve,timer});child.stdin.write(JSON.stringify({jsonrpc:'2.0',id,method,params})+'\n');});}
async function call(name,args={}){const r=await request('tools/call',{name,arguments:args});assert(!r.error,JSON.stringify(r));assert.equal(r.result.structuredContent.tool,name,'concurrent calls must retain their own tool identity');return r.result.structuredContent;}
const delay=ms=>new Promise(r=>setTimeout(r,ms));
async function main(){
 const init=await request('initialize');assert.equal(init.result.serverInfo.version,'2.1.0');
 const rendered=await call('read_workspace_activity',{path:root});assert.equal(rendered.result.activity.length,0);
 const started=Date.now();let done=false;
 const long=call('exec_command',{cwd:root,cmd:'printf first; sleep 3; printf last',yield_time_ms:5000}).then(r=>{done=true;return r;});
 await delay(180);
 const before=Date.now();const snap=await call('read_workspace_activity',{path:root,viewer_id:'test-panel',bridge:'standard',display_mode:'inline'});
 const latency=Date.now()-before;assert(latency<900,'observation blocked '+latency+'ms');assert.equal(done,false);
 assert(snap.result.activity.some(x=>x.tool==='exec_command'&&x.status==='running'));
 let registered=snap;for(let attempt=0;attempt<30&&!registered.result.commands.some(x=>x.running);attempt++){await delay(50);registered=await call('read_workspace_activity',{path:root});}assert(registered.result.commands.some(x=>x.running),'command never appeared while running');assert.equal(done,false);assert.equal(snap.result.ui.viewers[0].reads,1);
 const parallel=await Promise.all(Array.from({length:12},()=>call('read_workspace_activity',{path:root})));assert(parallel.every(x=>x.result.activity.length===1));
 const command=await long;assert.equal(command.isError,false);assert(command.result.output.includes('firstlast'));
 const final=await call('read_workspace_activity',{path:root});assert.equal(final.result.activity[0].status,'returned');assert.equal(final.result.commands[0].running,false);assert(final.result.commands[0].output.includes('firstlast'));
 console.log('PASS independent observation: '+latency+'ms while command took '+(Date.now()-started)+'ms; running->returned; isolated tool identity');
 const live=(await call('exec_command',{cwd:root,cmd:'printf untouched; sleep 1',yield_time_ms:0})).result;
 await delay(180);await call('read_workspace_activity',{path:root});await call('read_workspace_activity',{path:root});
 const delta=await call('poll_command',{session_id:live.session_id,yield_ms:1500});assert(delta.result.output.includes('untouched'),'UI consumed model output');
 const file=path.join(root,'ordered.txt');
 await Promise.all([call('write_file',{path:file,content:'before'}),call('edit_file',{path:file,old_text:'before',new_text:'after'})]);assert.equal(fs.readFileSync(file,'utf8'),'after');
 await call('update_plan',{path:root,plan:[{step:'verify progress',status:'in_progress'}]});
 await call('write_file',{path:path.join(other,'private.txt'),content:'other-workspace'});
 await call('read_file',{path:path.join(root,'missing.txt')});
 const scoped=await call('read_workspace_activity',{path:root});assert(!scoped.result.activity.some(x=>x.target.includes('-other/')));assert.equal(scoped.result.plans.length,1);assert(scoped.result.activity.some(x=>x.status==='failed'));assert(scoped.result.activity.some(x=>x.preview&&x.preview.includes('after')));
 const bad=await call('read_workspace_activity',{path:root,viewer_id:'invalid/id'});assert.equal(bad.isError,true);
 assert(log.includes('UI_CONNECTED'));assert.equal(scoped.result.ui.viewers.length,1);
 console.log('PASS non-consuming snapshots, ordered writes, scoped activity/plans, errors, bounded receipts and UI diagnostics');
}
main().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{for(const p of pending.values())clearTimeout(p.timer);child.stdin.end();await new Promise(r=>child.once('exit',r));for(const folder of [root,other]){const resolved=path.resolve(folder);assert(resolved.startsWith(path.resolve(os.tmpdir())+path.sep+'workspace-activity-test-'));fs.rmSync(resolved,{recursive:true,force:true});assert(!fs.existsSync(resolved));}console.log('isolated process exited and test directories removed');});
