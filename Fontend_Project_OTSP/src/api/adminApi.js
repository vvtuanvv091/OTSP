import axiosClient from './axiosClient';

export const adminApi = {
  getDashboardStats: () => axiosClient.get('/admin/dashboard/stats'),
  getProfile: () => axiosClient.get('/admin/profile'),
  updateProfile: (data) => axiosClient.put('/admin/profile', data),
  uploadAvatar: (file) => {
    const formData = new FormData();
    formData.append('avatarFile', file);
    // axiosClient mặc định gửi POST dạng application/json. Ta cần đổi sang multipart/form-data
    // để trình duyệt gửi FormData đúng định dạng giúp ASP.NET Core nhận diện được file ảnh avatarFile.
    return axiosClient.post('/admin/profile/avatar', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
  getPendingTutors: () => axiosClient.get('/admin/tutor-approvals'),
  getTutorApprovalDashboard: (params) => axiosClient.get('/admin/tutor-approvals/dashboard', { params }),
  getPendingQualifications: (params) => axiosClient.get('/admin/tutor-approvals/qualifications/pending', { params }),
  approveTutor: (id) => axiosClient.post(`/admin/tutor-approvals/${id}/approve`),
  rejectTutor: (id, reason) =>
    axiosClient.post(`/admin/tutor-approvals/${id}/reject`, { reason }),
  approveQualification: (id) =>
    axiosClient.post(`/admin/tutor-approvals/qualifications/${id}/approve`),
  rejectQualification: (id, reason) =>
    axiosClient.post(`/admin/tutor-approvals/qualifications/${id}/reject`, { reason }),
  getQualificationFile: (id) =>
    axiosClient.get(`/admin/tutor-approvals/qualifications/${id}`, { responseType: 'blob' }),
  aiVerifyQualification: (id) =>
    axiosClient.post(`/admin/tutor-approvals/qualifications/${id}/ai-verify`),
  getStudents: (params) => axiosClient.get('/admin/students', { params }),
  updateStudent: (userId, data) => axiosClient.put(`/admin/students/${encodeURIComponent(userId)}`, data),
  deleteStudent: (userId) => axiosClient.delete(`/admin/students/${encodeURIComponent(userId)}`),
  lockStudent: (userId) => axiosClient.put(`/admin/students/${encodeURIComponent(userId)}/lock`),
  unlockStudent: (userId) => axiosClient.put(`/admin/students/${encodeURIComponent(userId)}/unlock`),
  getTutors: (params) => axiosClient.get('/admin/tutors', { params }),
  updateTutor: (userId, data) => axiosClient.put(`/admin/tutors/${encodeURIComponent(userId)}`, data),
  deleteTutor: (userId) => axiosClient.delete(`/admin/tutors/${encodeURIComponent(userId)}`),
  lockTutor: (userId) => axiosClient.put(`/admin/tutors/${encodeURIComponent(userId)}/lock`),
  unlockTutor: (userId) => axiosClient.put(`/admin/tutors/${encodeURIComponent(userId)}/unlock`),
  getConnectionRequests: (params) => axiosClient.get('/admin/connection-requests', { params }),
  cancelConnectionRequest: (id, reason) => axiosClient.post(`/admin/connection-requests/${id}/cancel`, { reason }),
  getClassrooms: (params) => axiosClient.get('/admin/classrooms', { params }),
  updateClassroom: (id, data) => {
    const payload = data && typeof data === 'object'
      ? { ...data, name: data.name ?? data.nameClass, nameClass: data.nameClass ?? data.name }
      : data;
    return axiosClient.put(`/admin/classrooms/${id}`, payload);
  },
  deleteClassroom: (id) => axiosClient.delete(`/admin/classrooms/${id}`),
  cancelClassroom: (id, reason) => axiosClient.post(`/admin/classrooms/${id}/cancel`, { reason }),
  cancelClassSession: (id, reason) => axiosClient.post(`/admin/classrooms/sessions/${id}/cancel`, { reason }),
  getSubjects: (params) => axiosClient.get('/admin/catalog/subjects', { params }),
  createSubject: (data) => axiosClient.post('/admin/catalog/subjects', data),
  updateSubject: (id, data) => axiosClient.put(`/admin/catalog/subjects/${id}`, data),
  deleteSubject: (id) => axiosClient.delete(`/admin/catalog/subjects/${id}`),
  getGradeLevels: (params) => axiosClient.get('/admin/catalog/grade-levels', { params }),
  createGradeLevel: (data) => axiosClient.post('/admin/catalog/grade-levels', data),
  updateGradeLevel: (id, data) => axiosClient.put(`/admin/catalog/grade-levels/${id}`, data),
  deleteGradeLevel: (id) => axiosClient.delete(`/admin/catalog/grade-levels/${id}`),
  getLearningDocuments: (params) => axiosClient.get('/admin/learning-documents', { params }),
  getLearningDocumentFile: (id) => axiosClient.get(`/admin/learning-documents/${id}/file`, { responseType: 'blob' }),
  approveLearningDocument: (id) => axiosClient.put(`/admin/learning-documents/${id}/approve`),
  rejectLearningDocument: (id, reason) => axiosClient.put(`/admin/learning-documents/${id}/reject`, { reason }),
  updateLearningDocument: (id, data) => axiosClient.put(`/admin/learning-documents/${id}`, data),
  deleteLearningDocument: (id) => axiosClient.delete(`/admin/learning-documents/${id}`),
  deleteComplaint: (id) => axiosClient.delete(`/admin/operations/complaints/${id}`),
};

export default adminApi;

