// Display helpers shared by the dashboard UI and the Node tests.
export const pad = n => String(n).padStart(2, '0');

export const clock = iso => {
  const d = new Date(iso);
  if (isNaN(d)) return '—';
  return pad(d.getHours()) + ':' + pad(d.getMinutes()) + ':' + pad(d.getSeconds());
};

const sameDay = (a, b) => a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();

// Absolute start time: today keeps only the clock, older calls carry their date.
export const stamp = (iso, now = new Date()) => {
  const d = new Date(iso);
  if (isNaN(d)) return '—';
  const yesterday = new Date(now);
  yesterday.setDate(now.getDate() - 1);
  if (sameDay(d, now)) return '今天 ' + clock(iso);
  if (sameDay(d, yesterday)) return '昨天 ' + clock(iso);
  return pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + ' ' + clock(iso);
};

export const ago = (iso, now = Date.now()) => {
  const seconds = (now - new Date(iso).getTime()) / 1000;
  if (!isFinite(seconds)) return '';
  if (seconds < 5) return '刚刚';
  if (seconds < 60) return Math.floor(seconds) + ' 秒前';
  if (seconds < 3600) return Math.floor(seconds / 60) + ' 分钟前';
  if (seconds < 86400) return Math.floor(seconds / 3600) + ' 小时前';
  return stamp(iso, new Date(now));
};

export const duration = ms => {
  if (ms == null || ms < 0 || !isFinite(ms)) return '—';
  if (ms < 1000) return Math.round(ms) + ' ms';
  if (ms < 60000) return (ms / 1000).toFixed(1) + ' s';
  return Math.floor(ms / 60000) + ' 分 ' + Math.round((ms % 60000) / 1000) + ' 秒';
};

export const bytes = n => {
  if (!n) return '0 B';
  if (n < 1024) return n + ' B';
  if (n < 1048576) return (n / 1024).toFixed(1) + ' KB';
  return (n / 1048576).toFixed(1) + ' MB';
};

export const base = path => {
  const parts = String(path || '').split(/[\\/]/);
  return parts[parts.length - 1] || path || '';
};

export const shortSession = id => (id && id.length > 6 ? id.slice(-6) : id || '');
