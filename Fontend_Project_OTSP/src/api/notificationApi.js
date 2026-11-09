import axiosClient from './axiosClient';

export const notificationApi = {
  getNotifications: ({ page = 1, pageSize = 10 } = {}) =>
    axiosClient.get('/notifications', { params: { page, pageSize } }),
  markAsRead: (id) => axiosClient.post(`/notifications/${id}/mark-read`),
  markAllAsRead: () => axiosClient.post('/notifications/mark-all-read'),
};

export default notificationApi;

