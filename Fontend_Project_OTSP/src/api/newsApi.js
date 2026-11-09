import axiosClient from './axiosClient';

const newsApi = {
  getArticles: (params) => axiosClient.get('/news', { params }),
  getArticle: (id) => axiosClient.get(`/news/${id}`),
};

export default newsApi;
