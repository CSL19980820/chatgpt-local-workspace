import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { PathLink } from './path-link.jsx';
import { Icon } from './icons.jsx';
import { bytes } from '@/lib/format.js';

export function ImageView({ detail }) {
  const [failed, setFailed] = useState(false);
  const [actual, setActual] = useState(false);
  const [dimensions, setDimensions] = useState('');
  return <section className="image-card" aria-label="本次读取的图片">
    <div className="image-toolbar">
      <span className="image-name"><Icon name="image" />{detail.name || '图片'}</span>
      <Button variant="secondary" size="xs" aria-pressed={actual} onClick={() => setActual(!actual)}>{actual ? '适应窗口' : '原始尺寸'}</Button>
    </div>
    <div className={'image-stage' + (actual ? ' actual-size' : '')}>
      {!detail.preview_url || failed
        ? <div className="image-unavailable"><Icon name="image" size={28} /><strong>图片预览不可用</strong><span>{detail.is_error ? '本次未成功读取图片。' : '历史预览已过期或来自旧版本，请重新读取图片。'}</span></div>
        : <img src={detail.preview_url} alt={'本次读取：' + detail.name} onError={() => setFailed(true)} onLoad={event => setDimensions(event.currentTarget.naturalWidth + ' × ' + event.currentTarget.naturalHeight)} />}
    </div>
    <div className="image-facts"><span>{dimensions}</span><span>{detail.mime_type}</span><span>{bytes(detail.size_bytes)}</span><span>读取时的图片</span></div>
    <div className="image-path"><PathLink value={detail.path} /></div>
  </section>;
}
