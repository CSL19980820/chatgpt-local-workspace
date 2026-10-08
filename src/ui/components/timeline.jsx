import { cn } from 'cn';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group';
import { Icon, SpinIcon } from './icons.jsx';
import { DetailBody, Empty } from './detail-pane.jsx';
import { clock, duration, hm, span, stamp } from '@/lib/format.js';
import { isRunning, liveElapsed, stateText } from '@/lib/rows.js';
import { titleOf } from '@/lib/labels.js';
import { iconOf, phraseOf } from '@/lib/phrase.js';
import { groupTurns } from '@/lib/adapter.js';

export const Counts = ({ added, removed }) => (
  added === undefined && removed === undefined
    ? null
    : <span className="counts tabular"><span className="plus">+{added || 0}</span> <span className="minus">−{removed || 0}</span></span>
);

// One call as one 32px line: a gray glyph for the kind of action (a spinner while it runs),
// the verb, the object in a quiet mono chip, then right-aligned facts in tabular numbers.
export function EventRow({ row, open, selected, flash, onToggle, now, snapshot }) {
  const running = isRunning(row);
  const failed = row.status === 'failed';
  const elapsed = liveElapsed(row, now);
  const phrase = phraseOf(row);
  const title = titleOf(row.tool);
  const state = stateText(row);
  const detail = row.detail || {};
  const exit = detail.kind === 'command' ? (row.session && row.session.exit_code != null ? row.session.exit_code : detail.exit_code) : null;
  const hint = title + ' · ' + state + ' · 开始 ' + stamp(row.started_at) + ' · 耗时 ' + duration(elapsed) + (row.target ? '\n' + row.target : '');
  return (
    <div className={cn('event-wrap', open && 'open')}>
      <button
        type="button"
        className={cn('event', 'k-' + (detail.kind || 'none'), selected && 'selected', running && 'live', failed && 'failed', flash && 'flash')}
        data-id={row.id}
        title={hint}
        aria-label={title + ' · ' + state + ' · ' + duration(elapsed)}
        aria-expanded={!!open}
        onClick={() => onToggle(row)}
      >
        <span className="event-icon">{running ? <SpinIcon size={14} className="event-spin" /> : failed ? <Icon name="circleAlert" size={16} className="event-icon-failed" /> : <Icon name={iconOf(row)} size={16} />}</span>
        <span className="event-main">
          <span className="event-title">{phrase.verb}</span>
          {phrase.object ? <span className={cn('event-sub', phrase.mono && 'mono')}>{phrase.object}</span> : null}
          {phrase.note ? <span className="event-note">{phrase.note}</span> : null}
        </span>
        <span className="event-meta">
          {phrase.counts ? <Counts {...phrase.counts} /> : null}
          {failed ? <span className="event-fail"><Icon name="circleAlert" size={13} />{exit != null && exit !== 0 ? '退出 ' + exit : '失败'}</span> : null}
          <span className={cn('event-elapsed', 'tabular', running && 'event-live')}>{duration(elapsed)}</span>
          <span className="event-time tabular" aria-hidden="true">{hm(row.started_at)}</span>
          <Icon name="chevronRight" size={14} className="event-chevron" />
        </span>
      </button>
      {open ? <div className="event-detail">{<DetailBody row={row} now={now} snapshot={snapshot} inline />}</div> : null}
    </div>
  );
}

// The start of a turn: when it began, how long it worked and how many calls it made.
// Turns come from server turn ids when sent, otherwise from time gaps.
function TurnDivider({ group, now }) {
  const running = group.rows.some(isRunning);
  const worked = (running ? now : group.end) - group.start;
  const at = new Date(group.start).toISOString();
  return (
    <div className="turn-divider" role="separator" title={'开始于 ' + clock(at)}>
      <span className="turn-time tabular">{hm(at)}</span>
      {group.rows.length > 1 || running
        ? <span className="turn-label">{running ? <><SpinIcon size={12} />正在工作 {span(worked)}</> : '工作了 ' + span(worked)}</span>
        : null}
      {group.rows.length > 1 ? <span className="turn-count tabular">{group.rows.length} 次调用</span> : null}
    </div>
  );
}

const FILTERS = [['all', '全部'], ['running', '进行中'], ['failed', '失败'], ['returned', '已返回']];

export function Filters({ query, onQuery, state, onState, shown, total, inputRef, onClose }) {
  return (
    <div className="filters" role="search">
      <span className="search">
        <Icon name="search" size={14} />
        <Input id="search" ref={inputRef} className="search-input" aria-label="搜索调用" placeholder="搜索调用、文件或命令"
          value={query} onChange={event => onQuery(event.target.value)}
          onKeyDown={event => { if (event.key === 'Escape') onClose(); }} />
        {query ? <button type="button" className="search-clear" aria-label="清除搜索" onClick={() => onQuery('')}><Icon name="x" size={14} /></button> : null}
      </span>
      <ToggleGroup id="state" aria-label="活动状态" value={state || 'all'} onValueChange={value => onState(value === 'all' ? '' : value)}>
        {FILTERS.map(([value, label]) => <ToggleGroupItem key={value} value={value} aria-label={label}>{label}</ToggleGroupItem>)}
      </ToggleGroup>
      <span id="shown" className="shown tabular">{shown === total ? total + ' 次' : shown + ' / ' + total}</span>
    </div>
  );
}

// Arrow keys (or j / k) walk the rows; with the inspector open the focused row is selected.
function onRowKeys(event, onSelect) {
  const key = event.key;
  if (!['ArrowDown', 'ArrowUp', 'j', 'k'].includes(key) || !event.target.classList.contains('event')) return;
  const all = [...event.currentTarget.querySelectorAll('.event')];
  const next = all[all.indexOf(event.target) + (key === 'ArrowDown' || key === 'j' ? 1 : -1)];
  if (!next) return;
  event.preventDefault();
  next.focus();
  next.scrollIntoView({ block: 'nearest' });
  onSelect(next.getAttribute('data-id'));
}

// The activity stream: oldest at the top, newest at the bottom, grouped into turns.
export function Timeline({ rows, visible, expanded, selectedId, flashId, inspector, onToggle, onSelect, now, snapshot, filtered, onSetup, onReset }) {
  const groups = groupTurns(visible);
  return (
    <section id="timeline" className="timeline" aria-label="调用时间线" onKeyDown={event => onRowKeys(event, onSelect)}>
      {groups.map(group => (
        <div className="turn" key={group.key}>
          <TurnDivider group={group} now={now} />
          {group.rows.map(row => (
            <EventRow key={row.id} row={row} now={now} snapshot={snapshot} flash={row.id === flashId}
              open={!inspector && expanded.has(row.id)} selected={inspector && row.id === selectedId} onToggle={onToggle} />
          ))}
        </div>
      ))}
      {visible.length === 0
        ? (filtered || rows.length
          ? <Empty icon="search" title="没有匹配的调用" hint="换个关键词，或切回“全部”。">
              <Button variant="outline" size="sm" onClick={onReset}>清除筛选</Button>
            </Empty>
          : <Empty icon="inbox" title="暂无调用记录" hint="在 ChatGPT 或 Codex 里让模型使用本地工作区后，每一次读取、写入和命令都会实时出现在这里。">
              <Button variant="default" size="sm" onClick={onSetup}><Icon name="plus" size={14} />登记对话</Button>
            </Empty>)
        : null}
    </section>
  );
}

// Static placeholder while the first snapshot loads: gray bars, no shimmer.
export function Skeleton() {
  const widths = [62, 44, 71, 38, 55, 66, 48, 59];
  return (
    <div className="skeleton" aria-busy="true" aria-label="正在加载">
      <div className="sk-divider"><i style={{ width: 40 }} /></div>
      {widths.map((width, index) => (
        <div className="sk-row" key={index}><i className="sk-icon" /><i style={{ width: width + '%' }} /><i className="sk-meta" /></div>
      ))}
    </div>
  );
}
