import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { Icon, SpinIcon } from './icons.jsx';
import { TaskReceipt } from './task-receipt.jsx';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '@/components/ui/dialog';

// The plan card sits above the timeline: collapsed it shows progress and the current step,
// expanded it shows the full checklist of every visible plan.
export function PlanCard({ plans, grouped, threadName, open, onToggle }) {
  const [detailsOpen, setDetailsOpen] = useState(false);
  const steps = [];
  for (const plan of plans || []) for (const step of plan.plan || []) steps.push({ step: step.step, status: step.status });
  if (!steps.length) return null;
  const done = steps.filter(step => step.status === 'completed').length;
  const current = steps.find(step => step.status === 'in_progress');
  const running = (plans || []).some(plan => plan.task?.running);

  const stepIcon = status => (
    status === 'completed' ? <Icon name="check" />
      : status === 'in_progress' ? <Icon name="chevronRight" />
        : <Icon name="circle" />
  );

  return (
    <Collapsible id="plan-card" className={'plan-card' + ((plans || []).every(plan => plan.task?.can_finish) ? ' done' : '')} open={open} onOpenChange={onToggle}>
      <div className="plan-heading"><CollapsibleTrigger id="plan-toggle" className="plan-head" title="展开或收起执行计划">
        <span className="plan-mark"><Icon name="listChecks" /></span>
        <span className="plan-name">执行计划</span>
        <span id="plan-count" className="plan-count">{done} / {steps.length}</span>
        <span className="chevron"><Icon name="chevron" /></span>
      </CollapsibleTrigger>
      {(plans || []).some(plan => plan.task) ? <Button id="task-details" variant="ghost" size="xs" onClick={() => setDetailsOpen(true)} title="查看任务状态和续做操作">任务详情</Button> : null}</div>
      <Dialog open={detailsOpen} onOpenChange={setDetailsOpen}>
        <DialogContent className="task-dialog"><DialogHeader><DialogTitle>任务详情</DialogTitle><DialogDescription>需要时查看进度、核对记录或复制提示；你也可以直接在原对话中发送“继续”。</DialogDescription></DialogHeader>
          <div className="task-receipts">{(plans || []).map(plan => <TaskReceipt key={plan.thread_id + plan.path} task={plan.task} title={grouped ? threadName(plan.thread_id) : ''} />)}</div>
        </DialogContent>
      </Dialog>
      <CollapsibleContent id="plan-body" className="plan-body">
        <div id="plan-bar" className="plan-bar">
          {steps.map((step, index) => <i key={index} className={step.status} />)}
        </div>
        <div className="plan-current">
          <span className="mark">{running ? <SpinIcon /> : <Icon name={done === steps.length ? 'check' : 'chevronRight'} />}</span>
          <span id="plan-current-text" className="ellipsis" title={current ? current.step : null}>
            {current ? current.step : (done === steps.length ? '步骤已完成' : '等待下一步')}
          </span>
        </div>
        <div id="plan-steps" className="plan-steps">
          {(plans || []).map(plan => (
            <div key={plan.thread_id + plan.updated_at}>
              {grouped ? <div className="plan-group">{threadName(plan.thread_id)}</div> : null}
              {plan.explanation ? <div className="plan-explain">{plan.explanation}</div> : null}
              {(plan.plan || []).map((step, index) => (
                <div key={index} className={'plan-step ' + (step.status || 'pending')}>
                  <span className="mark">{stepIcon(step.status)}</span>
                  <span>{step.step}</span>
                </div>
              ))}
            </div>
          ))}
        </div>
      </CollapsibleContent>
    </Collapsible>
  );
}
