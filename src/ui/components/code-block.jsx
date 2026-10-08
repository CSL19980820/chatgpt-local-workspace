import { useEffect, useRef, useState } from 'react';
import { cn } from 'cn';
import { Icon } from './icons.jsx';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';

// Small ghost copy button with a tooltip; flips to a check for a moment after copying.
export function CopyButton({ text, label = '复制', className }) {
  const [done, setDone] = useState(false);
  const timer = useRef(null);
  useEffect(() => () => clearTimeout(timer.current), []);
  if (!text) return null;
  const copy = async event => {
    event.stopPropagation();
    try { await navigator.clipboard.writeText(text); } catch { return; }
    setDone(true);
    clearTimeout(timer.current);
    timer.current = setTimeout(() => setDone(false), 1400);
  };
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <button type="button" className={cn('icon-btn copy-btn', className)} aria-label={done ? '已复制' : label} onClick={copy}>
          <Icon name={done ? 'check' : 'copy'} size={14} />
        </button>
      </TooltipTrigger>
      <TooltipContent>{done ? '已复制' : label}</TooltipContent>
    </Tooltip>
  );
}

// A bordered code surface: a quiet head (glyph, dimmed folder, file name, counts, actions),
// the body, then an optional foot with plain facts.
export function CodeBlock({ icon, dir, title, hint, right, copy, copyLabel, actions, foot, className, children }) {
  return (
    <div className={cn('code-block', className)}>
      {(title || icon || copy || actions)
        ? <div className="code-head">
            {icon ? <Icon name={icon} size={14} className="code-icon" /> : null}
            <span className="code-name" title={hint || undefined}>
              {dir ? <span className="card-dir">{dir}</span> : null}
              {title ? <span className="card-title" title={hint || undefined}>{title}</span> : null}
            </span>
            {right ? <span className="code-right">{right}</span> : null}
            <span className="code-actions">{actions}<CopyButton text={copy} label={copyLabel} /></span>
          </div>
        : null}
      {children}
      {foot ? <div className="code-foot">{foot}</div> : null}
    </div>
  );
}

// Long bodies render a window of lines and a plain "show more" bar instead of a fade.
// fromEnd keeps the last lines (command output), otherwise the first ones.
export function LineWindow({ lines, limit = 40, fromEnd = false, render, className, unit = '行' }) {
  const [all, setAll] = useState(false);
  const hidden = all ? 0 : Math.max(0, lines.length - limit);
  const shown = hidden ? (fromEnd ? lines.slice(lines.length - limit) : lines.slice(0, limit)) : lines;
  const offset = hidden && fromEnd ? lines.length - limit : 0;
  const more = hidden
    ? <button type="button" className="show-more" onClick={() => setAll(true)}>
        <Icon name={fromEnd ? 'chevronUp' : 'chevron'} size={14} />{fromEnd ? '显示前面 ' + hidden + ' ' + unit : '显示其余 ' + hidden + ' ' + unit}
      </button>
    : null;
  return (
    <>
      {fromEnd ? more : null}
      <div className={className + (all ? ' all' : '')}>{shown.map((line, index) => render(line, index + offset))}</div>
      {fromEnd ? null : more}
    </>
  );
}
