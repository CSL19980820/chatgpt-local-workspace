const test = require('node:test'), assert = require('node:assert/strict');
const fs = require('node:fs'), os = require('node:os'), path = require('node:path');
const {spawn, execFileSync} = require('node:child_process');
const exe = process.env.WORKSPACE_TEST_EXE || path.join(__dirname, '../dist-next/LocalWorkspace.exe');
const {launchDashboardBrowser} = require('../scripts/browser-launch.cjs');

function runtime(root) {
  const child = spawn(exe, ['--mcp'], {windowsHide:true, stdio:['pipe','pipe','pipe'], env:{...process.env, WORKSPACE_STATE_DIR:path.join(root,'.state')}});
  let seq=0, buffer='', base='', logs=''; const pending=new Map();
  const exited=new Promise(resolve=>child.once('exit',resolve));
  child.stderr.on('data',data=>{logs+=data;const match=logs.match(/\[Dashboard\] (http:\/\/127\.0\.0\.1:\d+\/)/);if(match)base=match[1];});
  child.stdout.on('data',data=>{buffer+=data;let end;while((end=buffer.indexOf('\n'))>=0){const value=JSON.parse(buffer.slice(0,end));buffer=buffer.slice(end+1);const p=pending.get(value.id);if(p){clearTimeout(p.timer);pending.delete(value.id);p.resolve(value);}}});
  function rpc(method,params={}) { return new Promise((resolve,reject)=>{const id=++seq;pending.set(id,{resolve,timer:setTimeout(()=>reject(Error('RPC timeout: '+method+' '+logs)),20000)});child.stdin.write(JSON.stringify({jsonrpc:'2.0',id,method,params})+'\n');}); }
  return {rpc, base:()=>base, child, async call(name,args={},session='reliability'){const r=await rpc('tools/call',{name,arguments:args,_meta:{'openai/session':session}});assert(!r.error,JSON.stringify(r));return r.result;},async stop(){child.stdin.end();await exited;for(const p of pending.values())clearTimeout(p.timer);}};
}
const data=r=>r.structuredContent.result;

test('guarded edits, undo/redo conflicts, persistent ownership and evidence', {timeout:90000}, async t=>{
  const root=fs.mkdtempSync(path.join(os.tmpdir(),'workspace-reliability-')), file=path.join(root,'sample.txt');
  let app=runtime(root), browser;
  try {
    await app.rpc('initialize');
    const initial=await app.call('write_file',{path:file,content:'first\r\nkeep\r\n'});assert(!initial.isError);
    const preview=await app.call('edit_file',{path:file,old_text:'first',new_text:'second',dry_run:true});assert.equal(data(preview).applied,false);assert(fs.readFileSync(file,'utf8').startsWith('first'));
    const changed=await app.call('edit_file',{path:file,old_text:'first',new_text:'second',expected_sha256:data(preview).before_sha256});assert(!changed.isError);const changeId=data(changed).change_id;
    assert((await app.call('write_file',{path:file,content:'stale',overwrite:true,expected_sha256:data(preview).before_sha256})).isError);
    assert((await app.call('edit_file',{path:file,old_text:'second\nkeep',new_text:'bad'})).isError);
    assert.equal(data(await app.call('restore_change',{change_id:changeId})).applied,false);
    fs.appendFileSync(file,'outside');assert((await app.call('restore_change',{change_id:changeId,apply:true})).isError);assert(fs.readFileSync(file,'utf8').endsWith('outside'));
    fs.writeFileSync(file,'second\r\nkeep\r\n');assert(!((await app.call('restore_change',{change_id:changeId,apply:true})).isError));assert.equal(fs.readFileSync(file,'utf8'),'first\r\nkeep\r\n');
    assert(!((await app.call('restore_change',{change_id:changeId,apply:true,redo:true})).isError));
    assert((await app.call('restore_change',{change_id:changeId,apply:true},'foreign')).isError);
    fs.writeFileSync(path.join(root,'a.txt'),'a\n');fs.writeFileSync(path.join(root,'b.txt'),'b\n');
    const patch=data(await app.call('apply_patch',{cwd:root,patch:'*** Begin Patch\n*** Update File: a.txt\n@@\n-a\n+A\n*** Update File: b.txt\n@@\n-b\n+B\n*** End Patch'}));assert(patch.change_id);
    fs.writeFileSync(path.join(root,'b.txt'),'external');assert((await app.call('restore_change',{change_id:patch.change_id,apply:true})).isError);assert.equal(fs.readFileSync(path.join(root,'a.txt'),'utf8'),'A\n');
    fs.writeFileSync(path.join(root,'b.txt'),'B\n');assert(!(await app.call('restore_change',{change_id:patch.change_id,apply:true})).isError);assert.equal(fs.readFileSync(path.join(root,'a.txt'),'utf8'),'a\n');assert.equal(fs.readFileSync(path.join(root,'b.txt'),'utf8'),'b\n');
    const registered=data(await app.call('register_conversation',{path:root,title:'Durable fixture'}));
    const evidence=await app.call('file_info',{path:file});
    // A separate task isolates intentional conflict diagnostics from completion checks.
    const proof=await app.call('file_info',{path:file},'proof');
    const plan=[{step:'Verify file',status:'completed',activity_ids:[proof.structuredContent.activity_id]}];
    assert(!(await app.call('update_plan',{path:root,plan},'proof')).isError);
    assert.equal(data(await app.call('check_task_completion',{path:root},'proof')).can_finish,true);
    assert((await app.call('update_plan',{path:root,plan:[{...plan[0],activity_ids:[evidence.structuredContent.activity_id]}]},'proof')).isError);
    await app.call('exec_command',{cwd:root,shell:'powershell',cmd:'exit 7',yield_time_ms:3000},'proof');
    let check=data(await app.call('check_task_completion',{path:root},'proof'));assert.equal(check.can_finish,false);
    await app.call('update_plan',{path:root,plan},'proof');assert.equal(data(await app.call('check_task_completion',{path:root},'proof')).can_finish,false);
    const repaired=await app.call('exec_command',{cwd:root,shell:'powershell',cmd:'exit 0',yield_time_ms:3000},'proof');
    await app.call('update_plan',{path:root,plan,resolved_issue_id:check.last_issue_id,recovery_note:'Retried command successfully',recovery_evidence_id:repaired.structuredContent.activity_id},'proof');
    assert.equal(data(await app.call('check_task_completion',{path:root},'proof')).recovery_verified,true);
    const stateFile=fs.readFileSync(path.join(root,'.state','changes.bin'));assert(!stateFile.includes(Buffer.from('second')),'encrypted history');
    await app.stop();app=runtime(root);await app.rpc('initialize');
    assert.equal(data(await app.call('register_conversation',{path:root,title:'Durable fixture'})).thread_id,registered.thread_id);
    assert(data(await app.call('workspace_history',{path:root})).changes.some(c=>c.id===changeId));
    assert.equal(data(await app.call('check_task_completion',{path:root},'proof')).can_finish,true);
    assert(!(await app.call('restore_change',{change_id:changeId,apply:true})).isError);assert(fs.readFileSync(file,'utf8').startsWith('first'));
    const scoped=data(await app.call('read_workspace_activity',{path:root}));assert(scoped.activity.some(a=>a.tool==='restore_change'),'restores belong to the workspace timeline');
    // Actual rendered evidence/history details against the rebuilt EXE.
    browser=(await launchDashboardBrowser()).browser;const page=await browser.newPage({viewport:{width:1000,height:740}});const errors=[];page.on('pageerror',e=>errors.push(String(e)));await page.goto(app.base());
    const snapshot=await(await fetch(app.base()+'api/snapshot')).json();assert(snapshot.activity.some(a=>a.detail?.change_id===changeId));
    await page.getByText('文件恢复记录',{exact:true}).first().waitFor({timeout:10000});
    await page.getByText('执行证据',{exact:true}).first().click();assert((await page.locator('#detail-body').innerText()).includes('活动 ID'));
    await page.setViewportSize({width:640,height:720});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));assert((await page.locator('#detail-title').boundingBox()).height<30,'title must stay on one line');
    await page.getByText('文件恢复记录',{exact:true}).first().click();
    if(process.env.WORKSPACE_CAPTURE_RELIABILITY==='1')await page.screenshot({path:path.join(__dirname,'../work/reliability-narrow.png')});
    await page.setViewportSize({width:1200,height:780});
    if(process.env.WORKSPACE_CAPTURE_RELIABILITY==='1')await page.screenshot({path:path.join(__dirname,'../docs/images/dashboard-reliability.png')});
    await app.call('edit_file',{path:file,old_text:'first',new_text:'previewed',dry_run:true});await page.getByText('预览，未修改文件',{exact:true}).waitFor();assert((await page.locator('#detail-body').innerText()).includes('previewed'));assert(fs.readFileSync(file,'utf8').startsWith('first'));assert.deepEqual(errors,[]);
  } finally {if(browser)await browser.close();await app.stop();fs.rmSync(root,{recursive:true,force:true});}
});

test('command retries, output cursors, lost runtime reconciliation and private review baselines', {timeout:90000}, async()=>{
  const root=fs.mkdtempSync(path.join(os.tmpdir(),'workspace-retry-')), repo=path.join(root,'repo');fs.mkdirSync(repo);let app=runtime(root);
  const git=(...args)=>execFileSync('git',args,{cwd:repo,windowsHide:true,stdio:'pipe'});
  try {
    await app.rpc('initialize');
    const args={cwd:root,shell:'powershell',cmd:"Add-Content -LiteralPath count.txt -Value once; [Console]::Write('0123456789')",request_id:'one',yield_time_ms:3000};
    const one=data(await app.call('exec_command',args)),two=data(await app.call('exec_command',args));assert.equal(one.session_id,two.session_id);assert.equal(two.request_replayed,true);assert.equal(fs.readFileSync(path.join(root,'count.txt'),'utf8').trim(),'once');
    assert((await app.call('exec_command',{...args,cmd:'echo other'})).isError);
    const page=data(await app.call('read_command',{session_id:one.session_id,offset:2,length:3}));assert.equal(page.output,'234');assert.equal(page.next_offset,5);assert.equal(page.output_mode,'page');assert(!('full_output' in page));
    assert.equal(data(await app.call('read_command',{session_id:one.session_id,offset:-3,length:3})).output,'789');
    const flood=data(await app.call('exec_command',{cwd:root,shell:'powershell',cmd:"[Console]::Write(('x' * 150000))",yield_time_ms:3000}));
    const gap=data(await app.call('read_command',{session_id:flood.session_id,offset:0,length:5}));assert.equal(gap.gap,true);assert(gap.oldest_offset>0);
    git('init','-q');fs.writeFileSync(path.join(repo,'a.txt'),'before\n');git('add','a.txt');git('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','-qm','fixture');
    const indexBefore=fs.readFileSync(path.join(repo,'.git','index')),head=git('rev-parse','HEAD').toString();
    assert.equal(data(await app.call('open_workspace',{path:repo})).review.available,true);
    fs.writeFileSync(path.join(repo,'a.txt'),'after\n');fs.writeFileSync(path.join(repo,'new.txt'),'new\n');
    const review=data(await app.call('show_changes',{path:repo,since:'workspace_open',mark_reviewed:true}));assert.equal(review.count,2);assert(review.output.includes('+after'));assert(review.output.includes('+new'));
    assert.equal(data(await app.call('show_changes',{path:repo,since:'last_shown'})).count,0);assert.equal(git('rev-parse','HEAD').toString(),head);assert(fs.readFileSync(path.join(repo,'.git','index')).equals(indexBefore));
    await app.call('update_plan',{path:root,plan:[{step:'Verify',status:'completed',evidence:'Declared check'}]},'interrupted');
    await app.call('exec_command',{cwd:root,shell:'powershell',cmd:'Start-Sleep -Seconds 30',yield_time_ms:0},'interrupted');
    await app.stop();app=runtime(root);await app.rpc('initialize');
    assert((await app.call('exec_command',args)).isError);assert.equal(fs.readFileSync(path.join(root,'count.txt'),'utf8').trim(),'once');
    const lost=data(await app.call('check_task_completion',{path:root},'interrupted'));assert.equal(lost.can_finish,false);assert.match(lost.last_issue,/PROCESS_RESTARTED/);
    assert.equal(data(await app.call('show_changes',{path:repo,since:'workspace_open'})).count,2);
  } finally {await app.stop();fs.rmSync(root,{recursive:true,force:true});}
});

test('Windows credential store round trip uses an isolated synthetic target',()=>{
  const script="$asm=[Reflection.Assembly]::LoadFrom($args[0]);$type=$asm.GetType('WorkspaceCredentials');$flags=[Reflection.BindingFlags]'Static,NonPublic';$save=$type.GetMethod('SaveTarget',$flags);$read=$type.GetMethod('ReadTarget',$flags);$name='LocalWorkspacePlugin/test-'+[Guid]::NewGuid().ToString('N');try{$save.Invoke($null,@($name,'synthetic-test-key'))|Out-Null;if($read.Invoke($null,@($name)) -ne 'synthetic-test-key'){throw 'Credential mismatch'}}finally{$save.Invoke($null,@($name,''))|Out-Null};if($read.Invoke($null,@($name)) -ne ''){throw 'Credential test target remains'}";
  // The path is passed as a positional script argument, not interpolated into shell code.
  execFileSync('powershell.exe',['-NoProfile','-NonInteractive','-Command','& { '+script+' }',exe],{windowsHide:true,stdio:'pipe'});
});
