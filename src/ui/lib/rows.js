// Turns one live snapshot into everything the timeline renders: absolute start time,
// running/failed state, elapsed time and the typed detail of each call.
//
// A call counts as running while either the tool call itself is still in flight, or the
// command session it started is still alive. Sessions are matched by session_id, which the
// current server reports directly and older servers only carry inside the raw receipt.
import { detailFromReceipt } from './receipt.js';
import { base } from './format.js';
import { stateOf, titleOf } from './labels.js';

const parseTime = value => {
  const ms = Date.parse(value);
  return isFinite(ms) ? ms : 0;
};

const firstLine = value => String(value || '').split('\n')[0];

export function buildRows(snapshot, now = Date.now()) {
  const commands = snapshot.commands || [];
  const activity = snapshot.activity || [];
  const bySession = new Map(commands.filter(command => command.session_id).map(command => [command.session_id, command]));
  const rows = [];

  for (const call of activity) {
    // Prefer the shaped detail; fall back to rebuilding it from an older server's receipt.
    const detail = call.detail || detailFromReceipt(call.tool, call.preview);
    const sessionId = call.session_id || (detail && detail.session_id) || null;
    const session = sessionId ? bySession.get(sessionId) || null : null;
    const failed = !!(session && ((session.exit_code != null && session.exit_code !== 0) || session.timed_out || session.stopped));
    const live = !!(session && session.running);
    const start = parseTime(call.started_at);
    const elapsed = live ? Math.max(0, now - start) : call.elapsed_ms;
    rows.push({
      id: call.id, tool: call.tool, thread_id: call.thread_id, target: call.target,
      status: failed ? 'failed' : call.status, live, sessionId: sessionId, session,
      started_at: call.started_at, start, elapsed_ms: elapsed, detail,
      synthetic: false,
    });
  }

  // A background command outlives the tool call that started it: keep it on the timeline
  // even when the call itself already returned.
  const known = new Set(activity.map(call => call.session_id || (call.detail && call.detail.session_id)).filter(Boolean));
  for (const command of commands) {
    if (!command.running || known.has(command.session_id)) continue;
    const start = parseTime(command.started_at);
    rows.push({
      id: 'session:' + command.session_id, tool: 'exec_command', thread_id: command.thread_id, target: command.cwd,
      status: 'running', live: true, sessionId: command.session_id, session: command,
      started_at: command.started_at, start, elapsed_ms: Math.max(0, now - start), synthetic: true,
      detail: {
        kind: 'command', session_id: command.session_id, command: command.command, shell: command.shell,
        cwd: command.cwd, summary: firstLine(command.command), tool: 'exec_command', target: command.cwd,
      },
    });
  }

  rows.sort((a, b) => a.start - b.start);
  return rows.reverse();
}

export const summaryOf = row => {
  if (row.detail && row.detail.summary) return row.detail.summary;
  if (row.session && row.session.command) return firstLine(row.session.command);
  if (row.detail && row.detail.command) return firstLine(row.detail.command);
  return base(row.target);
};

export const countStates = rows => ({
  total: rows.length,
  running: rows.filter(row => row.status === 'running' || row.live).length,
  failed: rows.filter(row => row.status === 'failed').length,
});

export const isRunning = row => row.status === 'running' || row.live;

// A call that returned while its command session is still alive reads as 进行中, not 已返回:
// the timeline, the filter and the inspector must agree on that wording.
export const effectiveState = row => (isRunning(row) ? 'running' : row.status);
export const stateText = row => stateOf(effectiveState(row));

// Elapsed time for a live row keeps counting between snapshots.
export const liveElapsed = (row, now) => (isRunning(row) ? Math.max(0, now - row.start) : row.elapsed_ms);

export function matches(row, query, state) {
  if (state && effectiveState(row) !== state) return false;
  if (!query) return true;
  // The placeholder promises calls, files and commands, so the search covers the paths the
  // inspector shows too — a patch row is found by any file it touched, not just its root.
  const detail = row.detail || {};
  const paths = [(detail.files || []).map(file => file.path), (detail.reads || []).map(read => read.path), (detail.matches || []).map(match => match.path)];
  const haystack = [row.tool, titleOf(row.tool), summaryOf(row), row.target, detail.command || ''].concat(...paths).join(' ').toLowerCase();
  return haystack.includes(query);
}

export const filterRows = (rows, query, state) => rows.filter(row => matches(row, query, state));

// Old servers shipped no `detail` field at all; that is a version gap, not an empty call.
export const staleServer = snapshot => !!snapshot && (snapshot.activity || []).length > 0 && !(snapshot.activity || []).some(call => call.detail);
