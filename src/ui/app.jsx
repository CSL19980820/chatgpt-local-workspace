import { DiagnosticsDialog } from './components/diagnostics-dialog.jsx';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { TooltipProvider } from '@/components/ui/tooltip';
import { Header } from './components/header.jsx';
import { PlanCard } from './components/plan-card.jsx';
import { Sidebar } from './components/sidebar.jsx';
import { Timeline } from './components/timeline.jsx';
import { DetailPane } from './components/detail-pane.jsx';
import { SetupDialog } from './components/setup-dialog.jsx';
import { copyText } from '@/lib/copy.js';
import { buildRows, countStates, filterRows, isRunning } from '@/lib/rows.js';

const readHash = () => new URLSearchParams(location.hash.slice(1)).get('thread') || '';
const stored = (key, fallback) => { try { const value = localStorage.getItem(key); return value == null ? fallback : value === 'true'; } catch { return fallback; } };
const store = (key, value) => { try { localStorage.setItem(key, String(value)); } catch { /* private mode */ } };

export function App() {
  const [diagnosticsOpen, setDiagnosticsOpen] = useState(() => location.hash === '#diagnostics');
  useEffect(() => { const onHash = () => { if (location.hash === '#diagnostics') setDiagnosticsOpen(true); }; window.addEventListener('hashchange', onHash); return () => window.removeEventListener('hashchange', onHash); }, []);
  const [snapshot, setSnapshot] = useState(null);
  const [error, setError] = useState('');
  const [paused, setPaused] = useState(false);
  const [thread, setThread] = useState(readHash);
  const [follow, setFollow] = useState(true);
  const [query, setQuery] = useState('');
  const [state, setState] = useState('');
  const [selectedId, setSelectedId] = useState('');
  const [planOpen, setPlanOpen] = useState(() => stored('workspace-plan-open', false));
  const [collapsed, setCollapsed] = useState(() => stored('workspace-sidebar-collapsed', false));
  const [setupOpen, setSetupOpen] = useState(false);
  const [copied, setCopied] = useState(false);
  const [now, setNow] = useState(() => Date.now());

  const threadRef = useRef(thread), pausedRef = useRef(paused), busyRef = useRef(false), epochRef = useRef(0), instanceRef = useRef(''), timerRef = useRef(null), copiedRef = useRef(null);
  threadRef.current = thread;
  pausedRef.current = paused;

  // One request per second: the page never waits for a long tool call to render state.
  const schedule = useCallback(() => {
    clearTimeout(timerRef.current);
    if (!pausedRef.current && !document.hidden) timerRef.current = setTimeout(() => loadRef.current(), 1000);
  }, []);

  const load = useCallback(async (force) => {
    if (busyRef.current) return;
    busyRef.current = true;
    const own = epochRef.current, filter = threadRef.current;
    const abort = new AbortController();
    const timeout = setTimeout(() => abort.abort(), 8000);
    try {
      const response = await fetch('/api/snapshot' + (filter ? '?thread=' + encodeURIComponent(filter) : ''), { signal: abort.signal, cache: 'no-store' });
      const value = await response.json();
      if (!response.ok) throw Error(value.error || '连接失败');
      if (own !== epochRef.current) return;
      if (instanceRef.current && instanceRef.current !== value.instance_id) throw Error('服务实例已改变，请重新打开工作台。');
      instanceRef.current = value.instance_id;
      setSnapshot(value);
      setError('');
    } catch (caught) {
      if (own === epochRef.current) setError(caught.name === 'AbortError' ? '连接超时' : caught.message);
    } finally {
      clearTimeout(timeout);
      busyRef.current = false;
      if (!force) schedule();
    }
  }, [schedule]);
  const loadRef = useRef(load);
  loadRef.current = load;

  useEffect(() => { loadRef.current(); return () => clearTimeout(timerRef.current); }, [thread]);
  useEffect(() => { const timer = setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(timer); }, []);
  useEffect(() => {
    const onHash = () => { epochRef.current++; setThread(readHash()); setSelectedId(''); setFollow(true); };
    const onVisible = () => { if (document.hidden) clearTimeout(timerRef.current); else if (!pausedRef.current) loadRef.current(); };
    window.addEventListener('hashchange', onHash);
    document.addEventListener('visibilitychange', onVisible);
    return () => { window.removeEventListener('hashchange', onHash); document.removeEventListener('visibilitychange', onVisible); };
  }, []);

  const rows = useMemo(() => (snapshot ? buildRows(snapshot, now) : []), [snapshot, now]);
  const visible = useMemo(() => filterRows(rows, query.trim().toLowerCase(), state), [rows, query, state]);
  const counts = useMemo(() => countStates(rows), [rows]);
  // Per-conversation totals for the sidebar. A filtered snapshot only carries the selected
  // conversation, so the counts are shown only for the unfiltered view.
  const stats = useMemo(() => {
    if (thread) return null;
    const map = { all: { total: 0, running: 0 } };
    for (const row of rows) {
      const entry = map[row.thread_id || 'unassigned'] || (map[row.thread_id || 'unassigned'] = { total: 0, running: 0 });
      entry.total += 1;
      map.all.total += 1;
      if (isRunning(row)) { entry.running += 1; map.all.running += 1; }
    }
    return map;
  }, [rows, thread]);
  const selected = visible.find(row => row.id === selectedId) || (follow ? visible[0] || null : null);

  useEffect(() => {
    if (!follow || !visible.length) return;
    if (selectedId !== visible[0].id) setSelectedId(visible[0].id);
  }, [follow, visible, selectedId]);

  const current = snapshot ? (snapshot.conversations || []).find(row => row.thread_id === thread) : null;
  const title = current ? current.title : (thread === 'unassigned' ? '未归属' : '全部对话');
  const connection = paused ? '已暂停' : error ? '已断开' : snapshot ? '实时' : '连接中';
  const connectionState = paused ? 'paused' : error ? 'error' : '';
  const threadName = id => ((snapshot && (snapshot.conversations || []).find(row => row.thread_id === id)) || {}).title || '未归属';

  const togglePause = () => {
    const next = !paused;
    setPaused(next);
    if (next) clearTimeout(timerRef.current); else loadRef.current();
  };
  const selectThread = hash => { if (location.hash.slice(1) === hash) return; location.hash = hash; };
  const copySelected = async () => {
    const text = selected ? copyText(selected) : '';
    if (!text) return;
    try { await navigator.clipboard.writeText(text); } catch { return; }
    setCopied(true);
    clearTimeout(copiedRef.current);
    copiedRef.current = setTimeout(() => setCopied(false), 1500);
  };

  return (
    <TooltipProvider delayDuration={200}>
      <div id="shell" className={'app-shell' + (collapsed ? ' collapsed' : '')}>
        <Sidebar conversations={snapshot ? snapshot.conversations : []} thread={thread} collapsed={collapsed} stats={stats}
          onSelect={selectThread} onToggle={() => { setCollapsed(!collapsed); store('workspace-sidebar-collapsed', !collapsed); }}
          onSetup={() => setSetupOpen(true)} />
        <main>
          <Header onDiagnostics={() => setDiagnosticsOpen(true)} title={title} context={current ? current.path : ''} chatUrl={current ? current.chat_url : null}
            counts={counts} sessions={snapshot ? (snapshot.commands || []).filter(command => command.running).length : 0}
            queued={snapshot ? snapshot.queued_calls || 0 : 0} connection={connection} state={connectionState}
            paused={paused} onPause={togglePause} onRefresh={() => loadRef.current(true)} />
          {error ? <div id="warning" className="banner">{error}</div> : null}
          <div className="workspace-body">
            <div className="activity-column">
              <PlanCard plans={snapshot ? snapshot.plans || [] : []} grouped={!thread} threadName={threadName}
                open={planOpen} onToggle={() => { setPlanOpen(!planOpen); store('workspace-plan-open', !planOpen); }} />
              <Timeline rows={rows} visible={visible} selectedId={selected ? selected.id : ''} onSelect={row => { setFollow(false); setSelectedId(row.id); }}
                follow={follow} onFollow={value => setFollow(value)} query={query} onQuery={setQuery} state={state} onState={setState} now={now} />
            </div>
            <DetailPane row={selected} now={now} snapshot={snapshot} onCopy={copySelected} copied={copied} />
          </div>
          <footer>
            <span id="footer">{snapshot ? 'v' + snapshot.version + ' · ' + (snapshot.default_shell || '') : ''}</span>
            <span id="fresh">{snapshot ? '同步于 ' + new Date(snapshot.checked_at).toLocaleTimeString('zh-CN', { hour12: false }) : ''}</span>
          </footer>
        </main>
      </div>
      <DiagnosticsDialog open={diagnosticsOpen} onOpenChange={value => { setDiagnosticsOpen(value); if (!value && location.hash === '#diagnostics') history.replaceState(null, '', location.pathname + location.search); }} />
      <SetupDialog open={setupOpen} onOpenChange={setSetupOpen} />
    </TooltipProvider>
  );
}
