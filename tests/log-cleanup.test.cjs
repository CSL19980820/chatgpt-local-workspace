const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const {spawn, execFileSync} = require('node:child_process');
const {launchDashboardBrowser} = require('../scripts/browser-launch.cjs');
const exe = process.env.WORKSPACE_TEST_EXE || path.join(__dirname, '../dist-next/LocalWorkspace.exe');

function runtime(root) {
  const child = spawn(exe, ['--mcp'], {
    windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'],
    env: {...process.env, WORKSPACE_STATE_DIR: path.join(root, '.state')}
  });
  let seq = 0, buffer = '', logs = '', base = '';
  const pending = new Map();
  const exited = new Promise(resolve => child.once('exit', resolve));
  child.stderr.on('data', data => {
    logs += data;
    const match = logs.match(/\[Dashboard\] (http:\/\/127\.0\.0\.1:\d+\/)/);
    if (match) base = match[1];
  });
  child.stdout.on('data', data => {
    buffer += data;
    let end;
    while ((end = buffer.indexOf('\n')) >= 0) {
      const value = JSON.parse(buffer.slice(0, end));
      buffer = buffer.slice(end + 1);
      const p = pending.get(value.id);
      if (p) { clearTimeout(p.timer); pending.delete(value.id); p.resolve(value); }
    }
  });
  function rpc(method, params = {}) {
    return new Promise((resolve, reject) => {
      const id = ++seq;
      pending.set(id, {resolve, timer: setTimeout(() => {
        pending.delete(id); reject(Error('RPC timeout: ' + method + ' ' + logs));
      }, 20000)});
      child.stdin.write(JSON.stringify({jsonrpc: '2.0', id, method, params}) + '\n');
    });
  }
  return {
    rpc, child,
    get base() { return base; },
    async call(name, args = {}) {
      const r = await rpc('tools/call', {name, arguments: args, _meta: {'openai/session': 'log-cleanup'}});
      assert(!r.error, JSON.stringify(r));
      assert(!r.result.isError, JSON.stringify(r.result));
      return r.result;
    },
    async snapshot() { assert(base, logs); const response = await fetch(base + 'api/snapshot', {headers: {Connection: 'close'}}); assert(response.ok); return response.json(); },
    async stop(crash = false) {
      if (child.exitCode === null && child.signalCode === null) {
        if (crash) child.kill(); else child.stdin.end();
      }
      await exited;
      for (const p of pending.values()) clearTimeout(p.timer);
    }
  };
}
const data = r => r.structuredContent.result;

// Only synthetic state under this test's temporary directory is encrypted/decrypted.
function state(root, name, value) {
  const writing = arguments.length === 3;
  const script = `
    $ErrorActionPreference='Stop'; Add-Type -AssemblyName System.Security;
    $file=$env:LOG_TEST_FILE; $name=$env:LOG_TEST_NAME;
    $entropy=[Text.Encoding]::UTF8.GetBytes('LocalWorkspace/state-v1/'+$name);
    if ($env:LOG_TEST_MODE -eq 'write') {
      $plain=[Text.Encoding]::UTF8.GetBytes([Console]::In.ReadToEnd());
      try { $bytes=[Security.Cryptography.ProtectedData]::Protect($plain,$entropy,[Security.Cryptography.DataProtectionScope]::CurrentUser); [IO.File]::WriteAllBytes($file,$bytes) }
      finally { [Array]::Clear($plain,0,$plain.Length) }
    } else {
      $plain=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($file),$entropy,[Security.Cryptography.DataProtectionScope]::CurrentUser);
      try { [Console]::Out.Write([Text.Encoding]::UTF8.GetString($plain)) }
      finally { [Array]::Clear($plain,0,$plain.Length) }
    }
  `;
  const output = execFileSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', script], {
    windowsHide: true, encoding: 'utf8', input: writing ? JSON.stringify(value).replace(/"\/Date\((-?\d+)\)\/"/g, '"\\/Date($1)\\/"') : '',
    env: {...process.env, LOG_TEST_FILE: path.join(root, '.state', name + '.bin'), LOG_TEST_NAME: name, LOG_TEST_MODE: writing ? 'write' : 'read'}
  });
  return writing ? undefined : JSON.parse(output);
}
function assertNoHistory(root) {
  for (const name of ['activity', 'activity-state', 'plans', 'threads']) {
    assert(!fs.existsSync(path.join(root, '.state', name + '.bin')), name + ' must be physically deleted');
  }
}

test('legacy logs, conversation titles and plans are deleted before the first rendered snapshot', {timeout: 60000}, async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'workspace-log-migration-'));
  const marker = 'synthetic-legacy-body-must-disappear';
  let app = runtime(root), browser;
  try {
    await app.rpc('initialize');
    const registration = data(await app.call('register_conversation', {path: root, title: 'Log migration fixture'}));
    await app.stop();
    const binding = state(root, 'thread-bindings').find(row => row.Id === registration.thread_id);
    state(root, 'threads', [{...binding, Title: marker, Path: root, Named: true, Created: '/Date(1000)/'}]);
    state(root, 'plans', {[registration.thread_id + '|' + root]: {
      Thread: registration.thread_id, Path: root, State: 'active', Updated: '/Date(1000)/',
      Steps: [{step: marker, status: 'in_progress', evidence: marker}]
    }});
    state(root, 'activity-state', [{Id: 'legacy-verified', Tool: 'read_file', Target: root, ThreadId: registration.thread_id, Status: 'returned', Verified: true, Started: '/Date(1000)/'}]);
    state(root, 'activity', [{
      Id: 'legacy-verified', Tool: 'read_file', Target: root.replace(/\\/g, '/') + '/sample.txt',
      Status: 'returned', Error: null, ThreadId: registration.thread_id,
      Started: '/Date(' + (Date.now() - 86400000) + ')/', Verified: true, Elapsed: 12,
      Preview: marker, Trace: marker, Detail: {kind: 'text', lines: [marker], output: marker, command: marker}
    }]);
    app = runtime(root);
    await app.rpc('initialize');
    assertNoHistory(root);
    const first = await app.snapshot();
    assert.equal(first.activity.length, 0, 'the dashboard must not display previous-process logs');
    assert.equal(first.conversations.length, 0, 'the sidebar must start without old conversations');
    assert.equal(first.plans.length, 0, 'the dashboard must start without the previous execution plan');
    assert(!JSON.stringify(first).includes(marker));
    const identities = state(root, 'thread-bindings');
    assert(identities.some(row => row.Id === registration.thread_id));
    assert(identities.every(row => Object.keys(row).every(key => ['Id', 'HostKey', 'ChatId'].includes(key))));
    assert(!JSON.stringify(identities).includes(marker));
    assert(!JSON.stringify(identities).includes(root));
    browser = (await launchDashboardBrowser()).browser;
    const page = await browser.newPage({viewport: {width: 1200, height: 760}});
    const errors = []; page.on('pageerror', error => errors.push(String(error)));
    await page.goto(first.dashboard_url);
    await page.getByText('暂无调用记录', {exact: true}).waitFor();
    assert.deepEqual(await page.locator('#threads .thread b').allInnerTexts(), ['全部对话']);
    assert.equal(await page.getByText('执行计划', {exact: true}).count(), 0);
    assert(!((await page.locator('body').innerText()).includes(marker)));
    assert.deepEqual(errors, []);
    if (process.env.WORKSPACE_CAPTURE_LOG_CLEANUP === '1') await page.screenshot({path: path.join(__dirname, '../work/log-cleanup-empty.png')});
    await browser.close(); browser = null;
    assert.equal(data(await app.call('get_workspace_status', {path: root})).workspace.activity.length, 0);
    const invalid = await app.rpc('tools/call', {name: 'update_plan', arguments: {path: root, plan: [{step: 'Old evidence', status: 'completed', activity_ids: ['legacy-verified']}]}, _meta: {'openai/session': 'log-cleanup'}});
    assert(invalid.result.isError, 'deleted evidence must not validate a new task');
    // A new conversation has its own current evidence and can complete normally.
    const proof = await app.call('list_directory', {path: root});
    await app.call('update_plan', {path: root, plan: [{step: 'Current verification', status: 'completed', activity_ids: [proof.structuredContent.activity_id]}], resolved_issue_id: invalid.result.structuredContent.activity_id, recovery_note: 'Intentional stale-evidence rejection verified'});
    assert.equal(data(await app.call('check_task_completion', {path: root})).can_finish, true);
    assertNoHistory(root);
    await app.stop(); app = runtime(root); await app.rpc('initialize');
    const next = await app.snapshot();
    assert.equal(next.activity.length, 0, 'cleanup remains effective after another restart');
    assert.equal(next.conversations.length, 0); assert.equal(next.plans.length, 0);
    assert.equal(data(await app.call('check_task_completion', {path: root})).state, 'untracked');
    assertNoHistory(root);
  } finally { if (browser) await browser.close(); await app.stop(); fs.rmSync(root, {recursive: true, force: true}); }
});

test('clear completed logs preserves running output, task evidence, failure closure and retry ownership', {timeout: 60000}, async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'workspace-clear-logs-'));
  const app = runtime(root);
  let browser;
  try {
    await app.rpc('initialize'); await app.rpc('tools/list');
    fs.writeFileSync(path.join(root, 'evidence.txt'), 'current evidence');
    const read = await app.call('read_file', {path: path.join(root, 'evidence.txt')});
    const evidence = read.structuredContent.activity_id;
    await app.call('update_plan', {path: root, plan: [{step: 'Verify cleanup', status: 'completed', activity_ids: [evidence]}]});
    const args = {cwd: root, cmd: "printf once >> counter.txt; printf 'completed-output'", request_id: 'clear-once', yield_time_ms: 3000};
    const completed = data(await app.call('exec_command', args));
    assert.equal(completed.output, 'completed-output'); assert(!('full_output' in completed));
    const page = data(await app.call('read_command', {session_id: completed.session_id, offset: -6, length: 6}));
    assert.equal(page.output, 'output'); assert(!('full_output' in page));
    const compatibility = data(await app.call('read_command', {session_id: completed.session_id, include_full_output: true}));
    assert.equal(compatibility.full_output, compatibility.output); assert.equal(compatibility.output_chars, 16);
    const failed = await app.rpc('tools/call', {name: 'exec_command', arguments: {cwd: root, cmd: 'exit 7', yield_time_ms: 3000}, _meta: {'openai/session': 'log-cleanup'}});
    assert(failed.result.isError);
    const before = data(await app.call('check_task_completion', {path: root}));
    assert(!before.can_finish); assert(before.last_issue_id);
    const running = data(await app.call('exec_command', {cwd: root, cmd: "printf 'running-output'; read -r line; printf '%s' \"$line\"", yield_time_ms: 300}));
    assert(running.running);
    const ledger = fs.readFileSync(path.join(root, '.state', 'requests.bin'));
    const origin = new URL(app.base).origin;
    const token = (await (await fetch(app.base + 'api/local-actions')).json()).token;
    assert.equal((await fetch(app.base + 'api/clear-logs')).status, 405);
    assert.equal((await fetch(app.base + 'api/clear-logs', {method: 'POST', headers: {Origin: origin}})).status, 403);
    assert.equal((await fetch(app.base + 'api/clear-logs', {method: 'POST', headers: {Origin: 'https://example.com', 'X-Workspace-Token': token}})).status, 403);
    browser = (await launchDashboardBrowser()).browser; const pageView = await browser.newPage({viewport: {width: 1000, height: 640}});
    const errors = []; pageView.on('pageerror', error => errors.push(error.message));
    await pageView.goto(app.base); await pageView.locator('#footer').filter({hasText: '2.4.0'}).waitFor();
    await pageView.locator('#pause').click();
    await pageView.locator('#more').click();
    await pageView.locator('#clear-logs').click();
    await pageView.locator('#clear-confirm-action').click();
    await pageView.locator('#notice').waitFor();
    await pageView.waitForFunction(() => document.querySelectorAll('#timeline .event').length === 1);
    assert.equal(await pageView.locator('#pause').textContent(), '恢复');
    const cleared = await app.snapshot(); assert.equal(cleared.activity.length, 0);
    assert.equal(cleared.commands.length, 1); assert.equal(cleared.commands[0].session_id, running.session_id);
    assert(cleared.commands[0].running); assert(cleared.commands[0].output.includes('running-output'));
    assert.equal(cleared.plans.length, 1); assert.equal(cleared.conversations.length, 1);
    const stats = cleared.conversations[0].stats; assert.deepEqual([stats.calls, stats.failed, stats.added, stats.removed], [0, 0, 0, 0], JSON.stringify(stats));
    assert.equal((await (await fetch(app.base + 'api/clear-logs', {method: 'POST', headers: {Origin: origin}})).json()).error, '请从本地工作台操作。');
    const again = await (await fetch(app.base + 'api/clear-logs', {method: 'POST', headers: {Origin: origin, 'X-Workspace-Token': token}})).json();
    assert.equal(again.scope_code, 'completed_logs'); assert(!isNaN(Date.parse(again.cleared_at))); assert.match(again.scope, /。$/);
    assert.deepEqual(again.kept, {running_calls: 0, running_commands: 1, plans: 1, conversations: 1});
    assert(fs.readFileSync(path.join(root, '.state', 'requests.bin')).equals(ledger));
    const after = data(await app.call('check_task_completion', {path: root}));
    assert(!after.can_finish); assert.equal(after.last_issue_id, before.last_issue_id); assert.deepEqual(after.missing_evidence, []);
    const retry = data(await app.call('exec_command', args));
    assert(retry.request_replayed); assert(retry.logs_cleared); assert.equal(retry.output, '');
    assert.equal(fs.readFileSync(path.join(root, 'counter.txt'), 'utf8'), 'once');
    const done = data(await app.call('write_stdin', {session_id: running.session_id, chars: 'finished\n', close: true, yield_time_ms: 3000}));
    assert(!done.running); assert(done.output.includes('finished'));
    const finalOutput = data(await app.call('read_command', {session_id: running.session_id}));
    assert(finalOutput.output.includes('running-output') && finalOutput.output.includes('finished'));
    await pageView.locator('#diagnostics').click();
    await pageView.locator('.component-version').first().waitFor();
    assert.equal(await pageView.locator('.component-version').count(), 5);
    assert((await pageView.locator('.component-versions').textContent()).includes('1.0.4258.31'));
    const report = await (await fetch(app.base + 'api/diagnostics')).json();
    const mcp = report.versions.find(row => row.label === '本地 MCP'); assert.equal(mcp.running, '2.4.0'); assert.equal(mcp.installed, '2.4.0'); assert.equal(mcp.restart_required, false);
    assert.deepEqual(errors, []);
  } finally { try { if (browser) await browser.close(); } finally { await app.stop(); fs.rmSync(root, {recursive: true, force: true}); } }
});

test('bindings avoid writes for reads and reclaim unused capacity while preserving retry and undo owners', {timeout: 60000}, async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'workspace-binding-capacity-'));
  let app = runtime(root);
  let locker;
  try {
    await app.rpc('initialize');
    const owner = data(await app.call('register_conversation', {path: root})).thread_id;
    const file = path.join(root, 'owned.txt'); fs.writeFileSync(file, 'before');
    const change = data(await app.call('edit_file', {path: file, old_text: 'before', new_text: 'after'}));
    const args = {cwd: root, cmd: 'printf once >> retry.txt', request_id: 'capacity-once', yield_time_ms: 3000};
    await app.call('exec_command', args);
    const bindingPath = path.join(root, '.state', 'thread-bindings.bin');
    const encrypted = fs.readFileSync(bindingPath);
    for (let i = 0; i < 5; i++) await app.call('list_directory', {path: file});
    await app.call('register_conversation', {path: root, title: 'Renamed for current display'});
    assert(fs.readFileSync(bindingPath).equals(encrypted), 'reads and display titles must not rewrite identity state');
    locker = spawn('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', "$lock=[IO.File]::Open($env:BINDING_LOCK_FILE,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);try{[Console]::Out.WriteLine('ready');[Console]::In.ReadLine() | Out-Null}finally{$lock.Dispose()}"], {windowsHide:true,stdio:['pipe','pipe','pipe'],env:{...process.env,BINDING_LOCK_FILE:bindingPath}});
    const unlocked = new Promise(resolve => locker.once('exit', resolve));
    await new Promise((resolve,reject)=>{let text='';const timer=setTimeout(()=>reject(Error('Binding lock fixture timeout')),5000);locker.stdout.on('data',chunk=>{text+=chunk;if(text.includes('ready')){clearTimeout(timer);resolve();}});locker.once('error',reject);});
    const pendingCall = {name:'exec_command',arguments:{cwd:root,cmd:'printf once >> pending-command.txt',request_id:'pending-binding',yield_time_ms:3000},_meta:{'openai/session':'binding-save-retry'}};
    for (let i=0;i<2;i++){const refused=await app.rpc('tools/call',pendingCall);assert(refused.error||refused.result?.isError,'an unsaved identity must block side effects on every retry');assert(!fs.existsSync(path.join(root,'pending-command.txt')));}
    locker.stdin.end();await unlocked;locker=null;
    const resumed=await app.rpc('tools/call',pendingCall);assert(!resumed.error&&!resumed.result.isError,JSON.stringify(resumed));
    assert.equal(fs.readFileSync(path.join(root,'pending-command.txt'),'utf8'),'once');
    await app.stop();
    const preserved = state(root, 'thread-bindings');
    state(root, 'thread-bindings', [...preserved, ...Array.from({length: 200 - preserved.length}, (_, i) => ({Id: 'unused-' + i, ChatId: '', HostKey: 'stale-' + i}))]);
    app = runtime(root); await app.rpc('initialize');
    const fresh = await app.rpc('tools/call', {name: 'register_conversation', arguments: {path: root}, _meta: {'openai/session': 'new-host'}});
    assert(!fresh.error && !fresh.result.isError, JSON.stringify(fresh));
    assert.notEqual(data(fresh.result).thread_id, owner);
    const saved = state(root, 'thread-bindings'); assert(saved.some(binding => binding.Id === owner)); assert(saved.length < 200);
    assert.equal(data(await app.call('register_conversation', {path: root})).thread_id, owner);
    const retry = await app.rpc('tools/call', {name: 'exec_command', arguments: args, _meta: {'openai/session': 'log-cleanup'}});
    assert(retry.result.isError); assert.match(data(retry.result).message, /REQUEST_RECONCILIATION_REQUIRED/);
    assert.equal(fs.readFileSync(path.join(root, 'retry.txt'), 'utf8'), 'once');
    await app.call('restore_change', {change_id: change.change_id, apply: true}); assert.equal(fs.readFileSync(file, 'utf8'), 'before');
  } finally { if(locker)locker.stdin.end();await app.stop();fs.rmSync(root, {recursive: true, force: true,maxRetries:20,retryDelay:100}); }
});

test('history deletion preserves retry protection and file undo ownership across restarts', {timeout: 60000}, async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'workspace-log-ownership-'));
  const file = path.join(root, 'sample.txt');
  fs.writeFileSync(file, 'before');
  let app = runtime(root);
  try {
    await app.rpc('initialize');
    const owner = data(await app.call('register_conversation', {path: root, title: 'Previous title must disappear'})).thread_id;
    const change = data(await app.call('edit_file', {path: file, old_text: 'before', new_text: 'after'}));
    const args = {cwd: root, shell: 'powershell', cmd: 'Add-Content -LiteralPath count.txt -Value once', request_id: 'cleanup-once', yield_time_ms: 3000};
    await app.call('exec_command', args);
    const protectedFiles = ['requests.bin', 'changes.bin'].map(name => fs.readFileSync(path.join(root, '.state', name)));
    await app.stop();
    app = runtime(root); await app.rpc('initialize');
    assert.equal((await app.snapshot()).conversations.length, 0);
    for (const [index, name] of ['requests.bin', 'changes.bin'].entries()) assert(fs.readFileSync(path.join(root, '.state', name)).equals(protectedFiles[index]));
    assert.equal(data(await app.call('register_conversation', {path: root})).thread_id, owner);
    assert(!JSON.stringify(await app.snapshot()).includes('Previous title must disappear'));
    const retry = await app.rpc('tools/call', {name: 'exec_command', arguments: args, _meta: {'openai/session': 'log-cleanup'}});
    assert(retry.result.isError); assert.match(data(retry.result).message, /REQUEST_RECONCILIATION_REQUIRED/);
    assert.equal(fs.readFileSync(path.join(root, 'count.txt'), 'utf8').trim(), 'once');
    await app.call('restore_change', {change_id: change.change_id, apply: true});
    assert.equal(fs.readFileSync(file, 'utf8'), 'before');
    assertNoHistory(root);
  } finally { await app.stop(); fs.rmSync(root, {recursive: true, force: true}); }
});

test('current call bodies remain visible but never return after a normal exit or crash', {timeout: 60000}, async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'workspace-log-lifetime-'));
  const file = path.join(root, 'sample.txt'), marker = 'synthetic-current-body-must-disappear';
  fs.writeFileSync(file, marker);
  let app = runtime(root);
  try {
    await app.rpc('initialize');
    const read = await app.call('read_file', {path: file});
    const id = read.structuredContent.activity_id;
    const current = data(await app.call('get_workspace_status', {path: root})).workspace;
    assert(current.activity.some(row => row.id === id));
    assert(JSON.stringify(current.activity).includes(marker), 'current logs still show the actual receipt');
    assertNoHistory(root);
    await app.stop(); app = runtime(root); await app.rpc('initialize');
    assert.equal((await app.snapshot()).activity.length, 0);
    await app.call('read_file', {path: file});
    await app.stop(true); app = runtime(root); await app.rpc('initialize');
    const restarted = await app.snapshot();
    assert.equal(restarted.activity.length, 0, 'a forced process exit cannot resurrect logs');
    assert.equal(restarted.conversations.length, 0); assert.equal(restarted.plans.length, 0);
    assert(!JSON.stringify(restarted).includes(marker));
    assertNoHistory(root);
    for (let i = 0; i < 105; i++) await app.call('list_directory', {path: file});
    assert.equal(data(await app.call('get_workspace_status', {path: root})).workspace.activity.length, 100);
    assertNoHistory(root);
  } finally { await app.stop(); fs.rmSync(root, {recursive: true, force: true}); }
});
