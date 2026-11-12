import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import newsApi from '../../api/newsApi';
import commonApi from '../../api/commonApi';
import {
  DEFAULT_NEWS_CATEGORIES,
  getCategoryStyle,
  getArticleImage,
} from './newsHelpers';
import '../../styles/news/NewsPage.css';

const PAGE_SIZE = 8;

const dateLabel = (value) =>
  value
    ? new Date(value).toLocaleDateString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    })
    : '';

const excerpt = (content = '', limit = 155) => {
  const value = content.replace(/\s+/g, ' ').trim();
  return value.length > limit ? `${value.slice(0, limit).trim()}…` : value;
};

export default function NewsPage() {
  const [params, setParams] = useSearchParams();
  const selectedCategory = params.get('category') || '';
  const searchQuery = params.get('search') || '';
  const page = Math.max(1, Number(params.get('page')) || 1);
  const [data, setData] = useState({ articles: [], categories: [], total: 0, totalPages: 0 });
  const [search, setSearch] = useState(searchQuery);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [newsletterEmail, setNewsletterEmail] = useState('');
  const [newsletterSubmitting, setNewsletterSubmitting] = useState(false);
  const [newsletterFeedback, setNewsletterFeedback] = useState(null);

  const [prevSearchQuery, setPrevSearchQuery] = useState(searchQuery);
  if (prevSearchQuery !== searchQuery) {
    setPrevSearchQuery(searchQuery);
    setSearch(searchQuery);
  }

  useEffect(() => {
    let active = true;
    async function loadArticles() {
      setLoading(true);
      setError('');
      try {
        const response = await newsApi.getArticles({
          search: searchQuery,
          category: selectedCategory,
          page,
          pageSize: PAGE_SIZE,
        });
        if (!active) return;
        const apiArticles = Array.isArray(response.data?.articles) ? response.data.articles : [];
        const apiCategories = Array.isArray(response.data?.categories) ? response.data.categories : [];
        setData({
          ...response.data,
          articles: apiArticles,
          categories: apiCategories,
          total: response.data?.total ?? apiArticles.length,
          totalPages: response.data?.totalPages ?? 1,
        });
      } catch (requestError) {
        if (!active) return;
        setData({ articles: [], categories: [], total: 0, totalPages: 0 });
        setError(
          requestError.response?.data?.message ||
          'Không thể tải danh sách bài viết từ máy chủ. Vui lòng thử lại sau.'
        );
      } finally {
        if (active) setLoading(false);
      }
    }
    loadArticles();
    return () => {
      active = false;
    };
  }, [searchQuery, page, selectedCategory]);

  const updateQuery = (next) => {
    const query = new URLSearchParams(params);
    Object.entries(next).forEach(([key, value]) =>
      value ? query.set(key, String(value)) : query.delete(key)
    );
    if ('category' in next || 'search' in next) query.delete('page');
    setParams(query);
  };

  const handleNewsletterSubmit = async (e) => {
    e.preventDefault();
    const trimmedEmail = newsletterEmail.trim();
    if (!trimmedEmail || newsletterSubmitting) return;

    setNewsletterSubmitting(true);
    setNewsletterFeedback(null);
    try {
      await commonApi.submitContact({
        fullName: trimmedEmail.split('@')[0] || 'Bạn đọc OTSP',
        email: trimmedEmail,
        topic: 'Đăng ký nhận bản tin tuyển sinh OTSP',
        message: `Đăng ký nhận bản tin tuyển sinh và cẩm nang học tập qua email: ${trimmedEmail}`,
      });
      setNewsletterEmail('');
      setNewsletterFeedback({ type: 'success', text: 'Đã đăng ký nhận bản tin thành công!' });
    } catch (err) {
      setNewsletterFeedback({
        type: 'error',
        text: err.response?.data?.message || err.userMessage || 'Chưa thể gửi đăng ký lúc này.',
      });
    } finally {
      setNewsletterSubmitting(false);
    }
  };

  const visibleArticles = data.articles || [];
  const featured =
    page === 1 && !selectedCategory && !searchQuery ? visibleArticles[0] : null;
  const listArticles = featured ? visibleArticles.slice(1) : visibleArticles;
  const categories = data.categories?.length ? data.categories : DEFAULT_NEWS_CATEGORIES;
  const sidebarRecent = visibleArticles.slice(0, 4);

  return (
    <main className="news-page">
      <div className="container news-layout container py-4">
        {/* Thanh điều hướng nhỏ & tiêu đề */}
        <header className="news-page-heading">
          <div className="d-flex flex-wrap align-items-center justify-content-between gap-3">
            <div>
              <span className="news-eyebrow">
                OTSP <i /> TIN TỨC &amp; CẨM NANG GIÁO DỤC
              </span>
              <h1>Tin tức &amp; chia sẻ học tập</h1>
              <p>
                Cập nhật lịch tuyển sinh, phương pháp ôn thi, kinh nghiệm chọn gia sư và thông báo chính thức từ OTSP.
              </p>
            </div>
            <form
              className="news-search mb-0"
              onSubmit={(event) => {
                event.preventDefault();
                updateQuery({ search: search.trim() });
              }}
            >
              <i className="bi bi-search" aria-hidden="true" />
              <input
                aria-label="Tìm tin tức"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Tìm tiêu đề, chuyên mục…"
              />
              {(search || params.get('search')) && (
                <button
                  type="button"
                  className="news-search-clear"
                  onClick={() => {
                    setSearch('');
                    updateQuery({ search: '' });
                  }}
                  title="Xóa tìm kiếm"
                >
                  <i className="bi bi-x-lg" />
                </button>
              )}
              <button type="submit">Tìm kiếm</button>
            </form>
          </div>

          {/* Thanh lọc nhanh chuyên mục trên đầu */}
          <div className="news-quick-cats mt-3">
            <button
              type="button"
              className={`news-quick-cat-pill ${!selectedCategory ? 'active' : ''}`}
              onClick={() => updateQuery({ category: '' })}
            >
              Tất cả
            </button>
            {categories.map((cat) => (
              <button
                key={cat}
                type="button"
                className={`news-quick-cat-pill ${selectedCategory === cat ? 'active' : ''}`}
                onClick={() => updateQuery({ category: cat })}
              >
                {cat}
              </button>
            ))}
          </div>
        </header>

        {error && (
          <div className="alert alert-danger py-2" role="alert">
            {error}
          </div>
        )}

        <div className="news-layout-grid">
          {/* CỘT TRÁI: TIN TIÊU ĐIỂM + DANH SÁCH TIN TỨC */}
          <section className="news-main-column" aria-label="Tin tức mới nhất">
            {loading ? (
              <div className="news-loading">
                <span className="spinner-border spinner-border-sm text-danger" role="status" />
                <span>Đang tải tin tức…</span>
              </div>
            ) : visibleArticles.length === 0 ? (
              <div className="news-empty">
                <i className="bi bi-newspaper" />
                <strong>Chưa có bài viết phù hợp</strong>
                <span>Hãy chọn chuyên mục khác hoặc thử từ khóa khác.</span>
                {(selectedCategory || searchQuery) && (
                  <button
                    type="button"
                    className="btn btn-outline-danger btn-sm rounded-pill px-3 mt-2"
                    onClick={() => {
                      setSearch('');
                      updateQuery({ category: '', search: '' });
                    }}
                  >
                    Xem tất cả bài viết
                  </button>
                )}
              </div>
            ) : (
              <>
                {featured && <FeaturedArticle article={featured} />}

                {listArticles.length > 0 && (
                  <div className="news-list-section">
                    <div className="news-section-bar">
                      <span>
                        <i className="bi bi-collection-play-fill me-2 text-danger" />
                        {selectedCategory ? `Chuyên mục: ${selectedCategory}` : 'Bài viết & Tin tức mới cập nhật'}
                      </span>
                      <small className="text-muted">{data.total || visibleArticles.length} bài viết</small>
                    </div>
                    <div className="news-list">
                      {listArticles.map((article, index) => (
                        <NewsListItem
                          key={article.id}
                          article={article}
                          index={index}
                        />
                      ))}
                    </div>
                  </div>
                )}

                {data.totalPages > 1 && (
                  <nav className="news-pagination" aria-label="Phân trang">
                    <button
                      type="button"
                      disabled={page <= 1}
                      onClick={() => updateQuery({ page: page - 1 })}
                    >
                      ← Trang trước
                    </button>
                    <span>
                      Trang <strong>{page}</strong> / {data.totalPages}
                    </span>
                    <button
                      type="button"
                      disabled={page >= data.totalPages}
                      onClick={() => updateQuery({ page: page + 1 })}
                    >
                      Trang tiếp →
                    </button>
                  </nav>
                )}
              </>
            )}
          </section>

          {/* CỘT PHẢI: SIDEBAR ĐẦY ĐỦ (CHUYÊN MỤC + TIN ĐỌC NHIỀU + CẨM NANG + NHẬN TIN) */}
          <aside className="news-sidebar">
            {/* 1. Chuyên mục */}
            <section className="news-category-panel">
              <h2>
                <i className="bi bi-folder2-open text-danger me-1" /> Chuyên mục
              </h2>
              <div className="news-category-list">
                <button
                  type="button"
                  className={!selectedCategory ? 'active' : ''}
                  onClick={() => updateQuery({ category: '' })}
                >
                  <i className="bi bi-chevron-right news-cat-chevron" />
                  <span>Tất cả tin tức</span>
                </button>
                {categories.map((category) => (
                  <button
                    type="button"
                    key={category}
                    className={selectedCategory === category ? 'active' : ''}
                    onClick={() => updateQuery({ category })}
                  >
                    <i className="bi bi-chevron-right news-cat-chevron" />
                    <span>{category}</span>
                  </button>
                ))}
              </div>
            </section>

            {/* 2. Bài viết tiêu điểm / Đọc nhiều */}
            {sidebarRecent.length > 0 && (
              <section className="news-trending-panel">
                <h2>
                  <i className="bi bi-fire text-danger me-1" /> Tin nổi bật tuần qua
                </h2>
                <div className="news-trending-list">
                  {sidebarRecent.map((item, idx) => (
                    <Link
                      key={item.id}
                      to={`/news/${item.id}`}
                      className="news-trending-item"
                    >
                      <span className={`news-trending-rank rank-${idx + 1}`}>
                        {String(idx + 1).padStart(2, '0')}
                      </span>
                      <div className="news-trending-copy">
                        <span className="news-trending-cat">{item.category}</span>
                        <h3>{item.title}</h3>
                        <time>{dateLabel(item.publishedAt)}</time>
                      </div>
                    </Link>
                  ))}
                </div>
              </section>
            )}

            {/* 3. Tiện ích kết nối nhanh OTSP */}
            <section className="news-guide-widget">
              <div className="news-guide-widget-head">
                <span className="news-note-icon">
                  <i className="bi bi-mortarboard-fill" />
                </span>
                <div>
                  <strong>Cẩm nang học tập OTSP</strong>
                  <span>Hỗ trợ học viên &amp; phụ huynh 24/7</span>
                </div>
              </div>
              <p className="news-guide-widget-desc">
                Tìm kiếm gia sư đã xác thực bằng cấp, đặt lịch học thử linh hoạt hoặc tải tài liệu ôn thi miễn phí.
              </p>
              <div className="news-guide-links">
                <Link to="/search" className="news-guide-link-item">
                  <i className="bi bi-search text-danger" />
                  <span>Tìm gia sư 1-1 &amp; Lớp nhóm</span>
                  <i className="bi bi-arrow-right ms-auto" />
                </Link>
                <Link to="/documents" className="news-guide-link-item">
                  <i className="bi bi-file-earmark-pdf text-danger" />
                  <span>Kho tài liệu ôn thi công khai</span>
                  <i className="bi bi-arrow-right ms-auto" />
                </Link>
                <Link to="/guide" className="news-guide-link-item">
                  <i className="bi bi-Compass text-danger bi-compass" />
                  <span>Hướng dẫn sử dụng nền tảng</span>
                  <i className="bi bi-arrow-right ms-auto" />
                </Link>
                <Link to="/faq" className="news-guide-link-item">
                  <i className="bi bi-question-circle text-danger" />
                  <span>Câu hỏi thường gặp (FAQ)</span>
                  <i className="bi bi-arrow-right ms-auto" />
                </Link>
              </div>
            </section>

            {/* 4. Đăng ký nhận bản tin */}
            <section className="news-subscribe-panel">
              <h3>
                <i className="bi bi-envelope-paper-heart-fill text-danger me-2" />
                Nhận bản tin tuyển sinh
              </h3>
              <p>
                Nhận thông báo lịch thi mới nhất và mẹo ôn tập hàng tuần qua email.
              </p>
              <form onSubmit={handleNewsletterSubmit}>
                <input
                  type="email"
                  required
                  value={newsletterEmail}
                  onChange={(e) => setNewsletterEmail(e.target.value)}
                  placeholder="Nhập email của bạn…"
                  disabled={newsletterSubmitting}
                />
                <button type="submit" disabled={newsletterSubmitting}>
                  {newsletterSubmitting ? 'Đang gửi…' : 'Đăng ký ngay'}
                </button>
                {newsletterFeedback && (
                  <div
                    className={`alert ${newsletterFeedback.type === 'success' ? 'alert-success' : 'alert-danger'} py-2 px-3 small mb-0 mt-1`}
                    role="status"
                  >
                    {newsletterFeedback.text}
                  </div>
                )}
              </form>
            </section>
          </aside>
        </div>
      </div>

    </main>
  );
}

function FeaturedArticle({ article }) {
  const images = [
    '/assets/img/events-item-1.jpg',
    '/assets/img/events-item-2.jpg',
    '/assets/img/course-1.jpg',
    '/assets/img/course-2.jpg',
  ];
  const catStyle = getCategoryStyle(article.category);

  return (
    <Link to={`/news/${article.id}`} className="news-featured-card">
      <div className="news-featured-collage">
        <div className="news-collage-main">
          <img src={getArticleImage(article, 0)} alt={article.title} />
          <span className="news-collage-headline">{article.title}</span>
        </div>
        <div className="news-collage-right-stack">
          <div className="news-collage-side-top">
            <img src={images[1]} alt="" />
          </div>
          <div className="news-collage-side-info">
            <span className="news-collage-side-info-title">THÔNG TIN CẦN BIẾT</span>
            <div className="news-collage-side-thumbs">
              <img src={images[2]} alt="" />
              <img src={images[3]} alt="" />
            </div>
          </div>
        </div>
        <div className="news-collage-strip">
          {images.map((image, idx) => (
            <img key={idx} src={image} alt="" />
          ))}
        </div>
      </div>

      <div className="news-featured-copy">
        <div className="d-flex align-items-center gap-2">
          <span
            className="news-category-badge"
            style={{ backgroundColor: catStyle.bg, color: '#fff' }}
          >
            <i className={`bi ${catStyle.icon}`} />
            {article.category || 'Tin tức'}
          </span>
          <span className="badge rounded-pill bg-danger-subtle text-danger border border-danger-subtle" style={{ fontSize: 10 }}>
            Tiêu điểm
          </span>
        </div>
        <h2>{article.title}</h2>
        <p>{excerpt(article.content, 185)}</p>
        <div className="news-meta-row">
          <span>
            <i className="bi bi-calendar3 me-1" />
            {dateLabel(article.publishedAt)} · <strong>{article.author || 'OTSP News'}</strong>
          </span>
          <span className="news-read-more">
            Đọc chi tiết <i className="bi bi-arrow-right" />
          </span>
        </div>
      </div>
    </Link>
  );
}

function NewsListItem({ article, index }) {
  const catStyle = getCategoryStyle(article.category);

  return (
    <Link to={`/news/${article.id}`} className="news-list-item">
      <div className="news-list-image">
        <img
          src={getArticleImage(article, index + 1)}
          alt={article.title}
          loading="lazy"
        />
      </div>
      <div className="news-list-copy">
        <div className="news-list-top-meta">
          <span
            className="news-category-badge"
            style={{
              backgroundColor: catStyle.lightBg,
              color: catStyle.text,
              border: `1px solid ${catStyle.border}`,
            }}
          >
            <i className={`bi ${catStyle.icon}`} />
            {article.category || 'Tin giáo dục'}
          </span>
          <span className="news-list-date">
            <i className="bi bi-clock me-1" />
            {dateLabel(article.publishedAt)}
          </span>
        </div>
        <h2>{article.title}</h2>
        <p>{excerpt(article.content, 130)}</p>
      </div>
      <span className="news-list-arrow" aria-hidden="true" title="Đọc chi tiết">
        <i className="bi bi-chevron-right" />
      </span>
    </Link>
  );
}
