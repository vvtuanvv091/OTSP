import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import newsApi from '../../api/newsApi';
import {
  DEFAULT_NEWS_CATEGORIES,
  getCategoryStyle,
  getArticleImage,
} from './newsHelpers';

const dateFullLabel = (value) =>
  value
    ? new Date(value).toLocaleDateString('vi-VN', {
        weekday: 'long',
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      })
    : '';

const dateShortLabel = (value) =>
  value
    ? new Date(value).toLocaleDateString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      })
    : '';

export default function NewsArticlePage() {
  const { articleId } = useParams();
  const [article, setArticle] = useState(null);
  const [related, setRelated] = useState([]);
  const [categories, setCategories] = useState(DEFAULT_NEWS_CATEGORIES);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [copied, setCopied] = useState(false);
  const copyTimerRef = useRef(null);

  useEffect(() => {
    return () => {
      if (copyTimerRef.current) clearTimeout(copyTimerRef.current);
    };
  }, []);

  useEffect(() => {
    window.scrollTo({ top: 0, behavior: 'smooth' });
    let active = true;

    async function loadArticle() {
      setLoading(true);
      setError('');
      try {
        const response = await newsApi.getArticle(articleId);
        if (!active) return;
        const data = response.data;
        setArticle(data);
        setRelated(Array.isArray(data?.relatedArticles) ? data.relatedArticles.slice(0, 4) : []);
        if (data?.categories?.length) {
          setCategories(data.categories);
        }
      } catch (requestError) {
        if (!active) return;
        setArticle(null);
        setRelated([]);
        setError(
          requestError.response?.data?.message || 'Không tải được nội dung bài viết.'
        );
      } finally {
        if (active) setLoading(false);
      }
    }

    loadArticle();
    return () => {
      active = false;
    };
  }, [articleId]);

  const handleCopyLink = () => {
    navigator.clipboard?.writeText(window.location.href).catch(() => {});
    setCopied(true);
    if (copyTimerRef.current) clearTimeout(copyTimerRef.current);
    copyTimerRef.current = setTimeout(() => setCopied(false), 2500);
  };

  if (loading) {
    return (
      <div className="container py-5 text-center">
        <span className="spinner-border text-danger" role="status" />
        <div className="mt-2 text-secondary">Đang tải nội dung bài viết…</div>
      </div>
    );
  }

  if (error || !article) {
    return (
      <main className="container py-5" style={{ maxWidth: 860 }}>
        <div className="alert alert-warning rounded-4 p-4" role="status">
          <h5 className="fw-bold mb-2">
            <i className="bi bi-exclamation-triangle-fill me-2" />
            {error || 'Không tìm thấy bài viết.'}
          </h5>
          <p className="mb-3 small">
            Bài viết có thể đã được gỡ hoặc đường dẫn không tồn tại.
          </p>
          <Link to="/news" className="btn btn-danger rounded-pill px-4">
            ← Quay lại trang Tin tức
          </Link>
        </div>
      </main>
    );
  }

  const catStyle = getCategoryStyle(article.category);
  const coverImage = getArticleImage(article, 0);
  const paragraphs = (article.content || '')
    .split(/\n+/)
    .map((p) => p.trim())
    .filter(Boolean);
  const wordCount = (article.content || '').split(/\s+/).filter(Boolean).length;
  const readingMinutes = Math.max(2, Math.ceil(wordCount / 160));

  return (
    <main className="news-detail-page">
      <div className="container news-detail-container">
        {/* Breadcrumb */}
        <nav aria-label="breadcrumb" className="news-detail-breadcrumb">
          <Link to="/">Trang chủ</Link>
          <i className="bi bi-chevron-right" />
          <Link to="/news">Tin tức</Link>
          <i className="bi bi-chevron-right" />
          <Link to={`/news?category=${encodeURIComponent(article.category || '')}`}>
            {article.category || 'Chuyên mục'}
          </Link>
          <i className="bi bi-chevron-right" />
          <span className="text-truncate">{article.title}</span>
        </nav>

        <div className="news-detail-grid">
          {/* CỘT NỘI DUNG BÀI ĐỌC */}
          <article className="news-detail-card">
            <header className="news-detail-header">
              <div className="d-flex flex-wrap align-items-center gap-2 mb-3">
                <Link
                  to={`/news?category=${encodeURIComponent(article.category || '')}`}
                  className="news-detail-cat-badge text-decoration-none"
                  style={{ backgroundColor: catStyle.bg, color: '#fff' }}
                >
                  <i className={`bi ${catStyle.icon}`} />
                  {article.category || 'Tin giáo dục'}
                </Link>
                <span className="news-detail-readtime">
                  <i className="bi bi-clock-history me-1" />
                  {readingMinutes} phút đọc
                </span>
              </div>

              <h1>{article.title}</h1>

              <div className="news-detail-meta">
                <div className="news-detail-author">
                  <span className="news-author-avatar">
                    <i className="bi bi-mortarboard-fill" />
                  </span>
                  <div>
                    <strong>{article.author || 'Ban Biên Tập OTSP'}</strong>
                    <time dateTime={article.publishedAt}>
                      {dateFullLabel(article.publishedAt)}
                    </time>
                  </div>
                </div>

                <div className="news-detail-actions">
                  <button
                    type="button"
                    className="news-share-btn"
                    onClick={handleCopyLink}
                    title="Sao chép liên kết bài viết"
                  >
                    <i className={`bi ${copied ? 'bi-check2 text-success' : 'bi-link-45deg'}`} />
                    <span>{copied ? 'Đã sao chép link' : 'Chia sẻ'}</span>
                  </button>
                  <Link to="/news" className="news-share-btn text-decoration-none">
                    <i className="bi bi-arrow-left" />
                    <span>Danh sách tin</span>
                  </Link>
                </div>
              </div>
            </header>

            {/* Ảnh bìa minh họa bài viết */}
            <figure className="news-detail-cover">
              <img src={coverImage} alt={article.title} />
              <figcaption>
                <span>
                  <i className="bi bi-camera me-1" /> Chuyên mục {article.category} · OTSP Education News
                </span>
              </figcaption>
            </figure>

            {/* Khung tóm tắt nhanh */}
            {paragraphs.length > 1 && (
              <div className="news-detail-lead">
                <i className="bi bi-quote news-lead-icon" />
                <p>{paragraphs[0]}</p>
              </div>
            )}

            {/* Nội dung chi tiết được chia đoạn rõ ràng */}
            <div className="news-detail-body">
              {(paragraphs.length > 1 ? paragraphs.slice(1) : paragraphs).map(
                (para, idx) => {
                  const isHeading = /^\d+\.\s+/.test(para);
                  if (isHeading) {
                    return (
                      <h2 key={idx} className="news-detail-subheading">
                        {para}
                      </h2>
                    );
                  }
                  if (para.startsWith('- ') || para.startsWith('• ')) {
                    return (
                      <div key={idx} className="news-detail-bullet">
                        <i className="bi bi-check-circle-fill text-danger me-2" />
                        <span>{para.replace(/^[-•]\s*/, '')}</span>
                      </div>
                    );
                  }
                  return <p key={idx}>{para}</p>;
                }
              )}
            </div>

            {/* Khung thông tin bổ trợ cuối bài */}
            <div className="news-detail-cta-box">
              <div>
                <strong>Bạn cần tư vấn lộ trình học tập hoặc tìm gia sư 1-1?</strong>
                <p className="mb-0">
                  Kết nối ngay với đội ngũ gia sư đã kiểm duyệt bằng cấp trên OTSP hoặc đặt lịch học thử trực tuyến.
                </p>
              </div>
              <div className="d-flex flex-wrap gap-2">
                <Link to="/search" className="btn btn-danger rounded-pill px-4 fw-semibold">
                  Tìm gia sư ngay
                </Link>
                <Link to="/guide" className="btn btn-outline-secondary rounded-pill px-3 fw-semibold">
                  Xem hướng dẫn
                </Link>
              </div>
            </div>

            <footer className="news-detail-footer">
              <div className="news-detail-tags">
                <span>Từ khóa:</span>
                <Link to={`/news?category=${encodeURIComponent(article.category || '')}`}>
                  #{article.category}
                </Link>
                <Link to="/search">#GiaSuOTSP</Link>
                <Link to="/documents">#OnThiHieuQua</Link>
              </div>
              <Link to="/news" className="news-back-all">
                ← Xem tất cả tin tức
              </Link>
            </footer>

            {/* Bài viết liên quan dưới chân bài */}
            {related.length > 0 && (
              <section className="news-related-section">
                <h3>
                  <i className="bi bi-Newspaper text-danger me-2 bi-newspaper" />
                  Bài viết cùng chủ đề &amp; Tin tức khác
                </h3>
                <div className="news-related-grid">
                  {related.slice(0, 4).map((item, idx) => (
                    <Link
                      key={item.id}
                      to={`/news/${item.id}`}
                      className="news-related-card"
                    >
                      <div className="news-related-img">
                        <img src={getArticleImage(item, idx + 1)} alt={item.title} />
                      </div>
                      <div className="news-related-copy">
                        <span className="news-related-cat">{item.category}</span>
                        <h4>{item.title}</h4>
                        <time>{dateShortLabel(item.publishedAt)}</time>
                      </div>
                    </Link>
                  ))}
                </div>
              </section>
            )}
          </article>

          {/* CỘT PHẢI: SIDEBAR ĐIỀU HƯỚNG & TIN MỚI */}
          <aside className="news-detail-sidebar">
            {/* Tin tức mới cập nhật */}
            {related.length > 0 && (
              <section className="news-sidebar-box">
                <h2>
                  <i className="bi bi-lightning-charge-fill text-danger me-1" /> Tin mới cập nhật
                </h2>
                <div className="news-sidebar-recent">
                  {related.map((item, idx) => (
                    <Link
                      key={item.id}
                      to={`/news/${item.id}`}
                      className="news-sidebar-recent-item"
                    >
                      <img src={getArticleImage(item, idx + 2)} alt="" />
                      <div>
                        <span>{item.category}</span>
                        <h3>{item.title}</h3>
                        <time>{dateShortLabel(item.publishedAt)}</time>
                      </div>
                    </Link>
                  ))}
                </div>
              </section>
            )}

            {/* Chuyên mục */}
            <section className="news-sidebar-box">
              <h2>
                <i className="bi bi-folder2-open text-danger me-1" /> Chuyên mục tin tức
              </h2>
              <div className="news-sidebar-cats">
                <Link to="/news" className="news-sidebar-cat-link">
                  <span>› Tất cả tin tức</span>
                  <i className="bi bi-arrow-up-right" />
                </Link>
                {categories.map((cat) => (
                  <Link
                    key={cat}
                    to={`/news?category=${encodeURIComponent(cat)}`}
                    className={`news-sidebar-cat-link ${
                      article.category === cat ? 'active' : ''
                    }`}
                  >
                    <span>› {cat}</span>
                    <i className="bi bi-arrow-up-right" />
                  </Link>
                ))}
              </div>
            </section>

            {/* Tiện ích hỗ trợ */}
            <section className="news-sidebar-box news-sidebar-highlight">
              <h3>
                <i className="bi bi-headset text-danger me-2" />
                Hỗ trợ Học viên &amp; Phụ huynh
              </h3>
              <p>
                Cần giải đáp về quy trình học thử, học phí hoặc cách sử dụng phòng học trực tuyến?
              </p>
              <div className="d-grid gap-2">
                <Link to="/faq" className="btn btn-sm btn-outline-danger rounded-pill fw-semibold">
                  Xem Câu hỏi thường gặp
                </Link>
                <Link to="/contact" className="btn btn-sm btn-danger rounded-pill fw-semibold">
                  Liên hệ Ban quản trị
                </Link>
              </div>
            </section>
          </aside>
        </div>
      </div>

      <style>{`
        .news-detail-page { min-height: 75vh; padding: 26px 0 64px; background: #f8fafc; color: #1e293b; }
        .news-detail-container { max-width: 1220px; }
        .news-detail-breadcrumb { display: flex; align-items: center; gap: 8px; margin-bottom: 18px; font-size: 13px; color: #64748b; overflow: hidden; }
        .news-detail-breadcrumb a { color: #64748b; text-decoration: none; font-weight: 600; flex-shrink: 0; }
        .news-detail-breadcrumb a:hover { color: #db303a; }
        .news-detail-breadcrumb i { font-size: 10px; color: #94a3b8; }
        .news-detail-breadcrumb span { color: #1e293b; font-weight: 700; }

        .news-detail-grid { display: grid; grid-template-columns: minmax(0, 2.7fr) minmax(270px, 1fr); align-items: start; gap: 24px; }
        .news-detail-card { background: #fff; border: 1px solid #e2e8f0; border-radius: 16px; padding: 28px 32px; box-shadow: 0 4px 16px rgba(15,23,42,.04); min-width: 0; }
        .news-detail-cat-badge { display: inline-flex; align-items: center; gap: 6px; padding: 5px 13px; border-radius: 999px; font-size: 12px; font-weight: 700; }
        .news-detail-readtime { font-size: 12.5px; color: #64748b; font-weight: 600; }
        .news-detail-header h1 { margin: 0 0 18px; color: #0f172a; font-size: clamp(24px, 3vw, 34px); font-weight: 800; line-height: 1.28; letter-spacing: -.02em; }
        .news-detail-meta { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 14px; padding-bottom: 18px; border-bottom: 1px solid #e2e8f0; margin-bottom: 22px; }
        .news-detail-author { display: flex; align-items: center; gap: 11px; }
        .news-author-avatar { width: 42px; height: 42px; border-radius: 50%; display: grid; place-items: center; background: #fff1f2; color: #db303a; font-size: 19px; }
        .news-detail-author strong { display: block; font-size: 14px; color: #1e293b; }
        .news-detail-author time { font-size: 12px; color: #64748b; }
        .news-detail-actions { display: flex; align-items: center; gap: 8px; }
        .news-share-btn { display: inline-flex; align-items: center; gap: 6px; padding: 6px 13px; border: 1px solid #dbe2eb; border-radius: 999px; background: #fff; color: #334155; font-size: 12.5px; font-weight: 600; transition: all .15s; }
        .news-share-btn:hover { border-color: #db303a; color: #db303a; }

        .news-detail-cover { margin: 0 0 24px; border-radius: 13px; overflow: hidden; border: 1px solid #e2e8f0; background: #f1f5f9; }
        .news-detail-cover img { width: 100%; max-height: 390px; object-fit: cover; display: block; }
        .news-detail-cover figcaption { padding: 9px 14px; font-size: 12px; color: #64748b; background: #f8fafc; border-top: 1px solid #e2e8f0; }

        .news-detail-lead { position: relative; margin: 0 0 24px; padding: 16px 20px 16px 46px; background: var(--bs-tertiary-bg, #fff8f8); border: 1px solid var(--bs-border-color, #fecdd3); border-radius: 12px; }
        .news-lead-icon { position: absolute; left: 14px; top: 12px; font-size: 24px; color: #db303a; opacity: .65; }
        .news-detail-lead p { margin: 0; font-size: 16px; font-weight: 600; color: #1e293b; line-height: 1.65; }

        .news-detail-body { color: #334155; font-size: 16px; line-height: 1.85; }
        .news-detail-body p { margin: 0 0 16px; }
        .news-detail-subheading { margin: 24px 0 12px; color: #0f172a; font-size: 19px; font-weight: 800; }
        .news-detail-bullet { display: flex; align-items: flex-start; margin: 0 0 10px; padding-left: 6px; font-size: 15.5px; }

        .news-detail-cta-box { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 16px; margin: 28px 0 22px; padding: 20px 22px; background: linear-gradient(135deg, #fff1f2, #fff); border: 1px solid #fecdd3; border-radius: 14px; }
        .news-detail-cta-box strong { display: block; color: #9f1239; font-size: 15.5px; margin-bottom: 4px; }
        .news-detail-cta-box p { color: #475569; font-size: 13px; max-width: 520px; }

        .news-detail-footer { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; padding-top: 18px; border-top: 1px solid #e2e8f0; }
        .news-detail-tags { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; font-size: 12.5px; color: #64748b; }
        .news-detail-tags a { padding: 4px 10px; border-radius: 6px; background: #f1f5f9; color: #334155; text-decoration: none; font-weight: 600; }
        .news-detail-tags a:hover { background: #fff1f2; color: #db303a; }
        .news-back-all { color: #db303a; font-weight: 700; font-size: 13.5px; text-decoration: none; }

        .news-related-section { margin-top: 32px; padding-top: 24px; border-top: 2px solid #f1f5f9; }
        .news-related-section h3 { margin: 0 0 16px; font-size: 18px; font-weight: 800; color: #0f172a; }
        .news-related-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px; }
        .news-related-card { display: flex; gap: 12px; padding: 10px; border: 1px solid #e2e8f0; border-radius: 12px; text-decoration: none; color: inherit; background: #f8fafc; transition: all .18s; }
        .news-related-card:hover { background: #fff; border-color: #db303a; box-shadow: 0 6px 16px rgba(15,23,42,.06); }
        .news-related-img { width: 92px; height: 68px; border-radius: 8px; overflow: hidden; flex-shrink: 0; }
        .news-related-img img { width: 100%; height: 100%; object-fit: cover; }
        .news-related-copy { min-width: 0; }
        .news-related-cat { display: block; font-size: 10.5px; font-weight: 700; color: #db303a; margin-bottom: 2px; }
        .news-related-copy h4 { margin: 0 0 4px; font-size: 13.5px; font-weight: 700; color: #1e293b; line-height: 1.35; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
        .news-related-copy time { font-size: 11px; color: #94a3b8; }

        /* SIDEBAR */
        .news-detail-sidebar { display: grid; gap: 18px; position: sticky; top: 92px; }
        .news-sidebar-box { padding: 18px; background: #fff; border: 1px solid #e2e8f0; border-radius: 14px; box-shadow: 0 3px 12px rgba(15,23,42,.04); }
        .news-sidebar-box h2 { margin: 0 0 14px; padding-bottom: 10px; border-bottom: 1px solid #f1f5f9; font-size: 15px; font-weight: 800; color: #0f172a; }
        .news-sidebar-recent { display: grid; gap: 12px; }
        .news-sidebar-recent-item { display: flex; gap: 10px; text-decoration: none; color: inherit; padding-bottom: 11px; border-bottom: 1px dashed #e2e8f0; }
        .news-sidebar-recent-item:last-child { padding-bottom: 0; border-bottom: 0; }
        .news-sidebar-recent-item img { width: 68px; height: 54px; border-radius: 7px; object-fit: cover; flex-shrink: 0; }
        .news-sidebar-recent-item span { display: block; font-size: 10px; font-weight: 700; color: #db303a; }
        .news-sidebar-recent-item h3 { margin: 2px 0; font-size: 12.5px; font-weight: 700; color: #1e293b; line-height: 1.35; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
        .news-sidebar-recent-item:hover h3 { color: #db303a; }
        .news-sidebar-recent-item time { font-size: 10.5px; color: #94a3b8; }

        .news-sidebar-cats { display: grid; gap: 4px; }
        .news-sidebar-cat-link { display: flex; align-items: center; justify-content: space-between; padding: 7px 10px; border-radius: 8px; text-decoration: none; color: #475569; font-size: 13px; font-weight: 600; transition: all .15s; }
        .news-sidebar-cat-link:hover, .news-sidebar-cat-link.active { background: #fff1f2; color: #db303a; }

        .news-sidebar-highlight { background: linear-gradient(145deg, #fff, #fff5f5); border-color: #fecdd3; }
        .news-sidebar-highlight h3 { font-size: 14.5px; font-weight: 800; color: #1e293b; margin: 0 0 6px; }
        .news-sidebar-highlight p { font-size: 12.5px; color: #64748b; margin: 0 0 12px; line-height: 1.5; }

        [data-bs-theme="dark"] .news-detail-page { background: #0f172a; color: #e2e8f0; }
        [data-bs-theme="dark"] .news-detail-card, [data-bs-theme="dark"] .news-sidebar-box, [data-bs-theme="dark"] .news-share-btn { background: #1e293b; border-color: #334155; color: #e2e8f0; }
        [data-bs-theme="dark"] .news-detail-header h1, [data-bs-theme="dark"] .news-detail-subheading, [data-bs-theme="dark"] .news-related-section h3, [data-bs-theme="dark"] .news-sidebar-box h2, [data-bs-theme="dark"] .news-detail-author strong, [data-bs-theme="dark"] .news-sidebar-recent-item h3, [data-bs-theme="dark"] .news-related-copy h4 { color: #f8fafc; }
        [data-bs-theme="dark"] .news-detail-body { color: #cbd5e1; }
        [data-bs-theme="dark"] .news-detail-lead, [data-bs-theme="dark"] .news-detail-cta-box, [data-bs-theme="dark"] .news-related-card { background: #0f172a; border-color: #334155; }
        [data-bs-theme="dark"] .news-detail-lead p { color: #e2e8f0; }

        @media (max-width: 991.98px) {
          .news-detail-grid { grid-template-columns: 1fr; }
          .news-detail-sidebar { position: static; }
        }
        @media (max-width: 575.98px) {
          .news-detail-card { padding: 18px 16px; }
          .news-related-grid { grid-template-columns: 1fr; }
        }
      `}</style>
    </main>
  );
}
