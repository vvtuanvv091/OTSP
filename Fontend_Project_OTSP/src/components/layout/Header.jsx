import { useState, useEffect, useRef } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import useUnreadNotificationCount from '../../hooks/useUnreadNotificationCount';
import { resolveAssetUrl } from '../../api/config';

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
      {/* Định dạng CSS đồng bộ từ phong cách thẻ người dùng của dự án OTSP */}
      <style>{`
        #header,
        .header.sticky-top {
          z-index: 1050 !important;
          position: sticky !important;
          top: 0 !important;
          backdrop-filter: none !important;
          -webkit-backdrop-filter: none !important;
          transform: none !important;
          filter: none !important;
          background: var(--bs-body-bg, #ffffff) !important;
        }
        [data-bs-theme="dark"] #header,
        [data-bs-theme="dark"] .header.sticky-top {
          background: #14152D !important;
        }
        @media (max-width: 1199.98px) {
          body:not(.mobile-nav-active) .navmenu > ul {
            display: none !important;
          }
          body.mobile-nav-active {
            overflow: hidden !important;
          }
          body.mobile-nav-active .navmenu {
            position: fixed !important;
            top: 0 !important;
            left: 0 !important;
            right: 0 !important;
            bottom: 0 !important;
            width: 100vw !important;
            height: 100vh !important;
            height: 100dvh !important;
            background: rgba(15, 23, 42, 0.65) !important;
            z-index: 9998 !important;
            display: flex !important;
            align-items: flex-start !important;
            justify-content: center !important;
            padding: 16px 14px !important;
            overflow-y: auto !important;
          }
          body.mobile-nav-active .navmenu > ul {
            display: flex !important;
            flex-direction: column !important;
            position: relative !important;
            top: 0 !important;
            left: 0 !important;
            right: 0 !important;
            bottom: auto !important;
            margin: 0 auto !important;
            width: 100% !important;
            max-width: 420px !important;
            max-height: calc(100dvh - 32px) !important;
            background: var(--bs-body-bg, #ffffff) !important;
            border: 1px solid var(--bs-border-color, #e2e8f0) !important;
            border-radius: 18px !important;
            padding: 16px !important;
            overflow-y: auto !important;
            box-shadow: 0 20px 45px rgba(0, 0, 0, 0.35) !important;
          }
          [data-bs-theme="dark"] body.mobile-nav-active .navmenu > ul {
            background: #18181b !important;
            border-color: rgba(255, 255, 255, 0.14) !important;
          }
          body.mobile-nav-active .navmenu > .mobile-nav-toggle {
            display: none !important;
          }
        }
        .header-account-tools {
          display: inline-flex !important;
          align-items: center !important;
          position: relative !important;
        }
        .header-user-pill-btn {
          background: var(--bs-card-bg, #ffffff) !important;
          border: 1.5px solid #0f172a !important;
          border-radius: 9999px !important;
          padding: 3px 14px 3px 4px !important;
          color: var(--bs-body-color, #0f172a) !important;
          display: inline-flex !important;
          align-items: center !important;
          position: relative !important;
          cursor: pointer !important;
          text-decoration: none !important;
          transition: all 0.15s ease-in-out !important;
          max-width: 220px !important;
          user-select: none !important;
          line-height: 1 !important;
          box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05) !important;
        }
        [data-bs-theme="dark"] .header-user-pill-btn {
          background: #1e293b !important;
          border-color: #64748b !important;
          color: #f8fafc !important;
          box-shadow: 0 1px 3px rgba(0, 0, 0, 0.25) !important;
        }
        .header-user-pill-btn:hover,
        .header-user-pill-btn[aria-expanded="true"] {
          border-color: #dc2626 !important;
          box-shadow: 0 2px 10px rgba(220, 38, 38, 0.18) !important;
        }
        .user-avatar-circle {
          width: 32px !important;
          height: 32px !important;
          min-width: 32px !important;
          max-width: 32px !important;
          border-radius: 50% !important;
          background: #e0e7ff !important;
          color: #3730a3 !important;
          font-weight: 700 !important;
          font-size: 14px !important;
          display: inline-flex !important;
          align-items: center !important;
          justify-content: center !important;
          margin-right: 8px !important;
          overflow: hidden !important;
          flex-shrink: 0 !important;
        }
        [data-bs-theme="dark"] .user-avatar-circle {
          background: #334155 !important;
          color: #93c5fd !important;
        }
        .user-avatar-img {
          width: 32px !important;
          height: 32px !important;
          max-width: 32px !important;
          max-height: 32px !important;
          border-radius: 50% !important;
          object-fit: cover !important;
          display: block !important;
          flex-shrink: 0 !important;
        }
        .header-user-name-text {
          font-size: 14px !important;
          font-weight: 600 !important;
          white-space: nowrap !important;
          overflow: hidden !important;
          text-overflow: ellipsis !important;
          max-width: 120px !important;
        }
        .header-user-chevron-icon {
          font-size: 12px !important;
          transition: transform 0.2s ease !important;
          color: var(--bs-secondary-color, #64748b) !important;
        }
        .header-user-pill-btn[aria-expanded="true"] .header-user-chevron-icon {
          transform: rotate(180deg) !important;
        }
        .header-pill-notification-badge {
          position: absolute !important;
          top: -5px !important;
          right: -4px !important;
          min-width: 20px !important;
          height: 20px !important;
          border-radius: 10px !important;
          font-size: 11px !important;
          font-weight: 700 !important;
          display: flex;
          align-items: center !important;
          justify-content: center !important;
          padding: 0 5px !important;
          background-color: #dc2626 !important;
          color: #ffffff !important;
          border: 2px solid var(--bs-body-bg, #ffffff) !important;
          box-shadow: 0 2px 6px rgba(220, 38, 38, 0.45) !important;
          z-index: 10 !important;
          line-height: 1 !important;
        }
        .header-consolidated-menu {
          position: absolute !important;
          top: calc(100% + 8px) !important;
          right: 0 !important;
          bottom: auto !important;
          left: auto !important;
          transform: none !important;
          width: 250px !important;
          min-width: min(250px, calc(100vw - 24px)) !important;
          max-width: min(290px, calc(100vw - 16px)) !important;
          max-height: calc(100vh - 80px) !important;
          max-height: calc(100dvh - 80px) !important;
          overflow-y: auto !important;
          overscroll-behavior: contain !important;
          border-radius: 16px !important;
          border: 1px solid var(--bs-border-color, #e2e8f0) !important;
          box-shadow: 0 14px 40px rgba(0, 0, 0, 0.12) !important;
          background-color: var(--bs-card-bg, #ffffff) !important;
          padding: 6px 0 !important;
          margin-top: 0 !important;
          z-index: 99999 !important;
        }
        [data-bs-theme="dark"] .header-consolidated-menu {
          background-color: #1e293b !important;
          border-color: #334155 !important;
          box-shadow: 0 16px 45px rgba(0, 0, 0, 0.45) !important;
        }
        .header-menu-header-box {
          padding: 10px 18px 8px 18px !important;
        }
        .header-menu-header-name {
          font-size: 15px !important;
          font-weight: 700 !important;
          color: var(--bs-body-color, #0f172a) !important;
          line-height: 1.3 !important;
        }
        .header-menu-header-role {
          font-size: 12.5px !important;
          color: var(--bs-secondary-color, #64748b) !important;
          margin-top: 2px !important;
        }
        .header-menu-item-icon {
          font-size: 17px !important;
          width: 22px !important;
          display: inline-flex !important;
          justify-content: center !important;
          color: var(--bs-secondary-color, #64748b) !important;
          flex-shrink: 0 !important;
        }
        .header-consolidated-menu .dropdown-item {
          padding: 9px 18px !important;
          font-size: 14px !important;
          font-weight: 500 !important;
          color: var(--bs-body-color, #1e293b) !important;
          transition: background-color 0.15s ease !important;
        }
        [data-bs-theme="dark"] .header-consolidated-menu .dropdown-item {
          color: #e2e8f0 !important;
        }
        .header-consolidated-menu .dropdown-item:hover {
          background-color: var(--bs-tertiary-bg, #f1f5f9) !important;
          color: var(--bs-body-color, #0f172a) !important;
        }
        [data-bs-theme="dark"] .header-consolidated-menu .dropdown-item:hover {
          background-color: #334155 !important;
          color: #ffffff !important;
        }
        .header-consolidated-menu .dropdown-divider {
          margin: 4px 0 !important;
          border-top-color: var(--bs-border-color, #f1f5f9) !important;
        }
        [data-bs-theme="dark"] .header-consolidated-menu .dropdown-divider {
          border-top-color: #334155 !important;
        }
        .header-menu-switch {
          cursor: pointer !important;
          width: 38px !important;
          height: 20px !important;
        }
        @media (max-width: 575.98px) {
          .header-user-pill-btn {
            max-width: 160px !important;
            padding: 2px 10px 2px 3px !important;
          }
          .user-avatar-circle {
            width: 28px !important;
            height: 28px !important;
            min-width: 28px !important;
            max-width: 28px !important;
            font-size: 13px !important;
            margin-right: 6px !important;
          }
          .user-avatar-img {
            width: 28px !important;
            height: 28px !important;
            max-width: 28px !important;
            max-height: 28px !important;
          }
          .header-user-name-text {
            max-width: 80px !important;
            font-size: 13px !important;
          }
          .header-consolidated-menu {
            position: absolute !important;
            top: calc(100% + 8px) !important;
            right: 0 !important;
            bottom: auto !important;
            left: auto !important;
            width: 250px !important;
            min-width: min(250px, calc(100vw - 24px)) !important;
            max-width: min(290px, calc(100vw - 16px)) !important;
            max-height: calc(100vh - 100px) !important;
            max-height: calc(100dvh - 100px) !important;
            overflow-y: auto !important;
            overscroll-behavior: contain;
            transform: none !important;
            margin-top: 0 !important;
            border-radius: 14px !important;
          }
        }
      `}</style>
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
