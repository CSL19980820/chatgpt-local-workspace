const {spawn}=require('child_process');
const fs=require('fs'),path=require('path'),os=require('os'),assert=require('assert');
const root=fs.mkdtempSync(path.join(os.tmpdir(),'workspace-patch-'));
const exe=process.env.WORKSPACE_TEST_EXE||path.join(__dirname,'../dist-next/LocalWorkspace.exe');
const child=spawn(exe,['--mcp'],{windowsHide:true,stdio:['pipe','pipe','pipe']});
let sequence=0,buffer='';const pending=new Map();
child.stdout.on('data',d=>{buffer+=d;let n;while((n=buffer.indexOf('\n'))>=0){const msg=JSON.parse(buffer.slice(0,n));buffer=buffer.slice(n+1);if(pending.has(msg.id)){pending.get(msg.id)(msg);pending.delete(msg.id);}}});child.stderr.resume();
const request=(method,params={})=>new Promise(resolve=>{const id=++sequence;pending.set(id,resolve);child.stdin.write(JSON.stringify({jsonrpc:'2.0',id,method,params})+'\n');});
const patch=async body=>{const r=await request('tools/call',{name:'apply_patch',arguments:{cwd:root,patch:'*** Begin Patch\n'+body+'\n*** End Patch'}});assert(!r.error,JSON.stringify(r));return r.result;};
const ok=async body=>{const r=await patch(body);assert(!r.isError,JSON.stringify(r));return r.structuredContent.result;};
const bad=async body=>{const r=await patch(body);assert(r.isError,JSON.stringify(r));};
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
async function main(){
 await request('initialize',{protocolVersion:'2025-06-18',capabilities:{},clientInfo:{name:'patch-test',version:'1'}});
 assert.equal((await ok('*** Add File: nested/a.txt\n+alpha\n+beta')).count,1);assert.equal(read('nested/a.txt'),'alpha\nbeta\n');
 await ok('*** Update File: nested/a.txt\n@@\n alpha\n-beta\n+gamma\n*** End of File');assert.equal(read('nested/a.txt'),'alpha\ngamma\n');
 const moved=await ok('*** Update File: nested/a.txt\n*** Move to: moved.txt\n@@ alpha\n-gamma\n+delta');assert.equal(moved.files[0].operation,'move');assert(!fs.existsSync(path.join(root,'nested/a.txt')));assert.equal(read('moved.txt'),'alpha\ndelta\n');
 await bad('*** Add File: moved.txt\n+overwrite');assert.equal(read('moved.txt'),'alpha\ndelta\n');
 fs.writeFileSync(path.join(root,'occupied.txt'),'occupied');await bad('*** Update File: moved.txt\n*** Move to: occupied.txt');assert.equal(read('occupied.txt'),'occupied');
 await bad('*** Add File: untouched.txt\n+new\n*** Update File: moved.txt\n@@\n-missing\n+wrong');assert(!fs.existsSync(path.join(root,'untouched.txt')));assert.equal(read('moved.txt'),'alpha\ndelta\n');
 fs.writeFileSync(path.join(root,'ambiguous.txt'),'same\nsame\n');await bad('*** Update File: ambiguous.txt\n@@\n-same\n+changed');assert.equal(read('ambiguous.txt'),'same\nsame\n');
 for(const p of ['../escape.txt','C:/escape.txt','moved.txt:stream','trailing.','NUL.txt'])await bad('*** Add File: '+p+'\n+no');
 fs.writeFileSync(path.join(root,'bom.txt'),Buffer.concat([Buffer.from([239,187,191]),Buffer.from('one\r\ntwo\r\n')]));await ok('*** Update File: bom.txt\n@@\n one\n-two\n+three');assert(fs.readFileSync(path.join(root,'bom.txt')).equals(Buffer.concat([Buffer.from([239,187,191]),Buffer.from('one\r\nthree\r\n')])));
 fs.writeFileSync(path.join(root,'unicode.txt'),Buffer.concat([Buffer.from([255,254]),Buffer.from('first\r\nlast','utf16le')]));await ok('*** Update File: unicode.txt\n@@\n first\n-last\n+final\n*** End of File');assert(fs.readFileSync(path.join(root,'unicode.txt')).equals(Buffer.concat([Buffer.from([255,254]),Buffer.from('first\r\nfinal','utf16le')])));
 fs.writeFileSync(path.join(root,'readonly.txt'),'before');fs.chmodSync(path.join(root,'readonly.txt'),0o444);try{const r=await patch('*** Add File: partial.txt\n+retained\n*** Update File: readonly.txt\n@@\n-before\n+after');if(process.platform==='win32'){assert(r.isError);assert.equal(r.structuredContent.result.partial,true);assert.equal(r.structuredContent.result.rollback,'not_attempted');assert.equal(read('partial.txt'),'retained\n');assert.equal(read('readonly.txt'),'before');}}finally{fs.chmodSync(path.join(root,'readonly.txt'),0o666);}
 const target=path.join(root,'target');fs.mkdirSync(target);const link=path.join(root,'junction');fs.symlinkSync(target,link,'junction');await bad('*** Add File: junction/blocked.txt\n+blocked');assert(!fs.existsSync(path.join(target,'blocked.txt')));fs.unlinkSync(link);
 await bad('*** Update File: moved.txt\n@@\n alpha\n-delta\n+again\n*** Update File: moved.txt\n@@\n alpha\n-delta\n+other');assert.equal(read('moved.txt'),'alpha\ndelta\n');
 await ok('*** Delete File: moved.txt');assert(!fs.existsSync(path.join(root,'moved.txt')));
 const review=await request('tools/call',{name:'show_changes',arguments:{path:root}});assert(review.result.structuredContent.result.files.some(f=>f.path.endsWith('/bom.txt')));
 assert(!fs.readdirSync(root).some(x=>x.startsWith('.patch-')));
 console.log('PASS patch add/update/move/delete, validation before mutation, ambiguity, confinement, ADS, reparse points, encoding/newlines, partial failure and review');
}
const watchdog=setTimeout(()=>{child.kill();console.error('Patch tests timed out');process.exitCode=2;},45000);
main().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>{clearTimeout(watchdog);child.stdin.end();});
child.on('exit',()=>fs.rmSync(root,{recursive:true,force:true}));
