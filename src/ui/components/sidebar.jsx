import { cn } from 'cn';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { Icon } from './icons.jsx';

// A conversation is recognized by its printed initial instead of another identical dot:
// in the collapsed rail that initial is the only thing left to tell them apart.
const glyphOf = entry => (entry.icon ? <Icon name={entry.icon} /> : <span className="thread-initial">{Array.from(entry.title || '?')[0]}</span>);

// Each entry keeps its own call count, plus a live mark while something in it is running:
// the list answers "which conversation is busy" without opening one.
function Meta({ stats }) {
  if (!stats || (!stats.total && !stats.running)) return null;
  return (
    <span className="thread-meta">
      {stats.running > 0 ? <i className="thread-live" title="有调用正在进行" /> : null}
      <span>{stats.total}</span>
    </span>
  );
}

// Conversation switcher: every registered thread, the unassigned bucket and the
// registration entry point. Collapsing keeps the glyphs only.
export function Sidebar({ conversations, thread, collapsed, stats, onSelect, onToggle, onSetup }) {
  const rows = [{ thread_id: '', title: '全部对话', icon: 'layers' }, ...(conversations || [])];
  const foot = [{ thread_id: 'unassigned', title: '未归属', icon: 'hash' }, { id: 'setup-toggle', title: '登记对话', icon: 'plus' }];
  const entry = (item, key) => {
    const all = item.thread_id === '';
    const active = item.thread_id !== undefined && item.thread_id === thread;
    const metric = stats && !item.id ? stats[all ? 'all' : item.thread_id || 'unassigned'] : null;
    return (
      <Tooltip key={key}>
        <TooltipTrigger asChild>
          <button
            type="button"
            id={item.id}
            className={cn('thread', active && 'active')}
            aria-label={item.title}
            aria-pressed={item.id ? undefined : active}
            onClick={() => (item.id ? onSetup() : onSelect(item.thread_id ? 'thread=' + encodeURIComponent(item.thread_id) : ''))}
          >
            <span className="thread-glyph">{glyphOf(item)}</span>
            <b>{item.title}</b>
            <Meta stats={metric} />
          </button>
        </TooltipTrigger>
        <TooltipContent side="right">{item.title}</TooltipContent>
      </Tooltip>
    );
  };
  return (
    <aside className="sidebar" aria-label="工作区">
      <div className="sidebar-head">
        <span className="brand"><Icon name="layers" size={18} /></span>
        <strong className="sidebar-label">本地工作区</strong>
        <Button
          id="collapse"
          variant="ghost"
          size="icon-sm"
          className="sidebar-toggle text-muted-foreground"
          aria-expanded={!collapsed}
          aria-label={collapsed ? '展开工作区' : '收起工作区'}
          title={collapsed ? '展开工作区' : '收起工作区'}
          onClick={onToggle}
        >
          <Icon name="panelLeft" />
        </Button>
      </div>
      <nav id="threads" className="thread-list" aria-label="对话列表">
        {rows.map(row => entry(row, row.thread_id || 'all'))}
      </nav>
      <div className="sidebar-foot">
        {foot.map(item => entry(item, item.id))}
      </div>
    </aside>
  );
}
