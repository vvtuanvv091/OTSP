import axios from 'axios';
import { API_BASE_URL } from './config';

const axiosClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

axiosClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token')
      || sessionStorage.getItem('token')
      || localStorage.getItem('otsp_token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    config.metadata = { ...config.metadata, requestStartedAt: Date.now() };
    window.dispatchEvent(new CustomEvent('otsp:request-start'));
    return config;
  },
  (error) => Promise.reject(error)
);

// Bảo đảm MỌI lỗi từ API đều có thông báo tiếng Việt cho người dùng (không để trống / "Request failed with status code ...").
// Các trang đang đọc `error.response.data.message` sẽ tự hiển thị được thông báo này.
const DEFAULT_ERROR_MESSAGES = {
  400: 'Yêu cầu không hợp lệ. Vui lòng kiểm tra lại thông tin đã nhập.',
  401: 'Bạn cần đăng nhập hoặc phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
  403: 'Bạn không có quyền truy cập chức năng này. Vui lòng đăng nhập bằng tài khoản có vai trò phù hợp.',
  404: 'Không tìm thấy dữ liệu bạn yêu cầu.',
  408: 'Máy chủ phản hồi quá lâu. Vui lòng thử lại.',
  409: 'Dữ liệu bị xung đột hoặc đã tồn tại. Vui lòng tải lại trang và thử lại.',
  413: 'Tệp tải lên quá lớn.',
  429: 'Bạn thao tác quá nhanh. Vui lòng chờ một lát rồi thử lại.',
  500: 'Máy chủ gặp sự cố. Vui lòng thử lại sau.',
  502: 'Máy chủ đang khởi động hoặc tạm thời không phản hồi. Vui lòng thử lại sau ít phút.',
  503: 'Máy chủ đang bảo trì hoặc quá tải. Vui lòng thử lại sau ít phút.',
  504: 'Máy chủ phản hồi quá lâu. Vui lòng thử lại.',
};

const NETWORK_ERROR_MESSAGE = 'Không kết nối được tới máy chủ. Vui lòng kiểm tra mạng, hoặc thử lại sau ít phút nếu máy chủ đang khởi động.';

function normalizeErrorMessage(error) {
  const response = error?.response;
  if (!response) {
    error.userMessage = error?.code === 'ECONNABORTED'
      ? DEFAULT_ERROR_MESSAGES[408]
      : NETWORK_ERROR_MESSAGE;
    return;
  }

  const data = response.data;
  const isBlob = typeof Blob !== 'undefined' && data instanceof Blob;
  const existing = (data && typeof data === 'object' && !isBlob)
    ? (data.message || data.title || (typeof data.error === 'string' ? data.error : ''))
    : (typeof data === 'string' ? data : '');
  const looksLikeHtml = typeof data === 'string' && /^\s*</.test(data);

  if (existing && !looksLikeHtml) {
    if (data && typeof data === 'object' && !data.message) data.message = existing;
    error.userMessage = existing;
    return;
  }

  const fallback = DEFAULT_ERROR_MESSAGES[response.status]
    || (response.status >= 500 ? DEFAULT_ERROR_MESSAGES[500] : 'Đã xảy ra lỗi. Vui lòng thử lại.');
  if (!isBlob) {
    response.data = (data && typeof data === 'object') ? { ...data, message: fallback } : { message: fallback };
  }
  error.userMessage = fallback;
}

// Đồng bộ tương thích tên trường giữa phiên bản Backend trước (avatarUrl, name)
function normalizePayloadFields(value, seen = new WeakSet()) {
  if (!value || typeof value !== 'object') return value;
  if (typeof Blob !== 'undefined' && value instanceof Blob) return value;
  if (value instanceof ArrayBuffer || ArrayBuffer.isView(value)) return value;
  if (seen.has(value)) return value;
  seen.add(value);

  if (Array.isArray(value)) {
    for (let i = 0; i < value.length; i += 1) {
      normalizePayloadFields(value[i], seen);
    }
    return value;
  }

  // if (value.avatarURL && !value.avatarUrl) value.avatarUrl = value.avatarURL;
  // if (value.avatarUrl && !value.avatarURL) value.avatarURL = value.avatarUrl;
  // if (value.tutorAvatarURL && !value.tutorAvatarUrl) value.tutorAvatarUrl = value.tutorAvatarURL;
  // if (value.studentAvatarURL && !value.studentAvatarUrl) value.studentAvatarUrl = value.studentAvatarURL;
  // if (value.senderAvatarURL && !value.senderAvatarUrl) value.senderAvatarUrl = value.senderAvatarURL;
  // if (value.nameClass && !value.name) value.name = value.nameClass;
  if (
    value.name
    && !value.nameClass
    && ('subjectId' in value || 'gradeLevelId' in value || 'maxStudents' in value || 'sessionsPerWeek' in value)
  ) {
    value.nameClass = value.name;
  }

  for (const key of Object.keys(value)) {
    normalizePayloadFields(value[key], seen);
  }
  return value;
}

axiosClient.interceptors.response.use(
  (response) => {
    window.dispatchEvent(new CustomEvent('otsp:request-end'));
    if (response?.data) {
      normalizePayloadFields(response.data);
    }
    return response;
  },
  (error) => {
    window.dispatchEvent(new CustomEvent('otsp:request-end'));
    normalizeErrorMessage(error);
    if (error.response?.status === 403) {
      window.dispatchEvent(new CustomEvent('otsp:forbidden', {
        detail: { message: error.response?.data?.message || 'Bạn không có quyền thực hiện thao tác này.' },
      }));
    }
    if (error.response?.status === 401) {
      if (!window.location.pathname.includes('/login') && !window.location.pathname.includes('/register')) {
        sessionStorage.setItem('otsp_auth_notice', 'Phiên đăng nhập đã hết hạn hoặc không còn hợp lệ. Vui lòng đăng nhập lại.');
        localStorage.removeItem('token');
        localStorage.removeItem('user');
        sessionStorage.removeItem('token');
        sessionStorage.removeItem('user');
        localStorage.removeItem('otsp_token');
        localStorage.removeItem('otsp_user');
        window.dispatchEvent(new Event('otsp:unauthorized'));
      }
    }
    return Promise.reject(error);
  }
);

const inFlightGetRequests = new Map();

const buildGetCacheKey = (url, config = {}) => {
  const paramsKey = config?.params ? JSON.stringify(config.params) : '';
  const responseType = config?.responseType || 'json';
  return `${url}?${paramsKey}::${responseType}`;
};

const rawGet = axiosClient.get.bind(axiosClient);
axiosClient.get = (url, config) => {
  if (config?.skipDedup) {
    return rawGet(url, config);
  }
  const key = buildGetCacheKey(url, config);
  if (inFlightGetRequests.has(key)) {
    return inFlightGetRequests.get(key);
  }
  const promise = rawGet(url, config).finally(() => {
    inFlightGetRequests.delete(key);
  });
  inFlightGetRequests.set(key, promise);
  return promise;
};

export default axiosClient;
