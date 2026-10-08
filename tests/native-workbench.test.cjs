const test = require('node:test'), assert = require('node:assert/strict');
const fs = require('node:fs'), path = require('node:path'), os = require('node:os');
const {spawn, execFileSync} = require('node:child_process');
const repo = path.resolve(__dirname, '..');
// msedgewebview2 helper processes keep the profile locked for a few seconds after the
// host exits, so a fixed short retry is flaky under a parallel test run.
async function removeTree(root, budgetMs = 15000) {
  const deadline = Date.now() + budgetMs; let last;
  for (;;) {
    try { fs.rmSync(root, {recursive: true, force: true}); if (!fs.existsSync(root)) return; } catch (error) { last = error; }
    if (Date.now() > deadline) throw last || Error('still present: ' + root);
    await new Promise(resolve => setTimeout(resolve, 250));
  }
}
test('shipped WebView2 assemblies render the real empty dashboard and desktop clearing resets counters', {timeout: 70000}, async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'workspace-native-test-'));
  const exe = process.env.WORKSPACE_TEST_EXE || path.join(repo, 'dist-next/LocalWorkspace.exe');
  const backend = spawn(exe, ['--mcp'], {windowsHide: true, stdio: ['pipe','pipe','pipe'], env: {...process.env, WORKSPACE_STATE_DIR: path.join(root, '.state')}});
  const exited = new Promise(resolve => backend.once('exit', resolve));
  let driver;
  try {
    const url = await new Promise((resolve, reject) => {
      let logs = ''; const timer = setTimeout(() => reject(Error('Native backend did not start')), 10000);
      backend.stderr.on('data', chunk => { logs += chunk; const match = logs.match(/\[Dashboard\] (http:\/\/127\.0\.0\.1:\d+\/)/); if (match) {clearTimeout(timer); resolve(match[1]);} });
      backend.once('error', error => {clearTimeout(timer); reject(error);});
    });
    const compiler = path.join(process.env.WINDIR, 'Microsoft.NET/Framework64/v4.0.30319/csc.exe');
    const driverExe = path.join(root, 'NativeWorkbenchSmoke.exe');
    execFileSync(compiler, ['/nologo','/target:winexe','/platform:x64','/out:' + driverExe,'/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll',
      '/r:' + path.join(repo,'vendor/webview2/Microsoft.Web.WebView2.Core.dll'),'/r:' + path.join(repo,'vendor/webview2/Microsoft.Web.WebView2.WinForms.dll'),path.join(__dirname,'fixtures/NativeWorkbenchSmoke.cs')], {windowsHide: true, encoding:'utf8'});
    const evidence = path.join(root, 'native');
    driver = spawn(driverExe, [exe,url,path.join(root,'webview-profile'),evidence], {windowsHide: true, stdio:'ignore'});
    const code = await new Promise((resolve, reject) => {const timer=setTimeout(()=>reject(Error('Hidden WebView2 test timed out')),30000);driver.once('exit',code=>{clearTimeout(timer);resolve(code);});driver.once('error',reject);});
    assert.equal(code, 0, fs.existsSync(evidence+'.error') ? fs.readFileSync(evidence+'.error','utf8') : 'Native driver failed');
    const report = JSON.parse(fs.readFileSync(evidence+'.json','utf8'));
    assert(report.desktop_log_counters_cleared && report.embedded_dashboard_loaded); assert.equal(report.sdk,'1.0.4258.31'); assert(report.runtime);
    console.log('Native WebView2 evidence: ' + JSON.stringify(report));
    if (process.env.WORKSPACE_CAPTURE_NATIVE === '1') {
      fs.mkdirSync(path.join(repo,'work'),{recursive:true});for(const suffix of ['png','json']) fs.copyFileSync(evidence+'.'+suffix,path.join(repo,'work/native-workbench-2.4.0.'+suffix));
    }
  } finally {
    if(driver && driver.exitCode === null) {try{execFileSync('taskkill.exe',['/PID',String(driver.pid),'/T','/F'],{windowsHide:true,stdio:'ignore'});}catch{}}
    backend.stdin.end(); await exited;
    // WebView2 releases its profile asynchronously after the last control closes.
    await removeTree(root); assert(!fs.existsSync(root));
  }
});
