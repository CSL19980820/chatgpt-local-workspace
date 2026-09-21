import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { Icon, SpinIcon } from './icons.jsx';
import { TaskReceipt } from './task-receipt.jsx';

// The plan card sits above the timeline: collapsed it shows progress and the current step,
// expanded it shows the full checklist of every visible plan.
export function PlanCard({ plans, grouped, threadName, open, onToggle }) {
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
      <CollapsibleTrigger id="plan-toggle" className="plan-head" title="展开或收起执行计划">
        <span className="plan-mark"><Icon name="listChecks" /></span>
        <span className="plan-name">执行计划</span>
        <span id="plan-count" className="plan-count">{done} / {steps.length}</span>
        <span className="chevron"><Icon name="chevron" /></span>
      </CollapsibleTrigger>
      <div className="task-receipts">{(plans || []).map(plan => <TaskReceipt key={plan.thread_id + plan.path} task={plan.task} title={grouped ? threadName(plan.thread_id) : ''} />)}</div>
      <CollapsibleContent id="plan-body" className="plan-body">
        <div id="plan-bar" className="plan-bar">
          {steps.map((step, index) => <i key={index} className={step.status} />)}
        </div>
        <div className="plan-current">
          <span className="mark">{running ? <SpinIcon /> : <Icon name={done === steps.length ? 'check' : 'chevronRight'} />}</span>
          <span id="plan-current-text" className="ellipsis" title={current ? current.step : null}>
            {current ? current.step : (done === steps.length ? '步骤已登记完成，请核对验收证据' : '等待下一步')}
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
