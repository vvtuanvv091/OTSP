import { useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import axiosClient from '../../api/axiosClient';

const NAV_ITEMS = [
  { path: '/guide', label: 'Hướng dẫn sử dụng', icon: 'bi-compass-fill', badge: 'Cẩm nang' },
  { path: '/faq', label: 'Câu hỏi thường gặp', icon: 'bi-patch-question-fill', badge: 'FAQ' },
  { path: '/terms', label: 'Điều khoản sử dụng', icon: 'bi-file-earmark-check-fill', badge: 'Quy định' },
  { path: '/privacy', label: 'Chính sách bảo mật', icon: 'bi-shield-lock-fill', badge: 'Bảo mật' },
  { path: '/contact', label: 'Liên hệ & Hỗ trợ', icon: 'bi-headset', badge: '24/7' },
  { path: '/about', label: 'Về Nền tảng OTSP', icon: 'bi-info-circle-fill', badge: 'Giới thiệu' },
];

export function InfoLayout({ eyebrow, title, subtitle, children }) {
  const location = useLocation();

  return (
    <main className="otsp-info-page">
      <div className="container otsp-info-container">
        <nav aria-label="breadcrumb" className="otsp-info-breadcrumb">
          <Link to="/">Trang chủ</Link>
          <i className="bi bi-chevron-right" />
          <span>{title}</span>
        </nav>

        <header className="otsp-info-hero">
          <h1>{title}</h1>
          <p>{subtitle}</p>
        </header>

        <div className="otsp-info-grid">
          {/* CỘT ĐIỀU HƯỚNG TRÁI */}
          <aside className="otsp-info-sidebar">
            <div className="otsp-info-nav-card">
              <div className="otsp-info-nav-title">Danh mục thông tin</div>
              <div className="otsp-info-nav-list">
                {NAV_ITEMS.map((item) => {
                  const active = location.pathname === item.path;
                  return (
                    <Link
                      key={item.path}
                      to={item.path}
                      className={`otsp-info-nav-item ${active ? 'active' : ''}`}
                      style={{ whiteSpace: 'nowrap', gap: 12, minWidth: 240 }}
                    >
                      <i className={`bi ${item.icon}`} />
                      <span>{item.label}</span>
                      <small>{item.badge}</small>
                    </Link>
                  );
                })}
              </div>
            </div>

            <div className="otsp-info-help-card">
              <div className="otsp-info-help-icon">
                <i className="bi bi-telephone-inbound-fill" />
              </div>
              <h3>Cần hỗ trợ trực tiếp?</h3>
              <p>
                Đội ngũ quản trị viên OTSP luôn sẵn sàng giải đáp mọi thắc mắc về lịch học, học phí và kỹ thuật phòng học.
              </p>
              <div className="otsp-info-help-line">
                <i className="bi bi-telephone-fill text-danger" />
                <span>Hotline: <strong>(+84) 984 391 940</strong></span>
              </div>
              <div className="otsp-info-help-line mb-3">
                <i className="bi bi-envelope-fill text-danger" />
                <span>Email: <strong>tongngoctuan09@gmail.com</strong></span>
              </div>
              <div className="d-grid gap-2">
                <Link to="/chat" className="btn btn-danger btn-sm rounded-pill fw-semibold">
                  <i className="bi bi-chat-dots-fill me-1" /> Chat với Hỗ trợ OTSP
                </Link>
                <Link to="/search" className="btn btn-outline-secondary btn-sm rounded-pill fw-semibold">
                  Tìm gia sư ngay
                </Link>
              </div>
            </div>
          </aside>

          {/* CỘT NỘI DUNG CHÍNH */}
          <section className="otsp-info-content">{children}</section>
        </div>
      </div>

      <style>{`
        .otsp-info-page { min-height: 78vh; padding: 26px 0 68px; background: #f8fafc; color: #1e293b; }
        .otsp-info-container { max-width: 1220px; }
        .otsp-info-breadcrumb { display: flex; align-items: center; gap: 8px; margin-bottom: 14px; font-size: 13px; color: #64748b; }
        .otsp-info-breadcrumb a { color: #64748b; text-decoration: none; font-weight: 600; }
        .otsp-info-breadcrumb a:hover { color: #db303a; }
        .otsp-info-breadcrumb i { font-size: 10px; color: #94a3b8; }
        .otsp-info-breadcrumb span { color: #db303a; font-weight: 700; }

        .otsp-info-hero { padding: 26px 30px; margin-bottom: 24px; border-radius: 16px; background: linear-gradient(135deg, #1e293b 0%, #0f172a 60%, #7f1d1d 100%); color: #fff; box-shadow: 0 8px 24px rgba(15,23,42,.12); }
        .otsp-info-eyebrow { display: inline-flex; align-items: center; padding: 4px 12px; border-radius: 999px; background: rgba(219,48,58,.25); border: 1px solid rgba(254,205,211,.3); color: #fecdd3; font-size: 11px; font-weight: 800; letter-spacing: .08em; margin-bottom: 10px; }
        .otsp-info-hero h1 { margin: 0 0 8px; color: #fff; font-size: clamp(24px, 3vw, 32px); font-weight: 800; letter-spacing: -.02em; }
        .otsp-info-hero p { margin: 0; color: #cbd5e1; font-size: 14.5px; max-width: 760px; line-height: 1.55; }

        .otsp-info-grid { display: grid; grid-template-columns: 290px minmax(0, 1fr); align-items: start; gap: 24px; }
        .otsp-info-sidebar { display: grid; gap: 18px; position: sticky; top: 92px; }
        .otsp-info-nav-card, .otsp-info-help-card, .otsp-info-card { background: #fff; border: 1px solid #e2e8f0; border-radius: 16px; padding: 20px; box-shadow: 0 4px 14px rgba(15,23,42,.04); }
        .otsp-info-nav-title { font-size: 12px; font-weight: 800; text-transform: uppercase; letter-spacing: .06em; color: #64748b; margin-bottom: 10px; padding: 0 6px; }
        .otsp-info-nav-list { display: grid; gap: 5px; }
        .otsp-info-nav-item { display: flex; align-items: center; gap: 10px; padding: 10px 12px; border-radius: 10px; text-decoration: none; color: #334155; font-size: 13.5px; font-weight: 600; transition: all .15s; }
        .otsp-info-nav-item i { font-size: 16px; color: #64748b; }
        .otsp-info-nav-item small { margin-left: auto; font-size: 10.5px; padding: 2px 8px; border-radius: 999px; background: #f1f5f9; color: #64748b; font-weight: 700; }
        .otsp-info-nav-item:hover { background: #fff1f2; color: #db303a; }
        .otsp-info-nav-item:hover i { color: #db303a; }
        .otsp-info-nav-item.active { background: #db303a; color: #fff; box-shadow: 0 4px 12px rgba(219,48,58,.25); }
        .otsp-info-nav-item.active i { color: #fff; }
        .otsp-info-nav-item.active small { background: rgba(255,255,255,.2); color: #fff; }

        .otsp-info-help-card { background: linear-gradient(145deg, #fff, #fff5f5); border-color: #fecdd3; }
        .otsp-info-help-icon { width: 42px; height: 42px; border-radius: 12px; display: grid; place-items: center; background: #db303a; color: #fff; font-size: 19px; margin-bottom: 12px; }
        .otsp-info-help-card h3 { font-size: 15.5px; font-weight: 800; color: #0f172a; margin: 0 0 6px; }
        .otsp-info-help-card p { font-size: 12.5px; color: #475569; line-height: 1.5; margin: 0 0 12px; }
        .otsp-info-help-line { display: flex; align-items: center; gap: 8px; font-size: 12.5px; color: #334155; margin-bottom: 6px; }

        .otsp-info-content { display: grid; gap: 20px; min-width: 0; }
        .otsp-info-card { padding: 26px 28px; }
        .otsp-info-card h2 { font-size: 20px; font-weight: 800; color: #0f172a; margin: 0 0 14px; display: flex; align-items: center; gap: 10px; }
        .otsp-info-card h2 i { color: #db303a; }

        .otsp-step-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; }
        .otsp-step-box { padding: 18px; border: 1px solid #e2e8f0; border-radius: 14px; background: #f8fafc; position: relative; transition: all .18s; }
        .otsp-step-box:hover { background: #fff; border-color: #fecdd3; box-shadow: 0 6px 18px rgba(15,23,42,.05); }
        .otsp-step-num { display: inline-flex; align-items: center; justify-content: center; padding: 3px 10px; border-radius: 999px; background: #db303a; color: #fff; font-size: 11px; font-weight: 800; margin-bottom: 10px; }
        .otsp-step-box h3 { font-size: 15.5px; font-weight: 800; color: #0f172a; margin: 0 0 6px; }
        .otsp-step-box p { font-size: 13px; color: #475569; line-height: 1.55; margin: 0; }

        .otsp-faq-item { border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; background: #fff; margin-bottom: 10px; }
        .otsp-faq-btn { width: 100%; display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 15px 18px; border: 0; background: transparent; text-align: left; font-size: 14.5px; font-weight: 700; color: #0f172a; }
        .otsp-faq-btn:hover { color: #db303a; }
        .otsp-faq-btn.open { background: #fff8f8; color: #db303a; border-bottom: 1px solid #fecdd3; }
        .otsp-faq-body { padding: 16px 18px; font-size: 13.5px; color: #475569; line-height: 1.65; background: #fff; }

        [data-bs-theme="dark"] .otsp-info-page { background: #0f172a; color: #e2e8f0; }
        [data-bs-theme="dark"] .otsp-info-nav-card, [data-bs-theme="dark"] .otsp-info-help-card, [data-bs-theme="dark"] .otsp-info-card, [data-bs-theme="dark"] .otsp-faq-item, [data-bs-theme="dark"] .otsp-faq-body { background: #1e293b; border-color: #334155; color: #e2e8f0; }
        [data-bs-theme="dark"] .otsp-info-card h2, [data-bs-theme="dark"] .otsp-step-box h3, [data-bs-theme="dark"] .otsp-faq-btn, [data-bs-theme="dark"] .otsp-info-help-card h3 { color: #f8fafc; }
        [data-bs-theme="dark"] .otsp-step-box { background: #0f172a; border-color: #334155; }
        [data-bs-theme="dark"] .otsp-step-box p, [data-bs-theme="dark"] .otsp-faq-body, [data-bs-theme="dark"] .otsp-info-help-card p { color: #cbd5e1; }
        [data-bs-theme="dark"] .otsp-info-nav-item:not(.active) { color: #cbd5e1; }
        [data-bs-theme="dark"] .otsp-info-nav-item:not(.active) small { background: #0f172a; color: #94a3b8; }

        @media (max-width: 991.98px) {
          .otsp-info-grid { grid-template-columns: 1fr; }
          .otsp-info-sidebar { position: static; }
        }
        @media (max-width: 575.98px) {
          .otsp-step-grid { grid-template-columns: 1fr; }
          .otsp-info-card { padding: 18px 16px; }
        }
      `}</style>
    </main>
  );
}

/* ============================================================================
 * 1. HƯỚNG DẪN SỬ DỤNG (/guide)
 * ============================================================================ */
export function GuidePage() {
  const [roleTab, setRoleTab] = useState('student');

  const studentSteps = [
    {
      step: 'Bước 01',
      title: 'Tìm kiếm Gia sư hoặc Lớp nhóm mở',
      desc: 'Truy cập mục "Tìm gia sư", lọc theo môn học (Toán, Tiếng Anh…), cấp học (Tiểu học, THCS, THPT), mức học phí và xem hồ sơ bằng cấp đã kiểm duyệt.',
    },
    {
      step: 'Bước 02',
      title: 'Chọn khung giờ trên Ma trận lịch rảnh',
      desc: 'Xem trực tiếp các ca rảnh (2 tiếng/buổi) của gia sư từ Thứ 2 đến Chủ Nhật để gửi yêu cầu học thử hoặc đăng ký vào lớp nhóm đang mở.',
    },
    {
      step: 'Bước 03',
      title: 'Thanh toán học phí tự động qua VietQR',
      desc: 'Quét mã QR ngân hàng với nội dung chuyển khoản chuẩn OTSP{mã_đơn}. Hệ thống SePay tự động xác nhận thanh toán trong vài giây và mở quyền vào lớp.',
    },
    {
      step: 'Bước 04',
      title: 'Tham gia phòng học trực tuyến & Đánh giá',
      desc: 'Nhấn "Vào phòng học" đúng giờ. Hệ thống mặc định tắt Mic/Camera ở màn hình chờ (Pre-join) để bạn tự kiểm tra thiết bị trước khi vào học.',
    },
  ];

  const tutorSteps = [
    {
      step: 'Bước 01',
      title: 'Đăng ký tài khoản & Nộp văn bằng xác thực',
      desc: 'Tạo tài khoản với vai trò Gia sư (hoặc đăng nhập nhanh bằng Google), cập nhật thông tin CCCD, tải lên bằng tốt nghiệp/chứng chỉ để Admin phê duyệt.',
    },
    {
      step: 'Bước 02',
      title: 'Thiết lập Ma trận lịch rảnh & Tạo lớp học',
      desc: 'Đánh dấu các khung giờ có thể giảng dạy tại trang "Lịch rảnh" và mở các lớp học 1-1 hoặc lớp nhóm (tối đa 5-10 học viên) theo môn đã đăng ký.',
    },
    {
      step: 'Bước 03',
      title: 'Duyệt yêu cầu kết nối & Giảng dạy trực tuyến',
      desc: 'Xác nhận lịch học thử của học viên, mở phòng học Jitsi Meet tích hợp sẵn, chia sẻ tài liệu PDF và bấm "Kết thúc buổi học" khi hoàn tất.',
    },
    {
      step: 'Bước 04',
      title: 'Theo dõi Thu nhập 80% & Nhận quyết toán',
      desc: 'Cập nhật số tài khoản ngân hàng để tạo mã VietQR cá nhân tại mục "Thu nhập". Hệ thống tự động thống kê số buổi hoàn thành và số tiền thực nhận (80%).',
    },
  ];

  const currentSteps = roleTab === 'student' ? studentSteps : tutorSteps;

  return (
    <InfoLayout
      title="Hướng Dẫn Sử Dụng OTSP"
      subtitle="Quy trình chi tiết từ bước tìm kiếm gia sư, đặt lịch học thử, thanh toán VietQR cho đến khi tham gia phòng học trực tuyến."
    >
      <div className="otsp-info-card">
        <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-4">
          <h2 className="mb-0">
            <i className="bi bi-signpost-split-fill" /> Quy trình 4 bước theo vai trò
          </h2>
          <div className="btn-group" role="group" aria-label="Chọn vai trò hướng dẫn">
            <button
              type="button"
              className={`btn btn-sm px-3 fw-bold ${roleTab === 'student' ? 'btn-danger' : 'btn-outline-danger'
                }`}
              onClick={() => setRoleTab('student')}
            >
              <i className="bi bi-person-video3 me-1" /> Học viên &amp; Phụ huynh
            </button>
            <button
              type="button"
              className={`btn btn-sm px-3 fw-bold ${roleTab === 'tutor' ? 'btn-danger' : 'btn-outline-danger'
                }`}
              onClick={() => setRoleTab('tutor')}
            >
              <i className="bi bi-easel2-fill me-1" /> Gia sư giảng dạy
            </button>
          </div>
        </div>

        <div className="otsp-step-grid">
          {currentSteps.map((item) => (
            <div key={item.step} className="otsp-step-box">
              <span className="otsp-step-num">{item.step}</span>
              <h3>{item.title}</h3>
              <p>{item.desc}</p>
            </div>
          ))}
        </div>
      </div>

      <div className="otsp-info-card">
        <h2>
          <i className="bi bi-camera-video-fill" /> Lưu ý khi sử dụng Phòng học Video (Jitsi Meet)
        </h2>
        <div className="row g-3">
          <div className="col-md-4">
            <div className="p-3 rounded-3 border h-100">
              <strong className="d-block mb-1 text-danger">
                <i className="bi bi-mic-mute-fill me-1" /> 1. Quyền riêng tư Camera/Mic
              </strong>
              <p className="small text-secondary mb-0">
                Mặc định khi vào phòng, Camera và Micro luôn ở trạng thái <strong>TẮT</strong>. Bạn có thể kiểm tra hình ảnh tại màn hình chờ trước khi chủ động bật.
              </p>
            </div>
          </div>
          <div className="col-md-4">
            <div className="p-3 rounded-3 border h-100">
              <strong className="d-block mb-1 text-danger">
                <i className="bi bi-window-stack me-1" /> 2. Chống nhân đôi tài khoản
              </strong>
              <p className="small text-secondary mb-0">
                Nếu bạn bấm <strong>"Mở Tab riêng"</strong>, hệ thống sẽ tự động đóng khung video ở tab cũ để tránh hiện tượng 2 tài khoản trùng nhau gây vọng âm thanh.
              </p>
            </div>
          </div>
          <div className="col-md-4">
            <div className="p-3 rounded-3 border h-100">
              <strong className="d-block mb-1 text-danger">
                <i className="bi bi-check2-circle me-1" /> 3. Kết thúc buổi học
              </strong>
              <p className="small text-secondary mb-0">
                Sau khi giảng dạy xong, Gia sư bấm nút <strong>"Kết thúc buổi học"</strong> để hệ thống ghi nhận điểm danh và tính vào thu nhập trong tháng.
              </p>
            </div>
          </div>
        </div>
      </div>
    </InfoLayout>
  );
}

/* ============================================================================
 * 2. CÂU HỎI THƯỜNG GẶP (/faq)
 * ============================================================================ */
const FAQ_LIST = [
  {
    category: 'Học thử & Đăng ký',
    q: 'Làm thế nào để đặt lịch học thử với gia sư trên OTSP?',
    a: 'Bạn chỉ cần vào trang "Tìm gia sư", bấm vào hồ sơ gia sư mong muốn, chọn môn học, khung giờ rảnh trên bảng lịch tuần và gửi yêu cầu. Gia sư sẽ xác nhận và hệ thống tự động tạo phòng học trực tuyến cho buổi học thử.',
  },
  {
    category: 'Học thử & Đăng ký',
    q: 'Học sinh dưới 18 tuổi đăng ký tài khoản cần lưu ý gì?',
    a: 'Để đảm bảo an toàn cho học sinh phổ thông, khi ngày sinh thể hiện dưới 18 tuổi, hệ thống sẽ yêu cầu bổ sung Họ tên và Số điện thoại của Phụ huynh / Người giám hộ để phối hợp theo dõi lịch học và học phí.',
  },
  {
    category: 'Học phí & Hoàn tiền',
    q: 'Thanh toán học phí qua mã QR mất bao lâu để được xác nhận?',
    a: 'Hệ thống tích hợp cổng đối soát tự động SePay và VietQR. Ngay khi bạn chuyển khoản đúng số tiền và giữ nguyên nội dung mã đơn (ví dụ: OTSP12), trạng thái thanh toán sẽ chuyển sang "Thành công" chỉ sau 3 – 10 giây.',
  },
  {
    category: 'Học phí & Hoàn tiền',
    q: 'Nếu gia sư hủy lớp hoặc buổi học không diễn ra thì học phí xử lý thế nào?',
    a: 'Học phí của học viên được OTSP bảo lãnh cho đến khi các buổi học hoàn thành. Nếu phát sinh sự cố từ phía gia sư, bạn có thể gửi yêu cầu tại mục "Khiếu nại" để Admin xác minh và hỗ trợ đổi gia sư hoặc hoàn trả học phí tương ứng.',
  },
  {
    category: 'Phòng học Video',
    q: 'Tại sao vào phòng học Camera và Micro không tự bật?',
    a: 'Để tôn trọng quyền riêng tư của cả học viên và gia sư, OTSP thiết lập chế độ mặc định tắt sẵn Camera và Micro khi mới mở phòng. Bạn hoàn toàn chủ động bấm biểu tượng Micro/Camera trên thanh công cụ khi đã sẵn sàng.',
  },
  {
    category: 'Gia sư & Đối soát',
    q: 'Gia sư nhận thanh toán học phí từ nền tảng như thế nào?',
    a: 'Mỗi buổi học sau khi bấm "Kết thúc buổi học" (trạng thái Hoàn thành) sẽ được cộng vào doanh thu tháng. Nền tảng thu 20% phí vận hành, gia sư thực nhận 80% tổng học phí qua tài khoản ngân hàng VietQR đã đăng ký ở mục "Thu nhập".',
  },
];

export function FaqPage() {
  const [openIndex, setOpenIndex] = useState(0);
  const [selectedCat, setSelectedCat] = useState('Tất cả');
  const categories = ['Tất cả', 'Học thử & Đăng ký', 'Học phí & Hoàn tiền', 'Phòng học Video', 'Gia sư & Đối soát'];

  const filtered = FAQ_LIST.filter(
    (item) => selectedCat === 'Tất cả' || item.category === selectedCat
  );

  return (
    <InfoLayout
      title="Câu Hỏi Thường Gặp (FAQ)"
      subtitle="Tổng hợp những câu hỏi phổ biến nhất từ phụ huynh, học viên và gia sư trong quá trình dạy và học trên OTSP."
    >
      <div className="otsp-info-card">
        <div className="d-flex flex-wrap gap-2 mb-4">
          {categories.map((cat) => (
            <button
              key={cat}
              type="button"
              className={`btn btn-sm rounded-pill px-3 fw-semibold ${selectedCat === cat ? 'btn-danger' : 'btn-outline-secondary'
                }`}
              onClick={() => {
                setSelectedCat(cat);
                setOpenIndex(0);
              }}
            >
              {cat}
            </button>
          ))}
        </div>

        <div>
          {filtered.map((item, idx) => {
            const isOpen = openIndex === idx;
            return (
              <div key={item.q} className="otsp-faq-item">
                <button
                  type="button"
                  className={`otsp-faq-btn ${isOpen ? 'open' : ''}`}
                  onClick={() => setOpenIndex(isOpen ? -1 : idx)}
                >
                  <span>
                    <span className="badge bg-danger-subtle text-danger me-2">
                      {item.category}
                    </span>
                    {item.q}
                  </span>
                  <i className={`bi ${isOpen ? 'bi-chevron-up' : 'bi-chevron-down'}`} />
                </button>
                {isOpen && <div className="otsp-faq-body">{item.a}</div>}
              </div>
            );
          })}
        </div>
      </div>
    </InfoLayout>
  );
}

/* ============================================================================
 * 3. ĐIỀU KHOẢN SỬ DỤNG (/terms)
 * ============================================================================ */
export function TermsPage() {
  return (
    <InfoLayout
      title="Điều Khoản Sử Dụng"
      subtitle="Các quy định pháp lý, quyền lợi và trách nhiệm của Học viên, Phụ huynh và Gia sư khi tham gia hệ sinh thái giáo dục OTSP."
    >
      <div className="otsp-info-card">
        <h2>
          <i className="bi bi-1-circle-fill" /> Điều khoản về Tài khoản &amp; Độ tuổi
        </h2>
        <p className="text-secondary">
          Người dùng khi đăng ký tài khoản trên OTSP cam kết cung cấp thông tin họ tên, email, số điện thoại và giấy tờ định danh chính xác:
        </p>
        <ul className="text-secondary small lh-lg mb-0">
          <li>
            <strong>Đối với Gia sư:</strong> Bắt buộc từ đủ 18 tuổi trở lên, cung cấp đầy đủ thông tin CCCD và văn bằng/chứng chỉ học thuật để Ban quản trị đối soát trước khi nhận lớp.
          </li>
          <li>
            <strong>Đối với Học viên dưới 18 tuổi:</strong> Cần có sự đồng ý và thông tin liên hệ của Phụ huynh hoặc Người giám hộ hợp pháp trên hồ sơ đăng ký.
          </li>
        </ul>
      </div>

      <div className="otsp-info-card">
        <h2>
          <i className="bi bi-2-circle-fill" /> Quy định về Lớp học, Lịch dạy &amp; Điểm danh
        </h2>
        <ul className="text-secondary small lh-lg mb-0">
          <li>
            Lịch học chính thức chỉ được tính từ <strong>Ngày bắt đầu khóa học (StartDate)</strong> đã công bố trên thông tin lớp học.
          </li>
          <li>
            Mỗi buổi học tiêu chuẩn kéo dài 120 phút (2 giờ). Gia sư có trách nhiệm mở phòng đúng giờ và bấm kết thúc buổi học sau khi hoàn thành giảng dạy.
          </li>
          <li>
            Trường hợp cần thay đổi hoặc hủy lịch đột xuất, hai bên cần thông báo trước tối thiểu 06 giờ qua hệ thống Tin nhắn OTSP.
          </li>
        </ul>
      </div>

      <div className="otsp-info-card">
        <h2>
          <i className="bi bi-3-circle-fill" /> Chính sách Học phí &amp; Đối soát doanh thu
        </h2>
        <ul className="text-secondary small lh-lg mb-0">
          <li>
            Học viên thanh toán học phí qua cổng VietQR / SePay theo đúng mã định danh đơn hàng <code>OTSP&#123;id&#125;</code>.
          </li>
          <li>
            Tỷ lệ phân chia doanh thu minh bạch: <strong>Gia sư nhận 80%</strong> học phí các buổi đã hoàn thành; <strong>Nền tảng OTSP thu 20%</strong> phí vận hành hạ tầng, phòng học video và kiểm duyệt.
          </li>
          <li>
            Mọi hành vi giao dịch ngầm ngoài hệ thống nhằm trốn phí hoặc gian lận lịch học sẽ dẫn đến khóa tài khoản vĩnh viễn.
          </li>
        </ul>
      </div>
    </InfoLayout>
  );
}

/* ============================================================================
 * 4. LIÊN HỆ & HỖ TRỢ (/contact)
 * ============================================================================ */
export function ContactPage() {
  const [form, setForm] = useState({
    fullName: '',
    email: '',
    topic: 'Tư vấn chọn gia sư / lớp học',
    message: '',
  });
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);
    setErrorMsg('');
    try {
      await axiosClient.post('/home/contact', form);
      setSubmitted(true);
    } catch (err) {
      setErrorMsg(err.response?.data?.message || err.userMessage || 'Không thể gửi thông tin. Vui lòng thử lại sau.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <InfoLayout
      title="Liên Hệ &amp; Hỗ Trợ"
      subtitle="Đội ngũ tư vấn giáo dục và hỗ trợ kỹ thuật của OTSP luôn sẵn sàng lắng nghe và đồng hành cùng bạn."
    >
      <div className="row g-3">
        <div className="col-md-4">
          <div className="otsp-info-card h-100 text-center">
            <div className="mx-auto mb-2 d-grid" style={{ width: 46, height: 46, placeItems: 'center', borderRadius: 12, background: '#fff1f2', color: '#db303a', fontSize: 20 }}>
              <i className="bi bi-telephone-outbound-fill" />
            </div>
            <h3 className="h6 fw-bold mb-1">Tổng đài Hotline</h3>
            <p className="text-danger fw-bold mb-1">(+84) 984 391 940</p>
            <small className="text-muted">Hoạt động 08:00 – 22:00 hàng ngày</small>
          </div>
        </div>
        <div className="col-md-4">
          <div className="otsp-info-card h-100 text-center">
            <div className="mx-auto mb-2 d-grid" style={{ width: 46, height: 46, placeItems: 'center', borderRadius: 12, background: '#fff1f2', color: '#db303a', fontSize: 20 }}>
              <i className="bi bi-envelope-check-fill" />
            </div>
            <h3 className="h6 fw-bold mb-1">Hộp thư Hỗ trợ</h3>
            <p className="text-danger fw-bold mb-1">tongngoctuan09@gmail.com</p>
            <small className="text-muted">Phản hồi trong vòng 2 giờ làm việc</small>
          </div>
        </div>
        <div className="col-md-4">
          <div className="otsp-info-card h-100 text-center">
            <div className="mx-auto mb-2 d-grid" style={{ width: 46, height: 46, placeItems: 'center', borderRadius: 12, background: '#fff1f2', color: '#db303a', fontSize: 20 }}>
              <i className="bi bi-geo-alt-fill" />
            </div>
            <h3 className="h6 fw-bold mb-1">Văn phòng điều hành</h3>
            <p className="fw-semibold mb-1 small">Khu Công Nghệ Giáo Dục OTSP</p>
            <small className="text-muted">Biên Hòa – TP. Hồ Chí Minh – Hà Nội</small>
          </div>
        </div>
      </div>

      <div className="otsp-info-card">
        <h2>
          <i className="bi bi-send-check-fill" /> Gửi yêu cầu tư vấn hoặc phản hồi
        </h2>
        {submitted ? (
          <div className="alert alert-success rounded-4 p-4 mb-0">
            <h5 className="fw-bold mb-2">
              <i className="bi bi-check-circle-fill me-2" />
              Đã gửi thông tin liên hệ thành công!
            </h5>
            <p className="small mb-3">
              Cảm ơn <strong>{form.fullName}</strong>. Bộ phận CSKH của OTSP đã ghi nhận nội dung về <em>"{form.topic}"</em> và sẽ phản hồi qua email <strong>{form.email}</strong> sớm nhất.
            </p>
            <button
              type="button"
              className="btn btn-sm btn-outline-success rounded-pill px-3"
              onClick={() => {
                setSubmitted(false);
                setForm({ fullName: '', email: '', topic: 'Tư vấn chọn gia sư / lớp học', message: '' });
              }}
            >
              Gửi yêu cầu khác
            </button>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="row g-3">
            <div className="col-md-6">
              <label className="form-label small fw-bold">Họ và tên của bạn *</label>
              <input
                type="text"
                required
                className="form-control"
                value={form.fullName}
                onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                placeholder="Nhập họ và tên…"
              />
            </div>
            <div className="col-md-6">
              <label className="form-label small fw-bold">Email hoặc Số điện thoại *</label>
              <input
                type="text"
                required
                className="form-control"
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
                placeholder="VD: example@gmail.com / 098xxxxxxx"
              />
            </div>
            <div className="col-12">
              <label className="form-label small fw-bold">Chủ đề cần hỗ trợ</label>
              <select
                className="form-select"
                value={form.topic}
                onChange={(e) => setForm({ ...form, topic: e.target.value })}
              >
                <option>Tư vấn chọn gia sư / lớp học</option>
                <option>Hỗ trợ thanh toán học phí VietQR</option>
                <option>Hỗ trợ duyệt hồ sơ Gia sư</option>
                <option>Báo lỗi kỹ thuật phòng học Video</option>
                <option>Hợp tác giáo dục &amp; Khác</option>
              </select>
            </div>
            <div className="col-12">
              <label className="form-label small fw-bold">Nội dung chi tiết *</label>
              <textarea
                rows={4}
                required
                className="form-control"
                value={form.message}
                onChange={(e) => setForm({ ...form, message: e.target.value })}
                placeholder="Mô tả chi tiết nhu cầu học tập hoặc vấn đề bạn cần hỗ trợ…"
              />
            </div>
            {errorMsg && (
              <div className="col-12">
                <div className="alert alert-danger py-2 small mb-0" role="alert">
                  <i className="bi bi-exclamation-triangle-fill me-1" />
                  {errorMsg}
                </div>
              </div>
            )}
            <div className="col-12 d-flex flex-wrap justify-content-between align-items-center gap-3 pt-2">
              <span className="small text-muted">
                <i className="bi bi-shield-check text-success me-1" />
                Thông tin của bạn được bảo mật tuyệt đối theo tiêu chuẩn OTSP.
              </span>
              <button
                type="submit"
                className="btn btn-danger rounded-pill px-4 fw-bold"
                disabled={submitting}
              >
                {submitting ? (
                  <>
                    <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true" />
                    Đang gửi…
                  </>
                ) : (
                  <>
                    <i className="bi bi-paperplane-fill me-1" /> Gửi thông tin ngay
                  </>
                )}
              </button>
            </div>
          </form>
        )}
      </div>
    </InfoLayout>
  );
}

/* ============================================================================
 * 5. VỀ NỀN TẢNG OTSP (/about)
 * ============================================================================ */
export function AboutCenterPage() {
  return (
    <InfoLayout
      title="Nền Tảng Kết Nối Gia Sư OTSP"
      subtitle="Nền tảng công nghệ giáo dục kết nối trực tiếp Gia sư chất lượng cao và Học viên trên toàn quốc với quy trình kiểm duyệt minh bạch."
    >
      <div className="otsp-info-card">
        <h2>
          <i className="bi bi-award-fill" /> Sứ mệnh &amp; Giá trị cốt lõi của OTSP
        </h2>
        <p className="text-secondary">
          <strong>OTSP (Online Tutoring &amp; Smart Platform)</strong> ra đời nhằm giải quyết triệt để bài toán tìm kiếm gia sư uy tín của các bậc phụ huynh, đồng thời mang lại môi trường giảng dạy trực tuyến chuyên nghiệp, tự động hóa từ khâu xếp lịch đến đối soát học phí.
        </p>
        <div className="otsp-step-grid mt-3">
          <div className="otsp-step-box">
            <span className="otsp-step-num">100% Kiểm duyệt</span>
            <h3>Xác thực văn bằng &amp; CCCD</h3>
            <p>
              Mọi hồ sơ gia sư trên hệ thống đều được quản trị viên kiểm tra bằng cấp, chứng chỉ chuyên môn trước khi hiển thị công khai.
            </p>
          </div>
          <div className="otsp-step-box">
            <span className="otsp-step-num">Linh hoạt</span>
            <h3>Học thử &amp; Học nhóm tiết kiệm</h3>
            <p>
              Hỗ trợ đặt lịch học thử 1-1 theo khung giờ rảnh thực tế và tham gia các lớp nhóm nhỏ chất lượng cao với chi phí tối ưu.
            </p>
          </div>
        </div>
      </div>
    </InfoLayout>
  );
}

