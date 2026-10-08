import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { TooltipProvider } from '@/components/ui/tooltip';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { DiagnosticsDialog } from './components/diagnostics-dialog.jsx';
import { Header } from './components/header.jsx';
import { PlanCard } from './components/plan-card.jsx';
import { Sidebar } from './components/sidebar.jsx';
import { Filters, Skeleton, Timeline } from './components/timeline.jsx';
import { ChangesCard } from './components/changes-card.jsx';
import { DetailPane, Empty } from './components/detail-pane.jsx';
import { Button } from '@/components/ui/button';
import { SetupDialog } from './components/setup-dialog.jsx';
import { Icon } from './components/icons.jsx';
import { copyText } from '@/lib/copy.js';
import { clock } from '@/lib/format.js';
import { buildRows, countStates, filterRows, isRunning } from '@/lib/rows.js';
import { changedFiles, clearSummary, conversationStats, createdAt, rowCounts, sourceOf, sumCounts } from '@/lib/adapter.js';

const readHash = () => new URLSearchParams(location.hash.slice(1)).get('thread') || '';
const stored = (key, fallback) => { try { const value = localStorage.getItem(key); return value == null ? fallback : value === 'true'; } catch { return fallback; } };
const store = (key, value) => { try { localStorage.setItem(key, String(value)); } catch { /* private mode */ } };
const storedRaw = key => { try { return localStorage.getItem(key); } catch { return null; } };
// The inspector docks as a third column from DOCK_MIN and opens by default from AUTO_OPEN.
const RAIL_MAX = 720, DOCK_MIN = 1100, AUTO_OPEN = 1280, INSPECTOR_MIN = 320, INSPECTOR_MAX = 640;
const snapshotUrl = filter => '/api/snapshot' + (filter ? '?thread=' + encodeURIComponent(filter) : '');

// Local endpoints answer JSON, but a proxy, an old build or a crash page may not: never let
// a parse error hide the real status.
async function readJson(response) {
  const text = await response.text();
  try { return text ? JSON.parse(text) : {}; } catch { return {}; }
}
const failure = (result, response, fallback) => Error(fallback + '：' + ((result && result.error) || '服务返回 HTTP ' + response.status));

export function App() {
  const [diagnosticsOpen, setDiagnosticsOpen] = useState(() => location.hash === '#diagnostics');
  const [snapshot, setSnapshot] = useState(null);
  const [error, setError] = useState('');
  const [paused, setPaused] = useState(false);
  const [thread, setThread] = useState(readHash);
  const [query, setQuery] = useState('');
  const [state, setState] = useState('');
  const [searchOpen, setSearchOpen] = useState(false);
  const [expanded, setExpanded] = useState(() => new Set());
  const [selectedId, setSelectedId] = useState('');
  // null = automatic (open on wide windows); true/false = the user's explicit choice.
  const [inspectorPref, setInspectorPref] = useState(() => { const value = storedRaw('workspace-inspector-open'); return value == null ? null : value === 'true'; });
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [inspectorWidth, setInspectorWidth] = useState(() => Number(storedRaw('workspace-inspector-width')) || (window.innerWidth < 1440 ? 360 : window.innerWidth < 1800 ? 400 : 480));
  const [viewport, setViewport] = useState(() => window.innerWidth);
  const [flashId, setFlashId] = useState('');
  const [planOpen, setPlanOpen] = useState(() => stored('workspace-plan-open', false));
  const [collapsed, setCollapsed] = useState(() => stored('workspace-sidebar-collapsed', false));
  const [setupOpen, setSetupOpen] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [copied, setCopied] = useState(false);
  const [notice, setNotice] = useState(null);
  const [now, setNow] = useState(() => Date.now());
  const [clearing, setClearing] = useState(false);
  const [atBottom, setAtBottom] = useState(true);

  const threadRef = useRef(thread), pausedRef = useRef(paused), busyRef = useRef(false), epochRef = useRef(0), instanceRef = useRef(''), timerRef = useRef(null), copiedRef = useRef(null);
  const clearingRef = useRef(false), noticeRef = useRef(null), streamRef = useRef(null), bottomRef = useRef(true), searchRef = useRef(null), statsCache = useRef({}), sourceCache = useRef({}), flashRef = useRef(null);
  const docked = viewport >= DOCK_MIN;
  const inspector = docked ? (inspectorPref ?? viewport >= AUTO_OPEN) : drawerOpen;
  threadRef.current = thread;
  pausedRef.current = paused;

  // One request per second: the page never waits for a long tool call to render state.
  const schedule = useCallback(() => {
    clearTimeout(timerRef.current);
    if (!pausedRef.current && !document.hidden) timerRef.current = setTimeout(() => loadRef.current(), 1000);
  }, []);

  const load = useCallback(async (force) => {
    if (busyRef.current || clearingRef.current) return;
    busyRef.current = true;
    const own = epochRef.current, filter = threadRef.current;
    const abort = new AbortController();
    const timeout = setTimeout(() => abort.abort(), 8000);
    try {
      const response = await fetch(snapshotUrl(filter), { signal: abort.signal, cache: 'no-store' });
      const value = await readJson(response);
      if (!response.ok) throw failure(value, response, '连接失败');
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
    const onHash = () => {
      if (location.hash === '#diagnostics') { setDiagnosticsOpen(true); return; }
      epochRef.current++; setThread(readHash()); setSelectedId(''); setExpanded(new Set()); bottomRef.current = true; setAtBottom(true);
    };
    const onVisible = () => { if (document.hidden) clearTimeout(timerRef.current); else if (!pausedRef.current) loadRef.current(); };
    // Ctrl+F or "/" opens the search; Esc closes an empty search.
    const onKey = event => {
      const typing = /^(INPUT|TEXTAREA)$/.test(event.target.tagName);
      if ((event.ctrlKey && event.key.toLowerCase() === 'f') || (event.key === '/' && !typing)) {
        event.preventDefault(); setSearchOpen(true); requestAnimationFrame(() => searchRef.current && searchRef.current.focus());
      }
    };
    const onResize = () => setViewport(window.innerWidth);
    window.addEventListener('resize', onResize);
    window.addEventListener('hashchange', onHash);
    window.addEventListener('keydown', onKey);
    document.addEventListener('visibilitychange', onVisible);
    return () => { window.removeEventListener('resize', onResize); window.removeEventListener('hashchange', onHash); window.removeEventListener('keydown', onKey); document.removeEventListener('visibilitychange', onVisible); };
  }, []);

  const rows = useMemo(() => (snapshot ? buildRows(snapshot, now) : []), [snapshot, now]);
  const visible = useMemo(() => filterRows(rows, query.trim().toLowerCase(), state), [rows, query, state]);
  const counts = useMemo(() => countStates(rows), [rows]);
  const files = useMemo(() => changedFiles(visible), [visible]);
  // Header total: call-level counts, which also cover files a long patch left unlisted.
  const changes = useMemo(() => {
    const counts = visible.map(rowCounts).filter(item => item.added !== undefined || item.removed !== undefined);
    return counts.length ? sumCounts(counts) : null;
  }, [visible]);
  const omitted = visible.reduce((total, row) => total + (files.length && row.detail && row.detail.kind === 'write' ? row.detail.omitted_files || 0 : 0), 0);

  // Sidebar numbers. A filtered snapshot only carries the selected conversation, so the
  // others keep their last known numbers (never a stale "running").
  const stats = useMemo(() => {
    const fresh = conversationStats(snapshot ? snapshot.conversations : [], rows, isRunning);
    const cache = statsCache.current;
    for (const [id, value] of Object.entries(fresh)) if (!thread || id === thread || !value.derived) cache[id] = value;
    const out = {};
    for (const [id, value] of Object.entries(cache)) out[id] = thread && id !== thread && value.derived ? { ...value, running: 0 } : value;
    return out;
  }, [snapshot, rows, thread]);

  // Source chips survive a filtered snapshot the same way.
  const sources = useMemo(() => {
    for (const conversation of (snapshot && snapshot.conversations) || []) {
      const value = sourceOf(conversation, rows);
      if (value || !sourceCache.current[conversation.thread_id]) sourceCache.current[conversation.thread_id] = value;
    }
    return { ...sourceCache.current };
  }, [snapshot, rows]);

  const newest = visible[visible.length - 1] || null;
  const selected = visible.find(row => row.id === selectedId) || newest;

  // Stick to the newest call while the reader is at the bottom, like a chat.
  const onScroll = () => {
    const node = streamRef.current;
    if (!node) return;
    const bottom = node.scrollHeight - node.scrollTop - node.clientHeight < 32;
    bottomRef.current = bottom;
    if (bottom !== atBottom) setAtBottom(bottom);
  };
  const toBottom = () => { const node = streamRef.current; if (node) node.scrollTop = node.scrollHeight; bottomRef.current = true; setAtBottom(true); };
  useLayoutEffect(() => { if (bottomRef.current) toBottom(); }, [visible.length, thread, query, state, !!snapshot]);

  const current = snapshot ? (snapshot.conversations || []).find(row => row.thread_id === thread) : null;
  const title = current ? current.title || '未命名对话' : (thread === 'unassigned' ? '未归属' : '全部对话');
  const startedAt = current ? createdAt(current) ?? (rows[0] && rows[0].start) : (rows[0] && rows[0].start) || undefined;
  const lastAt = (stats[thread] || {}).lastAt;
  const connection = paused ? '已暂停' : error ? '已断开' : snapshot ? '实时' : '连接中';
  const connectionState = paused ? 'paused' : error ? 'error' : snapshot ? 'live' : '';
  const threadName = id => ((snapshot && (snapshot.conversations || []).find(row => row.thread_id === id)) || {}).title || '未归属';

  const say = (text, tone) => {
    clearTimeout(noticeRef.current);
    setNotice({ text, tone });
    noticeRef.current = setTimeout(() => setNotice(null), 4000);
  };
  const togglePause = () => {
    const next = !paused;
    setPaused(next);
    if (next) clearTimeout(timerRef.current); else loadRef.current();
  };
  const clearLogs = async () => {
    if (clearingRef.current) return;
    clearingRef.current = true; setClearing(true); epochRef.current++;
    try {
      const bootstrap = await fetch('/api/local-actions', { cache: 'no-store', signal: AbortSignal.timeout(5000) });
      const access = await readJson(bootstrap);
      if (!bootstrap.ok || !access.token) throw failure(access, bootstrap, '无法读取本地操作凭证，日志未清空');
      const response = await fetch('/api/clear-logs', { method: 'POST', headers: { 'X-Workspace-Token': access.token }, signal: AbortSignal.timeout(5000) });
      const result = await readJson(response);
      if (!response.ok) throw failure(result, response, '日志未清空');
      setSelectedId(''); setExpanded(new Set());
      say(clearSummary(result));
    } catch (caught) {
      say(caught.name === 'TimeoutError' ? '清空日志超时，请稍后立即同步确认结果。' : caught.message, 'error');
    } finally {
      clearingRef.current = false; setClearing(false);
      await loadRef.current(true);
      schedule();
    }
  };
  const selectThread = hash => { if (location.hash.slice(1) === hash) return; location.hash = hash; };
  const toggleRow = row => {
    setSelectedId(row.id);
    if (inspector) return;
    setExpanded(previous => { const next = new Set(previous); if (next.has(row.id)) next.delete(row.id); else next.add(row.id); return next; });
  };
  const openRow = id => {
    if (inspector) setSelectedId(id); else setExpanded(previous => new Set(previous).add(id));
    setFlashId(id);
    clearTimeout(flashRef.current);
    flashRef.current = setTimeout(() => setFlashId(''), 1200);
    requestAnimationFrame(() => {
      const node = document.querySelector('#timeline .event[data-id="' + CSS.escape(id) + '"]');
      if (node) node.scrollIntoView({ block: 'nearest' });
    });
  };
  const toggleInspector = () => {
    if (!docked) { setDrawerOpen(!drawerOpen); return; }
    const next = !inspector;
    setInspectorPref(next); store('workspace-inspector-open', next);
  };
  // Keep at least 480px for the stream; the handle and arrow keys both land here.
  const resizeInspector = value => {
    const sidebar = collapsed || viewport <= RAIL_MAX ? 52 : 260;
    const next = Math.round(Math.max(INSPECTOR_MIN, Math.min(INSPECTOR_MAX, viewport - sidebar - 480, value)));
    setInspectorWidth(next); store('workspace-inspector-width', next);
  };
  const selectRow = id => { if (inspector && id) setSelectedId(id); };
  const resetFilters = () => { setQuery(''); setState(''); };
  const toggleSearch = () => {
    const next = !searchOpen;
    setSearchOpen(next);
    if (next) requestAnimationFrame(() => searchRef.current && searchRef.current.focus());
    else resetFilters();
  };
  const copySelected = async () => {
    const text = selected ? copyText(selected) : '';
    if (!text) return;
    try { await navigator.clipboard.writeText(text); } catch { return; }
    setCopied(true);
    clearTimeout(copiedRef.current);
    copiedRef.current = setTimeout(() => setCopied(false), 1500);
  };

  const toggleSidebar = () => { setCollapsed(!collapsed); store('workspace-sidebar-collapsed', !collapsed); };
  const streamBody = snapshot
    ? <>
        <Timeline rows={rows} visible={visible} expanded={expanded} selectedId={selected ? selected.id : ''} flashId={flashId} inspector={inspector}
          onToggle={toggleRow} onSelect={selectRow} now={now} snapshot={snapshot} filtered={!!(query || state)} onSetup={() => setSetupOpen(true)} onReset={resetFilters} />
        <ChangesCard files={files} omitted={omitted} total={changes} root={current ? current.path : ''} scope={thread ? '本对话' : '当前视图'} onOpen={openRow} />
      </>
    : error
      ? <Empty id="connect-error" tone="error" icon="offline" title="无法连接本地工作区" hint={error + '。请确认本地工作区程序正在运行，然后重试。'}>
          <Button variant="default" size="sm" onClick={() => loadRef.current(true)}><Icon name="refresh" size={14} />重试</Button>
          <Button variant="outline" size="sm" onClick={() => setDiagnosticsOpen(true)}>诊断连接</Button>
        </Empty>
      : <Skeleton />;

  return (
    <TooltipProvider>
      <div id="shell" className={'app-shell' + (collapsed ? ' collapsed' : '') + (viewport <= RAIL_MAX ? ' rail' : '')}>
        <Sidebar conversations={snapshot ? snapshot.conversations : []} thread={thread} collapsed={collapsed} rail={viewport <= RAIL_MAX} loaded={!!snapshot} stats={stats} sources={sources} now={now} version={snapshot ? snapshot.version : ''}
          onSelect={selectThread}
          onSetup={() => setSetupOpen(true)} onDiagnostics={() => setDiagnosticsOpen(true)} />
        <main className={'main-sheet' + (inspector && rows.length ? ' with-inspector' : '') + (docked ? '' : ' narrow')}>
          <div className="main-column">
            <Header title={title} source={current ? sources[current.thread_id] : null} context={current ? current.path : ''} chatUrl={current ? current.chat_url : null}
              startedAt={snapshot ? startedAt : undefined} lastAt={lastAt} now={now} calls={snapshot ? rows.length : 0}
              counts={counts} changes={changes} files={files.length}
              queued={snapshot ? snapshot.queued_calls || 0 : 0} connection={connection} state={connectionState}
              synced={snapshot ? clock(snapshot.checked_at) : ''}
              paused={paused} onPause={togglePause} onRefresh={() => loadRef.current(true)} onDiagnostics={() => setDiagnosticsOpen(true)}
              onClear={() => setConfirmOpen(true)} clearing={clearing}
              searchOpen={searchOpen || !!query || !!state} onSearch={toggleSearch} inspector={inspector && rows.length > 0} onInspector={toggleInspector}
              sidebarCollapsed={collapsed} sidebarRail={viewport <= RAIL_MAX} onSidebar={toggleSidebar} />
            <PlanCard plans={snapshot ? snapshot.plans || [] : []} grouped={!thread} threadName={threadName}
              open={planOpen} onToggle={() => { setPlanOpen(!planOpen); store('workspace-plan-open', !planOpen); }} />
            {searchOpen || query || state
              ? <Filters query={query} onQuery={setQuery} state={state} onState={setState} shown={visible.length} total={rows.length} inputRef={searchRef}
                  onClose={() => { if (!query) toggleSearch(); }} />
              : null}
            {error && snapshot ? <div id="warning" className="banner" role="alert"><Icon name="circleAlert" size={14} />{error}<button type="button" className="banner-action" onClick={() => loadRef.current(true)}>重试</button></div> : null}
            <div className="stream-column">
              <div id="stream" className="stream-scroll" ref={streamRef} onScroll={onScroll} tabIndex={-1}>
                <div className="stream">{streamBody}</div>
              </div>
              {!atBottom && visible.length
                ? <button type="button" id="jump-latest" className="jump-latest" onClick={toBottom}><Icon name="arrowDown" size={14} />跳到最新</button>
                : null}
            </div>
          </div>
          {inspector && rows.length
            ? <DetailPane row={selected} now={now} snapshot={snapshot} onCopy={copySelected} copied={copied} onClose={toggleInspector}
                width={Math.max(INSPECTOR_MIN, Math.min(inspectorWidth, viewport - (collapsed || viewport <= RAIL_MAX ? 52 : 260) - 480))} onResize={resizeInspector} docked={docked} />
            : null}
        </main>
      </div>
      {notice ? <div id="notice" className={'notice' + (notice.tone === 'error' ? ' error' : '')} role="status">{notice.text}</div> : null}
      <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <AlertDialogContent id="clear-confirm">
          <AlertDialogHeader>
            <AlertDialogTitle>清空已完成日志？</AlertDialogTitle>
            <AlertDialogDescription>会删除已完成的调用记录和命令输出，无法撤销。运行中的命令、执行计划、任务证据和已登记的对话都会保留。</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel id="clear-cancel">取消</AlertDialogCancel>
            <AlertDialogAction id="clear-confirm-action" variant="destructive" onClick={clearLogs}>清空</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
      <DiagnosticsDialog open={diagnosticsOpen} onOpenChange={value => { setDiagnosticsOpen(value); if (!value && location.hash === '#diagnostics') history.replaceState(null, '', location.pathname + location.search); }} />
      <SetupDialog open={setupOpen} onOpenChange={setSetupOpen} />
    </TooltipProvider>
  );
}
