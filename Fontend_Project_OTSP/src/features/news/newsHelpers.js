import { resolveAssetUrl } from '../../api/config';

export const DEFAULT_NEWS_CATEGORIES = [
  'Tuyển sinh',
  'Phương pháp học',
  'Kinh nghiệm chọn gia sư',
  'Xu hướng giáo dục',
  'Thông báo OTSP',
];

export const getCategoryStyle = (category = '') => {
  const norm = category.toLowerCase();
  if (norm.includes('tuyển sinh')) {
    return { bg: '#dc3545', lightBg: '#fff0f1', text: '#b42332', border: '#f8c9ce', icon: 'bi-megaphone-fill' };
  }
  if (norm.includes('kinh nghiệm') || norm.includes('gia sư')) {
    return { bg: '#6f42c1', lightBg: '#f3eeff', text: '#59339d', border: '#d9c8ff', icon: 'bi-person-check-fill' };
  }
  if (norm.includes('phương pháp')) {
    return { bg: '#0d6efd', lightBg: '#eef5ff', text: '#0a58ca', border: '#c6dfff', icon: 'bi-lightbulb-fill' };
  }
  if (norm.includes('xu hướng')) {
    return { bg: '#198754', lightBg: '#eaf8f0', text: '#146c43', border: '#bce6cf', icon: 'bi-graph-up-arrow' };
  }
  return { bg: '#d97706', lightBg: '#fff8eb', text: '#b45309', border: '#fde0a6', icon: 'bi-bell-fill' };
};

export const getArticleImage = (article, index = 0) => {
  if (article?.image) return resolveAssetUrl(article.image);
  const cat = (article?.category || '').toLowerCase();
  if (cat.includes('tuyển sinh')) return '/assets/img/events-item-1.jpg';
  if (cat.includes('kinh nghiệm')) return '/assets/img/course-1.jpg';
  if (cat.includes('phương pháp')) return '/assets/img/events-item-2.jpg';
  if (cat.includes('xu hướng')) return '/assets/img/course-2.jpg';
  if (cat.includes('thông báo')) return '/assets/img/course-3.jpg';
  const fallbacks = [
    '/assets/img/course-1.jpg',
    '/assets/img/events-item-2.jpg',
    '/assets/img/course-2.jpg',
    '/assets/img/events-item-1.jpg',
    '/assets/img/course-3.jpg',
  ];
  return fallbacks[index % fallbacks.length];
};

