import { useEffect, useMemo, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import notificationApi from '../../api/notificationApi';

const PAGE_SIZE = 10;

function getRoleBase(user, isTutor, isStudent, isAdmin) {
  const roles = (user?.roles || []).map((role) => String(role).toLowerCase());
  const role = String(user?.role || '').toLowerCase();
  if (isAdmin || role === 'admin' || roles.includes('admin')) return '/admin';
  if (isTutor || role === 'tutor' || roles.includes('tutor')) return '/tutor';
  if (isStudent || ['student', 'student_parent'].includes(role) || roles.some((item) => ['student', 'student_parent'].includes(item))) return '/student';
  return '';
}

function getNotificationTarget(notification, roleBase) {
  const rawTarget = notification?.targetUrl || '';
  const [path, query = ''] = rawTarget.split('?');
  const normalizedPath = path.toLowerCase();
  const suffix = query ? `?${query}` : '';

  const isAdmin = roleBase === '/admin';
  const portalBase = roleBase || '/student';
  if (normalizedPath.startsWith('/room/') || normalizedPath.includes('/room/')) {
    const roomIdx = normalizedPath.indexOf('/room/');
    const roomPath = path.slice(roomIdx);
    return `${roomPath}${suffix}`;
  }
  if (normalizedPath.includes('/chat')) return roleBase ? `${roleBase}/chat${suffix}` : `/chat${suffix}`;
  if (
    normalizedPath.includes('connectionrequest') ||
    normalizedPath.includes('connection-request') ||
    normalizedPath.includes('send_details_tutor')
  ) {
    return isAdmin ? '/admin/connection-requests' : `${portalBase}/connection-requests`;
  }
  if (normalizedPath.includes('complaint')) {
    return isAdmin ? '/admin/complaints' : `${portalBase}/complaints`;
  }
  if (normalizedPath.includes('studentschedule') || normalizedPath.includes('/schedule')) {
    return isAdmin ? '/admin/classrooms' : `${portalBase}/schedule${suffix}`;
  }
  if (normalizedPath.includes('studentparent_payment') || normalizedPath.includes('/payments')) {
    if (normalizedPath.startsWith('/student/payments/') && roleBase !== '/tutor' && !isAdmin) {
      return `${path}${suffix}`;
    }
    return isAdmin ? '/admin/payments' : roleBase === '/tutor' ? '/tutor/income' : `/student/payments${suffix}`;
  }
  if (normalizedPath.includes('/income') || normalizedPath.includes('tutorincome')) {
    return isAdmin ? '/admin/reconciliation' : '/tutor/income';
  }
  if (normalizedPath.includes('tutor-approvals')) {
    return '/admin/tutor-approvals';
  }
  if (normalizedPath.includes('availability')) {
    return isAdmin ? '/admin/tutors' : roleBase === '/tutor' ? '/tutor/availability' : `${portalBase}/classrooms`;
  }
  if (normalizedPath.includes('classroom')) {
    return isAdmin ? '/admin/classrooms' : `${portalBase}/classrooms`;
  }
  if (normalizedPath.includes('learningdocument') || normalizedPath.includes('/documents')) {
    return isAdmin ? '/admin/learning-documents' : `${portalBase}/documents`;
  }
  if (normalizedPath.includes('profile')) {
    return isAdmin ? '/admin/accounts' : `${portalBase}/profile`;
  }
  if (normalizedPath.includes('review')) {
    return isAdmin ? '/admin/reviews' : roleBase === '/tutor' ? '/tutor/reviews' : `${portalBase}/classrooms`;
  }
  return roleBase ? `${roleBase}/notifications` : '/notifications';
}

function getContent(notification) {
  if (notification?.displayContent) return notification.displayContent;
  return String(notification?.content || '').replace(/\s*\[url:[^\]]+\]\s*/i, '').trim();
}

export default function NotificationsPage() {
  const { user, isTutor, isStudent, isAdmin } = useAuth();
  const location = useLocation();
  const [items, setItems] = useState([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [busyId, setBusyId] = useState(null);
  const roleBase = useMemo(() => getRoleBase(user, isTutor, isStudent, isAdmin), [user, isTutor, isStudent, isAdmin]);

  useEffect(() => {
    let active = true;
    async function loadNotifications() {
      if (!user) {
        if (active) {
          setItems([]);
          setLoading(false);
        }
        return;
      }
      setLoading(true);
      setError('');
      try {
        const response = await notificationApi.getNotifications({ page, pageSize: PAGE_SIZE });
        if (!active) return;
        const data = response.data || {};
        setItems(Array.isArray(data.items) ? data.items : []);
        setPage(data.page || page);
        setTotalPages(Math.max(1, data.totalPages || 1));
        setTotalCount(data.totalCount || 0);
        setUnreadCount(data.unreadCount || 0);
        window.dispatchEvent(new CustomEvent('otsp:notification-count', { detail: { unreadCount: data.unreadCount || 0 } }));
      } catch (requestError) {
        if (active) {
          setError(requestError?.response?.data?.message || 'Không tải được thông báo. Vui lòng thử lại.');
        }
      } finally {
        if (active) setLoading(false);
      }
    }
    loadNotifications();
    return () => {
      active = false;
    };
  }, [user, location.pathname, page]);

  const markAsRead = async (notification) => {
    if (notification.isRead || busyId === notification.id) return;
    setBusyId(notification.id);
    try {
      await notificationApi.markAsRead(notification.id);
      setItems((current) => current.map((item) => item.id === notification.id ? { ...item, isRead: true } : item));
      const nextUnreadCount = Math.max(0, unreadCount - 1);
      setUnreadCount(nextUnreadCount);
      window.dispatchEvent(new CustomEvent('otsp:notification-count', { detail: { unreadCount: nextUnreadCount } }));
    } catch (requestError) {
      setError(requestError?.response?.data?.message || 'Không thể cập nhật trạng thái đã đọc.');
    } finally {
      setBusyId(null);
    }
  };

  const markAllAsRead = async () => {
    if (unreadCount === 0) return;
    setError('');
    try {
      await notificationApi.markAllAsRead();
      setItems((current) => current.map((item) => ({ ...item, isRead: true })));
      setUnreadCount(0);
      window.dispatchEvent(new CustomEvent('otsp:notification-count', { detail: { unreadCount: 0 } }));
    } catch (requestError) {
      setError(requestError?.response?.data?.message || 'Không thể đánh dấu đã đọc tất cả.');
    }
  };

  return (
    <div className="notifications-page-shell">
      <header className="notifications-header-box mb-4">
        <div className="d-flex flex-wrap align-items-center justify-content-between gap-3">
          <div>
            <span className="badge badge-soft-brand mb-2 px-2 py-1">OTSP · Tài khoản của bạn</span>
            <h1 className="h3 fw-bold mb-1">Thông báo</h1>
            <p className="text-secondary small mb-0">Cập nhật thật về lớp học, kết nối và tin nhắn của bạn.</p>
          </div>
          <div className="d-flex align-items-center gap-3">
            <span className="small text-secondary"><strong className="text-body">{unreadCount}</strong> chưa đọc</span>
            {unreadCount > 0 && (
              <button type="button" className="btn btn-outline-danger btn-sm d-flex align-items-center gap-2 px-3 py-2 rounded-3" onClick={markAllAsRead}>
                <i className="bi bi-check2-all" aria-hidden="true" />Đánh dấu tất cả đã đọc
              </button>
            )}
          </div>
        </div>
      </header>

      {error && <div className="alert alert-danger" role="alert">{error}</div>}
      {loading ? (
        <div className="notifications-state" role="status"><span className="spinner-border spinner-border-sm me-2" />Đang tải thông báo…</div>
      ) : items.length === 0 ? (
        <div className="notifications-state"><i className="bi bi-bell-slash fs-3 d-block mb-2" />Chưa có thông báo nào.</div>
      ) : (
        <>
          <div className="notifications-list-container d-flex flex-column gap-2">
            {items.map((notification) => {
              const isMessage = /tin nhắn/i.test(notification.title || '') || /\/chat/i.test(notification.targetUrl || '');
              const target = getNotificationTarget(notification, roleBase);
              return (
                <Link
                  key={notification.id}
                  to={target}
                  className={`notification-card-link ${notification.isRead ? 'is-read' : 'is-unread'} ${isMessage ? 'is-message-card' : ''}`}
                  onClick={() => markAsRead(notification)}
                >
                  <span className={`notification-card-icon-wrap ${notification.isRead ? 'icon-muted' : 'icon-brand'}`}>
                    <i className={`bi ${isMessage ? 'bi-chat-dots' : 'bi-bell'}${notification.isRead ? '' : '-fill'}`} aria-hidden="true" />
                  </span>
                  <span className="notification-card-body flex-grow-1 min-w-0 text-start">
                    <span className="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-1">
                      <strong className="text-body">{notification.title}</strong>
                      <span className="d-flex align-items-center gap-2">
                        {!notification.isRead && <span className="badge rounded-pill bg-danger-subtle text-danger">Mới</span>}
                        <time className="text-secondary small">{new Date(notification.createdAt).toLocaleString('vi-VN')}</time>
                      </span>
                    </span>
                    <span className="notification-card-text">{getContent(notification)}</span>
                    {isMessage && <span className="d-block mt-2"><span className="badge bg-danger-subtle text-danger border border-danger-subtle rounded-pill"><i className="bi bi-chat-text me-1" aria-hidden="true" />Mở cuộc trò chuyện</span></span>}
                  </span>
                  <span className="notification-arrow text-secondary"><i className="bi bi-chevron-right" aria-hidden="true" /></span>
                  {busyId === notification.id && <span className="visually-hidden">Đang cập nhật đã đọc</span>}
                </Link>
              );
            })}
          </div>
          <div className="notifications-pagination d-flex flex-wrap justify-content-between align-items-center gap-2 mt-3">
            <span className="small text-secondary">Hiển thị {items.length} / {totalCount} thông báo</span>
            {totalPages > 1 && <nav className="d-flex align-items-center gap-2" aria-label="Phân trang thông báo">
              <button type="button" className="btn btn-outline-secondary btn-sm" disabled={page <= 1 || loading} onClick={() => setPage((current) => Math.max(1, current - 1))}>Trước</button>
              <span className="small text-secondary">Trang {page} / {totalPages}</span>
              <button type="button" className="btn btn-outline-secondary btn-sm" disabled={page >= totalPages || loading} onClick={() => setPage((current) => Math.min(totalPages, current + 1))}>Tiếp</button>
            </nav>}
          </div>
        </>
      )}

      <style>{`
        .notifications-page-shell { width: 100%; max-width: none; margin: 0.5rem 0 2.5rem; padding: 0; }
        .notifications-header-box { padding: 1.25rem 1.4rem; border: 1px solid var(--bs-border-color, #eaedf1); border-radius: 14px; background: var(--bs-card-bg, #fff); }
        .badge-soft-brand { border-radius: 6px; background: rgba(219,48,58,.1); color: #B7212B; font-size: .75rem; font-weight: 700; }
        .notification-card-link { display: flex; width: 100%; align-items: center; gap: 1rem; padding: 1.1rem 1.25rem; border: 1px solid var(--bs-border-color, #eaedf1); border-radius: 12px; background: var(--bs-card-bg, #fff); color: inherit; text-decoration: none; transition: .18s ease; }
        .notification-card-link:hover { transform: translateY(-1px); border-color: #DB303A; box-shadow: 0 5px 14px rgba(0,0,0,.06); }
        .notification-card-link.is-unread { border-left: 4px solid #DB303A; background: #fffafa; }
        .notification-card-icon-wrap { display: grid; width: 42px; height: 42px; flex: 0 0 42px; place-items: center; border-radius: 50%; font-size: 1.1rem; }
        .icon-brand { background: #ffeaec; color: #B7212B; }
        .icon-muted { background: var(--bs-tertiary-bg); color: var(--bs-secondary-color); }
        .notification-card-text { display: block; color: var(--bs-secondary-color); font-size: .9rem; }
        .notification-arrow { margin-left: .5rem; }
        .notifications-state { padding: 2.5rem 1rem; text-align: center; color: var(--bs-secondary-color); background: var(--bs-card-bg, #fff); border: 1px solid var(--bs-border-color, #eaedf1); border-radius: 12px; }
        @media (max-width: 575.98px) { .notification-card-link { align-items: flex-start; gap: .7rem; padding: .9rem; } .notification-card-icon-wrap { width: 36px; height: 36px; flex-basis: 36px; } .notification-arrow { display: none; } }
      `}</style>
    </div>
  );
}
