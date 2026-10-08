import { useEffect, useState } from 'react';
import { cn } from 'cn';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '@/components/ui/dialog';
import { Icon } from './icons.jsx';
import { versionView } from '@/lib/adapter.js';

const labels = { pass: '通过', fail: '需处理', pending: '待验证', unavailable: '未检查' };
const readable = text => (text || '').replace(/\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z/g, value => new Date(value).toLocaleString('zh-CN', { hour12: false }));

// One component per row: which version is running now, which one is on disk, and one plain
// status (contract §4/§7.2: ok | restart_required | unknown_running | missing).
const STATUS = {
  ok: ['版本一致', 'ok'],
  restart_required: ['重启后才会使用新版本', 'warn'],
  unknown_running: ['运行版本未知', 'muted'],
  missing: ['磁盘上没有找到程序文件', 'warn'],
};
function VersionRow({ item }) {
  const component = item;
  const running = component.running ? '正在运行 ' + component.running : '正在运行 —';
  const installed = component.installed ? '磁盘文件 ' + component.installed : '磁盘文件 —';
  const [statusText, tone] = STATUS[component.status] || ['状态未知', 'muted'];
  return (
    <div className="diag-row component-version">
      <div className="diag-main">
        <strong>{component.label}</strong>
        {component.detail ? <p>{component.detail}</p> : null}
      </div>
      <div className="diag-side tabular">
        <span>{running}</span>
        <span>{installed}</span>
        <em className={cn('version-status', tone)} data-status={component.status}>{statusText}</em>
      </div>
    </div>
  );
}

export function DiagnosticsDialog({ open, onOpenChange }) {
  const [report, setReport] = useState(null), [error, setError] = useState(''), [loading, setLoading] = useState(false);
  async function check() {
    setLoading(true); setError('');
    try {
      const response = await fetch('/api/diagnostics', { cache: 'no-store', signal: AbortSignal.timeout(15000) });
      if (!response.ok) throw Error('当前服务暂不能诊断，请确认本地程序已启动并完成升级。');
      setReport(await response.json());
    } catch (err) { setError(err.name === 'TimeoutError' ? '检查超时，请稍后重试。' : err.message); }
    finally { setLoading(false); }
  }
  useEffect(() => { if (open) check(); }, [open]);
  return <Dialog open={open} onOpenChange={onOpenChange}>
    <DialogContent className="diagnostics-dialog">
      <DialogHeader>
        <DialogTitle>连接诊断</DialogTitle>
        <DialogDescription>逐项检查当前配置与实际收到的请求，定位连接卡在哪一步。</DialogDescription>
      </DialogHeader>
      <div className="diagnostics-actions">
        <span>{report ? 'v' + report.version + ' · 检查于 ' + new Date(report.checked_at).toLocaleTimeString('zh-CN', { hour12: false }) : '本地工作区'}</span>
        <Button variant="secondary" size="sm" onClick={check} disabled={loading}><Icon name="refresh" size={14} />{loading ? '检查中…' : '重新检查'}</Button>
      </div>
      {error ? <div className="alert" role="alert"><span className="mark"><Icon name="alert" size={14} /></span><div>{error}</div></div> : null}
      {!report && loading ? <p className="note" role="status">正在检查配置、隧道和工具调用…</p> : null}
      {report?.versions?.length
        ? <section className="diag-section" aria-label="组件版本">
            <h3>组件版本</h3>
            <div className="diag-list component-versions">{report.versions.map(versionView).map(item => <VersionRow key={item.key} item={item} />)}</div>
          </section>
        : null}
      {report?.checks?.length
        ? <section className="diag-section" aria-label="检查项">
            <h3>检查项</h3>
            <div className="diag-list diagnostics-checks">
              {report.checks.map((item, index) => (
                <div className="diag-row diagnostic-check" key={index}>
                  <div className="diag-main"><strong>{item.label}</strong><p>{readable(item.detail)}</p></div>
                  <span className={cn('diagnostic-status', item.status)}><i className="dot" />{labels[item.status] || '未知'}</span>
                </div>
              ))}
            </div>
          </section>
        : null}
      {report?.last_failure ? <p className="note">最近一次失败的调用：{new Date(report.last_failure).toLocaleString('zh-CN', { hour12: false })}</p> : null}
      <p id="diagnostics-scope" className="note">{report?.scope || '检查只读取状态：不会重启连接，也不会执行工作区命令。'}</p>
    </DialogContent>
  </Dialog>;
}
