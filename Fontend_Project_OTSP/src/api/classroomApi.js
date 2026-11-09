import axiosClient from './axiosClient';

export const classroomApi = {
  getSessionRoom: (sessionId) => axiosClient.get(`/sessions/${sessionId}/room`),
  joinSession: (sessionId) => axiosClient.post(`/sessions/${sessionId}/join`),
  endSession: (sessionId) => axiosClient.post(`/sessions/${sessionId}/end`),
  leaveSession: (sessionId) => axiosClient.post(`/sessions/${sessionId}/leave`),
};

export default classroomApi;

