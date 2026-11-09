import axiosClient from './axiosClient';

export const tutorApi = {
  // Bảng điều khiển & Thu nhập gia sư
  getDashboard: () => axiosClient.get('/tutor/dashboard/stats'),
  getIncome: (month) => axiosClient.get('/tutor/income', { params: month ? { month } : {} }),
  saveIncomeBankAccount: (data) => axiosClient.post('/tutor/income/bank-account', data),

  // Hồ sơ cá nhân & Bằng cấp, chứng chỉ
  getProfile: () => axiosClient.get('/tutor/profile'),
  updateProfile: (data) => axiosClient.put('/tutor/profile', data),
  uploadAvatar: (file) => {
    const formData = new FormData();
    formData.append('avatarFile', file);
    return axiosClient.post('/tutor/profile/avatar', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
  getQualifications: () => axiosClient.get('/tutor/qualifications'),
  getQualificationFile: (fileName) =>
    axiosClient.get(`/tutor/qualifications/file/${encodeURIComponent(fileName)}`, { responseType: 'blob' }),
  uploadQualification: (formData) =>
    axiosClient.post('/tutor/qualifications', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }),
  deleteQualification: (id) => axiosClient.delete(`/tutor/qualifications/${id}`),

  // Lịch giảng dạy & Thời gian rảnh
  getSchedule: (weekOffset = 0, jumpDate = null) => {
    let url = `/tutor/schedules?weekOffset=${weekOffset}`;
    if (jumpDate) url += `&jumpDate=${encodeURIComponent(jumpDate)}`;
    return axiosClient.get(url);
  },
  createSession: (data) => axiosClient.post('/tutor/schedules/sessions', data),
  cancelSession: (id) => axiosClient.post(`/tutor/schedules/sessions/${id}/cancel`),
  confirmTrialSchedule: (data) => axiosClient.post('/tutor/schedules/trial/confirm', data),
  getAvailabilities: () => axiosClient.get('/tutor/availabilities'),
  saveAvailabilities: (data) => axiosClient.post('/tutor/availabilities', data),

  // Yêu cầu kết nối dạy học
  getConnectionRequests: (params) => axiosClient.get('/tutor/connection-requests', { params }),
  acceptConnectionRequest: (id) => axiosClient.post(`/tutor/connection-requests/${id}/accept`),
  rejectConnectionRequest: (id, reason) =>
    axiosClient.post(`/tutor/connection-requests/${id}/reject`, { reason }),

  // Quản lý lớp học
  getClassrooms: (params) => axiosClient.get('/tutor/classrooms', { params }),
  getClassroom: (id) => axiosClient.get(`/tutor/classrooms/${id}`),
  getClassroomOptions: () => axiosClient.get('/tutor/classrooms/options'),
  createClassroom: (formData) => {
    if (formData instanceof FormData) {
      if (formData.has('name') && !formData.has('nameClass')) {
        formData.append('nameClass', formData.get('name'));
      } else if (formData.has('nameClass') && !formData.has('name')) {
        formData.append('name', formData.get('nameClass'));
      }
    }
    return axiosClient.post('/tutor/classrooms', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
  updateClassroom: (id, data) => {
    if (data instanceof FormData) {
      if (data.has('name') && !data.has('nameClass')) {
        data.append('nameClass', data.get('name'));
      } else if (data.has('nameClass') && !data.has('name')) {
        data.append('name', data.get('nameClass'));
      }
      return axiosClient.post(`/tutor/classrooms/${id}/update`, data, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
    }
    const payload = data && typeof data === 'object'
      ? { ...data, name: data.name ?? data.nameClass, nameClass: data.nameClass ?? data.name }
      : data;
    return axiosClient.put(`/tutor/classrooms/${id}`, payload);
  },
  cancelClassroom: (id) => axiosClient.post(`/tutor/classrooms/${id}/cancel`),

  // Danh sách học viên & Môn học phụ trách
  getStudents: (params) => axiosClient.get('/tutor/students', { params }),
  getSubjects: (params) => axiosClient.get('/tutor/subjects', { params }),
  addSubject: (data) => axiosClient.post('/tutor/subjects', data),
  updateSubjectRate: (id, hourlyFee) =>
    axiosClient.put(`/tutor/subjects/${id}`, { hourlyFee }),
  removeSubject: (id) => axiosClient.delete(`/tutor/subjects/${id}`),

  // Quản lý tài liệu học tập
  getDocuments: (params) => axiosClient.get('/tutor/documents', { params }),
  downloadDocument: (id) => axiosClient.get(`/tutor/documents/${id}/download`, { responseType: 'blob' }),
  uploadDocument: (formData) =>
    axiosClient.post('/tutor/documents', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }),
  deleteDocument: (id) => axiosClient.delete(`/tutor/documents/${id}`),

  // Đánh giá từ học viên
  getReviews: (params) => axiosClient.get('/tutor/reviews', { params }),
};

export default tutorApi;
