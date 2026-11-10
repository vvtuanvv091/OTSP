import { useEffect, useState, Suspense } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import Header from './Header';
import Footer from './Footer';
import { useAuth } from '../../context/AuthContext';
import useUnreadNotificationCount from '../../hooks/useUnreadNotificationCount';
import { resolveAssetUrl } from '../../api/config';
import '../../styles/admin-layout.css';

export default function AdminLayout() {
  const { user } = useAuth();
  const [unreadCount] = useUnreadNotificationCount(user);
  const location = useLocation();
  const [sidebarOpen, setSidebarOpen] = useState(window.innerWidth >= 992);

  const [prevPathname, setPrevPathname] = useState(location.pathname);
  if (prevPathname !== location.pathname) {
    setPrevPathname(location.pathname);
    if (window.innerWidth < 992) {
      setSidebarOpen(false);
    }
  }

  useEffect(() => {
    const handleResize = () => {
      if (window.innerWidth < 992) {
        setSidebarOpen(false);
      } else {
        setSidebarOpen(true);
      }
    };
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, []);

  useEffect(() => {
    if (sidebarOpen) {
      document.body.classList.add('admin-sidebar-open');
      document.body.classList.remove('admin-sidebar-collapsed');
    } else {
      document.body.classList.remove('admin-sidebar-open');
      document.body.classList.add('admin-sidebar-collapsed');
    }
    return () => {
      document.body.classList.remove('admin-sidebar-open');
      document.body.classList.remove('admin-sidebar-collapsed');
    };
  }, [sidebarOpen]);

  useEffect(() => {
    const header = document.getElementById('header');
    const shell = document.querySelector('.admin-layout-shell');
    if (!header || !shell) return undefined;

    const syncHeaderOffset = () => {
      shell.style.setProperty('--admin-header-offset', `${header.offsetHeight}px`);
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

  const adminLink = (label, icon, to) => (
    <NavLink to={to} className={({ isActive }) => `admin-sidebar-link ${isActive ? 'active' : ''}`} key={label}>
      <i className={`bi ${icon}`} aria-hidden="true"></i>
      <span>{label}</span>
    </NavLink>
  );

  return (
    <div className="admin-layout d-flex flex-column min-vh-100">
      <Header />

      <div className="admin-layout-shell flex-grow-1">
        <aside className="admin-sidebar" id="adminSidebar" aria-label="Thanh điều hướng quản trị">
          <div className="admin-sidebar-user" id="adminSidebarUser">
            <div className="d-flex align-items-center gap-2 min-w-0">
              {user?.avatarUrl ? (
                <img
                  className="admin-sidebar-avatar"
                  src={resolveAssetUrl(user.avatarUrl)}
                  alt=""
                  referrerPolicy="no-referrer"
                  onError={(e) => {
                    e.currentTarget.style.display = 'none';
                  }}
                />
              ) : (
                <span className="admin-sidebar-avatar"><i className="bi bi-person-fill" aria-hidden="true" /></span>
              )}
              <span className="admin-sidebar-user-info">
                <strong>{user?.fullName || user?.email || 'Quản trị viên'}</strong>
                <small className="text-success fw-semibold">
                  <i className="bi bi-patch-check-fill me-1" aria-hidden="true" />Quản trị viên · Đã xác minh
                </small>
              </span>
            </div>
            <button
              type="button"
              className="admin-sidebar-close-btn"
              onClick={() => setSidebarOpen(false)}
              title="Thu gọn thanh điều hướng"
              aria-label="Thu gọn thanh điều hướng"
            >
              <i className="bi bi-chevron-left" aria-hidden="true" />
            </button>
          </div>

          <nav className="admin-sidebar-nav">
            <span className="admin-sidebar-heading">TỔNG QUAN</span>
            <NavLink to="/admin/dashboard" end className={({ isActive }) => `admin-sidebar-link ${isActive ? 'active' : ''}`}>
              <i className="bi bi-grid" aria-hidden="true" /><span>Dashboard</span>
            </NavLink>

            <span className="admin-sidebar-heading">NGƯỜI DÙNG</span>
            <NavLink to="/admin/tutor-approvals" className={({ isActive }) => `admin-sidebar-link ${isActive ? 'active' : ''}`}>
              <i className="bi bi-person-check" aria-hidden="true" /><span>Duyệt hồ sơ gia sư</span>
            </NavLink>
            {adminLink('Gia sư', 'bi-mortarboard', '/admin/tutors')}
            {adminLink('Học viên / Phụ huynh', 'bi-person', '/admin/students')}

            <span className="admin-sidebar-heading">HỌC TẬP</span>
            {adminLink('Yêu cầu kết nối', 'bi-person-plus', '/admin/connection-requests')}
            {adminLink('Lớp học & lịch dạy', 'bi-calendar3', '/admin/classrooms')}
            {adminLink('Môn học / Cấp học', 'bi-book', '/admin/catalog')}
            {adminLink('Tài liệu ôn tập', 'bi-files', '/admin/learning-documents')}

            <span className="admin-sidebar-heading">TƯƠNG TÁC</span>
            {adminLink('Đánh giá & nhận xét', 'bi-star', '/admin/reviews')}
            {adminLink('Trò chuyện hỗ trợ', 'bi-chat-dots', '/admin/chat')}
            <NavLink to="/admin/notifications" className={({ isActive }) => `admin-sidebar-link ${isActive ? 'active' : ''}`}>
              <i className="bi bi-bell" aria-hidden="true" /><span>Thông báo hệ thống</span>
              {unreadCount > 0 && <span className="badge bg-danger rounded-pill ms-auto">{unreadCount > 99 ? '99+' : unreadCount}</span>}
            </NavLink>
            {adminLink('Tin nhắn báo cáo', 'bi-chat', '/admin/messages')}
            {adminLink('Khiếu nại', 'bi-flag', '/admin/complaints')}
            {adminLink('Tin tức & thông báo', 'bi-newspaper', '/admin/news')}

            <span className="admin-sidebar-heading">TÀI CHÍNH</span>
            {adminLink('Giao dịch học phí', 'bi-credit-card', '/admin/payments')}
            {adminLink('Doanh thu & đối soát', 'bi-graph-up', '/admin/reconciliation')}

            <span className="admin-sidebar-heading">QUẢN TRỊ</span>
            {adminLink('Hồ sơ admin', 'bi-person-vcard', '/admin/profile')}
            {adminLink('Phân quyền tài khoản', 'bi-shield-check', '/admin/accounts')}
            {adminLink('Nhật ký thao tác', 'bi-clock-history', '/admin/audit-logs')}
            {adminLink('Cảnh báo bất thường', 'bi-exclamation-triangle', '/admin/alerts')}
            {adminLink('Tính năng AI', 'bi-stars', '/admin/ai')}

          </nav>
        </aside>

        {!sidebarOpen && (
          <button
            type="button"
            className="admin-menu-toggle"
            aria-label="Mở thanh điều hướng"
            aria-controls="adminSidebar"
            aria-expanded={sidebarOpen}
            onClick={() => setSidebarOpen(true)}
            title="Mở thanh điều hướng"
          >
            <i className="bi bi-list" aria-hidden="true" />
          </button>
        )}
        <button type="button" className="admin-sidebar-backdrop" aria-label="Đóng thanh điều hướng" onClick={() => setSidebarOpen(false)} />

        <main id="adminPageContent" role="main" className="admin-layout-main">
          <Suspense
            fallback={
              <div className="d-flex flex-column align-items-center justify-content-center py-5 text-secondary" style={{ minHeight: '50vh' }}>
                <span className="spinner-border spinner-border-sm text-danger mb-2" role="status" aria-hidden="true" />
                <span className="small">Đang mở trang quản trị…</span>
              </div>
            }
          >
            <Outlet />
          </Suspense>
        </main>
      </div>

      <Footer />
    </div>
  );
}
