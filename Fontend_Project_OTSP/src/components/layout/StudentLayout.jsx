import { useState, useEffect } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import Header from './Header';
import Footer from './Footer';
import { useAuth } from '../../context/AuthContext';
import useUnreadNotificationCount from '../../hooks/useUnreadNotificationCount';
import { resolveAssetUrl } from '../../api/config';

export default function StudentLayout({ children }) {
  const { user } = useAuth();
  const location = useLocation();
  const [sidebarOpen, setSidebarOpen] = useState(window.innerWidth >= 992);
  const [unreadCount] = useUnreadNotificationCount(user);

  const [prevPathname, setPrevPathname] = useState(location.pathname);
  if (prevPathname !== location.pathname) {
    setPrevPathname(location.pathname);
    if (window.innerWidth < 992) {
      setSidebarOpen(false);
    }
  }

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

  // Đồng bộ độ cao thanh điều hướng Header để thanh bên gắn khít ngay dưới Header
  useEffect(() => {
    const syncHeaderOffset = () => {
      const header = document.getElementById('header');
      const shell = document.querySelector('.student-dashboard-shell');
      if (header && shell) {
        const h = Math.round(header.getBoundingClientRect().height || header.offsetHeight || 140);
        shell.style.setProperty('--student-header-offset', h + 'px');
        document.documentElement.style.setProperty('--student-header-offset', h + 'px');
      }
    };

    syncHeaderOffset();
    const timer = setTimeout(syncHeaderOffset, 150);
    window.addEventListener('resize', syncHeaderOffset);

    let observer;
    const header = document.getElementById('header');
    if (header && window.ResizeObserver) {
      observer = new ResizeObserver(syncHeaderOffset);
      observer.observe(header);
    }

    return () => {
      clearTimeout(timer);
      window.removeEventListener('resize', syncHeaderOffset);
      if (observer) observer.disconnect();
    };
  }, []);

  const path = location.pathname;

  return (
    <>
      <Header />

      <div className="student-dashboard-shell">
        <aside className="student-sidebar" id="studentSidebar" aria-label="Thanh điều hướng học viên">
          <div className="student-sidebar-user" id="studentSidebarUser">
            <div className="d-flex align-items-center gap-2 min-w-0" style={{ minWidth: 0, flex: '1 1 auto' }}>
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
                <strong>{user?.fullName || user?.email || 'Học viên'}</strong>
                <small>Học viên / Phụ huynh</small>
              </div>
            </div>

            {/* Nút thu gọn / đóng thanh bên */}
            <button
              type="button"
              className="student-sidebar-close-btn"
              onClick={() => setSidebarOpen(false)}
              title="Thu gọn menu điều hướng"
              aria-label="Thu gọn menu điều hướng"
            >
              <i className="bi bi-chevron-left d-none d-lg-inline"></i>
              <i className="bi bi-x-lg d-lg-none"></i>
            </button>
          </div>

          <nav className="student-sidebar-nav">
            <span className="student-sidebar-heading">TỔNG QUAN</span>
            <NavLink
              to="/student/dashboard"
              className={({ isActive }) =>
                `student-sidebar-link ${isActive || path === '/student' || path.includes('/StudentDashboard') ? 'active' : ''}`
              }
            >
              <i className="bi bi-grid" aria-hidden="true"></i>
              <span>Tổng quan</span>
            </NavLink>

            <span className="student-sidebar-heading">HỌC TẬP</span>
            <NavLink
              to="/student/schedule"
              className={({ isActive }) =>
                `student-sidebar-link ${isActive || path.includes('/StudentSchedule') ? 'active' : ''}`
              }
            >
              <i className="bi bi-calendar3" aria-hidden="true"></i>
              <span>Lịch học</span>
            </NavLink>
            <NavLink
              to="/student/classrooms"
              className={({ isActive }) =>
                `student-sidebar-link ${isActive || path.includes('/StudentClassroom') ? 'active' : ''}`
              }
            >
              <i className="bi bi-easel" aria-hidden="true"></i>
              <span>Lớp của tôi</span>
            </NavLink>
            <NavLink
              to="/student/connection-requests"
              className={({ isActive }) =>
                `student-sidebar-link ${isActive || path.includes('/Send_Details_Tutor/MyRequests') ? 'active' : ''}`
              }
            >
              <i className="bi bi-person-plus" aria-hidden="true"></i>
              <span>Yêu cầu kết nối</span>
            </NavLink>
            <NavLink
              to="/student/documents"
              className={({ isActive }) =>
                `student-sidebar-link ${isActive || path.includes('/StudentLearningDocument') ? 'active' : ''}`
              }
            >
              <i className="bi bi-folder2-open" aria-hidden="true"></i>
              <span>Tài liệu</span>
            </NavLink>

            <span className="student-sidebar-heading">TƯƠNG TÁC</span>
            <NavLink
              to="/student/chat"
              className={({ isActive }) =>
                `student-sidebar-link ${isActive || path === '/chat' ? 'active' : ''}`
              }
            >
              <i className="bi bi-chat-dots" aria-hidden="true"></i>
              <span>Tin nhắn</span>
            </NavLink>
            <NavLink
              to="/student/notifications"
              className={({ isActive }) =>
                `student-sidebar-link ${isActive || path === '/notifications' ? 'active' : ''}`
              }
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
              to="/student/complaints"
              className={({ isActive }) =>
                `student-sidebar-link ${isActive || path === '/student/complaints' ? 'active' : ''}`
              }
            >
              <i className="bi bi-flag" aria-hidden="true"></i>
              <span>Khiếu nại</span>
            </NavLink>

            <span className="student-sidebar-heading">TÀI CHÍNH &amp; GIA ĐÌNH</span>
            <NavLink
              to="/student/payments"
              className={({ isActive }) =>
                `student-sidebar-link ${isActive || path.includes('/StudentParent_Payment') ? 'active' : ''}`
              }
            >
              <i className="bi bi-credit-card-2-front" aria-hidden="true"></i>
              <span>Học phí &amp; thanh toán</span>
            </NavLink>
            <NavLink
              to="/student/profile#guardians"
              className={() =>
                `student-sidebar-link ${(path.includes('/profile') || path.includes('/Student_Parent_Profile')) && location.hash === '#guardians' ? 'active' : ''}`
              }
            >
              <i className="bi bi-people" aria-hidden="true"></i>
              <span>Người giám hộ</span>
            </NavLink>

            <span className="student-sidebar-heading">TÀI KHOẢN</span>
            <NavLink
              to="/student/profile"
              className={() =>
                `student-sidebar-link ${(path === '/student/profile' || path === '/Student_Parent/Student_Parent_Profile') && location.hash !== '#guardians' ? 'active' : ''}`
              }
            >
              <i className="bi bi-person-vcard" aria-hidden="true"></i>
              <span>Hồ sơ</span>
            </NavLink>
          </nav>
        </aside>

        {!sidebarOpen && (
          <button
            type="button"
            className="student-menu-toggle d-lg-none"
            id="studentMenuToggle"
            aria-label="Mở thanh điều hướng"
            aria-controls="studentSidebar"
            aria-expanded={false}
            onClick={() => setSidebarOpen(true)}
            title="Mở thanh điều hướng"
          >
            <i className="bi bi-list" aria-hidden="true"></i>
          </button>
        )}
        <button
          type="button"
          className="student-sidebar-backdrop"
          id="studentSidebarBackdrop"
          aria-label="Đóng thanh điều hướng"
          onClick={() => setSidebarOpen(false)}
        ></button>

        <main id="studentPageContent" role="main" className="student-dashboard-main pb-3">
          {children || <Outlet />}
        </main>
      </div>

      <Footer />
    </>
  );
}
