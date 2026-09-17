// Plain-text export of one call, used by the inspector's copy button.
export function copyText(row) {
  const detail = row && row.detail;
  if (!detail) return '';
  if (detail.kind === 'command') {
    const live = row.session || {};
    return '$ ' + (detail.command || '') + '\n\n' + (live.output || detail.output_tail || '');
  }
  if (detail.kind === 'text') return detail.body || '';
  if (detail.kind === 'write') {
    const out = [];
    for (const file of detail.files || []) {
      out.push(file.path);
      for (const line of (file.diff && file.diff.rows) || []) {
        out.push((line.kind === 'add' ? '+' : line.kind === 'remove' ? '-' : ' ') + line.text);
      }
    }
    return out.join('\n');
  }
  if (detail.kind === 'read') return (detail.reads || []).map(read => read.path + ' · 第 ' + read.start_line + '–' + read.end_line + ' 行').join('\n');
  if (detail.kind === 'search') return (detail.matches || []).map(match => match.path + ':' + match.line + ':' + match.column + '  ' + match.text).join('\n');
  if (detail.kind === 'list') return (detail.entries || []).map(entry => entry.path).join('\n');
  if (detail.kind === 'plan') return (detail.steps || []).map(step => '- ' + step.step).join('\n');
  return detail.summary || '';
}
