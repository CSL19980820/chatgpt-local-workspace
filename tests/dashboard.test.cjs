const {spawn}=require('node:child_process');
const fs=require('node:fs'),os=require('node:os'),path=require('node:path'),assert=require('node:assert/strict'),http=require('node:http'),vm=require('node:vm');
const root=fs.mkdtempSync(path.join(os.tmpdir(),'workspace-dashboard-test-'));
const child=spawn(process.env.WORKSPACE_TEST_EXE||path.join(__dirname,'../dist-next/LocalWorkspace.exe'),['--mcp'],{windowsHide:true,stdio:['pipe','pipe','pipe']});
let sequence=0,buffer='',log='',base='';const pending=new Map();
child.stderr.on('data',d=>{log+=d;const m=log.match(/\[Dashboard\] (http:\/\/127\.0\.0\.1:\d+\/)/);if(m)base=m[1]});
child.stdout.on('data',d=>{buffer+=d;let at;while((at=buffer.indexOf('\n'))>=0){const m=JSON.parse(buffer.slice(0,at));buffer=buffer.slice(at+1);const p=pending.get(m.id);if(p){clearTimeout(p.timer);pending.delete(m.id);p.resolve(m);}}});
function request(method,params={}){return new Promise((resolve,reject)=>{const id=++sequence;const timer=setTimeout(()=>reject(Error('Timed out '+method)),20000);pending.set(id,{resolve,timer});child.stdin.write(JSON.stringify({jsonrpc:'2.0',id,method,params})+'\n')})}
async function call(name,args={}){const r=await request('tools/call',{name,arguments:args});assert(!r.error,JSON.stringify(r));return r.result.structuredContent}
const delay=ms=>new Promise(r=>setTimeout(r,ms));
async function get(thread=''){const r=await fetch(base+'api/snapshot'+(thread?'?thread='+encodeURIComponent(thread):''));assert.equal(r.status,200);return r.json()}
async function status(headers={},method='GET'){return new Promise((resolve,reject)=>{const req=http.request(base+'api/snapshot',{method,headers},r=>{r.resume();r.on('end',()=>resolve(r.statusCode))});req.on('error',reject);req.end()})}
async function main(){
 await request('initialize');for(let n=0;!base&&n<40;n++)await delay(25);assert(base);
 const a=(await call('register_conversation',{title:'界面优化 · 验收线程 A',path:root,chat_id:'00000000-0000-4000-8000-000000000001'})).result;
 const b=(await call('register_conversation',{title:'命令排查 · 验收线程 B',path:root})).result;
 assert.notEqual(a.thread_id,b.thread_id);assert.equal(b.chat_id,'');assert(a.dashboard_url.startsWith(base+'#thread='));
 const again=(await call('register_conversation',{title:'界面优化 · 验收线程 A',path:root,chat_id:a.chat_id})).result;assert.equal(again.thread_id,a.thread_id);
 const bad=await request('tools/call',{name:'register_conversation',arguments:{title:'bad',path:root,chat_id:'invented'}});assert(bad.error||bad.result.isError);
 await call('update_plan',{path:root,thread_id:a.thread_id,plan:[{step:'检查当前页面',status:'completed'},{step:'验证持续输出与时间线',status:'in_progress'},{step:'完成页面验收',status:'pending'}]});
 await call('update_plan',{path:root,thread_id:b.thread_id,plan:[{step:'检查隐藏命令',status:'pending'}]});
 await call('file_info',{path:root,thread_id:b.thread_id});
 await call('read_file',{path:path.join(root,'missing.txt'),thread_id:b.thread_id});
 await call('file_info',{path:root});
 const seconds=process.argv.includes('--preview')?60:3;
 const command=(await call('exec_command',{cwd:root,thread_id:a.thread_id,cmd:`printf '正在检查实时输出…\n'; for i in $(seq 1 ${seconds}); do printf '步骤 %s：检查完成\n' "$i"; sleep 1; done; printf '验收完成\n'`,yield_time_ms:0})).result;
 const one=await get(a.thread_id),two=await get(b.thread_id),none=await get('unassigned');
 assert(one.activity.every(x=>x.thread_id===a.thread_id));assert(two.activity.every(x=>x.thread_id===b.thread_id));assert(none.activity.every(x=>x.thread_id==='unassigned'));assert.equal(one.plans.length,1);assert.equal(two.plans.length,1);assert.equal(two.commands.length,0);assert.equal(one.commands.length,1);assert(one.commands[0].running);assert(one.activity.length>0&&two.activity.length>0&&none.activity.length>0);
 assert.equal((await call('read_command',{session_id:command.session_id,thread_id:b.thread_id})).result.error_code,'THREAD_MISMATCH');
 await call('read_command',{session_id:command.session_id});assert((await get(a.thread_id)).activity.some(x=>x.tool==='read_command'));
 assert.equal(await status({Origin:'https://evil.example'}),403);assert.equal(await status({Host:'evil.example'}),403);assert.equal(await status({'Sec-Fetch-Site':'cross-site'}),403);assert.equal(await status({},'POST'),405);
 const html=await (await fetch(base)).text();assert(html.includes('调用时间线'));for(const [,script]of html.matchAll(/<script>([\s\S]*?)<\/script>/g))new vm.Script(script);
 const unknown=await fetch(base+'api/snapshot?thread=not-a-thread');assert.equal(unknown.status,400);
 console.log('PASS registered conversations, same-project thread isolation, plans/output, session ownership, unassigned fallback, HTTP origin/host/method checks and page script syntax');
 if(process.argv.includes('--preview')){console.log('PREVIEW '+base);console.log('THREAD '+a.thread_id);console.log('Preview auto-closes after 180 seconds.');await delay(180000);return;}
 // Parallel test files slow the shell down, so wait for the real completion instead of a fixed delay.
 let final=await get(a.thread_id);for(let n=0;final.commands[0].running&&n<40;n++){await delay(300);final=await get(a.thread_id);}
 assert.equal(final.commands[0].running,false);assert(final.commands[0].output.includes('验收完成'));assert.equal(final.commands[0].exit_code,0);
 const delta=(await call('poll_command',{session_id:command.session_id,thread_id:a.thread_id,yield_ms:0})).result;assert(delta.output.includes('验收完成'));
 assert(log.includes('[thread='+a.thread_id+']'));assert(log.includes('[thread='+b.thread_id+']'));
 console.log('PASS local HTTP observed actual running->complete command without consuming incremental output');
}
main().catch(e=>{console.error(e);process.exitCode=1}).finally(async()=>{for(const p of pending.values())clearTimeout(p.timer);child.stdin.end();await new Promise(r=>child.once('exit',r));const resolved=path.resolve(root);assert(resolved.startsWith(path.resolve(os.tmpdir())+path.sep+'workspace-dashboard-test-'));fs.rmSync(resolved,{recursive:true,force:true});assert(!fs.existsSync(resolved));console.log('isolated process and preview files cleaned')});
