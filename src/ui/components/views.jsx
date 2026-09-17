import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Separator } from '@/components/ui/separator';
import { Icon, SpinIcon } from './icons.jsx';
import { bytes, duration } from '@/lib/format.js';
import { OPERATIONS } from '@/lib/labels.js';
import { liveElapsed } from '@/lib/rows.js';

// shadcn Card keeps the shell, the dense ".card" recipe keeps the data-grid look.
function Box({ badge, badgeClass, title, hint, right, children, className }) {
  return (
    <Card className={'card gap-0 py-0 rounded-lg ' + (className || '')}>
      {(badge || title)
        ? <CardHeader className="card-head flex flex-row items-center gap-2 p-0">
            {badge ? <Badge variant="secondary" className={'badge ' + (badgeClass || '')}>{badge}</Badge> : null}
            {title ? <span className="card-title" title={hint || undefined}>{title}</span> : null}
            {right ? <span className="counts">{right}</span> : null}
          </CardHeader>
        : null}
      {children}
    </Card>
  );
}

const Counts = ({ added, removed }) => (
  <span className="counts"><span className="plus">+{added}</span> <span className="minus">−{removed}</span></span>
);

const Rows = ({ rows }) => (
  <div className="rows">
    {rows.filter(row => row && row.value !== undefined && row.value !== null && row.value !== '').map((row, index) => (
      <div key={index} className={'row' + (row.stack ? ' stack' : '')}>
        <span className="label">{row.label}</span>
        <span className={'value' + (row.mono ? ' mono' : '')}>{row.value}</span>
      </div>
    ))}
  </div>
);

const Note = ({ children }) => <div className="note">{children}</div>;
const Alert = ({ children }) => <div className="alert"><span className="mark"><Icon name="alert" /></span><div>{children}</div></div>;
const Output = ({ text, region }) => <pre className={'output' + (region ? ' region' : '') + (text ? '' : ' empty-output')}>{text || '等待输出…'}</pre>;

function Diff({ diff, region }) {
  if (!diff) return null;
  return (
    <div className={'diff' + (region ? ' region' : '')}>
      {(diff.rows || []).map((row, index) => (
        <div key={index} className={'diff-row ' + (row.kind || 'context')}>
          <span className="diff-no">{row.old_line == null ? '' : row.old_line}</span>
          <span className="diff-no">{row.new_line == null ? '' : row.new_line}</span>
          <span className="diff-mark">{row.kind === 'add' ? '+' : row.kind === 'remove' ? '-' : ' '}</span>
          <span className="diff-text">{row.text || ''}</span>
        </div>
      ))}
      {diff.identical ? <Note>写入内容与原有内容完全一致。</Note> : null}
      {diff.truncated ? <Note>差异较大，这里只显示一部分。</Note> : null}
      {diff.coarse ? <Note>差异按行尾对齐，可能不是最小改动。</Note> : null}
    </div>
  );
}

const MODE = {
  add: '新建文件', replace: '整体覆盖原有内容', edit: '精确替换第一处匹配', update: '按补丁更新',
};

// One card per structured detail kind, mirroring src/WorkspaceDetail.cs.
export function DetailViews({ row, now }) {
  const detail = row.detail;
  if (!detail) return null;
  const kind = detail.kind;

  if (kind === 'write') {
    // A rejected write reports paths only: no operation, size or diff may be shown.
    const rejected = detail.applied === false;
    return (
      <>
        {(detail.files || []).map((file, index) => {
          const diff = file.diff || {};
          const rows = [{ label: '位置', value: file.path, mono: true }];
          if (file.previous_path) rows.push({ label: '移动自', value: file.previous_path, mono: true });
          if (rejected) rows.push({ label: '结果', value: '这次调用没有写入这个文件' });
          if (!rejected && file.size_bytes) rows.push({ label: '写入大小', value: bytes(file.size_bytes) });
          if (!rejected && MODE[file.operation]) rows.push({ label: '写入方式', value: MODE[file.operation] });
          return (
            <Box key={index} badge={rejected ? '未写入' : OPERATIONS[file.operation] || '修改'} badgeClass={rejected ? 'failed' : file.operation}
              className={index === 0 && !rejected ? 'fill' : ''}
              title={file.name || file.path} right={!rejected && (diff.added || diff.removed) ? <Counts added={diff.added || 0} removed={diff.removed || 0} /> : null}>
              <Rows rows={rows} />
              {rejected ? null : <Diff diff={file.diff} region={index === 0} />}
            </Box>
          );
        })}
        {detail.replace
          ? <Box badge="替换文本">
              <CardContent className="card-body p-0">
                <div className="row stack"><span className="label">替换后</span><div className="card-text mono">{detail.replace.after.text || '（空）'}</div></div>
                <div className="row stack"><span className="label">原内容</span><div className="card-text mono">{detail.replace.before.text || '（空）'}</div></div>
              </CardContent>
            </Box>
          : null}
        {detail.omitted_files ? <Note>另有 {detail.omitted_files} 个文件未在此处展开。</Note> : null}
        {detail.root ? <Note>工作目录 {detail.root}</Note> : null}
        {detail.partial ? <Alert>补丁没有全部写入，上面只列出已经生效的文件。</Alert> : null}
      </>
    );
  }

  if (kind === 'read') {
    const content = detail.content || [];
    const read = (detail.reads || [])[0] || {};
    const name = detail.name || read.name || detail.path || '';
    const size = detail.file_bytes == null ? '' : bytes(detail.file_bytes);
    const range = read.returned_count ? '第 ' + read.start_line + '–' + read.end_line + ' 行' : '未读到内容';
    const tail = detail.at_end ? '读到文件结尾' : detail.next_line ? '可继续从第 ' + detail.next_line + ' 行' : '';
    const facts = (detail.binary ? [size] : [size, range, tail]).filter(Boolean);
    // A read is about the file itself, so the panel shows its text: the path stays in the
    // card title and its tooltip instead of taking a row of its own.
    return (
      <>
        <Box badge={detail.binary ? '不是文本' : '读取'} title={name} hint={detail.path} className={content.length ? 'fill' : ''}
          right={detail.binary ? size : range}>
          {content.length
            ? (
              <div className="file-body region">
                {content.map((line, index) => (
                  <div key={index} className="file-line">
                    <span className="file-no">{line.n == null ? '' : line.n}</span>
                    <span className="file-text">{line.text === '' ? '\u00a0' : line.text}</span>
                  </div>
                ))}
              </div>
            )
            : <CardContent className="card-body p-0"><Note>{detail.binary ? '这个文件不是文本文件。' : read.empty && !detail.is_error ? '文件是空的，没有内容可以展开。' : '这次没有读到内容。'}</Note></CardContent>}
          <div className="status-line">
            {facts.map((fact, index) => <span key={index}>{fact}</span>)}
            {detail.truncated ? <span className="bad">内容超过 10 KB，只展开了一部分</span> : null}
          </div>
        </Box>
        {(detail.notes || []).map((note, index) => <Note key={index}>{note}</Note>)}
      </>
    );
  }

  if (kind === 'search') {
    const matches = detail.matches || [];
    const notes = [];
    if (detail.complete === false) notes.push('结果是部分结果。');
    if (detail.truncated) notes.push('已达到扫描预算。');
    if (detail.skipped_paths) notes.push(detail.skipped_paths + ' 个路径被跳过。');
    if (detail.scanned_files) notes.push('已扫描 ' + detail.scanned_files + ' 个文件。');
    if (detail.note) notes.push(detail.note);
    return (
      <>
        <Box badge="搜索" title={detail.query ? '"' + detail.query + '"' : detail.pattern || '*'} className="fill" right={detail.returned_count + ' 处'}>
          <div className="card-body flush region">
            {matches.length
              ? matches.map((match, index) => (
                  <div key={index} className="match">
                    <div className="match-place">
                      <span className="card-title">{match.name || match.path}</span>
                      <span className="place">{match.line == null ? '' : ':' + match.line}{match.column == null ? '' : ':' + match.column}</span>
                      <span className="place">{match.path}</span>
                    </div>
                    <div className="match-text">{match.text || ''}</div>
                  </div>
                ))
              : <div className="card-body"><Note>没有匹配项。</Note></div>}
          </div>
        </Box>
        {notes.length ? <Note>{notes.join(' ')}</Note> : null}
      </>
    );
  }

  if (kind === 'command') {
    const live = row.session;
    const running = live ? live.running : !!detail.running;
    const output = (live ? live.output : detail.output_tail) || '';
    const exitCode = live ? live.exit_code : detail.exit_code;
    const seconds = (live && live.elapsed_seconds != null ? live.elapsed_seconds : detail.elapsed_seconds) || 0;
    const elapsed = running ? liveElapsed(row, now) : seconds * 1000;
    const timedOut = (live && live.timed_out) || detail.timed_out;
    const stopped = (live && live.stopped) || detail.stopped;
    return (
      <Box className="fill">
        <CardContent className="card-body p-0">
          <div className="command"><span className="prompt">$ </span>{detail.command || detail.summary || ''}</div>
        </CardContent>
        <Output text={output} region />
        <div className="status-line">
          <Badge variant={running ? 'default' : 'secondary'} className={'badge ' + (running ? 'running' : 'returned')}>
            {running ? <><SpinIcon size={11} /> 进行中</> : '退出码 ' + (exitCode == null ? '—' : exitCode)}
          </Badge>
          {timedOut ? <span className="bad">已超时</span> : null}
          {stopped ? <span className="bad">已停止</span> : null}
          <span>运行 {duration(elapsed)}</span>
          {detail.shell ? <span>{detail.shell}</span> : null}
          {detail.cwd ? <span>{detail.cwd}</span> : null}
        </div>
      </Box>
    );
  }

  if (kind === 'list') {
    const entries = detail.entries || [];
    return (
      <Box badge="目录" title={detail.path || '磁盘根目录'} className="fill" right={detail.total_entries + ' 项'}>
        <div className="card-body flush region">
          {entries.length
            ? entries.map((entry, index) => (
                <div key={index} className="entry">
                  <span className="mark"><Icon name={entry.directory ? 'folder' : 'file'} /></span>
                  <span className="name">{entry.name}</span>
                </div>
              ))
            : <div className="card-body"><Note>目录是空的。</Note></div>}
        </div>
        {detail.omitted ? <CardContent className="card-body p-0"><Note>另有 {detail.omitted} 项未在此处展开。</Note></CardContent> : null}
      </Box>
    );
  }

  if (kind === 'text') {
    const raw = String(detail.body || '');
    const lines = raw.replace(/\r\n/g, '\n').split('\n');
    return (
      <Box badge={detail.text_label || '输出'} title={detail.path || detail.text_label || ''} className="fill">
        <div className="text-body region">
          {raw.trim()
            ? lines.map((line, index) => (
                <div key={index} className={'text-line ' + (line.startsWith('+') && !line.startsWith('+++') ? 'add' : line.startsWith('-') && !line.startsWith('---') ? 'remove' : line.startsWith('@@') ? 'hunk' : '')}>{line}</div>
              ))
            : <div className="text-line muted">（无输出）</div>}
        </div>
        <div className="status-line">
          {detail.added || detail.removed ? <span>+{detail.added} −{detail.removed}</span> : null}
          {detail.exit_code ? <span className="bad">退出码 {detail.exit_code}</span> : null}
          {detail.timed_out ? <span className="bad">已超时</span> : null}
          {detail.truncated ? <span>内容已截断</span> : null}
        </div>
      </Box>
    );
  }

  if (kind === 'plan') {
    const done = detail.done || 0, total = detail.total || 0;
    return (
      <Box badge="计划" title={done + ' / ' + total} className="fill">
        <CardContent className="card-body p-0">
          <Progress value={total ? (done / total) * 100 : 0} className="h-1.5" />
          {detail.explanation ? <div className="card-text mt-2">{detail.explanation}</div> : null}
        </CardContent>
        <div className="steps region">
          {(detail.steps || []).map((step, index) => (
            <div key={index} className={'plan-step ' + (step.status || 'pending')}>
              <span className="mark">{step.status === 'completed' ? <Icon name="check" /> : step.status === 'in_progress' ? <SpinIcon /> : <Icon name="circle" />}</span>
              <span>{step.step}</span>
            </div>
          ))}
        </div>
      </Box>
    );
  }

  if (kind === 'info') {
    return (
      <Box badge={detail.summary || '信息'} title={detail.target || ''}>
        <Rows rows={detail.info || []} />
      </Box>
    );
  }

  return <Note>这次调用没有可展开的结构化详情。</Note>;
}
