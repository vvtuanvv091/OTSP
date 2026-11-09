import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import newsApi from '../../api/newsApi';
import commonApi from '../../api/commonApi';
import {
  DEFAULT_NEWS_CATEGORIES,
  getCategoryStyle,
  getArticleImage,
} from './newsHelpers';

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

      <style>{`
        .news-page { min-height: 76vh; padding: 28px 0 64px; color: #1d2939; background: #f8fafc; }
        .news-layout-container { max-width: 1220px; }
        .news-page-heading { margin: 0 0 20px; padding-bottom: 16px; border-bottom: 1px solid #e6ebf2; }
        .news-eyebrow { display: inline-flex; align-items: center; gap: 8px; color: #c92536; font-size: 11px; font-weight: 800; letter-spacing: .12em; }
        .news-eyebrow i { width: 22px; height: 2px; background: #c92536; display: inline-block; }
        .news-page-heading h1 { margin: 6px 0 4px; color: #172033; font-size: 28px; font-weight: 800; letter-spacing: -.025em; }
        .news-page-heading p { margin: 0; color: #64748b; font-size: 14px; }
        .news-search { display: flex; align-items: center; gap: 8px; min-width: 300px; padding: 5px 6px 5px 13px; border: 1px solid #dce3ec; border-radius: 10px; background: #fff; box-shadow: 0 2px 6px rgba(15,23,42,.03); }
        .news-search > i { color: #7b8798; font-size: 14px; }
        .news-search input { min-width: 0; flex: 1; padding: 6px 0; border: 0; outline: 0; color: #1e293b; background: transparent; font-size: 13.5px; }
        .news-search-clear { border: 0; background: transparent; color: #94a3b8; padding: 2px 6px; font-size: 12px; }
        .news-search button[type="submit"] { padding: 7px 16px; border: 0; border-radius: 7px; color: #fff; background: #db303a; font-size: 13px; font-weight: 700; transition: background .15s; }
        .news-search button[type="submit"]:hover { background: #be242e; }
        .news-quick-cats { display: flex; flex-wrap: wrap; gap: 8px; }
        .news-quick-cat-pill { padding: 6px 14px; border: 1px solid #e2e8f0; border-radius: 999px; background: #fff; color: #475569; font-size: 12.5px; font-weight: 600; transition: all .15s; }
        .news-quick-cat-pill:hover { border-color: #db303a; color: #db303a; }
        .news-quick-cat-pill.active { background: #db303a; border-color: #db303a; color: #fff; box-shadow: 0 3px 10px rgba(219,48,58,.22); }

        .news-layout-grid { display: grid; grid-template-columns: minmax(0, 2.75fr) minmax(270px, 1fr); align-items: start; gap: 24px; }
        .news-main-column { min-width: 0; }
        .news-preview-notice { display: flex; align-items: center; gap: 8px; margin: 0 0 14px; padding: 10px 14px; color: #6e5316; background: #fff8e6; border: 1px solid #f2dfa9; border-radius: 9px; font-size: 12.5px; }
        .news-preview-notice i { color: #bd8a16; }

        /* FEATURED COLLAGE CARD */
        .news-featured-card { display: block; overflow: hidden; margin-bottom: 18px; color: inherit; background: #fff; border: 1px solid #e2e8f0; border-radius: 14px; box-shadow: 0 4px 14px rgba(15,23,42,.05); text-decoration: none; transition: box-shadow .2s, transform .2s; }
        .news-featured-card:hover, .news-list-item:hover { transform: translateY(-2px); box-shadow: 0 10px 24px rgba(15,23,42,.09); }
        .news-featured-collage { display: grid; height: 265px; grid-template-columns: 2fr 1fr; grid-template-rows: 1fr 78px; gap: 7px; padding: 10px; background: #f1f5f9; }
        .news-collage-main { position: relative; grid-row: 1; overflow: hidden; border-radius: 8px; }
        .news-collage-main img, .news-collage-side-top img, .news-collage-strip img, .news-list-image img { width: 100%; height: 100%; object-fit: cover; }
        .news-collage-main img { filter: brightness(.72) contrast(1.04); transition: transform .35s; }
        .news-featured-card:hover .news-collage-main img { transform: scale(1.03); }
        .news-collage-headline { position: absolute; right: 16px; bottom: 14px; left: 16px; color: #fff; font-size: clamp(16px, 1.85vw, 22px); font-weight: 800; line-height: 1.25; text-shadow: 0 2px 8px rgba(0,0,0,.65); }
        .news-collage-right-stack { grid-row: 1 / span 2; display: grid; grid-template-rows: 1fr 1.15fr; gap: 7px; }
        .news-collage-side-top { overflow: hidden; border-radius: 8px; }
        .news-collage-side-info { display: flex; flex-direction: column; justify-content: space-between; padding: 10px; background: #fef3c7; border: 1px solid #fde68a; border-radius: 8px; overflow: hidden; }
        .news-collage-side-info-title { color: #991b1b; font-size: 11px; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; text-align: center; }
        .news-collage-side-thumbs { display: grid; grid-template-columns: 1fr 1fr; gap: 5px; height: 78px; }
        .news-collage-side-thumbs img { width: 100%; height: 100%; object-fit: cover; border-radius: 5px; }
        .news-collage-strip { display: grid; grid-column: 1 / 2; grid-template-columns: repeat(4, 1fr); gap: 6px; overflow: hidden; }
        .news-collage-strip img { min-width: 0; border-radius: 6px; }

        .news-featured-copy { padding: 16px 20px 18px; }
        .news-category-badge { display: inline-flex; align-items: center; gap: 5px; padding: 4px 11px; border-radius: 999px; font-size: 11px; font-weight: 700; letter-spacing: .01em; }
        .news-featured-copy h2 { margin: 8px 0 6px; color: #0f172a; font-size: 20px; font-weight: 800; line-height: 1.32; }
        .news-featured-copy p { margin: 0 0 10px; color: #475569; font-size: 13.5px; line-height: 1.55; }
        .news-meta-row { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px; color: #64748b; font-size: 12px; }
        .news-read-more { color: #db303a; font-weight: 700; font-size: 12.5px; display: inline-flex; align-items: center; gap: 4px; }

        /* ARTICLE LIST */
        .news-section-bar { display: flex; align-items: center; justify-content: space-between; margin: 4px 0 12px; padding: 0 2px; font-size: 14px; font-weight: 800; color: #1e293b; }
        .news-list { display: grid; gap: 12px; }
        .news-list-item { display: grid; grid-template-columns: 124px minmax(0, 1fr) auto; align-items: center; gap: 16px; padding: 12px 16px; color: inherit; background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; box-shadow: 0 2px 8px rgba(15,23,42,.035); text-decoration: none; transition: all .18s; }
        .news-list-image { width: 124px; height: 86px; overflow: hidden; border-radius: 9px; background: #e9eef4; flex-shrink: 0; }
        .news-list-copy { min-width: 0; }
        .news-list-top-meta { display: flex; align-items: center; gap: 8px; margin-bottom: 5px; }
        .news-list-copy h2 { margin: 0 0 4px; color: #1e293b; font-size: 15px; font-weight: 800; line-height: 1.35; display: -webkit-box; -webkit-line-clamp: 1; -webkit-box-orient: vertical; overflow: hidden; }
        .news-list-copy p { display: -webkit-box; overflow: hidden; margin: 0 0 5px; color: #64748b; font-size: 12.5px; line-height: 1.45; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
        .news-list-date { color: #94a3b8; font-size: 11.5px; font-weight: 500; }
        .news-list-arrow { width: 32px; height: 32px; border-radius: 50%; display: grid; place-items: center; color: #db303a; background: #fff1f2; font-size: 14px; transition: all .15s; }
        .news-list-item:hover .news-list-arrow { background: #db303a; color: #fff; }

        /* SIDEBAR */
        .news-sidebar { display: grid; gap: 18px; position: sticky; top: 92px; }
        .news-category-panel, .news-trending-panel, .news-guide-widget, .news-subscribe-panel { padding: 18px; background: #fff; border: 1px solid #e2e8f0; border-radius: 14px; box-shadow: 0 3px 12px rgba(15,23,42,.04); }
        .news-category-panel h2, .news-trending-panel h2 { margin: 0 0 12px; padding-bottom: 10px; border-bottom: 1px solid #f1f5f9; color: #1e293b; font-size: 15px; font-weight: 800; }
        .news-category-list { display: grid; gap: 3px; }
        .news-category-list button { display: flex; width: 100%; align-items: center; gap: 8px; padding: 7px 10px; border: 0; border-radius: 8px; color: #475569; background: transparent; text-align: left; font-size: 13px; font-weight: 600; transition: all .15s; }
        .news-cat-chevron { font-size: 10px; color: #94a3b8; transition: transform .15s; }
        .news-category-list button:hover, .news-category-list button.active { background: #fff1f2; color: #db303a; }
        .news-category-list button:hover .news-cat-chevron, .news-category-list button.active .news-cat-chevron { color: #db303a; transform: translateX(2px); }

        .news-trending-list { display: grid; gap: 12px; }
        .news-trending-item { display: flex; align-items: flex-start; gap: 11px; text-decoration: none; color: inherit; padding-bottom: 11px; border-bottom: 1px dashed #e2e8f0; }
        .news-trending-item:last-child { padding-bottom: 0; border-bottom: 0; }
        .news-trending-rank { width: 26px; height: 26px; border-radius: 7px; display: grid; place-items: center; font-size: 11px; font-weight: 800; flex-shrink: 0; background: #f1f5f9; color: #475569; }
        .news-trending-rank.rank-1 { background: #db303a; color: #fff; }
        .news-trending-rank.rank-2 { background: #fee2e2; color: #b91c1c; }
        .news-trending-rank.rank-3 { background: #fef3c7; color: #b45309; }
        .news-trending-copy { min-width: 0; }
        .news-trending-cat { display: block; font-size: 10.5px; font-weight: 700; color: #db303a; margin-bottom: 2px; }
        .news-trending-copy h3 { margin: 0 0 3px; font-size: 12.5px; font-weight: 700; color: #1e293b; line-height: 1.38; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
        .news-trending-item:hover h3 { color: #db303a; }
        .news-trending-copy time { font-size: 11px; color: #94a3b8; }

        .news-guide-widget-head { display: flex; align-items: center; gap: 10px; margin-bottom: 10px; }
        .news-note-icon { display: grid; width: 38px; height: 38px; flex: 0 0 38px; place-items: center; color: #db303a; background: #fff1f2; border-radius: 10px; font-size: 18px; }
        .news-guide-widget-head strong { display: block; color: #1e293b; font-size: 14px; font-weight: 800; }
        .news-guide-widget-head span { color: #64748b; font-size: 11.5px; }
        .news-guide-widget-desc { margin: 0 0 12px; color: #64748b; font-size: 12.5px; line-height: 1.5; }
        .news-guide-links { display: grid; gap: 7px; }
        .news-guide-link-item { display: flex; align-items: center; gap: 9px; padding: 8px 11px; border-radius: 9px; background: #f8fafc; border: 1px solid #e2e8f0; color: #334155; font-size: 12.5px; font-weight: 600; text-decoration: none; transition: all .15s; }
        .news-guide-link-item:hover { border-color: #db303a; color: #db303a; background: #fff1f2; }

        .news-subscribe-panel { background: linear-gradient(145deg, #fff, #fff5f5); border-color: #fecdd3; }
        .news-subscribe-panel h3 { margin: 0 0 6px; font-size: 14.5px; font-weight: 800; color: #1e293b; }
        .news-subscribe-panel p { margin: 0 0 12px; font-size: 12px; color: #64748b; line-height: 1.45; }
        .news-subscribe-panel form { display: grid; gap: 8px; }
        .news-subscribe-panel input { width: 100%; padding: 8px 12px; border: 1px solid #cbd5e1; border-radius: 8px; font-size: 12.5px; outline: none; }
        .news-subscribe-panel input:focus { border-color: #db303a; }
        .news-subscribe-panel button { width: 100%; padding: 8px 12px; border: 0; border-radius: 8px; background: #db303a; color: #fff; font-size: 12.5px; font-weight: 700; }

        .news-loading, .news-empty { display: flex; min-height: 220px; flex-direction: column; align-items: center; justify-content: center; gap: 9px; background: #fff; border: 1px solid #e2e8f0; border-radius: 14px; padding: 32px; color: #64748b; font-size: 13px; }
        .news-empty > i { color: #94a3b8; font-size: 36px; }
        .news-empty strong { color: #1e293b; font-size: 16px; }
        .news-pagination { display: flex; justify-content: center; align-items: center; gap: 14px; margin-top: 22px; color: #475569; font-size: 13px; }
        .news-pagination button { padding: 8px 14px; border: 1px solid #cbd5e1; border-radius: 8px; color: #1e293b; background: #fff; font-weight: 600; }
        .news-pagination button:disabled { opacity: .45; }

        [data-bs-theme="dark"] .news-page { color: #e8edf4; background: #0f172a; }
        [data-bs-theme="dark"] .news-page-heading { border-color: #1e293b; }
        [data-bs-theme="dark"] .news-page-heading h1, [data-bs-theme="dark"] .news-category-panel h2, [data-bs-theme="dark"] .news-trending-panel h2, [data-bs-theme="dark"] .news-empty strong, [data-bs-theme="dark"] .news-guide-widget-head strong, [data-bs-theme="dark"] .news-subscribe-panel h3, [data-bs-theme="dark"] .news-section-bar { color: #f1f5f9; }
        [data-bs-theme="dark"] .news-featured-card, [data-bs-theme="dark"] .news-list-item, [data-bs-theme="dark"] .news-category-panel, [data-bs-theme="dark"] .news-trending-panel, [data-bs-theme="dark"] .news-guide-widget, [data-bs-theme="dark"] .news-subscribe-panel, [data-bs-theme="dark"] .news-search, [data-bs-theme="dark"] .news-pagination button, [data-bs-theme="dark"] .news-empty, [data-bs-theme="dark"] .news-quick-cat-pill:not(.active) { background: #1e293b; border-color: #334155; color: #e2e8f0; }
        [data-bs-theme="dark"] .news-featured-copy h2, [data-bs-theme="dark"] .news-list-copy h2, [data-bs-theme="dark"] .news-trending-copy h3 { color: #f8fafc; }
        [data-bs-theme="dark"] .news-featured-copy p, [data-bs-theme="dark"] .news-list-copy p, [data-bs-theme="dark"] .news-guide-widget-desc { color: #94a3b8; }
        [data-bs-theme="dark"] .news-guide-link-item { background: #0f172a; border-color: #334155; color: #e2e8f0; }
        [data-bs-theme="dark"] .news-search input { color: #f1f5f9; }

        @media (max-width: 991.98px) {
          .news-layout-grid { grid-template-columns: 1fr; }
          .news-sidebar { position: static; }
        }
        @media (max-width: 575.98px) {
          .news-search { width: 100%; min-width: 0; }
          .news-featured-collage { height: 210px; grid-template-columns: 1fr; grid-template-rows: 1fr 54px; }
          .news-collage-right-stack { display: none; }
          .news-list-item { grid-template-columns: 88px minmax(0, 1fr); gap: 12px; padding: 10px; }
          .news-list-image { width: 88px; height: 70px; }
          .news-list-arrow { display: none; }
        }
      `}</style>
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
