import { createRoot } from 'react-dom/client';
import { App } from './app.jsx';

// Inside the desktop host (WebView2) the window already carries the product name, so the
// sidebar brand is hidden there and only shown in a plain browser tab.
if (window.chrome && window.chrome.webview) document.documentElement.classList.add('embedded');

createRoot(document.getElementById('root')).render(<App />);
