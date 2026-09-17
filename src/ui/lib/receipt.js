// Older plugin builds only returned a raw receipt (`preview`) instead of the shaped
// `detail` payload. This adapter rebuilds the same typed detail shape on the page, so the
// inspector and the live "running" link keep working while an old server is still running.
// It mirrors src/WorkspaceDetail.cs and never carries file content.
import { base } from './format.js';

const text = value => (value == null ? '' : String(value));
const num = (value, fallback = 0) => { const n = Number(value); return isFinite(n) ? n : fallback; };
const flag = value => value === true;
const list = value => (Array.isArray(value) ? value : []);
const display = path => text(path).replace(/\\/g, '/');
const clip = (value, limit) => { const s = text(value); return s.length <= limit ? s : s.slice(0, limit); };
const tail = (value, limit) => { const s = text(value); return s.length <= limit ? s : s.slice(s.length - limit); };
const counts = diff => {
  if (!diff) return '';
  const added = num(diff.added), removed = num(diff.removed);
  if (!added && !removed) return flag(diff.identical) ? '内容未变化' : '';
  return '+' + added + ' −' + removed;
};
const shapeDiff = diff => {
  if (!diff) return null;
  return {
    rows: list(diff.rows).slice(0, 200).map(row => ({
      kind: text(row.kind),
      old_line: row.old_line ?? null,
      new_line: row.new_line ?? null,
      text: clip(row.text, 240),
      truncated: flag(row.truncated) || text(row.text).length > 240,
    })),
    added: num(diff.added), removed: num(diff.removed),
    identical: flag(diff.identical), coarse: flag(diff.coarse),
    truncated: flag(diff.truncated), omitted_rows: 0,
  };
};
const fileEntry = (path, operation, diff, size = 0, previous = null) => {
  const shown = display(path);
  return {
    path: shown, name: base(shown), operation, size_bytes: size,
    previous_path: previous ? display(previous) : null,
    diff: shapeDiff(diff),
  };
};
const infoRows = (rows, summary) => ({ kind: 'info', session_id: null, info: rows, summary });
const row = (label, value, mono = false) => ({ label, value, mono });

const writeFile = inner => {
  const path = display(inner.requested_path || inner.path);
  const created = flag(inner.created);
  const operation = created ? 'add' : 'replace';
  const diff = inner.diff;
  const summary = (base(path) || path) + ' · ' + (created ? '新建文件' : '替换文件') + (counts(diff) ? ' · ' + counts(diff) : '');
  return { kind: 'write', session_id: null, files: [fileEntry(path, operation, diff)], count: 1, label: created ? '新建文件' : '替换文件', summary };
};

const editFile = inner => {
  const path = display(inner.requested_path || inner.path);
  const summary = (base(path) || path) + ' · 精确替换' + (counts(inner.diff) ? ' · ' + counts(inner.diff) : '');
  return { kind: 'write', session_id: null, files: [fileEntry(path, 'edit', inner.diff)], count: 1, summary };
};

const patch = inner => {
  const files = [];
  let added = 0, removed = 0, omitted = 0;
  for (const item of list(inner.files)) {
    if (files.length >= 6) { omitted++; continue; }
    added += num(item.diff && item.diff.added);
    removed += num(item.diff && item.diff.removed);
    files.push(fileEntry(item.path, text(item.operation), item.diff, 0, item.previous_path));
  }
  const count = num(inner.count, files.length);
  const detail = {
    kind: 'write', session_id: null, files, count, omitted_files: omitted, added, removed,
    error: clip(inner.error, 400), partial: flag(inner.partial), rollback: text(inner.rollback),
    root: display(inner.path),
  };
  detail.summary = '补丁 · ' + count + ' 个文件 · +' + added + ' −' + removed;
  return detail;
};

const review = inner => {
  const files = [];
  let omitted = 0;
  for (const item of list(inner.files)) {
    if (files.length >= 6) { omitted++; continue; }
    files.push(fileEntry(item.path, 'update', item.diff));
  }
  const count = num(inner.count, files.length);
  const detail = { kind: 'write', session_id: null, files, count, omitted_files: omitted + num(inner.omitted_files), scope: text(inner.scope), untracked_changes: num(inner.untracked_changes), root: display(inner.path) };
  detail.summary = count + ' 个文件有工具改动';
  return detail;
};

const readFile = inner => {
  const path = display(inner.requested_path || inner.path);
  const start = num(inner.start_line, 1), returned = num(inner.returned_count);
  const end = returned > 0 ? start + returned - 1 : start;
  const notes = [];
  if (flag(inner.path_corrected)) notes.push('请求路径与实际路径不同，已按实际文件读取。');
  return {
    kind: 'read', session_id: null, path, start_line: start, returned_count: returned, end_line: end,
    next_line: inner.next_line ?? null, notes,
    reads: [{ path, name: base(path), start_line: start, returned_count: returned, end_line: end, empty: returned === 0, next_line: inner.next_line ?? null }],
    summary: base(path) + (returned > 0 ? ' · 第 ' + start + '–' + end + ' 行' : ' · 未读到内容'),
  };
};

const search = (tool, inner) => {
  const query = text(inner.query), pattern = text(inner.pattern), content = tool === 'search_text';
  const matches = list(inner.matches).slice(0, 30).map(item => {
    const path = display(item.path);
    return { path, name: base(path), line: item.line ?? null, column: item.column ?? null, text: clip(item.text, 300) };
  });
  const returned = num(inner.returned_count, matches.length);
  const complete = inner.complete === undefined ? true : flag(inner.complete);
  const detail = {
    kind: 'search', session_id: null, root: display(inner.path), query: content ? query : null, pattern,
    matches, returned_count: returned, omitted: Math.max(0, returned - matches.length), complete,
    truncated: flag(inner.truncated), skipped_paths: num(inner.skipped_paths), next_offset: inner.next_offset ?? null,
    scanned_files: num(inner.scanned_files), note: clip(inner.note, 400),
  };
  detail.summary = content
    ? '"' + clip(query, 40) + '" · ' + returned + ' 处' + (complete ? '' : ' · 部分结果')
    : clip(pattern, 40) + ' · ' + returned + ' 个文件' + (complete ? '' : ' · 部分结果');
  return detail;
};

const command = (tool, inner) => {
  const name = text(inner.command);
  const first = name ? name.split('\n')[0] : '';
  const detail = {
    kind: 'command', session_id: text(inner.session_id), command: name,
    shell: text(inner.shell), shell_executable: text(inner.shell_executable), cwd: display(inner.cwd),
    output_tail: tail(inner.output, 4000), output_chars: text(inner.full_output).length,
    truncated: flag(inner.truncated), running: flag(inner.running), exit_code: inner.exit_code ?? null,
    timed_out: flag(inner.timed_out), stopped: flag(inner.stopped), elapsed_seconds: inner.elapsed_seconds ?? null,
    output_mode: text(inner.output_mode), input: null, sent_chars: 0,
  };
  if (tool === 'exec_command') detail.summary = clip(first, 80);
  else if (tool === 'stop_command') detail.summary = '会话 ' + text(inner.session_id).slice(-6) + ' · 停止命令';
  else if (tool === 'write_stdin') detail.summary = '会话 ' + text(inner.session_id).slice(-6) + ' · 续读输出';
  else detail.summary = '会话 ' + text(inner.session_id).slice(-6) + ' · 读取输出';
  return detail;
};

const listing = inner => {
  const entries = list(inner.entries).slice(0, 40).map(item => ({ name: text(item.name), path: display(item.path), directory: flag(item.directory) }));
  const path = display(inner.path);
  const total = num(inner.total_entries, entries.length);
  const label = path.length === 0 ? '磁盘根目录' : (base(path) || path);
  return {
    kind: 'list', session_id: null, path, entries, total_entries: total,
    returned_count: num(inner.returned_count, entries.length), omitted: Math.max(0, total - entries.length),
    next_offset: inner.next_offset ?? null, empty: flag(inner.empty), summary: label + ' · ' + total + ' 项',
  };
};

const git = (tool, inner) => {
  const output = text(inner.output);
  let added = 0, removed = 0;
  for (const line of output.replace(/\r\n/g, '\n').split('\n')) {
    if (line.startsWith('+') && !line.startsWith('+++')) added++;
    else if (line.startsWith('-') && !line.startsWith('---')) removed++;
  }
  const label = tool === 'git_diff' ? 'Git 差异' : 'Git 状态';
  const exit = inner.exit_code ?? null;
  const detail = {
    kind: 'text', session_id: null, text_label: label, path: display(inner.path), body: clip(output, 20000),
    exit_code: exit, truncated: flag(inner.truncated) || output.length > 20000, timed_out: flag(inner.timed_out),
    added: tool === 'git_diff' ? added : 0, removed: tool === 'git_diff' ? removed : 0,
  };
  if (exit != null && num(exit) !== 0) detail.summary = label + ' · 失败（退出码 ' + num(exit) + '）';
  else if (tool === 'git_diff') detail.summary = added === 0 && removed === 0 ? label + ' · 无改动' : label + ' · +' + added + ' −' + removed;
  else detail.summary = label + ' · ' + output.replace(/\r\n/g, '\n').split('\n').filter(line => line.trim()).length + ' 行';
  return detail;
};

const plan = inner => {
  const steps = [];
  let done = 0;
  for (const item of list(inner.plan)) {
    if (text(item.status) === 'completed') done++;
    steps.push({ step: clip(item.step, 240), status: text(item.status) });
  }
  return {
    kind: 'plan', session_id: null, steps, done, total: steps.length,
    explanation: clip(inner.explanation, 400), path: display(inner.path), updated_at: text(inner.updated_at),
    summary: '执行计划 · ' + done + '/' + steps.length,
  };
};

const info = (tool, inner) => {
  if (tool === 'file_info') {
    const path = display(inner.path), directory = flag(inner.directory);
    const rows = [row('路径', path, true), row('类型', directory ? '目录' : '文件')];
    if (!directory) rows.push(row('大小', text(inner.size_bytes) + ' B'));
    rows.push(row('修改时间', text(inner.last_modified_utc)));
    rows.push(row('创建时间', text(inner.created_utc)));
    rows.push(row('属性', text(inner.attributes)));
    return infoRows(rows, base(path) + (directory ? '/' : ''));
  }
  if (tool === 'create_directory') {
    const path = display(inner.path);
    return infoRows([row('路径', path, true), row('结果', flag(inner.created) ? '已新建目录' : '目录已存在')], base(path) + '/');
  }
  if (tool === 'read_image') {
    const path = display(inner.path);
    return infoRows([row('路径', path, true), row('类型', text(inner.mime_type)), row('大小', text(inner.size_bytes) + ' B')], base(path));
  }
  if (tool === 'open_workspace') {
    const gitRoot = text(inner.git_root);
    const guidance = list(inner.instructions).map(item => display(item.path));
    const shells = list(inner.shells).filter(item => flag(item.available)).map(item => text(item.name));
    const planSteps = inner.plan ? list(inner.plan.plan).length : 0;
    const rows = [row('工作目录', display(inner.path), true), row('Git 根', gitRoot ? display(gitRoot) : '无', !!gitRoot)];
    rows.push(row('约定文件', guidance.length ? guidance.join('\n') : '未发现 AGENTS.md', true));
    if (shells.length) rows.push(row('可用 Shell', shells.join('、')));
    rows.push(row('本地技能', list(inner.skills).length + ' 个入口'));
    rows.push(row('默认 Shell', text(inner.default_shell)));
    if (inner.plan) rows.push(row('当前计划', planSteps + ' 步'));
    rows.push(row('CodeGraph', flag(inner.codegraph_present) ? '存在' : '无'));
    return infoRows(rows, '工作区约定 · ' + guidance.length + ' 份指导文件');
  }
  if (tool === 'register_conversation') {
    const chat = text(inner.chat_id);
    return infoRows([
      row('对话', text(inner.title)), row('工作目录', display(inner.path), true), row('线程', text(inner.thread_id), true),
      row('ChatGPT 对话', chat || '未绑定（本地线程）', !!chat),
    ], clip(inner.title, 60));
  }
  if (tool === 'get_workspace_status') {
    const rows = [row('版本', text(inner.version)), row('实例', text(inner.instance_id), true), row('程序', display(inner.executable), true)];
    rows.push(row('工具数', text(inner.tool_count)));
    rows.push(row('运行中命令', text(inner.running_commands)));
    rows.push(row('面板地址', display(inner.dashboard_url), true));
    return infoRows(rows, '工作区状态 · v' + text(inner.version));
  }
  return null;
};

export function detailFromReceipt(tool, receiptText) {
  if (!receiptText) return null;
  let receipt;
  try { receipt = JSON.parse(receiptText); } catch { return null; }
  const inner = receipt && receipt.result;
  if (!inner || typeof inner !== 'object') return null;
  let detail = null;
  if (tool === 'write_file') detail = writeFile(inner);
  else if (tool === 'edit_file') detail = editFile(inner);
  else if (tool === 'apply_patch') detail = patch(inner);
  else if (tool === 'show_changes') detail = review(inner);
  else if (tool === 'read_file') detail = readFile(inner);
  else if (tool === 'search_text' || tool === 'search_files') detail = search(tool, inner);
  else if (tool === 'exec_command' || tool === 'poll_command' || tool === 'read_command' || tool === 'stop_command' || tool === 'write_stdin') detail = command(tool, inner);
  else if (tool === 'list_directory') detail = listing(inner);
  else if (tool === 'git_status' || tool === 'git_diff') detail = git(tool, inner);
  else if (tool === 'update_plan') detail = plan(inner);
  else detail = info(tool, inner);
  if (!detail) detail = { kind: 'none', session_id: null };
  let target = text(inner.path) || text(inner.requested_path) || text(inner.cwd);
  detail.tool = tool;
  detail.target = display(target);
  if (flag(receipt.isError)) {
    detail.is_error = true;
    const message = text(inner.message) || text(inner.error);
    detail.error = clip(message, 400);
    if (message) detail.summary = clip(message, 80);
  }
  return detail;
}
