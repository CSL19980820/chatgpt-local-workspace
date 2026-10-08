const test = require('node:test'), assert = require('node:assert/strict');
const fs = require('node:fs'), path = require('node:path'), os = require('node:os');
const {execFileSync} = require('node:child_process');
const repo = path.resolve(__dirname, '..');
// Real tunnel-client 0.0.16 `--log.format json --log.level info` startup lines (one per distinct msg,
// user paths and instance ids scrubbed) followed by synthetic failure / guard lines.
const sample = path.join(__dirname, 'fixtures/tunnel-0.0.16-startup-sample.jsonl');
const observedFx = ['provided','supplied','run','initialized custom fxevent.Logger','invoking',
  'OnStart hook executing','OnStart hook executed','OnStop hook executing','OnStop hook executed'];
const mustKeep = ['mcp channel route resolved','tunnel-client startup summary','starting control-plane poller','stopping control-plane poller',
  'poller started','poller stopped','Skipping MCP probe for transport','stdio MCP command started','stdio MCP command exited','🟢 tunnel-client started'];
test('raw log folds only INFO fx startup events from tunnel-client 0.0.16 into one desktop row per burst and keeps WARN/ERROR and useful INFO', {timeout: 60000}, () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'workspace-log-fold-'));
  try {
    const exe = process.env.WORKSPACE_TEST_EXE || path.join(repo, 'dist-next/LocalWorkspace.exe');
    const compiler = path.join(process.env.WINDIR, 'Microsoft.NET/Framework64/v4.0.30319/csc.exe');
    const probe = path.join(root, 'LogFoldProbe.exe'), out = path.join(root, 'result.json');
    execFileSync(compiler, ['/nologo','/target:exe','/platform:x64','/out:' + probe,'/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll',path.join(__dirname,'fixtures/LogFoldProbe.cs')], {windowsHide: true, encoding: 'utf8'});
    try { execFileSync(probe, [exe, sample, out], {windowsHide: true}); }
    catch (error) { assert.fail(fs.existsSync(out + '.error') ? fs.readFileSync(out + '.error', 'utf8') : String(error)); }
    const result = JSON.parse(fs.readFileSync(out, 'utf8'));
    const lines = fs.readFileSync(sample, 'utf8').split('\n').filter(Boolean);
    assert.equal(result.verdicts.length, lines.length);
    const seen = new Set();
    lines.forEach((line, i) => {
      let level = null, msg = null;
      if (line.startsWith('{')) { const obj = JSON.parse(line); level = obj.level; msg = obj.msg; }
      const expected = level === 'INFO' && observedFx.includes(msg) && !line.includes('[Workspace]');
      assert.equal(result.verdicts[i], expected, `line ${i + 1}: ${level} ${msg || line}`);
      if (result.verdicts[i]) seen.add(msg);
      if (level === 'WARN' || level === 'ERROR') assert.equal(result.verdicts[i], false, 'WARN/ERROR must never fold: ' + msg);
      if (mustKeep.includes(msg) || /HEALTH URL|WEB UI/.test(msg || '')) assert.equal(result.verdicts[i], false, 'useful INFO must stay: ' + msg);
    });
    assert.deepEqual([...seen].sort(), [...observedFx].sort(), 'every fx event observed in 0.0.16 output is folded');
    // Exact list: observed events plus the same-family fx success events; no failures, no fuzzy matches.
    assert.deepEqual([...result.events].sort(), [...observedFx, 'replaced', 'decorated', 'started'].sort());
    assert.equal(result.nullLine, false);
    assert.equal(result.sameBurst3s, true); assert.equal(result.sameBurst11s, false); assert.equal(result.sameBurstBackwards, false);
    assert.match(result.summary, /^已折叠 247 条 Tunnel 启动内部日志/);
    // Desktop raw log (preview MainForm, real FlushLogs): every kept line stays, the folded ones become one row.
    const fed = lines.map((line, i) => ({line, noise: result.verdicts[i]})).filter(row => !row.line.startsWith('[Dashboard] '));
    const kept = fed.filter(row => !row.noise).map(row => row.line), folded = fed.length - kept.length;
    const text = rows => rows.map(row => row.replace(/^\d\d:\d\d:\d\d /, ''));
    const once = text(result.desktopRows), summaryRows = rows => rows.filter(row => row.startsWith('已折叠 '));
    assert.equal(once.length, kept.length + 1);
    assert.deepEqual(summaryRows(once), [`已折叠 ${folded} 条 Tunnel 启动内部日志（fx 依赖注入 INFO；WARN/ERROR 不折叠，完整日志见 Tunnel 状态页 /ui）`]);
    assert.equal(once.indexOf(summaryRows(once)[0]), fed.findIndex(row => row.noise), 'summary sits where the first folded line was');
    for (const line of kept) assert(once.includes(line), 'kept line missing from desktop raw log: ' + line.slice(0, 80));
    // A second burst within 10 seconds keeps updating the same row; clearing the log starts a new one.
    const twice = text(result.desktopRowsTwice);
    assert.equal(twice.length, kept.length * 2 + 1); assert.equal(summaryRows(twice).length, 1); assert.match(summaryRows(twice)[0], new RegExp('^已折叠 ' + folded * 2 + ' 条'));
    assert.deepEqual(text(result.desktopRowsAfterClear), once);
  } finally { fs.rmSync(root, {recursive: true, force: true}); }
});
