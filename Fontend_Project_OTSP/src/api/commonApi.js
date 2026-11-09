import axiosClient from './axiosClient';
import { API_BASE_URL } from './config';

const HOME_CACHE_KEY = 'otsp_home_cache_v1';
let homeMemoryCache = null;
let homeCacheTimestamp = 0;
const HOME_CACHE_TTL_MS = 30_000;

export const getCachedHomeData = () => {
  if (homeMemoryCache) return homeMemoryCache;
  try {
    const raw = sessionStorage.getItem(HOME_CACHE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw);
      if (parsed && typeof parsed === 'object') {
        homeMemoryCache = parsed;
        return parsed;
      }
    }
  } catch {
    // Bỏ qua lỗi nếu trình duyệt chặn đọc sessionStorage
  }
  return null;
};

export const commonApi = {
  getHomeData: async () => {
    const now = Date.now();
    if (homeMemoryCache && now - homeCacheTimestamp < HOME_CACHE_TTL_MS) {
      return { data: homeMemoryCache };
    }
    const response = await axiosClient.get('/home');
    if (response?.data) {
      homeMemoryCache = response.data;
      homeCacheTimestamp = Date.now();
      try {
        sessionStorage.setItem(HOME_CACHE_KEY, JSON.stringify(response.data));
      } catch {
        // Bỏ qua lỗi nếu bộ nhớ sessionStorage bị đầy
      }
    }
    return response;
  },
  getSearchFilters: () => axiosClient.get('/search/filters'),
  searchTutors: (params) => axiosClient.get('/search/tutors', { params }),
  searchClasses: (params) => axiosClient.get('/search/classes', { params }),
  getClassDetails: (id) => axiosClient.get(`/search/classes/${id}`),
  getPublicDocuments: (params) => axiosClient.get('/learning-documents', { params }),
  downloadPublicDocumentUrl: (id) => `${API_BASE_URL}/learning-documents/${id}/download`,
  submitContact: (data) => axiosClient.post('/home/contact', data),
};

export default commonApi;
