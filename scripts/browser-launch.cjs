// One place that decides which Chromium-family browser the dashboard checks run in.
// The plugin ships no browser: playwright-core expects a download that a dev machine may
// not have, so try the bundled build first and then the browsers people actually install,
// reporting which one answered instead of dying on a single hardcoded path.
const fs = require('node:fs');
const { chromium } = require('playwright-core');

const LAUNCH_TIMEOUT = Number(process.env.WORKSPACE_BROWSER_TIMEOUT || 25000);
// A first-run profile or a background update check can stall a cold start; none of these
// change what the page renders.
const ARGS = [
  '--no-first-run',
  '--no-default-browser-check',
  '--disable-background-networking',
  '--disable-sync',
  '--disable-extensions',
];

const CANDIDATES = [
  { label: 'bundled chromium', path: null },
  { label: 'Chrome', env: 'PROGRAMFILES', relative: 'Google/Chrome/Application/chrome.exe' },
  { label: 'Chrome (x86)', env: 'PROGRAMFILES(X86)', relative: 'Google/Chrome/Application/chrome.exe' },
  { label: 'Edge', env: 'PROGRAMFILES(X86)', relative: 'Microsoft/Edge/Application/msedge.exe' },
  { label: 'Edge', env: 'PROGRAMFILES', relative: 'Microsoft/Edge/Application/msedge.exe' },
  { label: 'Chrome (unix)', path: '/usr/bin/google-chrome' },
  { label: 'Chromium (unix)', path: '/usr/bin/chromium' },
  { label: 'Chromium snap', path: '/snap/bin/chromium' },
];

const expand = candidate => {
  if (candidate.path !== undefined) return candidate.path;
  const base = process.env[candidate.env];
  return base ? base.replace(/\\/g, '/') + '/' + candidate.relative : null;
};

// Ordered candidates: an explicit WORKSPACE_TEST_BROWSER first, then the bundled build,
// then whatever is installed. Duplicates collapse so a path is only tried once.
function candidates() {
  const list = [];
  if (process.env.WORKSPACE_TEST_BROWSER) list.push({ label: 'WORKSPACE_TEST_BROWSER', path: process.env.WORKSPACE_TEST_BROWSER });
  for (const candidate of CANDIDATES) {
    const path = expand(candidate);
    if (path && list.some(entry => entry.path && entry.path.toLowerCase() === path.toLowerCase())) continue;
    list.push({ label: candidate.label, path });
  }
  return list;
}

// Tries each candidate in turn. Returns { browser, label, path } — label says what ran,
// so a check can report its own basis. Throws only when nothing could start.
async function launchDashboardBrowser(options = {}) {
  const attempts = [];
  for (const candidate of candidates()) {
    if (candidate.path && !fs.existsSync(candidate.path)) { attempts.push(candidate.label + ': not installed'); continue; }
    const started = Date.now();
    try {
      const browser = await chromium.launch({ executablePath: candidate.path || undefined, headless: true, timeout: LAUNCH_TIMEOUT, args: ARGS, ...options });
      return { browser, label: candidate.label, path: candidate.path || chromium.executablePath(), elapsed: Date.now() - started };
    } catch (error) {
      attempts.push(candidate.label + ': ' + String(error.message).split('\n')[0].slice(0, 160));
    }
  }
  throw new Error('没有可用的 Chromium 内核浏览器。已尝试：\n  ' + attempts.join('\n  ')
    + '\n可用 WORKSPACE_TEST_BROWSER 指定可执行文件。');
}

module.exports = { launchDashboardBrowser, candidates, LAUNCH_TIMEOUT };
