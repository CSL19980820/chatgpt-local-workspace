import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { DetailViews } from './views.jsx';
import { Icon } from './icons.jsx';
import { duration, stamp } from '@/lib/format.js';
import { KIND_ICON, titleOf } from '@/lib/labels.js';
import { isRunning, liveElapsed, staleServer, stateText } from '@/lib/rows.js';

const Empty = ({ title, hint }) => (
  <div className="empty">
    <Icon name="info" />
    <div>{title}</div>
    <div className="muted">{hint}</div>
  </div>
);

// Right-hand inspector: what the selected call actually did, and when it started.
export function DetailPane({ row, now, snapshot, onCopy, copied }) {
  const detail = row && row.detail;
  const kind = detail && detail.kind;
  const running = row ? isRunning(row) : false;
  const elapsed = row ? liveElapsed(row, now) : 0;
  const meta = row
    ? [stateText(row), '开始 ' + stamp(row.started_at), '耗时 ' + duration(elapsed), running ? '仍在运行' : ''].filter(Boolean).join(' · ')
    : '';
  return (
    <section className="detail-column" aria-label="调用详情">
      <div className="detail-head">
        <span className="detail-icon"><Icon name={row ? KIND_ICON[kind] || 'info' : 'info'} /></span>
        <h2 id="detail-title" title={row ? (detail && detail.target) || row.target || '' : ''}>
          {row ? titleOf(row.tool) : '调用详情'}
        </h2>
        <Separator orientation="vertical" className="h-4" />
        <span id="detail-state" className="detail-meta">{meta}</span>
        <span className="spacer" />
        <Button id="copy-detail" variant="ghost" size="xs" hidden={!row} onClick={onCopy}>{copied ? '已复制' : '复制'}</Button>
      </div>
      <div id="detail-body" tabIndex={0} aria-label="调用详情内容">
        {!row
          ? <Empty title="在左侧时间线选择一次调用" hint="这里显示它读取了哪些文件与行号、写入或替换了什么内容、执行了哪条命令以及返回结果。" />
          : (
            <>
              {detail && detail.is_error && detail.error ? <div className="alert"><span className="mark"><Icon name="alert" /></span><div>{detail.error}</div></div> : null}
              <DetailViews row={row} now={now} />
              {!detail
                ? <Empty title="这次调用没有结构化详情" hint={staleServer(snapshot)
                    ? '当前服务端是旧版本，没有返回文件、行号与命令输出。更新本地工作区插件并重启后，这里会显示每次调用实际做了什么。'
                    : '调用返回后这里会显示它所做的事。'} />
                : null}
              {detail && detail.target && detail.kind !== 'read'
                ? (
                  <>
                    <Separator className="mt-2" />
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <div className="detail-foot"><span>{detail.target}</span></div>
                      </TooltipTrigger>
                      <TooltipContent>{detail.target}</TooltipContent>
                    </Tooltip>
                  </>
                )
                : null}
            </>
          )}
      </div>
    </section>
  );
}
