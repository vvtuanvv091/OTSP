import { useState, useEffect, useRef } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import Header from './Header';
import Footer from './Footer';
import { useAuth } from '../../context/AuthContext';
import tutorApi from '../../api/tutorApi';
import useUnreadNotificationCount from '../../hooks/useUnreadNotificationCount';
import { resolveAssetUrl } from '../../api/config';

export default function TutorLayout({ children }) {
  const { user, isTutor } = useAuth();
  const location = useLocation();
  const shellRef = useRef(null);
  const [sidebarOpen, setSidebarOpen] = useState(window.innerWidth >= 992);
  const [unreadCount] = useUnreadNotificationCount(user);
  const [tutorStatus, setTutorStatus] = useState(user?.verificationStatus || 'Pending');

  const [prevPathname, setPrevPathname] = useState(location.pathname);
  if (prevPathname !== location.pathname) {
    setPrevPathname(location.pathname);
    if (window.innerWidth < 992) {
      setSidebarOpen(false);
    }
  }

  useEffect(() => {
    if (user) {
      const isTutorUser = isTutor ||
        user?.role === 'Tutor' ||
        user?.roles?.some((r) => String(r).toLowerCase() === 'tutor');

      if (isTutorUser) {
        tutorApi.getProfile()
          .then((res) => {
            const status = res.data?.profile?.verificationStatus || res.data?.verificationStatus;
            if (status) setTutorStatus(status);
          })
          .catch(() => {});
      }
    }
  }, [user, location.pathname, isTutor]);

  useEffect(() => {
    const handleResize = () => {
      setSidebarOpen(window.innerWidth >= 992);
    };
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, []);

  useEffect(() => {
    document.body.classList.toggle('student-sidebar-open', sidebarOpen);
    document.body.classList.toggle('student-sidebar-collapsed', !sidebarOpen && window.innerWidth >= 992);
    return () => {
      document.body.classList.remove('student-sidebar-open', 'student-sidebar-collapsed');
    };
  }, [sidebarOpen]);

  useEffect(() => {
    const header = document.getElementById('header');
    const shell = shellRef.current;
    if (!header || !shell) return undefined;

    const syncHeaderOffset = () => {
      const height = Math.round(header.getBoundingClientRect().height || header.offsetHeight);
      shell.style.setProperty('--student-header-offset', `${height}px`);
    };

    syncHeaderOffset();
    window.addEventListener('resize', syncHeaderOffset);
    const observer = window.ResizeObserver ? new ResizeObserver(syncHeaderOffset) : null;
    observer?.observe(header);

    return () => {
      window.removeEventListener('resize', syncHeaderOffset);
      observer?.disconnect();
    };
  }, []);

  return (
    <>
      <Header />

      <div className="student-dashboard-shell tutor-dashboard-shell" ref={shellRef}>
        <aside className="student-sidebar" id="studentSidebar" aria-label="Thanh điều hướng gia sư">
          <div className="student-sidebar-user" id="studentSidebarUser">
            {user?.avatarUrl ? (
              <img
                className="student-sidebar-avatar"
                style={{ width: '40px', height: '40px', maxWidth: '40px', maxHeight: '40px', objectFit: 'cover', borderRadius: '50%' }}
                src={resolveAssetUrl(user.avatarUrl)}
                alt=""
                referrerPolicy="no-referrer"
              />
            ) : (
              <span className="student-sidebar-avatar">
                <i className="bi bi-person-fill" aria-hidden="true"></i>
              </span>
            )}
            <div className="student-sidebar-user-info">
              <strong>{user?.fullName || user?.email || 'Gia sư'}</strong>
              {tutorStatus === 'Approved' ? (
                <small className="text-success fw-semibold">
                  <i className="bi bi-patch-check-fill me-1"></i>Gia sư · Đã xác minh
                </small>
              ) : tutorStatus === 'Rejected' ? (
                <small className="text-danger fw-semibold">
                  <i className="bi bi-x-octagon-fill me-1"></i>Gia sư · Bị từ chối
                </small>
              ) : (
                <small className="text-warning fw-semibold" style={{ color: '#d97706' }}>
                  <i className="bi bi-hourglass-split me-1"></i>Gia sư · Chờ xác nhận
                </small>
              )}
            </div>
          </div>

          <nav className="student-sidebar-nav">
            <span className="student-sidebar-heading">TỔNG QUAN</span>
            <NavLink
              to="/tutor/dashboard"
              end
              className={({ isActive }) => `student-sidebar-link ${isActive ? 'active' : ''}`}
            >
              <i className="bi bi-grid" aria-hidden="true"></i>
              <span>Tổng quan</span>
            </NavLink>

            <span className="student-sidebar-heading">GIẢNG DẠY</span>
            <NavLink
              to="/tutor/schedule"
              className={({ isActive }) => `student-sidebar-link ${isActive ? 'active' : ''}`}
            >
              <i className="bi bi-calendar3" aria-hidden="true"></i>
              <span>Lịch dạy</span>
            </NavLink>
            <NavLink
              to="/tutor/connection-requests"
              className={({ isActive }) => `student-sidebar-link ${isActive ? 'active' : ''}`}
            >
              <i className="bi bi-person-plus" aria-hidden="true"></i>
              <span>Yêu cầu kết nối</span>
            </NavLink>
            <NavLink
              to="/tutor/classrooms"
              className={({ isActive }) => `student-sidebar-link ${isActive ? 'active' : ''}`}
            >
              <i className="bi bi-easel" aria-hidden="true"></i>
              <span>Lớp đang dạy</span>
            </NavLink>
            <NavLink
              to="/tutor/availability"
              className={({ isActive }) => `student-sidebar-link ${isActive ? 'active' : ''}`}
            >
              <i className="bi bi-clock-history" aria-hidden="true"></i>
              <span>Lịch rảnh</span>
            </NavLink>
            <NavLink
              to="/tutor/students"
              className={({ isActive }) => `student-sidebar-link ${isActive ? 'active' : ''}`}
            >
              <i className="bi bi-people" aria-hidden="true"></i>
              <span>Học viên của tôi</span>
            </NavLink>
            <NavLink
              to="/tutor/subjects"
              className={({ isActive }) => `student-sidebar-link ${isActive ? 'active' : ''}`}
            >
              <i className="bi bi-book" aria-hidden="true"></i>
              <span>Môn học</span>
            </NavLink>
            <NavLink
              to="/tutor/documents"
              className={({ isActive }) => `student-sidebar-link ${isActive ? 'active' : ''}`}
            >
              <i className="bi bi-folder2-open" aria-hidden="true"></i>
              <span>Tài liệu</span>
            </NavLink>

            <span className="student-sidebar-heading">TƯƠNG TÁC</span>
            <NavLink
              to="/tutor/reviews"
              className={({ isActive }) => `student-sidebar-link ${isActive ? 'active' : ''}`}
            >
              <i className="bi bi-star" aria-hidden="true"></i>
              <span>Đánh giá</span>
            </NavLink>
            <NavLink
              to="/tutor/chat"
              className={({ isActive }) => `student-sidebar-link ${isActive ? 'active' : ''}`}
            >
              <i className="bi bi-chat-dots" aria-hidden="true"></i>
              <span>Tin nhắn</span>
            </NavLink>
            <NavLink
              to="/tutor/notifications"
              className={({ isActive }) => `student-sidebar-link ${isActive ? 'active' : ''}`}
            >
              <i className="bi bi-bell" aria-hidden="true"></i>
              <span>Thông báo</span>
              {unreadCount > 0 && (
                <span
                  className="badge bg-danger rounded-pill ms-auto px-2"
                  style={{ fontSize: 11, fontWeight: 700 }}
                >
                  {unreadCount > 99 ? '99+' : unreadCount}
                </span>
              )}
            </NavLink>
            <NavLink
              to="/tutor/complaints"
              className={({ isActive }) => `student-sidebar-link ${isActive ? 'active' : ''}`}
            >
              <i className="bi bi-flag" aria-hidden="true"></i>
              <span>Khiếu nại</span>
            </NavLink>

            <span className="student-sidebar-heading">TÀI CHÍNH</span>
            <NavLink
              to="/tutor/income"
              className={({ isActive }) => `student-sidebar-link ${isActive ? 'active' : ''}`}
            >
              <i className="bi bi-credit-card" aria-hidden="true"></i>
              <span>Thu nhập</span>
            </NavLink>

            <span className="student-sidebar-heading">TÀI KHOẢN</span>
            <NavLink
              to="/tutor/profile"
              className={({ isActive }) => `student-sidebar-link ${isActive ? 'active' : ''}`}
            >
              <i className="bi bi-person-vcard" aria-hidden="true"></i>
              <span>Hồ sơ</span>
            </NavLink>
          </nav>
        </aside>

        <button
          type="button"
          className="student-menu-toggle d-lg-none"
          id="studentMenuToggle"
          aria-label="Thu gọn thanh điều hướng"
          aria-controls="studentSidebar"
          aria-expanded={sidebarOpen}
          onClick={() => setSidebarOpen(!sidebarOpen)}
        >
          <i className={`bi ${sidebarOpen ? 'bi-x-lg' : 'bi-list'}`} aria-hidden="true"></i>
        </button>
        <button
          type="button"
          className="student-sidebar-backdrop"
          id="studentSidebarBackdrop"
          aria-label="Đóng thanh điều hướng"
          onClick={() => setSidebarOpen(false)}
        ></button>

        <main id="tutorPageContent" role="main" className="student-dashboard-main tutor-dashboard-main pb-3">
          {children || <Outlet />}
        </main>
      </div>

      <Footer />
    </>
  );
}
