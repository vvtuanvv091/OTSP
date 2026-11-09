// Địa chỉ gốc của Backend.
// - Khi chạy local (`npm run dev` tại cổng 5173): để trống để dùng proxy của Vite (/api, /hubs, /uploads → localhost:5170).
// - Khi chạy bản build (`vite preview` / production): đọc từ biến môi trường VITE_API_BASE_URL.
const rawOrigin = import.meta.env.PROD ? (import.meta.env.VITE_API_BASE_URL || '') : '';

export const API_ORIGIN = rawOrigin.trim().replace(/\/+$/, '');
export const API_BASE_URL = `${API_ORIGIN}/api`;
export const hubUrl = (path) => `${API_ORIGIN}${path}`;

export const resolveAssetUrl = (url) => {
  if (!url || typeof url !== 'string') return '';
  if (/^(https?:|blob:|data:)/i.test(url)) return url;
  if ((url.startsWith('/uploads/') || url.startsWith('/api/')) && API_ORIGIN) {
    return `${API_ORIGIN}${url}`;
  }
  return url;
};

