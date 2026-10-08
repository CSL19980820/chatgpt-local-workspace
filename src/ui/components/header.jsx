import { forwardRef } from 'react';
import { cn } from 'cn';
import { PathLink } from './path-link.jsx';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { Icon, SpinIcon } from './icons.jsx';
import { ago, hm, stamp } from '@/lib/format.js';

// Ghost icon button with a tooltip. The label is also the accessible name (sr-only text, so
// textContent reads e.g. "暂停" for scripts that look for it).
export const IconButton = forwardRef(function IconButton({ label, tip, icon, children, ...props }, ref) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button ref={ref} variant="ghost" size="icon-sm" aria-label={label} {...props}>
          {children || <Icon name={icon} />}<span className="sr-only">{label}</span>
        </Button>
      </TooltipTrigger>
      <TooltipContent>{tip || label}</TooltipContent>
    </Tooltip>
  );
});

// Header of the main column. Line one: the conversation and the window actions. Line two:
// where it comes from, when it started and was last active, and the compact numbers.
export function Header({
  title, source, context, chatUrl, startedAt, lastAt, now, calls, counts, changes, files, queued,
  connection, state, synced, paused, onPause, onRefresh, onDiagnostics, onClear, clearing,
  searchOpen, onSearch, inspector, onInspector, sidebarCollapsed, sidebarRail, onSidebar,
}) {
  const hasChanges = changes && (changes.added !== undefined || changes.removed !== undefined);
  const facts = [];
  if (startedAt) facts.push(<span key="start" className="fact start" title={'开始于 ' + stamp(new Date(startedAt).toISOString())}>开始 {hm(new Date(startedAt).toISOString())}</span>);
  if (lastAt) facts.push(<span key="last" className="fact" title={'最近活动 ' + stamp(new Date(lastAt).toISOString())}>最近活动 {ago(new Date(lastAt).toISOString(), now)}</span>);
  return (
    <header className="workspace-head">
      <div className="head-row">
        {/* The sidebar toggle sits here in every state, so the sidebar never spends a row on it. */}
        {sidebarRail ? null : <IconButton id="collapse" label={sidebarCollapsed ? '展开侧栏' : '收起侧栏'} icon="panelLeft" className="head-sidebar"
          aria-expanded={!sidebarCollapsed} onClick={onSidebar} />}
        <div className="head-title">
          <h1 id="title" title={title}>{title}</h1>
          {source ? <span id="source" className="source-chip">{source}</span> : null}
          {chatUrl
            ? <a id="chat" className="head-link" href={chatUrl} target="_blank" rel="noopener noreferrer" aria-label="打开对应对话" title="打开对应 ChatGPT 对话"><Icon name="external" size={14} /></a>
            : null}
        </div>
        <div className="head-actions">
          <span id="connection" className={cn('connection', state)} title={synced ? '同步于 ' + synced : undefined}>
            <i className="dot" />{connection}
          </span>
          <span className="head-sep" aria-hidden="true" />
          <IconButton id="search-toggle" label="搜索与筛选" tip="搜索与筛选（Ctrl+F）" icon="search" aria-pressed={searchOpen} onClick={onSearch} />
          <IconButton id="pause" label={paused ? '恢复' : '暂停'} tip={paused ? '恢复自动同步' : '暂停自动同步'} icon={paused ? 'play' : 'pause'} aria-pressed={paused} onClick={onPause} />
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <IconButton id="more" label="更多操作" icon="more" />
            </DropdownMenuTrigger>
            <DropdownMenuContent id="more-menu" align="end">
              <DropdownMenuItem id="refresh" onSelect={onRefresh}><Icon name="refresh" />立即同步</DropdownMenuItem>
              <DropdownMenuItem id="menu-diagnostics" onSelect={onDiagnostics}><Icon name="activity" />诊断连接</DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem id="clear-logs" variant="destructive" disabled={clearing} onSelect={onClear}><Icon name="trash" />清空已完成日志…</DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
          <IconButton id="inspector-toggle" label={inspector ? '关闭检查器' : '打开检查器'} icon="panelRight" aria-pressed={inspector} onClick={onInspector} />
        </div>
      </div>
      <div className="head-meta">
        {context ? <span id="context" className="context" title={context}><Icon name="folder" size={13} /><PathLink value={context} /></span> : null}
        {facts}
        <span className="head-stats">
          {calls ? <span className="stat tabular" id="metric-calls"><b>{calls}</b> 次调用</span> : null}
          {counts.running > 0
            ? <span id="metric-live" className="stat live"><SpinIcon size={12} /><b id="running">{counts.running}</b> 进行中</span>
            : null}
          {queued > 0 ? <span id="metric-queued" className="stat optional" title="等待执行的调用"><b id="queued">{queued}</b> 排队</span> : null}
          {counts.failed > 0 ? <span className="stat danger summary-failed"><Icon name="circleAlert" size={12} /><b id="failed">{counts.failed}</b> 失败</span> : null}
          {hasChanges
            ? <span id="diff-total" className="stat tabular" title="当前视图里写入改动的行数">{files ? <span className="files">{files} 个文件</span> : null}<span className="plus">+{changes.added || 0}</span><span className="minus">−{changes.removed || 0}</span></span>
            : null}
        </span>
      </div>
    </header>
  );
}
