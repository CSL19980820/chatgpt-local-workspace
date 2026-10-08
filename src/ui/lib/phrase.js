// One-line wording for a call, Codex style: a short verb, then the object it acted on.
// "运行 npm run build:ui", "编辑 dashboard.css", "读取 WorkspaceDetail.cs 第 96–117 行".
import { base } from './format.js';
import { titleOf } from './labels.js';
import { rowCounts } from './adapter.js';
import { isRunning } from './rows.js';

const firstLine = value => String(value || '').split('\n')[0];
const OPERATION_VERB = { add: '新建', delete: '删除', move: '移动', replace: '覆盖', edit: '编辑', update: '编辑' };

// Returns { verb, object, mono, note, counts }:
//   verb   short action word (always present)
//   object what it acted on; mono when it is a command, path or pattern
//   note   quiet trailing fact ("4 处", "第 1–8 行")
//   counts { added, removed } for edits when known
export function phraseOf(row) {
  const detail = row.detail || {};
  const running = isRunning(row);
  const failed = row.status === 'failed';
  const kind = detail.kind;

  if (kind === 'command') {
    const command = firstLine(detail.command || (row.session && row.session.command) || detail.summary);
    return { verb: running ? '正在运行' : '运行', object: command, mono: true };
  }
  if (kind === 'write') {
    const files = detail.files || [];
    const rejected = detail.applied === false && !detail.is_preview;
    const first = files[0] || {};
    const name = first.name || base(first.path || row.target);
    const total = Math.max(files.length, detail.count || 0);
    const object = name;
    const note = total > 1 ? '等 ' + total + ' 个文件' : '';
    if (rejected || (failed && detail.is_error)) return { verb: '未写入', object, mono: true, note };
    if (detail.is_preview) return { verb: '预览', object, mono: true, note };
    const ops = [...new Set(files.map(file => file.operation))];
    const verb = ops.length === 1 ? OPERATION_VERB[ops[0]] || '编辑' : '编辑';
    return { verb, object, mono: true, note, counts: rowCounts(row) };
  }
  if (kind === 'read') {
    const read = (detail.reads || [])[0] || {};
    const object = detail.name || read.name || base(detail.path || row.target);
    if (detail.is_error) return { verb: '未能读取', object, mono: true };
    if (detail.binary) return { verb: '读取', object, mono: true, note: '不是文本' };
    return { verb: '读取', object, mono: true, note: read.returned_count ? '第 ' + read.start_line + '–' + read.end_line + ' 行' : '' };
  }
  if (kind === 'search') {
    const object = detail.query ? '“' + detail.query + '”' : detail.pattern || '*';
    return { verb: '搜索', object, note: detail.returned_count != null ? detail.returned_count + ' 处' : '' };
  }
  if (kind === 'list') return { verb: '浏览', object: base(detail.path) || '磁盘根目录', mono: !!detail.path, note: detail.total_entries != null ? detail.total_entries + ' 项' : '' };
  if (kind === 'image') return { verb: '查看图片', object: detail.name || base(detail.path || row.target), mono: true };
  if (kind === 'plan') return { verb: '更新计划', object: (detail.done || 0) + '/' + (detail.total || 0) + ' 步完成' };
  if (kind === 'text') {
    const counts = detail.added || detail.removed ? { added: detail.added || 0, removed: detail.removed || 0 } : null;
    return { verb: detail.text_label || titleOf(row.tool), object: base(detail.path || row.target), counts };
  }
  const verb = titleOf(row.tool);
  let summary = detail.summary || base(row.target);
  // "工作区状态 · v2.2.1" under the verb 工作区状态 reads as "v2.2.1".
  if (summary && summary.startsWith(verb)) summary = summary.slice(verb.length).replace(/^\s*·\s*/, '');
  return { verb, object: summary };
}

// Leading glyph of a row: what kind of action it was, never its state (state lives on the
// right). Writes pick the shape of the operation.
export function iconOf(row) {
  const detail = row.detail || {};
  switch (detail.kind) {
    case 'command': return 'squareTerminal';
    case 'write': {
      const ops = [...new Set((detail.files || []).map(file => file.operation))];
      if (ops.length === 1 && ops[0] === 'add') return 'filePlus';
      if (ops.length === 1 && ops[0] === 'delete') return 'fileX';
      if (ops.length === 1 && ops[0] === 'move') return 'move';
      return 'filePen';
    }
    case 'read': return 'fileText';
    case 'search': return 'search';
    case 'list': return 'folderOpen';
    case 'image': return 'image';
    case 'plan': return 'listChecks';
    case 'text': return 'gitCompare';
    case 'workspace': return 'activity';
    default: return 'info';
  }
}
