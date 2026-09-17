import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Icon } from './icons.jsx';
import { clock, duration, stamp } from '@/lib/format.js';
import { isRunning, liveElapsed, stateText, summaryOf } from '@/lib/rows.js';
import { iconOf, titleOf, toneOf } from '@/lib/labels.js';

// One call: the type carries the colour, running and failed carry the accent, and the
// elapsed time keeps counting between snapshots.
export function EventRow({ row, selected, onSelect, now }) {
  const running = isRunning(row);
  const failed = row.status === 'failed';
  const elapsed = liveElapsed(row, now);
  const summary = summaryOf(row);
  const title = titleOf(row.tool);
  const state = stateText(row);
  const tone = toneOf(row.detail && row.detail.kind);
  const hint = title + ' · ' + state + ' · 开始 ' + stamp(row.started_at) + ' · 耗时 ' + duration(elapsed) + (row.target ? '\n' + row.target : '');
  return (
    <button
      type="button"
      className={'event k-' + tone + (selected ? ' selected' : '') + (running ? ' live' : '') + (failed ? ' failed' : '')}
      title={hint}
      aria-label={title + ' · ' + state + ' · ' + duration(elapsed)}
      aria-pressed={!!selected}
      onClick={() => onSelect(row)}
    >
      <span className={'event-icon ' + (failed ? 'failed' : running ? 'running' : 'returned')}>
        <Icon name={failed ? 'alert' : iconOf(row.detail && row.detail.kind)} />
        {running ? <i className="event-dot" /> : null}
      </span>
      <span className="event-main">
        <span className="event-title">{title}</span>
        {summary ? <span className="event-sub">{(running ? '运行中 · ' : '') + summary}</span> : null}
      </span>
      <span className="event-meta">
        <span className="event-time"><span className="event-time-label">开始</span>{clock(row.started_at)}</span>
        <span className={'event-elapsed' + (running ? ' event-live' : '')}>{duration(elapsed)}</span>
      </span>
    </button>
  );
}

// Timeline plus its filters: search, state, follow-latest.
export function Timeline({ rows, visible, selectedId, onSelect, follow, onFollow, query, onQuery, state, onState, now }) {
  return (
    <section className="timeline-section">
      <div className="section-head">
        <h2>调用时间线 <span id="shown" className="count">{visible.length === rows.length ? rows.length : visible.length + ' / ' + rows.length}</span></h2>
        <label className="follow">
          <Checkbox id="follow" checked={follow} onCheckedChange={value => onFollow(value === true)} aria-label="跟随最新" />
          跟随最新
        </label>
      </div>
      <div className="filters">
        <span className="search">
          <Icon name="search" />
          <Input id="search" className="search-input" aria-label="搜索调用" placeholder="搜索调用、文件或命令"
            value={query} onChange={event => onQuery(event.target.value)} />
        </span>
        <Select value={state || 'all'} onValueChange={value => onState(value === 'all' ? '' : value)}>
          <SelectTrigger id="state" size="sm" className="w-[104px]" aria-label="活动状态">
            <SelectValue placeholder="全部状态" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">全部状态</SelectItem>
            <SelectItem value="running">进行中</SelectItem>
            <SelectItem value="failed">失败</SelectItem>
            <SelectItem value="returned">已返回</SelectItem>
          </SelectContent>
        </Select>
      </div>
      <div id="timeline" className="timeline" tabIndex={0} aria-label="调用时间线">
        {visible.map(row => (
          <EventRow key={row.id} row={row} selected={row.id === selectedId} onSelect={onSelect} now={now} />
        ))}
        {visible.length === 0
          ? <div className="empty"><Icon name="info" /><div>{query || state ? '没有匹配的调用' : '暂无调用记录'}</div></div>
          : null}
      </div>
    </section>
  );
}
