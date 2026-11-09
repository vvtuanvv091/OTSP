import axiosClient from './axiosClient';

export const chatApi = {
  getConversations: () => axiosClient.get('/chat/conversations'),
  getMessages: (contactId, limit = 50) =>
    axiosClient.get(`/chat/messages/${contactId}`, { params: { limit } }),
  sendMessage: (data) => axiosClient.post('/chat/messages', data),
  uploadAttachment: (file) => {
    const formData = new FormData();
    formData.append('file', file);
    return axiosClient.post('/chat/upload-attachment', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
  editMessage: (messageId, content) =>
    axiosClient.put(`/chat/messages/${messageId}`, { content }),
  deleteMessage: (messageId) => axiosClient.delete(`/chat/messages/${messageId}`),
};

export default chatApi;
