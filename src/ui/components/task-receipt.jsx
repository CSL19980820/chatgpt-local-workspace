import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Icon } from './icons.jsx';

const labels = { active: '任务进行中', running: '工具正在执行', idle_unconfirmed: '暂无新调用', verification_required: '步骤已完成', needs_attention: '最近执行有问题', blocked: '已记录阻塞', paused: '按用户要求暂停', ready: '完成检查通过' };
export function TaskReceipt({ task, title }) {
  const [copied, setCopied] = useState(false), [error, setError] = useState('');
  useEffect(() => { setCopied(false); setError(''); }, [task?.resume_prompt]);
  if (!task) return null;
  async function copy() {
    try { await navigator.clipboard.writeText(task.resume_prompt); setCopied(true); setError(''); }
    catch { setError('未能复制，请在下方选取续做提示。'); }
  }
  return <div className={'task-receipt ' + task.state}>
    {title ? <div className="task-owner">{title}</div> : null}
    <div className="task-receipt-head"><strong>{labels[task.state] || '待检查'}</strong><span>{task.unfinished_steps?.length ? task.unfinished_steps.length + ' 项未完成' : task.missing_evidence?.length ? '证据未逐项登记' : '全部步骤已登记完成'}</span></div>
    <p>{task.state === 'verification_required' ? '计划中的步骤均已标记完成。证据尚未填写到逐项字段，不代表实际工作没有完成。' : task.next_action}</p>
    {task.reason ? <p className="task-reason">原因：{task.reason}</p> : null}
    {task.last_issue ? <details className="task-history"><summary>{task.state === 'needs_attention' ? '最近执行问题' : '历史执行记录'}</summary><p className="task-reason">{task.last_issue}{task.last_issue_at ? ' · ' + new Date(task.last_issue_at).toLocaleString('zh-CN', { hour12: false }) : ''}</p></details> : null}
    {task.missing_evidence?.length ? <details className="task-history"><summary>{task.missing_evidence.length} 项证据未逐项登记</summary><ul>{task.missing_evidence.map((step, index) => <li key={index}>{step}</li>)}</ul></details> : null}
    {task.state === 'idle_unconfirmed' ? <p className="note">超过 2 分钟没有新操作；可能仍在思考，不能据此判断已停止。</p> : null}
    {task.can_finish ? <p className="note">依据登记的证据与本地执行状态，不代替独立验收。</p> : <>
      <Button variant="secondary" size="sm" onClick={copy}><Icon name="copy" />{copied ? '已复制' : task.state === 'verification_required' ? '复制核对提示' : '复制续做提示'}</Button>
      {error ? <details open><summary>{error}</summary><p className="resume-text">{task.resume_prompt}</p></details> : null}
      <p className="note">需在原对话中发送提示；工作台不会自动唤醒模型。</p>
    </>}
  </div>;
}
