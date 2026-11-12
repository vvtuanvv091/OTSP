import axiosClient from './axiosClient';

const aiApi = {
  chatWithAgent: (payload) => {
    return axiosClient.post('/ai/agent/chat', payload);
  },

  askStudyAssistant: (payload) => {
    return axiosClient.post('/ai/study-assistant', payload);
  },

  submitFeedback: (payload) => {
    return axiosClient.post('/ai/feedback', payload);
  },
};

export default aiApi;

