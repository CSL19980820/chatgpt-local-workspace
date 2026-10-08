import { PathLink } from './path-link.jsx';
import { Button } from '@/components/ui/button';
import { DetailViews } from './views.jsx';
import { Icon } from './icons.jsx';
import { duration, stamp } from '@/lib/format.js';
import { titleOf } from '@/lib/labels.js';
import { isRunning, liveElapsed, staleServer, stateText } from '@/lib/rows.js';
import { iconOf } from '@/lib/phrase.js';

// W3C traceparent is "version-traceid-spanid-flags"; show just enough of the trace id to correlate.
const traceShort = value => {
  const parts = String(value).split('-');
  return (parts.length >= 2 ? parts[1] : parts[0]).slice(0, 8);
};

export const metaOf = (row, now) => {
  const running = isRunning(row);
  return [stateText(row), '开始 ' + stamp(row.started_at), '耗时 ' + duration(liveElapsed(row, now)), running && !/进行|运行/.test(stateText(row)) ? '仍在运行' : '', row.trace ? 'trace ' + traceShort(row.trace) : ''].filter(Boolean).join(' · ');
};

export const Empty = ({ icon = 'info', title, hint, children, id, tone }) => (
  <div className={'empty' + (tone ? ' ' + tone : '')} id={id}>
    <span className="empty-icon"><Icon name={icon} size={20} /></span>
    <div className="empty-title">{title}</div>
    {hint ? <div className="empty-hint">{hint}</div> : null}
    {children ? <div className="empty-actions">{children}</div> : null}
  </div>
);

// What one call actually did: shared by the inline expansion and the right inspector.
export function DetailBody({ row, now, snapshot, inline }) {
  const detail = row.detail;
  return (
    <>
      {detail && detail.is_error && detail.error ? <div className="alert"><span className="mark"><Icon name="circleAlert" size={14} /></span><div>{detail.error}</div></div> : null}
      <DetailViews row={row} now={now} />
      {!detail
        ? <Empty title="这次调用没有结构化详情" hint={staleServer(snapshot)
            ? '当前服务端是旧版本，没有返回文件、行号与命令输出。更新本地工作区插件并重启后，这里会显示每次调用实际做了什么。'
            : '调用返回后这里会显示它所做的事。'} />
        : null}
      {detail && detail.target && !['read', 'image', 'command'].includes(detail.kind)
        ? <div className="detail-foot" title={detail.target}><PathLink value={detail.target} /></div>
        : null}
      {!inline && (detail?.change_id || row?.id)
        ? (
          <details className="evidence">
            <summary>执行证据</summary>
            {row?.id ? <p>活动 ID：{row.id}</p> : null}
            {detail?.before_sha256 ? <p className="mono">修改前：{detail.before_sha256}<br />修改后：{detail.after_sha256}</p> : null}
            {detail?.change_id ? <p>文件恢复记录 ID：{detail.change_id}。可在原对话中要求预览撤销此记录；应用前会核对文件是否已被其他操作修改，命令产生的外部效果不在恢复范围内。</p> : null}
          </details>
        )
        : null}
    </>
  );
}

// Right-hand inspector: the selected call at full height. Docked as a resizable column on
// wide windows, a drawer over the stream on narrow ones.
export function DetailPane({ row, now, snapshot, onCopy, copied, onClose, width, onResize, docked }) {
  const startDrag = event => {
    if (!docked) return;
    event.preventDefault();
    const startX = event.clientX, startWidth = width;
    const move = moved => onResize(startWidth + (startX - moved.clientX));
    const up = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); document.body.classList.remove('resizing'); };
    document.body.classList.add('resizing');
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
  };
  const keys = event => {
    if (event.key === 'ArrowLeft') { event.preventDefault(); onResize(width + 16); }
    if (event.key === 'ArrowRight') { event.preventDefault(); onResize(width - 16); }
  };
  return (
    <section className={'detail-column' + (docked ? ' docked' : ' drawer')} aria-label="调用详情" style={docked ? { width } : undefined}
      onKeyDown={event => { if (!docked && event.key === 'Escape') onClose(); }}>
      {docked
        ? <div id="inspector-resize" className="resize-handle" role="separator" aria-orientation="vertical" aria-label="调整检查器宽度"
            aria-valuenow={Math.round(width)} tabIndex={0} onPointerDown={startDrag} onKeyDown={keys} />
        : null}
      <div className="detail-head">
        <div className="detail-line">
          {row ? <Icon name={iconOf(row)} size={16} className="detail-icon" /> : <Icon name="panelRight" size={16} className="detail-icon" />}
          <h2 id="detail-title" title={row ? (row.detail && row.detail.target) || row.target || '' : ''}>
            {row ? titleOf(row.tool) : '检查器'}
          </h2>
          <span className="spacer" />
          <Button id="copy-detail" variant="ghost" size="sm" hidden={!row} onClick={onCopy}><Icon name={copied ? 'check' : 'copy'} size={14} />{copied ? '已复制' : '复制'}</Button>
          <Button id="inspector-close" variant="ghost" size="icon-sm" aria-label="关闭检查器" title="关闭检查器" onClick={onClose}><Icon name="x" /></Button>
        </div>
        {row ? <div id="detail-state" className="detail-meta">{metaOf(row, now)}</div> : <div className="detail-meta">未选择调用</div>}
      </div>
      <div id="detail-body" tabIndex={0} aria-label="调用详情内容">
        {row
          ? <DetailBody row={row} now={now} snapshot={snapshot} />
          : <Empty icon="panelRight" title="选择一次调用" hint="这里显示它读取了哪些文件与行号、写入或替换了什么内容、执行了哪条命令以及返回结果。" />}
      </div>
    </section>
  );
}
