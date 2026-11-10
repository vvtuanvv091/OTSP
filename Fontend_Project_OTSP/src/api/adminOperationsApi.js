import axiosClient from './axiosClient';

const base = '/admin/operations';

export const adminOperationsApi = {
  getReviews: (params) => axiosClient.get(`${base}/reviews`, { params }),
  toggleReviewVisibility: (id) => axiosClient.put(`${base}/reviews/${id}/visibility`),
  deleteReview: (id) => axiosClient.delete(`${base}/reviews/${id}`),
  flagReview: (id, data) => axiosClient.post(`${base}/reviews/${id}/flag`, data),
  getReportedConversations: (params) => axiosClient.get(`${base}/reported-conversations`, { params }),
  getComplaints: (params) => axiosClient.get(`${base}/complaints`, { params }),
  reviewComplaint: (id, data) => axiosClient.put(`${base}/complaints/${id}`, data),
  getNews: (params) => axiosClient.get(`${base}/news`, { params }),
  createNews: (data) => axiosClient.post(`${base}/news`, data),
  updateNews: (id, data) => axiosClient.put(`${base}/news/${id}`, data),
  deleteNews: (id) => axiosClient.delete(`${base}/news/${id}`),
  getPayments: (params) => axiosClient.get(`${base}/payments`, { params }),
  getReconciliation: (period) => axiosClient.get(`${base}/reconciliation`, { params: { period } }),
  settlePayout: (data) => axiosClient.post(`${base}/reconciliation/settle`, data),
  getAccounts: (params) => axiosClient.get(`${base}/accounts`, { params }),
  createAccount: (data) => axiosClient.post(`${base}/accounts`, data),
  changeAccountRole: (id, role) => axiosClient.put(`${base}/accounts/${encodeURIComponent(id)}/role`, { role }),
  setAccountStatus: (id, locked) => axiosClient.put(`${base}/accounts/${encodeURIComponent(id)}/status`, locked),
  getAuditLogs: (params) => axiosClient.get(`${base}/audit-logs`, { params }),
  getAlerts: (params) => axiosClient.get(`${base}/alerts`, { params }),
  updateAlert: (id, status) => axiosClient.put(`${base}/alerts/${id}`, { status }),
  getAiUsage: () => axiosClient.get(`${base}/ai-usage`),
};

export default adminOperationsApi;
