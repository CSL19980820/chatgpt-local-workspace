// The one place that knows the additive 2.4.x backend fields (contract:
// lwp-backend-api-contract.md, 秦川 2026-10-08). Every field is optional: when the server
// sends it the UI uses it, otherwise the value is derived from the calls already loaded
// (or left unknown and simply not shown), so older servers keep working.
// If a name ever changes, change it here and nowhere else.
export const FIELDS = {
  // GET /api/snapshot conversations[i].stats = { calls, running, failed, added, removed, last_at }
  // (the same keys are also accepted flat on the conversation itself).
  conversationStats: 'stats',
  calls: 'calls',
  running: 'running',
  failed: 'failed',
  added: 'added',
  removed: 'removed',
  lastAt: 'last_at',
  // activity[i].turn_id: one model turn (Codex only); turn_source: "codex" | null.
  // activity[i].trace_id is diagnostic only and deliberately not used for grouping.
  turn: 'turn_id',
  turnSource: 'turn_source',
  // conversations[i]: { thread_id, title, path, chat_id, chat_url, association, created_at }
  association: 'association',
  // conversations[i].source: "codex" | "chatgpt" | "manual" (contract §7.1, from the host binding).
  source: 'source',
  chatUrl: 'chat_url',
  chatId: 'chat_id',
  createdAt: 'created_at',
  // Write calls: detail.files[i].added/removed per file, detail.added/removed for the whole
  // call (covers omitted_files too).
  fileAdded: 'added',
  fileRemoved: 'removed',
  callAdded: 'added',
  callRemoved: 'removed',
  // GET /api/diagnostics versions[i]: { id, label, running, installed, restart_required, status, detail }
  versionId: 'id',
  versionStatus: 'status',
  // POST /api/clear-logs: { cleared_activity, cleared_commands, scope, scope_code, cleared_at, kept }
  clearedActivity: 'cleared_activity',
  clearedCommands: 'cleared_commands',
  clearScope: 'scope',
};

// Write-kind tools that only show changes and never write.
const VIEW_ONLY_TOOLS = new Set(['show_changes']);

// Calls further apart than this start a new turn group when no turn id is sent.
export const TURN_GAP_MS = 30000;

const num = value => (typeof value === 'number' && isFinite(value) ? value : undefined);
const time = value => {
  const ms = typeof value === 'number' ? value : Date.parse(value);
  return isFinite(ms) ? ms : undefined;
};

// Line counts of one file in an edit call: explicit counts first, then the diff summary.
export function fileCounts(file) {
  const diff = (file && file.diff) || {};
  return {
    added: num(file && file[FIELDS.fileAdded]) ?? num(diff.added),
    removed: num(file && file[FIELDS.fileRemoved]) ?? num(diff.removed),
  };
}

// Files a call actually changed. Rejected writes, previews and change views changed
// nothing; a partially applied patch lists only the files that took effect.
const wrote = row => {
  const detail = row && row.detail;
  if (!detail || detail.kind !== 'write' || VIEW_ONLY_TOOLS.has(row.tool)) return false;
  if (detail.is_preview || detail.applied === false) return false;
  return row.status !== 'failed' || !!detail.partial;
};
export function fileChanges(row) {
  const detail = row && row.detail;
  if (!wrote(row)) return [];
  // A partial patch lists only the files that were written (contract §7.2); older builds
  // listed the rest with an empty `operation`, which is still skipped here.
  const files = (detail.files || []).filter(file => !(row.status === 'failed' && detail.partial && !file.operation));
  return files.map(file => ({
    path: file.path || '',
    name: file.name || String(file.path || '').split(/[\\/]/).pop(),
    operation: file.operation || 'update',
    ...fileCounts(file),
  }));
}

// Sum of known counts; a side stays undefined when no file reported it.
export function sumCounts(items) {
  let added, removed;
  for (const item of items) {
    if (item.added !== undefined) added = (added || 0) + item.added;
    if (item.removed !== undefined) removed = (removed || 0) + item.removed;
  }
  return { added, removed };
}

// Line counts of a whole call: the call-level total when the server sends it (it also
// covers files omitted from the list), otherwise the sum of the listed files.
export function rowCounts(row) {
  if (!wrote(row)) return { added: undefined, removed: undefined };
  const detail = row.detail;
  const added = num(detail[FIELDS.callAdded]), removed = num(detail[FIELDS.callRemoved]);
  if (added !== undefined || removed !== undefined) return { added, removed };
  return sumCounts(fileChanges(row));
}

// Every changed file across rows, merged by path in order of last change.
export function changedFiles(rows) {
  const map = new Map();
  for (const row of rows) {
    for (const file of fileChanges(row)) {
      const key = file.path.toLowerCase();
      const known = map.get(key);
      const merged = known ? { ...file, ...sumCounts([known, file]), rowId: row.id } : { ...file, rowId: row.id };
      map.delete(key);
      map.set(key, merged);
    }
  }
  return [...map.values()];
}

const statsFromServer = conversation => {
  if (!conversation) return null;
  const source = conversation[FIELDS.conversationStats] || conversation;
  const value = {
    calls: num(source[FIELDS.calls]),
    running: num(source[FIELDS.running]),
    failed: num(source[FIELDS.failed]),
    added: num(source[FIELDS.added]),
    removed: num(source[FIELDS.removed]),
    lastAt: time(source[FIELDS.lastAt]),
  };
  return Object.values(value).some(item => item !== undefined) ? value : null;
};

// Per-conversation totals for the sidebar. Server values win field by field; anything the
// server does not send is derived from the loaded calls of that conversation.
export function conversationStats(conversations, rows, isRunning) {
  const derived = new Map();
  const entry = id => derived.get(id) || (derived.set(id, { calls: 0, running: 0, failed: 0, changes: [], lastAt: undefined }), derived.get(id));
  // last_at: end of the latest call, or its start while it still runs.
  const all = entry('');
  for (const row of rows) {
    const own = entry(row.thread_id || 'unassigned');
    const end = isRunning(row) ? row.start : row.start + (row.elapsed_ms || 0);
    const counts = rowCounts(row);
    for (const target of [own, all]) {
      target.calls += 1;
      if (isRunning(row)) target.running += 1;
      if (row.status === 'failed') target.failed += 1;
      if (counts.added !== undefined || counts.removed !== undefined) target.changes.push(counts);
      if (row.start && (target.lastAt === undefined || end > target.lastAt)) target.lastAt = end;
    }
  }
  const result = {};
  for (const [id, value] of derived) result[id] = { calls: value.calls, running: value.running, failed: value.failed, lastAt: value.lastAt, ...sumCounts(value.changes), derived: true };
  for (const conversation of conversations || []) {
    const server = statsFromServer(conversation);
    if (!server) continue;
    const base = result[conversation.thread_id] || {};
    const merged = { ...base, derived: false };
    for (const key of Object.keys(server)) if (server[key] !== undefined) merged[key] = server[key];
    result[conversation.thread_id] = merged;
  }
  return result;
}

export const turnOf = row => {
  const value = row && row.raw && row.raw[FIELDS.turn];
  return typeof value === 'string' && value ? value : null;
};

// Groups chronologically sorted rows into turns. Two neighbouring calls that both carry a
// turn id (Codex) belong together exactly when the ids match; anywhere an id is missing
// (ChatGPT, older Codex, synthetic command rows) a gap longer than TURN_GAP_MS starts a new
// turn instead.
export function groupTurns(rows, gap = TURN_GAP_MS) {
  const groups = [];
  let current = null, lastEnd = -Infinity, lastTurn = null;
  for (const row of rows) {
    const end = row.start + (row.elapsed_ms || 0);
    const turn = turnOf(row);
    const split = !current || (turn && lastTurn ? turn !== lastTurn : row.start - lastEnd > gap);
    lastTurn = turn;
    if (split) {
      current = { key: (turn || 'gap') + ':' + row.id, turn, rows: [], start: row.start, end };
      groups.push(current);
    }
    current.rows.push(row);
    current.end = Math.max(current.end, end);
    lastEnd = Math.max(lastEnd, end);
  }
  return groups;
}

// Diagnostics: one component row. Server status wins; older servers get it derived.
export function versionView(component) {
  const id = component[FIELDS.versionId] || component.label;
  const status = component[FIELDS.versionStatus]
    || (component.restart_required ? 'restart_required' : !component.installed ? 'missing' : !component.running ? 'unknown_running' : 'ok');
  return { key: id, label: component.label, running: component.running || null, installed: component.installed || null, status, detail: component.detail || '' };
}

// Clear-log result as one sentence for the notice: the server's scope sentence when sent
// (it always ends with "。", contract §7.2; the count goes before that full stop).
export function clearSummary(result) {
  const cleared = (num(result[FIELDS.clearedActivity]) || 0) + (num(result[FIELDS.clearedCommands]) || 0);
  if (!cleared) return '没有可清空的已完成记录。';
  const scope = typeof result[FIELDS.clearScope] === 'string' && result[FIELDS.clearScope]
    ? result[FIELDS.clearScope].replace(/。$/, '')
    : '已清空已完成的调用与命令日志；运行中的命令、计划和对话都已保留';
  return scope + '（共 ' + cleared + ' 条）。';
}

// Where a conversation comes from, for the header chip: Codex when its calls carry a Codex
// turn source, ChatGPT when it is linked to a chat, 手动登记 for manual registrations.
// Unknown stays null and is simply not shown.
const SOURCE_LABEL = { codex: 'Codex', chatgpt: 'ChatGPT', manual: '手动登记' };
export function sourceOf(conversation, rows) {
  if (!conversation) return null;
  // The server knows the binding (contract §7.1); older servers fall back to inference.
  const sent = SOURCE_LABEL[String(conversation[FIELDS.source] || '').toLowerCase()];
  if (sent) return sent;
  const own = (rows || []).filter(row => row.thread_id === conversation.thread_id);
  if (own.some(row => row.raw && row.raw[FIELDS.turnSource] === 'codex')) return 'Codex';
  if (conversation[FIELDS.chatUrl] || conversation[FIELDS.chatId]) return 'ChatGPT';
  if (conversation[FIELDS.association] === 'manual') return '手动登记';
  return null;
}
export const createdAt = conversation => (conversation ? time(conversation[FIELDS.createdAt]) : undefined);
