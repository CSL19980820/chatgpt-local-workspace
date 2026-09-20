import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '@/components/ui/dialog';
import { Icon } from './icons.jsx';

const labels = { pass: '通过', fail: '需处理', pending: '待验证', unavailable: '未检查' };
const readable = text => (text || '').replace(/\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z/g, value => new Date(value).toLocaleString('zh-CN', { hour12: false }));
export function DiagnosticsDialog({ open, onOpenChange }) {
  const [report, setReport] = useState(null), [error, setError] = useState(''), [loading, setLoading] = useState(false);
  async function check() {
    setLoading(true); setError('');
    try {
      const response = await fetch('/api/diagnostics', { cache: 'no-store', signal: AbortSignal.timeout(15000) });
      if (!response.ok) throw Error('当前服务暂不能诊断，请确认已启动 2.1 版程序。');
      setReport(await response.json());
    } catch (err) { setError(err.name === 'TimeoutError' ? '检查超时，请稍后重试。' : err.message); }
    finally { setLoading(false); }
  }
  useEffect(() => { if (open) check(); }, [open]);
  return <Dialog open={open} onOpenChange={onOpenChange}>
    <DialogContent className="diagnostics-dialog">
      <DialogHeader><DialogTitle>连接诊断</DialogTitle><DialogDescription>逐项检查当前配置与实际收到的请求，定位连接卡在哪一步。</DialogDescription></DialogHeader>
      <div className="diagnostics-actions"><span>{report ? 'v' + report.version + ' · ' + new Date(report.checked_at).toLocaleTimeString('zh-CN') : '本地工作区'}</span><Button variant="secondary" size="sm" onClick={check} disabled={loading}><Icon name="refresh" />{loading ? '检查中…' : '重新检查'}</Button></div>
      {error ? <div className="alert" role="alert">{error}</div> : null}
      {!report && loading ? <p role="status">正在检查配置、隧道和工具调用…</p> : null}
      <div className="diagnostics-checks">{(report?.checks || []).map((check, index) => <div className="diagnostic-check" key={index}><div><strong>{check.label}</strong><p>{readable(check.detail)}</p></div><span className={'diagnostic-status ' + check.status}>{labels[check.status] || '未知'}</span></div>)}</div>
      {report?.last_failure ? <p className="note">最近失败调用：{new Date(report.last_failure).toLocaleString('zh-CN')}</p> : null}
      <p className="note">{report?.scope || '检查不会重新启动连接，不会执行工作区命令。'}</p>
    </DialogContent>
  </Dialog>;
}
