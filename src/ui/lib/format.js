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

// Compact "how long ago" for dense lists: 刚刚 / 5 分钟 / 2 小时 / 3 天.
export const since = (ms, now = Date.now()) => {
  const seconds = (now - ms) / 1000;
  if (!isFinite(seconds)) return '';
  if (seconds < 60) return '刚刚';
  if (seconds < 3600) return Math.floor(seconds / 60) + ' 分钟';
  if (seconds < 86400) return Math.floor(seconds / 3600) + ' 小时';
  return Math.floor(seconds / 86400) + ' 天';
};

// "6 分 40 秒" for how long a turn worked.
export const span = ms => {
  if (!isFinite(ms) || ms < 0) return '';
  if (ms < 1000) return '不到 1 秒';
  const total = Math.round(ms / 1000);
  if (total < 60) return total + ' 秒';
  const minutes = Math.floor(total / 60), seconds = total % 60;
  if (minutes < 60) return minutes + ' 分' + (seconds ? ' ' + seconds + ' 秒' : '');
  return Math.floor(minutes / 60) + ' 小时 ' + (minutes % 60) + ' 分';
};

// "E:/w/src/app.jsx" -> { dir: "src/", name: "app.jsx" }, relative to root when it is inside it.
export const splitPath = (path, root) => {
  const value = String(path || '').replace(/\\/g, '/');
  const prefix = String(root || '').replace(/\\/g, '/').replace(/\/+$/, '');
  const relative = prefix && value.toLowerCase().startsWith(prefix.toLowerCase() + '/') ? value.slice(prefix.length + 1) : value;
  const cut = relative.lastIndexOf('/');
  return { dir: cut >= 0 ? relative.slice(0, cut + 1) : '', name: cut >= 0 ? relative.slice(cut + 1) : relative };
};

// Longest shared directory of a set of paths ("" when they share none).
export const commonDir = paths => {
  const split = paths.map(path => String(path || '').replace(/\\/g, '/').split('/').slice(0, -1));
  if (!split.length) return '';
  let shared = split[0];
  for (const parts of split.slice(1)) {
    let index = 0;
    while (index < shared.length && index < parts.length && shared[index].toLowerCase() === parts[index].toLowerCase()) index += 1;
    shared = shared.slice(0, index);
  }
  return shared.join('/');
};

// "21:55" for compact time columns.
export const hm = iso => { const d = new Date(iso); return isNaN(d) ? '' : pad(d.getHours()) + ':' + pad(d.getMinutes()); };
