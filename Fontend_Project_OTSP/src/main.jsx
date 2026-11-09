import { StrictMode } from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import App from './app/App';
import Providers from './app/Providers';

import './styles/otsp-theme.css';
import './styles/otsp-red-theme.css';
import './styles/otsp-components.css';
import './styles/otsp-home.css';
import './styles/otsp-pages.css';
import './styles/student-sidebar.css';
import './styles/tutor-sidebar.css';
import './styles/tutor-mobile.css';
import './styles/tutor-ui-refresh.css';

// Áp dụng giao diện sáng/tối đã lưu trước khi React render giao diện
const savedTheme = localStorage.getItem('otsp-theme') === 'dark' ? 'dark' : 'light';
document.documentElement.setAttribute('data-bs-theme', savedTheme);

ReactDOM.createRoot(document.getElementById('root')).render(
  <StrictMode>
    <BrowserRouter>
      <Providers>
        <App />
      </Providers>
    </BrowserRouter>
  </StrictMode>
);
