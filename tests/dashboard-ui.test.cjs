// The dashboard ships as one React bundle, so the honest test renders the built page
// (src/dashboard.html) in a real browser and drives it: start time, running state, the
// typed inspector for every call, the plan card and the filters.
// Uses the installed Chromium-family browser; skips with a clear message when absent.
const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { spawn } = require('node:child_process');
const { launchDashboardBrowser } = require('../scripts/browser-launch.cjs');

const root = path.resolve(__dirname, '..');
const rowCount = 17; // scripts/sample-snapshot.cjs

const delay = ms => new Promise(resolve => setTimeout(resolve, ms));

// The sample preview serves the built page plus sample-snapshot.cjs, so no live instance is touched.
async function startPreview() {
  const child = spawn(process.execPath, [path.join(root, 'scripts', 'dashboard-preview.cjs'), '--sample'], {
    cwd: root, stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true,
  });
  const url = await new Promise((resolve, reject) => {
    let text = '';
    const timer = setTimeout(() => reject(new Error('预览服务没有启动')), 15000);
    child.stdout.on('data', chunk => {
      text += chunk;
      const match = text.match(/http:\/\/127\.0\.0\.1:\d+\//);
      if (match) { clearTimeout(timer); resolve(match[0]); }
    });
    child.on('error', error => { clearTimeout(timer); reject(error); });
  });
  return { url, stop: () => child.kill() };
}

test('dashboard', { timeout: 120000 }, async t => {
  // Browser first: a machine without any Chromium-family build should skip before a
  // preview server is spawned, and the run should say which browser it used.
  let launched;
  try {
    launched = await launchDashboardBrowser();
  } catch (error) {
    t.skip(error.message);
    return;
  }
  t.diagnostic('浏览器：' + launched.label + '（' + launched.path + '，' + launched.elapsed + ' ms）');

  const preview = await startPreview();
  const browser = launched.browser;
  const context = await browser.newContext({ viewport: { width: 1600, height: 900 } });
  const page = await context.newPage();
  const problems = [];
  page.on('console', message => { if (message.type() === 'error') problems.push(message.text()); });
  page.on('pageerror', error => problems.push(String(error)));
  await page.goto(preview.url, { waitUntil: 'load' });
  await page.waitForSelector('#timeline .event');

  const rows = page.locator('#timeline .event');
  const rowWithText = text => page.locator('#timeline .event').filter({ hasText: text });
  const detail = () => page.locator('#detail-body');

  try {
    await t.test('每次调用都给出开始时间，进行中的调用持续计时', async () => {
      assert.equal(await rows.count(), rowCount);
      const clocks = await page.locator('#timeline .event .event-time').allInnerTexts();
      assert(clocks.length === rowCount && clocks.every(text => /^开始\d{2}:\d{2}:\d{2}$/.test(text.replace(/\s+/g, ''))), '每行都要有开始时间：' + clocks.join(' | '));
      assert.match(await rows.first().getAttribute('title'), /开始 今天 \d{2}:\d{2}:\d{2}/);

      // sample-snapshot 里最新的一行是仍在运行的命令会话。
      const live = rows.first();
      assert.equal(await live.locator('.event-icon.running').count(), 1, '进行中的调用要有运行中图标');
      assert.match(await live.locator('.event-sub').innerText(), /^运行中 · /);
      assert.match(await live.locator('.event-elapsed').innerText(), /^\d+(\.\d+)? s$/);
      assert.match(await page.locator('#metric-live').innerText(), /执行中/);
      assert.match(await page.locator('#metric-live').innerText(), /1/);

      // 暂停轮询后，耗时靠本地计时继续累加，不受服务端时间戳影响。
      await page.click('#pause');
      const before = parseFloat(await live.locator('.event-elapsed').innerText());
      await delay(1500);
      const after = parseFloat(await live.locator('.event-elapsed').innerText());
      assert(after > before, '暂停同步后耗时仍要增长：' + before + ' -> ' + after);
      await page.click('#pause');
      assert.match(await page.locator('#connection').innerText(), /实时/);
    });

    await t.test('右侧检查器对每一次调用都给出结构化详情', async () => {
      for (let index = 0; index < rowCount; index += 1) {
        await rows.nth(index).click();
        await delay(60);
        const text = (await detail().innerText()).replace(/\s+/g, ' ').trim();
        const title = await page.locator('#detail-title').innerText();
        assert(!text.includes('没有结构化详情'), title + ' 缺少详情');
        assert(!text.includes('没有可展开的结构化详情'), title + ' 缺少详情');
        assert(text.length > 60, title + ' 详情过短：' + text);
        assert.match(await page.locator('#detail-state').innerText(), /开始 (今天|昨天|\d{2}-\d{2}) \d{2}:\d{2}:\d{2}/);
      }
    });

    await t.test('写入、读取、命令与搜索显示各自的字段', async () => {
      await rowWithText('dashboard.css').click();
      const write = await detail().innerText();
      assert.match(write, /写入方式/);
      assert.match(write, /整体覆盖原有内容/);
      assert.match(write, /位置/);
      assert((await page.locator('#detail-body .diff-row.add').count()) > 0, '写入要给出改动行');

      // 读取展示文件内容本身，不再用位置和行号占位。
      await rowWithText('WorkspaceDetail.cs').click();
      const read = await detail().innerText();
      assert((await page.locator('#detail-body .file-line').count()) >= 20, '读取要展开文本内容');
      assert.match(read, /精确替换/);
      assert.match(read, /读到文件结尾|可继续从第 118 行/);
      assert(!read.includes('起始行'), '读取不再逐项罗列位置与行号');
      assert(!read.includes('位置'), '读取不再展示位置');
      assert(!read.includes('src/WorkspaceDetail.cs'), '读取不再重复完整路径');
      assert.match(await page.locator('#detail-body .card-title').innerText(), /^WorkspaceDetail\.cs$/);
      assert.match(await page.locator('#detail-body .card-title').getAttribute('title'), /src\/WorkspaceDetail\.cs$/);

      // 不是文本的文件不展开内容，但有明确的大小与结论。
      await rowWithText('local-workspace.ico').click();
      const binary = await detail().innerText();
      assert.match(binary, /不是文本/);
      assert.match(binary, /23\.8 KB/);
      assert.equal(await page.locator('#detail-body .file-line').count(), 0, '非文本文件不展开内容');

      await rowWithText('npm run build:ui').click();
      const command = await detail().innerText();
      assert.match(command, /退出码 0/);
      assert.match(command, /Dashboard built/);

      await rowWithText('command-output').click();
      const search = await detail().innerText();
      assert.match(search, /4 处/);
      assert.equal(await page.locator('#detail-body .match').count(), 4);
    });

    await t.test('时间线按调用类型着色，选中的行不再画边缘线条', async () => {
      const shadows = await page.locator('#timeline .event.selected').evaluateAll(nodes => nodes.map(node => getComputedStyle(node).boxShadow));
      assert(shadows.every(value => value === 'none'), '选中的调用不再使用左侧线条：' + shadows.join(' | '));

      // 读 --tone 而不是图标颜色：颜色带 .13s 过渡，刚点过的行会给出插值出来的
      // oklab(...) 中间值，曾经让“至少四种颜色”在同一种灰上照样通过。
      const palette = async () => page.locator('#timeline .event').evaluateAll(nodes => nodes.map(node => ({
        kind: [...node.classList].find(name => name.startsWith('k-')),
        tone: getComputedStyle(node).getPropertyValue('--tone').trim(),
      })));
      const check = async (scheme, entries) => {
        const kinds = [...new Set(entries.map(entry => entry.kind))];
        const tones = [...new Set(entries.map(entry => entry.tone))];
        assert(kinds.length >= 6, scheme + ' 样例要覆盖多种调用类型，实际只有 ' + kinds.length + ' 种：' + kinds.join(' '));
        assert.equal(tones.length, kinds.length,
          scheme + ' ' + kinds.length + ' 类调用必须各自一种主色，实际只有 ' + tones.length + ' 种：' + tones.join(' | ')
          + '（' + entries.map(entry => entry.kind + '=' + entry.tone).join(' ') + '）');
      };
      await check('浅色', await palette());

      // 深色主题有自己的调色板，同样按类型区分。
      await page.emulateMedia({ colorScheme: 'dark' });
      await delay(120);
      await check('深色', await palette());
      await page.emulateMedia({ colorScheme: 'light' });
      await delay(120);

      assert.equal(await page.locator('#timeline .event.live').count(), 1, '只有仍在运行的调用带运行中效果');
      assert.equal(await page.locator('#timeline .event.failed').count(), 1, '失败的调用单独标记');
    });

    await t.test('侧栏用底色标记当前对话，折叠后只留下字形', async () => {
      const active = page.locator('#threads .thread.active');
      assert.equal(await active.count(), 1);
      assert.equal(await active.evaluate(node => getComputedStyle(node).boxShadow), 'none', '当前对话不再使用左侧线条');
      assert.match(await active.innerText(), /全部对话/);
      assert.equal(await page.locator('#threads .thread').first().locator('.thread-meta').innerText(), String(rowCount));

      await page.click('#collapse');
      await delay(250);
      assert.equal(await page.locator('#shell.collapsed').count(), 1);
      assert.equal(await page.locator('#threads .thread b').first().isVisible(), false, '折叠后不显示标题');
      assert.equal(await page.locator('#threads .thread-glyph').first().isVisible(), true, '折叠后仍要保留字形');
      assert.equal(await page.locator('.brand').isVisible(), false, '折叠后收起品牌图标，让折叠按钮居中');
      await page.click('#collapse');
      await delay(250);
      assert.equal(await page.locator('#shell.collapsed').count(), 0);
    });

    await t.test('被拒绝的写入只列出路径，不谎报替换', async () => {
      await rowWithText('overwrite=true').click();
      const body = await detail().innerText();
      assert.match(body, /已经存在/);
      assert.match(body, /未写入/);
      assert.match(body, /这次调用没有写入这个文件/);
      assert(!body.includes('写入方式'), '失败的写入不能声称写入方式');
      assert(!body.includes('写入大小'), '失败的写入不能给出写入大小');
      assert.equal(await page.locator('#detail-body .diff-row').count(), 0, '失败的写入不能给出改动');
    });

    await t.test('执行计划在时间线上方折叠展示进度', async () => {
      assert.equal(await page.locator('#plan-count').innerText(), '1 / 5');
      assert.equal(await page.locator('#plan-body').isVisible(), false, '默认折叠');
      const plan = await page.locator('#plan-card').boundingBox();
      const timeline = await page.locator('#timeline').boundingBox();
      assert(plan.y < timeline.y, '计划要位于时间线上方');
      await page.click('#plan-toggle');
      assert.equal(await page.locator('#plan-body').isVisible(), true);
      assert.equal(await page.locator('#plan-steps .plan-step').count(), 5);
      assert.equal(await page.locator('#plan-steps .plan-step.in_progress').count(), 1);
      assert.match(await page.locator('#plan-current-text').innerText(), /类型化详情/);
      assert.equal(await page.locator('#plan-bar i.in_progress').count(), 1);
    });

    await t.test('搜索与状态筛选只留下匹配的调用', async () => {
      await page.fill('#search', 'missing-fixture');
      await delay(120);
      assert.equal(await rows.count(), 1);
      assert.match(await rows.first().locator('.event-title').innerText(), /读取文件/);
      // 补丁行要能按它改过的文件名搜到，而不是只能搜工作目录根。
      await page.fill('#search', 'sample-snapshot.cjs');
      await delay(120);
      assert.equal(await rows.count(), 1);
      assert.match(await rows.first().locator('.event-title').innerText(), /应用补丁/);
      await page.fill('#search', '');
      await delay(120);
      assert.equal(await rows.count(), rowCount);

      await page.click('#state');
      await page.getByRole('option', { name: '进行中' }).click();
      await delay(120);
      assert.equal(await rows.count(), 1, '进行中只保留仍在运行的调用');
      assert.match(await rows.first().getAttribute('aria-label'), /^执行命令 · 进行中/);
      await page.click('#state');
      await page.getByRole('option', { name: '全部状态' }).click();
      await delay(120);
      assert.equal(await rows.count(), rowCount);
    });

    await t.test('窄屏不横向溢出，页面没有脚本错误', async () => {
      for (const width of [1920, 1366, 640]) {
        await page.setViewportSize({ width, height: 900 });
        await delay(150);
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
        assert(overflow <= 0, width + 'px 出现横向滚动 ' + overflow + 'px');
      }
      assert.deepEqual(problems, []);
    });
  } finally {
    await browser.close();
    preview.stop();
  }
});
