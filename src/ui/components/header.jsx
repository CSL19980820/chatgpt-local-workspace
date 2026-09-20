import { PathLink } from './path-link.jsx';
import { cn } from 'cn';
import { Button } from '@/components/ui/button';
import { Icon } from './icons.jsx';

// One row of context: which conversation is filtered, how much ran, how the sync is doing.
export function Header({ title, context, chatUrl, counts, sessions, queued, connection, state, paused, onPause, onRefresh, onDiagnostics }) {
  return (
    <header className="workspace-head">
      <h1 id="title" title={title}>{title}</h1>
      <span id="context" className="context" title={context}><PathLink value={context} /></span>
      {chatUrl
        ? <a id="chat" className="head-link" href={chatUrl} target="_blank" rel="noopener noreferrer" aria-label="打开对应对话"><Icon name="external" /></a>
        : null}
      <div className="summary">
        {counts.running > 0
          ? <span id="metric-live" className="metric live"><i className="pulse" /><b id="running">{counts.running}</b>执行中</span>
          : null}
        {sessions > 0
          ? <span id="metric-sessions" className="metric"><Icon name="terminal" /><b id="sessions">{sessions}</b>终端</span>
          : null}
        {queued > 0
          ? <span id="metric-queued" className="metric"><b id="queued">{queued}</b>排队</span>
          : null}
        <span className="metric summary-calls"><b id="count">{counts.total}</b>调用</span>
        <span className="metric summary-failed"><b id="failed">{counts.failed}</b>失败</span>
      </div>
      <span id="connection" className={cn('connection', state)}>{connection}</span>
      <Button id="diagnostics" variant="ghost" size="icon-sm" aria-label="诊断连接" title="诊断连接" onClick={onDiagnostics}><Icon name="activity" /></Button>
      <Button id="refresh" variant="ghost" size="icon-sm" aria-label="立即同步" title="立即同步" onClick={onRefresh}>
        <Icon name="refresh" />
      </Button>
      <Button id="pause" variant="secondary" size="sm" title={paused ? "恢复自动同步" : "暂停自动同步"} onClick={onPause}>{paused ? '恢复' : '暂停'}</Button>
    </header>
  );
}
