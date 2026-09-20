import { useState } from 'react';
import { Icon } from './icons.jsx';

export const isAddress = value => typeof value === 'string' && /^(?:[a-z]:[\\/]|\\\\[^? .]|https?:\/\/)/i.test(value);

// Files are selected in Explorer, so clicking an .exe or script never executes it.
export function PathLink({ value, children, className = '' }) {
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  if (!isAddress(value)) return <span className={className}>{children || value}</span>;
  const open = async () => {
    setBusy(true); setMessage('');
    try {
      const bootstrap = await fetch('/api/local-actions', { cache: 'no-store' });
      if (!bootstrap.ok) throw Error('当前服务不支持打开路径，请启动新版程序。');
      const { token } = await bootstrap.json();
      const response = await fetch('/api/open?target=' + encodeURIComponent(value), { method: 'POST', headers: { 'X-Workspace-Token': token } });
      const result = await response.json();
      if (!response.ok) throw Error(result.error || 'Windows 未能打开此位置。');
      setMessage('已在 Windows 中打开');
    } catch (error) { setMessage(error.message); }
    finally { setBusy(false); }
  };
  return <span className={'path-action ' + className}>
    <button type="button" className="path-link" title={'在 Windows 中打开：' + value} onClick={open} disabled={busy}>
      <span>{children || value}</span><Icon name="external" size={12} />
    </button>
    {message ? <span className="path-message" role="status">{message}</span> : null}
  </span>;
}

export function AddressValue({ value }) {
  if (typeof value !== 'string') return value;
  return value.split('\n').map((line, i) => <span className="address-line" key={i}><PathLink value={line} /></span>);
}
