import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import newsApi from '../../api/newsApi';
import {
  DEFAULT_NEWS_CATEGORIES,
  getCategoryStyle,
  getArticleImage,
} from './newsHelpers';
import '../../styles/news/NewsArticlePage.css';

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

    </main>
  );
}
