import { useState } from 'react';
import { Icon, fileIcon } from './icons.jsx';
import { Counts } from './timeline.jsx';
import { commonDir, splitPath } from '@/lib/format.js';

const VERB = { add: '新建', delete: '删除', move: '移动' };

// Codex-style summary under the stream: every file the visible calls changed, folder dimmed
// and name in bold, line counts on the right. Clicking a file jumps to the call that last
// changed it.
// Long lists stop after a few files so the summary never pushes the latest calls out of view.
const SHOWN = 5;
export function ChangesCard({ files, omitted, scope, root, total, onOpen }) {
  const [all, setAll] = useState(false);
  if (!files.length) return null;
  const rest = files.length - SHOWN > 1 ? files.length - SHOWN : 0;
  const listed = rest && !all ? files.slice(0, SHOWN) : files;
  const paths = files.map(file => file.path);
  const base = root && paths.every(path => path.replace(/\\/g, '/').toLowerCase().startsWith(root.replace(/\\/g, '/').replace(/\/+$/, '').toLowerCase() + '/'))
    ? root : commonDir(paths);
  return (
    <section id="changes-card" className="changes-card" aria-label="文件改动汇总">
      <div className="changes-head">
        <Icon name="filePen" size={16} className="changes-icon" />
        <span className="changes-title">{scope}改动了 {files.length} 个文件</span>
        {total ? <Counts added={total.added} removed={total.removed} /> : null}
      </div>
      <div className="changes-list">
        {listed.map(file => {
          const { dir, name } = splitPath(file.path, base);
          return (
            <button type="button" key={file.path} className="change-row" title={file.path + ' · 跳到这次改动'} onClick={() => onOpen(file.rowId)}>
              <Icon name={fileIcon(file.path)} size={16} className="change-icon" />
              <span className="change-path"><span className="change-dir">{dir}</span><span className="change-name">{file.name || name}</span></span>
              {VERB[file.operation] ? <span className="change-op">{VERB[file.operation]}</span> : null}
              <Counts added={file.added} removed={file.removed} />
            </button>
          );
        })}
        {rest ? (
          <button type="button" id="changes-more" className="change-toggle" aria-expanded={all} onClick={() => setAll(!all)}>
            <Icon name={all ? 'chevronUp' : 'chevron'} size={14} />{all ? '收起' : '显示其余 ' + rest + ' 个文件'}
          </button>
        ) : null}
        {omitted && (all || !rest) ? <div className="change-more">另有 {omitted} 个文件未在列表中展开</div> : null}
      </div>
    </section>
  );
}
