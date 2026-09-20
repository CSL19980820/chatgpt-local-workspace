const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs'), os = require('node:os'), path = require('node:path');
const { spawn, execFileSync } = require('node:child_process');
const { launchDashboardBrowser } = require('../scripts/browser-launch.cjs');

test('actual MCP image receipts, workspace details and local path actions', { timeout: 90000 }, async t => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'workspace-media-'));
  const exe = process.env.WORKSPACE_TEST_EXE || path.join(__dirname, '../dist-next/LocalWorkspace.exe');
  const child = spawn(exe, ['--mcp'], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
  const exited = new Promise(resolve => child.once('exit', resolve));
  let seq = 0, buffer = '', base = '', browser, originalWindows = null;
  const pending = new Map();
  child.stderr.on('data', d => { const match = String(d).match(/\[Dashboard\] (http:\/\/127\.0\.0\.1:\d+\/)/); if (match) base = match[1]; });
  child.stdout.on('data', d => {
    buffer += d;
    let index;
    while ((index = buffer.indexOf('\n')) >= 0) {
      const result = JSON.parse(buffer.slice(0, index)); buffer = buffer.slice(index + 1);
      const item = pending.get(result.id);
      if (item) { clearTimeout(item.timer); pending.delete(result.id); item.resolve(result); }
    }
  });
  const rpc = (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++seq;
    pending.set(id, { resolve, timer: setTimeout(() => reject(Error('MCP timeout')), 15000) });
    child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id, method, params }) + '\n');
  });
  const call = async (name, args = {}) => {
    const result = await rpc('tools/call', { name, arguments: args });
    assert(!result.error, JSON.stringify(result)); return result.result;
  };
  try {
    await rpc('initialize');
    assert(base);
    const thread = (await call('register_conversation', { path: root, title: '图片与工作区验收' })).structuredContent.result.thread_id;
    const imagePath = path.join(root, '读取 图片 #1.png');
    const original = fs.readFileSync(path.join(__dirname, '../docs/images/dashboard-patch-review.png'));
    fs.writeFileSync(imagePath, original);
    const receipt = await call('read_image', { path: imagePath, thread_id: thread });
    assert.equal(receipt.isError, false);
    const imageUrl = new URL(receipt.structuredContent.result.preview_url, base);
    fs.writeFileSync(imagePath, 'changed after read');
    const image = await fetch(imageUrl);
    assert.equal(image.headers.get('content-type'), 'image/png');
    assert(Buffer.from(await image.arrayBuffer()).equals(original), 'preview must retain exact bytes read, not current disk contents');
    assert(Buffer.from(receipt.content.find(item => item.type === 'image').data, 'base64').equals(original));
    await call('get_workspace_status', { thread_id: thread });
    const snapshot = await (await fetch(base + 'api/snapshot')).json();
    const detail = snapshot.activity.find(item => item.tool === 'get_workspace_status').detail;
    assert.equal(detail.kind, 'workspace');
    assert.equal(detail.tools.length, 25);
    assert(detail.workspaces.some(item => item.path === root.replace(/\\/g, '/')));
    assert(detail.info.some(item => item.label === '面板地址' && item.value === base));
    assert(!JSON.stringify(snapshot).includes(receipt.content[1].data));
    const token = (await (await fetch(base + 'api/local-actions')).json()).token;
    const open = (target, headers = {}, method = 'POST') => fetch(base + 'api/open?target=' + encodeURIComponent(target), { method, headers });
    const headers = { Origin: base.slice(0, -1), 'X-Workspace-Token': token };
    assert.equal((await open(root)).status, 403);
    assert.equal((await open(root, { ...headers, Origin: 'https://example.com' })).status, 403);
    assert.equal((await open(root, { ...headers, 'X-Workspace-Token': 'wrong' })).status, 403);
    assert.equal((await open(root, headers, 'GET')).status, 405);
    for (const target of ['cmd.exe', 'javascript:alert(1)', 'file:///C:/Windows/notepad.exe', 'C:/missing-workspace-32734', root + '" /select,C:/']) {
      assert.equal((await open(target, headers)).status, 400, target);
    }
    const launched = await launchDashboardBrowser(); browser = launched.browser;
    t.diagnostic('Browser: ' + launched.label);
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const errors = []; page.on('pageerror', error => errors.push(String(error)));
    await page.goto(base + '#thread=' + thread);
    await page.locator('#timeline .event').filter({ hasText: '查看图片' }).click();
    await page.waitForFunction(() => document.querySelector('.image-stage img')?.naturalWidth > 0);
    const imageSize = await page.locator('.image-stage img').evaluate(img => ({ w: img.naturalWidth, h: img.naturalHeight }));
    assert(imageSize.w > 100 && imageSize.h > 100);
    await page.getByRole('button', { name: '原始尺寸', exact: true }).click();
    assert.equal(await page.locator('.image-stage.actual-size').count(), 1);
    await page.getByRole('button', { name: '适应窗口', exact: true }).click();
    for (const selector of ['#refresh', '#pause', '#copy-detail', '#collapse']) {
      const style = await page.locator(selector).evaluate(el => ({ border: getComputedStyle(el).borderStyle, color: getComputedStyle(el).borderColor }));
      assert.equal(style.border, 'solid'); assert.equal(style.color, 'rgba(0, 0, 0, 0)');
    }
    await page.keyboard.press('Tab');
    await page.locator('#refresh').focus();
    assert.equal(await page.locator('#refresh').evaluate(el => getComputedStyle(el).outlineStyle), 'solid');
    await page.locator('#timeline .event').filter({ hasText: '工作区状态' }).click();
    assert.match(await page.locator('#detail-body').innerText(), /25 个/);
    assert.match(await page.locator('#detail-body').innerText(), /图片与工作区验收/);
    assert(await page.locator('#detail-body .path-link').count() >= 3);
    // An actual file gone missing must give useful feedback after a real browser POST.
    await page.locator('#timeline .event').filter({ hasText: '查看图片' }).click();
    fs.unlinkSync(imagePath);
    await page.locator('.image-path .path-link').click();
    await page.getByRole('status').filter({ hasText: '路径已不存在' }).waitFor();
    if (process.env.WORKSPACE_TEST_OPEN === '1') {
      const shell = script => { try { return execFileSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', script], { windowsHide: true, encoding: 'utf8', env: { ...process.env, WORKSPACE_REVEAL_FIXTURE: root }, stdio: ['ignore', 'pipe', 'pipe'] }).trim(); } catch(error) { console.error('Shell diagnostic:', String(error.stdout)); throw error; } };
      const previous = shell('$s=New-Object -ComObject Shell.Application; @($s.Windows() | ForEach-Object { $_.HWND }) -join ","');
      originalWindows = previous;
      await page.locator('#context .path-link').click();
      await page.getByRole('status').filter({ hasText: '已在 Windows 中打开' }).waitFor();
      const revealed = shell(`$s=New-Object -ComObject Shell.Application; $before=@('${previous}'.Split(',')); for($i=0;$i -lt 40;$i++){foreach($w in @($s.Windows())){try{if($w.Document.Folder.Self.Path -eq $env:WORKSPACE_REVEAL_FIXTURE){Write-Output 'EXPLORER_VERIFIED'; exit 0}}catch{}}; Start-Sleep -Milliseconds 200}; exit 1`);
      assert.equal(revealed, 'EXPLORER_VERIFIED');
      fs.writeFileSync(imagePath, original);
      await page.locator('.image-path .path-link').click();
      await page.locator('.image-path [role="status"]').filter({ hasText: '已在 Windows 中打开' }).waitFor();
      const selected = shell(`$s=New-Object -ComObject Shell.Application; $before=@('${previous}'.Split(',')); for($i=0;$i -lt 40;$i++){foreach($w in @($s.Windows())){try{if($w.Document.Folder.Self.Path -eq $env:WORKSPACE_REVEAL_FIXTURE -and $w.Document.SelectedItems().Count -eq 1){Write-Output 'FILE_SELECTED'; if($before -notcontains [string]$w.HWND){$w.Quit()}; exit 0}}catch{}}; Start-Sleep -Milliseconds 200}; foreach($w in @($s.Windows())){try{Write-Output ('folder='+$w.Document.Folder.Self.Path+' selected='+$w.Document.SelectedItems().Count)}catch{}}; exit 1`);
      assert.equal(selected, 'FILE_SELECTED');
      t.diagnostic('Verified Explorer folder through Windows Shell; closed only the newly created test window.');
    }
    for (const scheme of ['light', 'dark']) {
      await page.emulateMedia({ colorScheme: scheme });
      for (const width of [1440, 1000, 640]) {
        await page.setViewportSize({ width, height: 900 });
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        const geometry = await page.locator('.image-stage img').evaluate(img => ({ width: img.getBoundingClientRect().width, parent: img.parentElement.clientWidth }));
        assert(geometry.width <= geometry.parent, 'image must fit panel at ' + width);
      }
    }
    assert.deepEqual(errors, []);
    // Eviction is bounded and explicit. A retained URL must not read arbitrary disk files.
    fs.writeFileSync(imagePath, original);
    for (let i = 0; i < 101; i++) await call('read_image', { path: imagePath, thread_id: thread });
    assert.equal((await fetch(imageUrl)).status, 404);
    assert.equal((await fetch(base + 'api/images/not-a-cached-image')).status, 404);
  } finally {
    if (browser) await browser.close();
    if (originalWindows !== null) execFileSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', `$s=New-Object -ComObject Shell.Application; $before=@('${originalWindows}'.Split(',')); foreach($w in @($s.Windows())){try{if($w.Document.Folder.Self.Path -eq $env:WORKSPACE_REVEAL_FIXTURE -and $before -notcontains [string]$w.HWND){$w.Quit()}}catch{}}`], { windowsHide: true, env: { ...process.env, WORKSPACE_REVEAL_FIXTURE: root } });
    for (const item of pending.values()) clearTimeout(item.timer);
    child.stdin.end(); await exited;
    assert(path.resolve(root).startsWith(path.resolve(os.tmpdir()) + path.sep + 'workspace-media-'));
    fs.rmSync(root, { recursive: true, force: true });
  }
});
