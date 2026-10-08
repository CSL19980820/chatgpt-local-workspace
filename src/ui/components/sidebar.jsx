import { cn } from 'cn';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { Icon, SpinIcon } from './icons.jsx';
import { since } from '@/lib/format.js';

// The app mark: the hand-tuned 20px version of assets/local-workspace.svg (brackets + cursor),
// pixel-aligned at 20px and 40px. The favicon in the template uses the master.
const MARK = 'data:image/svg+xml,' + encodeURIComponent("<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20' viewBox='0 0 20 20'><rect x='2' y='2' width='16' height='16' rx='3.5' fill='#ffffff'/><rect x='2.5' y='2.5' width='15' height='15' rx='3.0' fill='none' stroke='#cdcdcd'/><rect x='5' y='5' width='2' height='10' fill='#161616'/><rect x='5' y='5' width='3' height='2' fill='#161616'/><rect x='5' y='13' width='3' height='2' fill='#161616'/><rect x='13' y='5' width='2' height='10' fill='#161616'/><rect x='12' y='5' width='3' height='2' fill='#161616'/><rect x='12' y='13' width='3' height='2' fill='#161616'/><rect x='9' y='8' width='2' height='4' fill='#161616'/></svg>");

// Right side of a conversation: a spinner while something runs, else the failed count.
function Badge({ stats, total }) {
  if (!stats) return null;
  if (stats.running > 0) return <span className="thread-meta" title={stats.running + ' 个调用进行中'}><SpinIcon size={14} /></span>;
  if (total) return stats.calls ? <span className="thread-meta tabular">{stats.calls}</span> : null;
  if (stats.failed > 0) return <span className="thread-meta failed tabular" title={stats.failed + ' 次调用失败'}><Icon name="circleAlert" size={12} />{stats.failed}</span>;
  return null;
}

// One quiet meta line under the title: source · last activity.
function Sub({ source, stats, now }) {
  const parts = [];
  if (source) parts.push(<span key="s">{source}</span>);
  if (stats && stats.lastAt) parts.push(<span key="t" className="tabular">{since(stats.lastAt, now) === '刚刚' ? '刚刚' : since(stats.lastAt, now) + '前'}</span>);
  if (!parts.length) return null;
  return <span className="thread-sub">{parts.map((part, index) => [index ? <i key={'d' + index} className="sep">·</i> : null, part])}</span>;
}

function Row({ id, title, icon, initial, active, stats, total, now, rail, onClick, pressed = true, source, sub = false, trail = null }) {
  const button = (
    <button type="button" id={id} className={cn('thread', active && 'active', sub && 'two-line')} aria-label={title}
      title={rail ? undefined : title} aria-pressed={pressed ? !!active : undefined} onClick={onClick}>
      <span className="thread-glyph">{icon ? <Icon name={icon} /> : <><Icon name="chat" className="glyph-icon" /><span className="thread-initial">{initial}</span></>}</span>
      <span className="thread-text">
        <b>{title}</b>
        {sub ? <Sub source={source} stats={stats} now={now} /> : null}
      </span>
      {trail || <Badge stats={stats} total={total} />}
    </button>
  );
  if (!rail) return button;
  // Collapsed to the icon rail, the name only survives as a tooltip.
  return (
    <Tooltip>
      <TooltipTrigger asChild>{button}</TooltipTrigger>
      <TooltipContent side="right">{title}</TooltipContent>
    </Tooltip>
  );
}

// Conversation switcher. Top: all conversations (its row lines up with the header's title row;
// the browser page adds the brand above it), then the registered ones, newest activity first;
// the unassigned bucket and diagnostics stay at the bottom, the version quietly on the
// diagnostics row. The collapse toggle lives in the main header so it never needs a row here.
export function Sidebar({ conversations, thread, collapsed, rail: railAuto, loaded, stats, sources, now, version, onSelect, onSetup, onDiagnostics }) {
  const pick = id => onSelect(id ? 'thread=' + encodeURIComponent(id) : '');
  const list = conversations || [];
  const rail = collapsed || railAuto;
  return (
    <aside className="sidebar" aria-label="工作区">
      <div className="sidebar-head">
        <span className="brand">{MARK ? <img className="brand-mark" src={MARK} alt="" aria-hidden="true" /> : null}本地工作区</span>
      </div>
      <nav id="threads" className="thread-list" aria-label="对话列表">
        <Row title="全部对话" icon="layers" active={thread === ''} stats={stats && stats['']} total now={now} rail={rail} onClick={() => pick('')} />
        <div className="section-label">
          <span>对话{list.length ? <em className="tabular">{list.length}</em> : null}</span>
          <Button id="setup-toggle" variant="ghost" size="icon-xs" aria-label="登记对话" title="登记对话" onClick={onSetup}><Icon name="plus" size={14} /></Button>
        </div>
        {list.map(item => (
          <Row key={item.thread_id} title={item.title || '未命名对话'} initial={Array.from(item.title || '?')[0]} sub
            source={sources[item.thread_id]} active={item.thread_id === thread} stats={stats && stats[item.thread_id]} now={now} rail={rail}
            onClick={() => pick(item.thread_id)} />
        ))}
        {loaded && list.length === 0 ? <p className="thread-empty">还没有登记的对话。点 + 登记后，调用会按对话归类。</p> : null}
      </nav>
      <div className="sidebar-foot">
        <Row id="unassigned" title="未归属" icon="inbox" active={thread === 'unassigned'} stats={stats && stats.unassigned} now={now} rail={rail} onClick={() => pick('unassigned')} />
        <Row id="diagnostics" title="诊断连接" icon="activity" pressed={false} rail={rail} onClick={onDiagnostics}
          trail={version ? <span id="footer" className="thread-meta version tabular" title={'本地工作区版本 ' + version}>v{version}</span> : null} />
      </div>
    </aside>
  );
}
