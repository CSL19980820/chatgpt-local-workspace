// Builds the single-file dashboard the plugin embeds:
//   Tailwind CSS v4 CLI -> src/dashboard.css      (theme tokens + layout recipes)
//   esbuild             -> src/ui/main.jsx        (React + shadcn/ui components)
// Both are inlined into one CSP-friendly HTML file, so the shipped page needs no CDN,
// no module loader and no Node at runtime.
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const esbuild = require('esbuild');

const root = path.resolve(__dirname, '..');
const read = file => fs.readFileSync(path.join(root, file), 'utf8');
const kb = text => Math.round(Buffer.byteLength(text) / 1024);

const css = execFileSync(
  process.execPath,
  [path.join(root, 'node_modules/@tailwindcss/cli/dist/index.mjs'), '-i', 'src/dashboard.css', '--minify'],
  { cwd: root, encoding: 'utf8', windowsHide: true, maxBuffer: 8 * 1024 * 1024 }
);

const bundle = esbuild.buildSync({
  entryPoints: [path.join(root, 'src/ui/main.jsx')],
  bundle: true,
  write: false,
  format: 'iife',
  minify: true,
  jsx: 'automatic',
  target: ['chrome110'],
  define: { 'process.env.NODE_ENV': '"production"' },
  legalComments: 'none',
  logLevel: 'warning',
});

// The page keeps one inline <script>: escape any literal that would close it early.
// Replacer functions keep "$" sequences in the bundle from being expanded.
const js = bundle.outputFiles[0].text.replace(/<\/script/gi, '<\\/script');
const html = read('src/dashboard.template.html')
  .replace('<!-- dashboard-css -->', () => '<style>' + css + '</style>')
  .replace('<!-- dashboard-js -->', () => '<script>' + js + '</script>');

fs.writeFileSync(path.join(root, 'src/dashboard.html'), html);
console.log('Dashboard built: ' + kb(html) + ' KB total (CSS ' + kb(css) + ' KB + script ' + kb(js) + ' KB).');
