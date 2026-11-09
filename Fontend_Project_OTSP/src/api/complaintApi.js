import axiosClient from './axiosClient';

export const complaintApi = {
  // Lấy danh sách khiếu nại của chính người dùng (Học viên hoặc Gia sư)
  getMyAppComplaints: () => axiosClient.get('/complaints/my'),

  // Lấy danh sách đối tượng gợi ý có thể khiếu nại (gia sư hoặc học viên liên quan)
  getTargets: () => axiosClient.get('/complaints/targets'),

  // Gửi khiếu nại mới (multipart/form-data kèm ảnh minh chứng tùy chọn)
  createComplaint: (formData) =>
    axiosClient.post('/complaints', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    }),

  // Admin xem danh sách khiếu nại
  getAdminComplaints: (params) => axiosClient.get('/complaints/admin', { params }),

  // Admin cập nhật trạng thái khiếu nại (Processing, Resolved, Rejected) kèm ghi chú giải quyết
  updateAdminComplaintStatus: (id, data) =>
    axiosClient.put(`/complaints/admin/${id}/status`, data),
};

export default complaintApi;

