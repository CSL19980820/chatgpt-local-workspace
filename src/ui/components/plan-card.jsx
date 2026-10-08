import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { Icon, SpinIcon } from './icons.jsx';
import { TaskReceipt } from './task-receipt.jsx';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '@/components/ui/dialog';

// The plan sits in its own band between the header and the stream (never floating over
// rows). Collapsed: progress segments, "1/5" and the current step. Expanded: every step
// with a check, a dot for the current one, an empty ring for what is left.
export function PlanCard({ plans, grouped, threadName, open, onToggle }) {
  const [detailsOpen, setDetailsOpen] = useState(false);
  const steps = [];
  for (const plan of plans || []) for (const step of plan.plan || []) steps.push({ step: step.step, status: step.status });
  if (!steps.length) return null;
  const done = steps.filter(step => step.status === 'completed').length;
  const current = steps.find(step => step.status === 'in_progress');
  const running = (plans || []).some(plan => plan.task?.running);
  const finished = done === steps.length;

  const stepIcon = status => (
    status === 'completed' ? <Icon name="circleCheck" size={16} />
      : status === 'in_progress' ? (running ? <SpinIcon size={14} /> : <Icon name="dot" size={16} />)
        : <Icon name="circle" size={16} />
  );

  return (
    <Collapsible id="plan-card" className={'plan-card' + (finished ? ' done' : '')} open={open} onOpenChange={onToggle}>
      <div className="plan-inner">
        <div className="plan-heading">
          <CollapsibleTrigger id="plan-toggle" className="plan-head" title={open ? '收起计划' : '展开计划'}>
            <Icon name="listChecks" size={16} className="plan-icon" />
            <span className="plan-name">计划</span>
            <span className="plan-progress" aria-hidden="true">
              {steps.map((step, index) => <i key={index} className={step.status || 'pending'} />)}
            </span>
            <span id="plan-count" className="plan-count tabular">{done}/{steps.length}</span>
            <span id="plan-current-text" className="plan-current" title={current ? current.step : null}>
              {current ? current.step : (finished ? '步骤已全部完成' : '等待下一步')}
            </span>
            <Icon name="chevron" size={16} className="chevron" />
          </CollapsibleTrigger>
          {(plans || []).some(plan => plan.task)
            ? <Button id="task-details" variant="ghost" size="xs" onClick={() => setDetailsOpen(true)} title="查看任务状态和续做操作">任务详情</Button>
            : null}
        </div>
        <CollapsibleContent id="plan-body" className="plan-body">
          <div id="plan-steps" className="plan-steps">
            {(plans || []).map(plan => (
              <div key={plan.thread_id + plan.updated_at} className="plan-group-wrap">
                {grouped ? <div className="plan-group">{threadName(plan.thread_id)}</div> : null}
                {plan.explanation ? <div className="plan-explain">{plan.explanation}</div> : null}
                {(plan.plan || []).map((step, index) => (
                  <div key={index} className={'plan-step ' + (step.status || 'pending')}>
                    <span className="mark">{stepIcon(step.status)}</span>
                    <span className="plan-step-text">{step.step}</span>
                  </div>
                ))}
              </div>
            ))}
          </div>
        </CollapsibleContent>
      </div>
      <Dialog open={detailsOpen} onOpenChange={setDetailsOpen}>
        <DialogContent className="task-dialog">
          <DialogHeader><DialogTitle>任务详情</DialogTitle><DialogDescription>需要时查看进度、核对记录或复制提示；你也可以直接在原对话中发送“继续”。</DialogDescription></DialogHeader>
          <div className="task-receipts">{(plans || []).map(plan => <TaskReceipt key={plan.thread_id + plan.path} task={plan.task} title={grouped ? threadName(plan.thread_id) : ''} />)}</div>
        </DialogContent>
      </Dialog>
    </Collapsible>
  );
}
