import { useState } from 'react';
import { Link } from 'react-router-dom';
import commonApi from '../../api/commonApi';

export default function Footer() {
  const [email, setEmail] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [feedback, setFeedback] = useState(null);

  const handleNewsletterSubmit = async (e) => {
    e.preventDefault();
    const trimmedEmail = email.trim();
    if (!trimmedEmail || submitting) return;

    setSubmitting(true);
    setFeedback(null);
    try {
      await commonApi.submitContact({
        fullName: trimmedEmail.split('@')[0] || 'Bạn đọc OTSP',
        email: trimmedEmail,
        topic: 'Đăng ký nhận bản tin giáo dục OTSP',
        message: `Đăng ký nhận bản tin giáo dục và thông báo tuyển sinh qua email: ${trimmedEmail}`,
      });
      setEmail('');
      setFeedback({ type: 'success', text: 'Đã ghi nhận đăng ký nhận bản tin thành công.' });
    } catch (err) {
      setFeedback({
        type: 'error',
        text: err.response?.data?.message || err.userMessage || 'Chưa thể gửi đăng ký lúc này. Vui lòng thử lại sau.',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <footer id="footer" className="footer position-relative">
      <div className="container footer-top">
        <div className="row gy-4">
          <div className="col-lg-4 col-md-6 footer-about">
            <Link to="/" className="logo d-flex align-items-center text-decoration-none">
              <span className="sitename text-danger fw-bold fs-3">OTSP</span>
            </Link>
            <p className="pt-3">
              Nền tảng kết nối gia sư và học viên trực tuyến hàng đầu. Đội ngũ gia sư được kiểm định bằng cấp nghiêm ngặt,
              học phí minh bạch và hỗ trợ học thử linh hoạt.
            </p>
            <div className="footer-contact pt-2">
              <p>
                <i className="bi bi-telephone-fill me-2 text-danger"></i>
                <strong>Hotline:</strong> <a href="tel:+84984391940" className="text-reset text-decoration-none">(+84) 984 391 940</a>
              </p>
              <p>
                <i className="bi bi-envelope-fill me-2 text-danger"></i>
                <strong>Email:</strong> <a href="mailto:tongngoctuan09@gmail.com" className="text-reset text-decoration-none">tongngoctuan09@gmail.com</a>
              </p>
            </div>
            <div className="social-links d-flex gap-2 mt-4">
              <a href="tel:+84984391940" aria-label="Gọi Hotline OTSP" title="Hotline OTSP"><i className="bi bi-telephone"></i></a>
              <a href="mailto:tongngoctuan09@gmail.com" aria-label="Gửi Email OTSP" title="Email OTSP"><i className="bi bi-envelope"></i></a>
              <Link to="/contact" aria-label="Liên hệ hỗ trợ OTSP" title="Liên hệ hỗ trợ"><i className="bi bi-headset"></i></Link>
              <Link to="/news" aria-label="Tin tức giáo dục OTSP" title="Tin tức giáo dục"><i className="bi bi-newspaper"></i></Link>
            </div>
          </div>

          <div className="col-6 col-lg-2 col-md-3 footer-links">
            <h4>Khám phá</h4>
            <ul className="list-unstyled">
              <li><Link to="/search">Danh mục gia sư</Link></li>
              <li><Link to="/search?tab=classes">Lớp học đang mở</Link></li>
              <li><Link to="/documents">Tài liệu công khai</Link></li>
              <li><Link to="/register">Đăng ký làm gia sư</Link></li>
            </ul>
          </div>

          <div className="col-6 col-lg-2 col-md-3 footer-links">
            <h4>Thông tin</h4>
            <ul className="list-unstyled">
              <li><Link to="/about">Về OTSP</Link></li>
              <li><Link to="/guide">Hướng dẫn sử dụng</Link></li>
              <li><Link to="/faq">Câu hỏi thường gặp</Link></li>
              <li><Link to="/privacy">Chính sách bảo mật</Link></li>
              <li><Link to="/terms">Điều khoản sử dụng</Link></li>
              <li><Link to="/contact">Liên hệ hỗ trợ</Link></li>
            </ul>
          </div>

          <div className="col-lg-4 col-md-12 footer-newsletter">
            <h4>Nhận bản tin giáo dục</h4>
            <p>Đăng ký email để nhận thông tin ưu đãi học tập và các mẹo ôn thi hữu ích.</p>
            <form onSubmit={handleNewsletterSubmit} className="php-email-form">
              <div className="newsletter-form d-flex">
                <input
                  type="email"
                  name="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="Nhập địa chỉ email của bạn"
                  className="form-control rounded-pill me-2"
                  disabled={submitting}
                  required
                />
                <button type="submit" className="btn btn-danger rounded-pill px-4" disabled={submitting}>
                  {submitting ? 'Đang gửi…' : 'Đăng ký'}
                </button>
              </div>
              {feedback && (
                <div className={`small mt-2 ${feedback.type === 'success' ? 'text-success' : 'text-danger'}`} role="status">
                  {feedback.text}
                </div>
              )}
            </form>
          </div>
        </div>
      </div>
    </footer>
  );
}

