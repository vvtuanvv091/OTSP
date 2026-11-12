import { API_BASE_URL } from '../api/config';
import { getStoredToken } from './authStorage';

const DEVICE_ID_STORAGE_KEY = 'otsp_device_id';
let inFlightVisitPromise = null;
let trackedVnDate = '';

function getVietnamDateKey() {
  return new Date(Date.now() + 7 * 60 * 60 * 1000).toISOString().slice(0, 10);
}

/**
 * Lấy hoặc khởi tạo mã định danh ổn định cho trình duyệt/thiết bị hiện tại.
 */
export function getOrCreateDeviceId() {
  try {
    const existing = localStorage.getItem(DEVICE_ID_STORAGE_KEY);
    if (existing && existing.trim().length >= 8) {
      return existing.trim();
    }
    const generated =
      typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
        ? crypto.randomUUID()
        : `dev-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`;
    localStorage.setItem(DEVICE_ID_STORAGE_KEY, generated);
    return generated;
  } catch {
    return '';
  }
}

/**
 * Ghi nhận 1 lượt truy cập cho thiết bị hiện tại trong ngày (chỉ gửi 1 lần mỗi phiên tải trang).
 */
export function trackDeviceVisit(path = '/') {
  const todayKey = getVietnamDateKey();
  if (trackedVnDate === todayKey && inFlightVisitPromise) {
    return inFlightVisitPromise;
  }
  if (trackedVnDate === todayKey) {
    return Promise.resolve();
  }

  trackedVnDate = todayKey;
  const deviceId = getOrCreateDeviceId();
  const token = getStoredToken();

  const headers = { 'Content-Type': 'application/json' };
  if (token) headers.Authorization = `Bearer ${token}`;

  inFlightVisitPromise = fetch(`${API_BASE_URL}/home/visit`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ path: path || '/', deviceId }),
    keepalive: true
  })
    .catch(() => {})
    .finally(() => {
      inFlightVisitPromise = null;
    });

  return inFlightVisitPromise;
}

/**
 * Đảm bảo lượt truy cập của thiết bị hiện tại đã được ghi nhận trước khi tải Dashboard (tối đa timeoutMs).
 */
export async function ensureVisitTracked(path = '/', timeoutMs = 1200) {
  const promise = trackDeviceVisit(path);
  await Promise.race([
    promise,
    new Promise((resolve) => setTimeout(resolve, timeoutMs))
  ]);
}
