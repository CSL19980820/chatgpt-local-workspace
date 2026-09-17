// Modern-era (MCP 2026-07-28) protocol tests: stateless _meta negotiation, server/discover,
// resultType/ttlMs/cacheScope, MRTR confirmations, Tasks extension, OTel trace capture.
// Legacy behaviour is covered by mcp.test.cjs and must not regress.
const { spawn } = require('child_process');
const fs = require('fs'), assert = require('assert'), path = require('path'), os = require('os');
const root = fs.mkdtempSync(path.join(os.tmpdir(), 'local-workspace-modern-'));
const dir = path.join(root, 'sample_space'); fs.mkdirSync(dir);
const exe = process.env.WORKSPACE_TEST_EXE || path.join(__dirname, '../dist-next/LocalWorkspace.exe');
const child = spawn(exe, ['--mcp'], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
let seq = 0, buffer = ''; const pending = new Map();
child.stdout.on('data', d => { buffer += d; let at; while ((at = buffer.indexOf('\n')) >= 0) { const msg = JSON.parse(buffer.slice(0, at)); buffer = buffer.slice(at + 1); const p = pending.get(msg.id); if (p) { pending.delete(msg.id); p(msg); } } });
child.stderr.resume();
const request = (method, params = {}) => new Promise(resolve => { const id = ++seq; pending.set(id, resolve); child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id, method, params }) + '\n'); });
const PV = '2026-07-28';
const meta = (caps, extra) => Object.assign({ 'io.modelcontextprotocol/protocolVersion': PV, 'io.modelcontextprotocol/clientInfo': { name: 'modern-test', version: '1' }, 'io.modelcontextprotocol/clientCapabilities': caps }, extra || {});
const fullCaps = { elicitation: {}, extensions: { 'io.modelcontextprotocol/tasks': {} } };
const modern = (method, params, caps, extraMeta) => request(method, Object.assign({}, params, { _meta: meta(caps === undefined ? fullCaps : caps, extraMeta) }));
const modernCall = async (name, args, caps, extraMeta) => { const r = await modern('tools/call', { name, arguments: args }, caps, extraMeta); assert(!r.error, JSON.stringify(r)); return r.result; };
const sleep = ms => new Promise(r => setTimeout(r, ms));

async function main() {
  // 1. server/discover: the stdio backward-compatibility probe and capability advertisement.
  const discover = (await modern('server/discover', {}, {})).result;
  assert.equal(discover.resultType, 'complete');
  assert(discover.supportedVersions.includes(PV), JSON.stringify(discover.supportedVersions));
  assert(discover.capabilities.tools, 'tools capability missing');
  assert(discover.capabilities.extensions['io.modelcontextprotocol/tasks'] !== undefined, 'tasks extension not advertised');
  assert.equal(discover.capabilities.extensions['io.modelcontextprotocol/tasks'] && 'ok', 'ok');
  assert.equal(discover._meta['io.modelcontextprotocol/serverInfo'].name, 'local-workspace');
  assert(discover.ttlMs > 0 && discover.cacheScope === 'private');
  assert(discover.instructions.includes('register_conversation'));

  // 2. Version negotiation errors.
  const badVersion = await request('server/discover', { _meta: Object.assign(meta({}), { 'io.modelcontextprotocol/protocolVersion': '2099-01-01' }) });
  assert.equal(badVersion.error.code, -32022, JSON.stringify(badVersion));
  assert(badVersion.error.data.supportedVersions.includes(PV));
  const noCaps = await request('tools/list', { _meta: { 'io.modelcontextprotocol/protocolVersion': PV } });
  assert.equal(noCaps.error.code, -32021, JSON.stringify(noCaps));

  // 3. Modern tools/list: resultType + CacheableResult fields + icons; ping removed in modern era.
  const list = (await modern('tools/list', {})).result;
  assert.equal(list.resultType, 'complete');
  assert.equal(list.tools.length, 24);
  assert.equal(list.cacheScope, 'private'); assert(list.ttlMs > 0);
  assert(list.tools.every(t => Array.isArray(t.icons) && t.icons[0].src.startsWith('data:image/svg+xml;base64,') && t.icons[0].mimeType === 'image/svg+xml'), 'icons missing');
  assert.equal(list._meta['io.modelcontextprotocol/serverInfo'].version, '2.0.0');
  const ping = await modern('ping', {});
  assert.equal(ping.error.code, -32601, 'ping must be removed in the modern era');

  // 4. Modern tools/call: resultType, serverInfo _meta, OTel traceparent captured into activity.
  const fixture = path.join(dir, 'fixture.txt');
  const written = await modernCall('write_file', { path: fixture, content: 'v1\n' });
  assert.equal(written.resultType, 'complete'); assert(!written.isError);
  assert.equal(written._meta['io.modelcontextprotocol/serverInfo'].name, 'local-workspace');
  const trace = '00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-01';
  const read = await modernCall('read_file', { path: fixture }, fullCaps, { traceparent: trace });
  assert(!read.isError);
  const activity = await modernCall('read_workspace_activity', { path: dir });
  const traced = activity.structuredContent.result.activity.filter(a => a.trace === trace);
  assert(traced.length >= 1, 'traceparent not recorded on activity: ' + JSON.stringify(activity.structuredContent.result.activity.map(a => a.tool + ':' + a.trace)));

  // 5. MRTR: overwrite of an existing file requires confirmation when elicitation is declared.
  const gate = await modernCall('write_file', { path: fixture, content: 'v2\n', overwrite: true });
  assert.equal(gate.resultType, 'input_required', JSON.stringify(gate));
  assert.equal(gate.inputRequests.confirm.method, 'elicitation/create');
  assert.equal(gate.inputRequests.confirm.params.mode, 'form');
  assert(typeof gate.requestState === 'string' && gate.requestState.includes('.'));
  assert.equal(fs.readFileSync(fixture, 'utf8'), 'v1\n', 'gated call must not write');

  // 5a. Tampered requestState is rejected.
  const tampered = await modernCall('write_file', { path: fixture, content: 'v2\n', overwrite: true }, fullCaps);
  const bad = tampered.requestState.slice(0, -2) + (tampered.requestState.endsWith('A') ? 'BA' : 'AB');
  const tamperedReply = await request('tools/call', { name: 'write_file', arguments: { path: fixture, content: 'v2\n', overwrite: true }, requestState: bad, inputResponses: { confirm: { action: 'accept' } }, _meta: meta(fullCaps) });
  assert(tamperedReply.result.isError);
  assert.equal(tamperedReply.result.structuredContent.result.error_code, 'CONFIRM_STATE_INVALID');

  // 5b. Changed arguments invalidate the fingerprint.
  const mismatch = await request('tools/call', { name: 'write_file', arguments: { path: fixture, content: 'other\n', overwrite: true }, requestState: gate.requestState, inputResponses: { confirm: { action: 'accept' } }, _meta: meta(fullCaps) });
  assert(mismatch.result.isError);
  assert.equal(mismatch.result.structuredContent.result.error_code, 'CONFIRM_STATE_INVALID');

  // 5c. Decline does not write; the state is single-use afterwards.
  const declined = await request('tools/call', { name: 'write_file', arguments: { path: fixture, content: 'v2\n', overwrite: true }, requestState: gate.requestState, inputResponses: { confirm: { action: 'decline' } }, _meta: meta(fullCaps) });
  assert(declined.result.isError);
  assert.equal(declined.result.structuredContent.result.error_code, 'CONFIRM_DECLINED');
  assert.equal(fs.readFileSync(fixture, 'utf8'), 'v1\n');

  // 5d. Fresh request + accept executes the write.
  const gate2 = await modernCall('write_file', { path: fixture, content: 'v2\n', overwrite: true });
  assert.equal(gate2.resultType, 'input_required');
  const accepted = await request('tools/call', { name: 'write_file', arguments: { path: fixture, content: 'v2\n', overwrite: true }, requestState: gate2.requestState, inputResponses: { confirm: { action: 'accept', content: { confirm: 'accept' } } }, _meta: meta(fullCaps) });
  assert(!accepted.result.isError, JSON.stringify(accepted.result));
  assert.equal(fs.readFileSync(fixture, 'utf8'), 'v2\n');

  // 5e. Replay of a consumed state is rejected.
  const replay = await request('tools/call', { name: 'write_file', arguments: { path: fixture, content: 'v2\n', overwrite: true }, requestState: gate2.requestState, inputResponses: { confirm: { action: 'accept' } }, _meta: meta(fullCaps) });
  assert(replay.result.isError);
  assert.equal(replay.result.structuredContent.result.error_code, 'CONFIRM_STATE_INVALID');

  // 5f. Without elicitation capability the gate is not applied (legacy-compatible behaviour).
  const noElicit = await modernCall('write_file', { path: fixture, content: 'v3\n', overwrite: true }, { extensions: {} });
  assert(!noElicit.isError); assert.equal(fs.readFileSync(fixture, 'utf8'), 'v3\n');

  // 6. Tasks extension: a still-running exec_command becomes a task handle.
  const task = await modernCall('exec_command', { shell: 'powershell', cmd: "Start-Sleep -Milliseconds 1500; Write-Output 'task-ok'", cwd: dir, yield_time_ms: 0 });
  assert.equal(task.resultType, 'task', JSON.stringify(task));
  assert.equal(task.status, 'working'); assert(task.taskId); assert.equal(task.pollIntervalMs, 1000);
  const early = (await modern('tasks/get', { taskId: task.taskId })).result;
  assert.equal(early.resultType, 'complete'); assert(['working', 'completed'].includes(early.status));
  let final = early;
  for (let i = 0; i < 30 && final.status === 'working'; i++) { await sleep(300); final = (await modern('tasks/get', { taskId: task.taskId })).result; }
  assert.equal(final.status, 'completed', JSON.stringify(final));
  assert(final.result.structuredContent.result.full_output.includes('task-ok'));
  assert.equal(final.result.isError, false);

  // 6a. tasks/cancel stops the process tree; status becomes cancelled.
  const doomed = await modernCall('exec_command', { shell: 'powershell', cmd: 'Start-Sleep -Seconds 30', cwd: dir, yield_time_ms: 0 });
  assert.equal(doomed.resultType, 'task');
  const ack = (await modern('tasks/cancel', { taskId: doomed.taskId })).result;
  assert.equal(ack.resultType, 'complete');
  let cancelled = null;
  for (let i = 0; i < 30; i++) { await sleep(300); const t = (await modern('tasks/get', { taskId: doomed.taskId })).result; if (t.status !== 'working') { cancelled = t; break; } }
  assert(cancelled && cancelled.status === 'cancelled', JSON.stringify(cancelled));

  // 6b. tasks/update is acknowledged; unknown taskId is an invalid-params error.
  const updateAck = (await modern('tasks/update', { taskId: task.taskId, inputResponses: {} })).result;
  assert.equal(updateAck.resultType, 'complete');
  const unknown = await modern('tasks/get', { taskId: 'no-such-task' });
  assert.equal(unknown.error.code, -32602);

  // 6c. Without the tasks extension the client keeps the classic session result.
  const classic = await modernCall('exec_command', { shell: 'powershell', cmd: 'Start-Sleep -Seconds 5', cwd: dir, yield_time_ms: 0 }, { elicitation: {} });
  assert.equal(classic.resultType, 'complete');
  assert(classic.structuredContent.result.running && classic.structuredContent.result.session_id);
  await modernCall('stop_command', { session_id: classic.structuredContent.result.session_id }, { elicitation: {} });

  // 7. Legacy era is untouched: initialize handshake, ping, no resultType on tools/list.
  const init = await request('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'legacy-test', version: '1' } });
  assert.equal(init.result.protocolVersion, '2025-06-18');
  assert.equal(init.result.serverInfo.name, 'local-workspace');
  assert(init.result.instructions.includes('register_conversation'));
  const legacyPing = await request('ping');
  assert.deepEqual(legacyPing.result, {});
  const legacyList = await request('tools/list');
  assert.equal(legacyList.result.tools.length, 24);
  assert(legacyList.result.resultType === undefined && legacyList.result.ttlMs === undefined, 'legacy tools/list must stay unchanged');
  const legacyStatus = await request('tools/call', { name: 'get_workspace_status', arguments: {} });
  assert.equal(legacyStatus.result.structuredContent.result.version, '2.0.0');
  assert(legacyStatus.result.structuredContent.result.protocol_versions.some(v => v.includes(PV)));
  console.log('PASS modern era: discover, negotiation, resultType/cache fields, icons, trace, MRTR gate, tasks lifecycle, legacy fallback');
}
const watchdog = setTimeout(() => { child.kill(); console.error('Test timed out'); process.exitCode = 2; }, 90000);
main().catch(e => { console.error(e); process.exitCode = 1; }).finally(() => { clearTimeout(watchdog); child.stdin.end(); });
child.on('exit', () => fs.rmSync(root, { recursive: true, force: true }));
