import { useState, useEffect, useRef } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import useUnreadNotificationCount from '../../hooks/useUnreadNotificationCount';
import { resolveAssetUrl } from '../../api/config';
import '../../styles/components/Header.css';

export default function Header() {
  const { user, logout, isTutor, isStudent, isAdmin } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [theme, setTheme] = useState(() => localStorage.getItem('otsp-theme') || 'light');
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [userDropdownOpen, setUserDropdownOpen] = useState(false);
  const [unreadCount] = useUnreadNotificationCount(user);

  // Trạng thái mở/đóng menu thả xuống cho Desktop và Mobile
  const [tutorDropdownOpen, setTutorDropdownOpen] = useState(false);
  const [subjectDropdownOpen, setSubjectDropdownOpen] = useState(false);
  const [aboutDropdownOpen, setAboutDropdownOpen] = useState(false);

  // Tự động đóng tất cả dropdown khi chuyển trang
  const [prevPathname, setPrevPathname] = useState(location.pathname);
  if (prevPathname !== location.pathname) {
    setPrevPathname(location.pathname);
    setMobileMenuOpen(false);
    setUserDropdownOpen(false);
    setTutorDropdownOpen(false);
    setSubjectDropdownOpen(false);
    setAboutDropdownOpen(false);
  }

  const dropdownRef = useRef(null);

  useEffect(() => {
    document.documentElement.setAttribute('data-bs-theme', theme);
    localStorage.setItem('otsp-theme', theme);
  }, [theme]);

  // Bấm ra ngoài vùng menu để tự động đóng dropdown người dùng
  useEffect(() => {
    const handleClickOutside = (e) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        setUserDropdownOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  // Đồng bộ class mở drawer trên thẻ body (chống cuộn nền khi mở menu mobile)
  useEffect(() => {
    document.body.classList.toggle('mobile-nav-active', mobileMenuOpen);
    return () => {
      document.body.classList.remove('mobile-nav-active');
    };
  }, [mobileMenuOpen]);

  const toggleTheme = () => {
    setTheme((prev) => (prev === 'dark' ? 'light' : 'dark'));
  };

  const toggleMobileNav = () => {
    setMobileMenuOpen((prev) => !prev);
  };

  const handleLogout = () => {
    navigate('/login', { replace: true, state: { loggedOut: true } });
    logout();
  };

  const userInitial = user?.fullName
    ? user.fullName.trim().charAt(0).toUpperCase()
    : (user?.email ? user.email.charAt(0).toUpperCase() : 'U');

  const roleLabel = isAdmin ? 'Quản trị viên' : isTutor ? 'Gia sư' : 'Học viên / Phụ huynh';
  const dashboardPath = isAdmin ? '/admin/dashboard' : isTutor ? '/tutor/dashboard' : isStudent ? '/student/dashboard' : '/';
  const profilePath = isAdmin ? '/admin/profile' : isTutor ? '/tutor/profile' : isStudent ? '/student/profile' : '/login';
  const chatPath = isAdmin ? '/admin/chat' : isTutor ? '/tutor/chat' : isStudent ? '/student/chat' : '/chat';
  const notificationsPath = isAdmin ? '/admin/notifications' : isTutor ? '/tutor/notifications' : isStudent ? '/student/notifications' : '/notifications';
  const avatarSrc = resolveAssetUrl(user?.avatarUrl);
  const isHomePage = location.pathname === '/';

  return (
    <header id="header" className="header sticky-top" style={{ zIndex: 1050 }}>
      {/* Thanh công cụ phụ phía trên (Top Toolbar) */}
      <div className="header-top">
        <div className="container-fluid container-xl d-flex align-items-center justify-content-between">
          {/* Khẩu hiệu giới thiệu nền tảng */}
          <div className="header-top-tagline text-truncate d-none d-sm-flex align-items-center">
            <span>Nền tảng kết nối Gia sư - Học viên online</span>
          </div>

          {/* Các công cụ góc phải */}
          <div className="header-top-right d-flex align-items-center gap-2 ms-auto">
            {!user ? (
              <>
                {/* Nút chuyển đổi giao diện Sáng / Tối */}
                <button
                  type="button"
                  onClick={toggleTheme}
                  className="header-circle-btn header-theme-toggle d-none d-md-inline-flex"
                  title={theme === 'dark' ? 'Giao diện sáng' : 'Giao diện tối'}
                >
                  <i className={`bi ${theme === 'dark' ? 'bi-sun-fill text-warning' : 'bi-moon-stars'}`}></i>
                </button>

                {/* Cụm nút Đăng nhập & Đăng ký */}
                <div className="header-auth-links d-flex align-items-center gap-2">
                  <Link to="/login" className="btn btn-outline-danger btn-sm rounded-pill px-3 fw-semibold">
                    Đăng nhập
                  </Link>
                  <Link to="/register" className="btn btn-danger btn-sm rounded-pill px-3 fw-semibold" style={{ backgroundColor: '#DB303A' }}>
                    Đăng ký
                  </Link>
                </div>
              </>
            ) : (
              /* Nút thông tin tài khoản người dùng và Menu thả xuống tổng hợp */
              <div className="header-auth-links" ref={dropdownRef}>
                <div className="header-account-tools position-relative">
                  <button
                    type="button"
                    className="header-user-pill-btn"
                    onClick={() => setUserDropdownOpen(!userDropdownOpen)}
                    aria-expanded={userDropdownOpen}
                  >
                    <span
                      className="user-avatar-circle"
                      style={{
                        width: 32,
                        height: 32,
                        minWidth: 32,
                        maxWidth: 32,
                        borderRadius: '50%',
                        overflow: 'hidden',
                        display: 'inline-flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        flexShrink: 0,
                        marginRight: 8
                      }}
                    >
                      {avatarSrc ? (
                        <img
                          className="user-avatar-img"
                          src={avatarSrc}
                          alt="Avatar"
                          width="32"
                          height="32"
                          style={{
                            width: 32,
                            height: 32,
                            maxWidth: 32,
                            maxHeight: 32,
                            borderRadius: '50%',
                            objectFit: 'cover',
                            display: 'block'
                          }}
                          onError={(e) => {
                            e.currentTarget.style.display = 'none';
                            e.currentTarget.parentElement?.querySelector('.user-avatar-initial')?.style.setProperty('display', 'inline');
                          }}
                        />
                      ) : null}
                      <span
                        className="user-avatar-initial"
                        style={{ display: avatarSrc ? 'none' : 'inline' }}
                      >
                        {userInitial}
                      </span>
                    </span>
                    <span className="header-user-name-text">{user.fullName || user.email}</span>
                    <i className="bi bi-chevron-down ms-1 header-user-chevron-icon" aria-hidden="true"></i>

                    {unreadCount > 0 && (
                      <span className="header-pill-notification-badge">{unreadCount > 99 ? '99+' : unreadCount}</span>
                    )}
                  </button>

                  {userDropdownOpen && (
                    <div
                      className="dropdown-menu dropdown-menu-end shadow header-consolidated-menu show"
                      style={{
                        position: 'absolute',
                        top: 'calc(100% + 8px)',
                        right: 0,
                        left: 'auto',
                        bottom: 'auto'
                      }}
                    >
                      {/* 1. Phần thông tin tài khoản */}
                      <div className="header-menu-header-box">
                        <div className="header-menu-header-name text-truncate">{user.fullName || user.email}</div>
                        <div className="header-menu-header-role">{roleLabel}</div>
                      </div>

                      <div className="dropdown-divider"></div>

                      {/* 2. Bảng điều khiển */}
                      <Link
                        className="dropdown-item d-flex align-items-center gap-3"
                        to={dashboardPath}
                        onClick={() => setUserDropdownOpen(false)}
                      >
                        <i className="bi bi-grid header-menu-item-icon"></i>
                        <span>Bảng điều khiển</span>
                      </Link>

                      {/* 3. Trang cá nhân / Hồ sơ */}
                      <Link
                        className="dropdown-item d-flex align-items-center gap-3"
                        to={profilePath}
                        onClick={() => setUserDropdownOpen(false)}
                      >
                        <i className="bi bi-person header-menu-item-icon"></i>
                        <span>Hồ sơ</span>
                      </Link>

                      {/* 4. Thu nhập / Học phí */}
                      {isTutor && (
                        <Link
                          className="dropdown-item d-flex align-items-center gap-3"
                          to="/tutor/income"
                          onClick={() => setUserDropdownOpen(false)}
                        >
                          <i className="bi bi-wallet2 header-menu-item-icon"></i>
                          <span>Thu nhập</span>
                        </Link>
                      )}
                      {isStudent && (
                        <Link
                          className="dropdown-item d-flex align-items-center gap-3"
                          to="/student/payments"
                          onClick={() => setUserDropdownOpen(false)}
                        >
                          <i className="bi bi-wallet2 header-menu-item-icon"></i>
                          <span>Học phí &amp; Lịch sử</span>
                        </Link>
                      )}

                      {/* 5. Hộp thư & Tin nhắn */}
                      <Link
                        className="dropdown-item d-flex align-items-center justify-content-between"
                        to={chatPath}
                        onClick={() => setUserDropdownOpen(false)}
                      >
                        <div className="d-flex align-items-center gap-3">
                          <i className="bi bi-chat-dots header-menu-item-icon"></i>
                          <span>Tin nhắn</span>
                        </div>
                      </Link>

                      <div className="dropdown-divider"></div>

                      {/* 6. Trung tâm thông báo */}
                      <Link
                        className="dropdown-item d-flex align-items-center justify-content-between"
                        to={notificationsPath}
                        onClick={() => setUserDropdownOpen(false)}
                      >
                        <div className="d-flex align-items-center gap-3">
                          <i className="bi bi-bell header-menu-item-icon"></i>
                          <span>Thông báo</span>
                        </div>
                        {unreadCount > 0 && (
                          <span className="badge bg-danger rounded-pill px-2" style={{ fontSize: 11 }}>
                            {unreadCount}
                          </span>
                        )}
                      </Link>

                      {/* 7. Nút bật/tắt chế độ tối */}
                      <div
                        className="dropdown-item d-flex align-items-center justify-content-between user-select-none"
                        onClick={toggleTheme}
                        role="button"
                      >
                        <div className="d-flex align-items-center gap-3">
                          <i className="bi bi-moon-stars header-menu-item-icon"></i>
                          <span>Giao diện tối</span>
                        </div>
                        <div className="form-check form-switch m-0 p-0 d-flex align-items-center">
                          <input
                            className="form-check-input ms-0 header-menu-switch"
                            type="checkbox"
                            role="switch"
                            checked={theme === 'dark'}
                            readOnly
                          />
                        </div>
                      </div>

                      <div className="dropdown-divider"></div>

                      {/* 8. Logout */}
                      <button
                        type="button"
                        className="dropdown-item d-flex align-items-center gap-3 text-danger fw-semibold"
                        onClick={handleLogout}
                      >
                        <i className="bi bi-box-arrow-right header-menu-item-icon text-danger"></i>
                        <span>Đăng xuất</span>
                      </button>
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Thanh điều hướng chính (Main Navigation) */}
      <div className="container-fluid container-xl position-relative header-main-nav">
        <div className="d-flex align-items-center justify-content-between w-100 py-2">
          {/* Logo thương hiệu */}
          <Link to="/" className="logo otsp-brand d-flex flex-column align-items-start text-decoration-none me-auto" aria-label="OTSP - Trang chủ">
            <div className="d-flex align-items-center">
              <span className="otsp-brand-mark"><i className="bi bi-mortarboard-fill" aria-hidden="true"></i></span>
              <span className="otsp-brand-name ms-2">OTSP</span>
            </div>
            <span className="otsp-brand-tagline d-block d-md-none">Nền tảng kết nối gia sư - học viên online</span>
          </Link>

          {/* Danh sách liên kết điều hướng */}
          <nav
            id="navmenu"
            className="navmenu"
            onClick={(e) => {
              if (e.target === e.currentTarget && mobileMenuOpen) {
                toggleMobileNav();
              }
            }}
          >
            <ul>
              {/* Tiêu đề thanh ngăn kéo Mobile (Drawer Header) */}
              <li className="mobile-drawer-header d-xl-none d-flex align-items-center justify-content-between">
                <div className="logo otsp-brand d-flex align-items-center">
                  <span className="otsp-brand-mark"><i className="bi bi-mortarboard-fill" aria-hidden="true"></i></span>
                  <span className="otsp-brand-name ms-2">OTSP</span>
                </div>
                <i className="bi bi-x fs-2" role="button" onClick={toggleMobileNav}></i>
              </li>

              <li className="mobile-drawer-item">
                <Link to="/" className={isHomePage ? 'active' : ''}>
                  Trang chủ
                </Link>
              </li>

              <li className="mobile-drawer-item dropdown" onMouseLeave={() => setTutorDropdownOpen(false)}>
                <a href="#" onClick={(e) => { e.preventDefault(); setTutorDropdownOpen(!tutorDropdownOpen); }}>
                  <span>Gia Sư</span>
                  <i className="bi bi-chevron-down toggle-dropdown"></i>
                </a>
                <ul className={tutorDropdownOpen ? 'dropdown-active' : ''}>
                  <li><Link to="/search">Tìm Gia Sư</Link></li>
                  <li><Link to="/register">Đăng Ký Gia Sư</Link></li>
                </ul>
              </li>

              <li className="mobile-drawer-item dropdown" onMouseLeave={() => setSubjectDropdownOpen(false)}>
                <a href="#" onClick={(e) => { e.preventDefault(); setSubjectDropdownOpen(!subjectDropdownOpen); }}>
                  <span>Môn Học</span>
                  <i className="bi bi-chevron-down toggle-dropdown"></i>
                </a>
                <ul className={subjectDropdownOpen ? 'dropdown-active' : ''}>
                  <li><a href="/#features" onClick={() => setSubjectDropdownOpen(false)}>Danh Sách Môn Học</a></li>
                  <li><Link to="/search?tab=classes" onClick={() => setSubjectDropdownOpen(false)}>Lớp Học Đang Mở</Link></li>
                </ul>
              </li>

              <li className="mobile-drawer-item"><Link to="/documents">Tài liệu công khai</Link></li>
              <li className="mobile-drawer-item"><Link to="/news">Tin tức</Link></li>

              <li className="mobile-drawer-item dropdown" onMouseLeave={() => setAboutDropdownOpen(false)}>
                <a href="#" onClick={(e) => { e.preventDefault(); setAboutDropdownOpen(!aboutDropdownOpen); }}>
                  <span>Giới thiệu</span>
                  <i className="bi bi-chevron-down toggle-dropdown"></i>
                </a>
                <ul className={`about-dropdown-menu ${aboutDropdownOpen ? 'dropdown-active' : ''}`}>
                  <li><Link to="/about" onClick={() => setAboutDropdownOpen(false)}>Về Nền Tảng OTSP</Link></li>
                  <li><Link to="/guide" onClick={() => setAboutDropdownOpen(false)}>Hướng Dẫn Sử Dụng</Link></li>
                  <li><Link to="/faq" onClick={() => setAboutDropdownOpen(false)}>Câu Hỏi Thường Gặp</Link></li>
                  <li><Link to="/terms" onClick={() => setAboutDropdownOpen(false)}>Điều Khoản Sử Dụng</Link></li>
                  <li><Link to="/privacy" onClick={() => setAboutDropdownOpen(false)}>Chính Sách Bảo Mật</Link></li>
                  <li><Link to="/contact" onClick={() => setAboutDropdownOpen(false)}>Liên Hệ</Link></li>
                </ul>
              </li>

              {/* Nút đăng nhập/đăng ký trên thanh menu Mobile */}
              {!user ? (
                <li className="mobile-drawer-auth d-xl-none">
                  <Link to="/login" className="mobile-drawer-btn-login mb-2">
                    Đăng nhập
                  </Link>
                  <Link to="/register" className="mobile-drawer-btn-register">
                    Đăng ký
                  </Link>
                </li>
              ) : (
                <li className="mobile-drawer-auth d-xl-none">
                  <button
                    type="button"
                    onClick={handleLogout}
                    className="btn btn-outline-danger w-100 py-2 fw-semibold rounded-pill d-flex align-items-center justify-content-center gap-2"
                  >
                    <i className="bi bi-box-arrow-right"></i>
                    <span>Đăng xuất</span>
                  </button>
                </li>
              )}
            </ul>
            <i className={`mobile-nav-toggle d-xl-none bi ${mobileMenuOpen ? 'bi-x' : 'bi-list'}`} onClick={toggleMobileNav}></i>
          </nav>

          {/* Nút tìm kiếm gia sư nổi bật trên Desktop */}
          <Link className="btn-getstarted d-none d-md-inline-flex" to="/search">
            Tìm gia sư
          </Link>
        </div>

        {/* Nút tìm gia sư toàn chiều rộng trên Mobile (ẩn trên trang đăng nhập/đăng ký cho gọn) */}
        {!['/login', '/register', '/forgot-password'].includes(location.pathname) && (
          <div className="header-mobile-search-bar d-md-none w-100 pb-2">
            <Link
              className="btn btn-getstarted w-100 text-center py-2 fw-semibold d-flex align-items-center justify-content-center gap-2"
              to="/search"
            >
              <i className="bi bi-search"></i>
              <span>Tìm gia sư</span>
            </Link>
          </div>
        )}
      </div>
    </header>
  );
}
