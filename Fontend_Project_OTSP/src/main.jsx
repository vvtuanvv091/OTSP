import { StrictMode } from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import App from './app/App';
import Providers from './app/Providers';
import ErrorBoundary from './components/common/ErrorBoundary';

import './styles/otsp-theme.css';
import './styles/otsp-red-theme.css';
import './styles/otsp-components.css';
import './styles/otsp-home.css';
import './styles/otsp-pages.css';
import './styles/student-sidebar.css';
import './styles/tutor-sidebar.css';
import './styles/tutor-mobile.css';
import './styles/tutor-ui-refresh.css';

// Tự động tải lại trang an toàn khi Vite gặp lỗi mã hash chunk JS thay đổi sau mỗi lần deploy lên Vercel
window.addEventListener('vite:preloadError', (event) => {
  event.preventDefault();
  const lastReload = sessionStorage.getItem('otsp_vite_preload_retry');
  const now = Date.now();
  if (!lastReload || now - parseInt(lastReload, 10) > 8000) {
    sessionStorage.setItem('otsp_vite_preload_retry', String(now));
    window.location.reload();
  }
});

// Áp dụng giao diện sáng/tối đã lưu trước khi React render giao diện
const savedTheme = localStorage.getItem('otsp-theme') === 'dark' ? 'dark' : 'light';
document.documentElement.setAttribute('data-bs-theme', savedTheme);

ReactDOM.createRoot(document.getElementById('root')).render(
  <StrictMode>
    <ErrorBoundary>
      <BrowserRouter>
        <Providers>
          <App />
        </Providers>
      </BrowserRouter>
    </ErrorBoundary>
  </StrictMode>
);
