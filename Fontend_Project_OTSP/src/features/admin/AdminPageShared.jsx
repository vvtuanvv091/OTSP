import '../../styles/admin-pages.css';

export function AdminPageHeader({ title, description }) {
  return (
    <header className="admin-page-header">
      <div className="admin-breadcrumb">Trang của tôi <span>›</span> Quản trị hệ thống</div>
      <h1>{title}</h1>
      {description && <p>{description}</p>}
    </header>
  );
}

export function AdminStats({ items, columns = 3 }) {
  return (
    <section className={`admin-stats ${columns === 4 ? 'has-four-columns' : ''}`} aria-label="Thống kê">
      {items.map((item) => (
        <article className="admin-stat-card" key={item.label}>
          <span>{item.label}</span>
          <strong>{item.value ?? 0}</strong>
          {item.note && <small>{item.note}</small>}
        </article>
      ))}
    </section>
  );
}

export function AdminFeedback({ error, message, onDismiss }) {
  if (!error && !message) return null;
  const isError = Boolean(error);
  return (
    <div className={`admin-feedback ${isError ? 'is-error' : 'is-success'}`} role="status">
      <span>{error || message}</span>
      {onDismiss && <button type="button" aria-label="Đóng thông báo" onClick={onDismiss}>×</button>}
    </div>
  );
}

export function AdminStatus({ value, isOnline }) {
  const locked = value === 'Locked';
  if (locked) {
    return (
      <span className="admin-status is-locked">
        Đã khóa
      </span>
    );
  }
  if (typeof isOnline === 'boolean') {
    return isOnline ? (
      <span className="admin-status is-active" title="Tài khoản kích hoạt · Đang trực tuyến trên hệ thống">
        ● Đang online
      </span>
    ) : (
      <span
        className="admin-status"
        style={{ background: '#f1f5f9', color: '#475569', border: '1px solid #cbd5e1' }}
        title="Tài khoản kích hoạt · Hiện không trực tuyến"
      >
        ○ Ngoại tuyến
      </span>
    );
  }
  return (
    <span className="admin-status is-active">
      Đang hoạt động
    </span>
  );
}

import OtspPagination from '../../components/common/OtspPagination';

export { OtspPagination as AdminPagination };

export function AdminLoadingRows({ columns, loading, hasItems, emptyText }) {
  if (loading) {
    return <tr><td colSpan={columns} className="admin-table-state">Đang tải dữ liệu…</td></tr>;
  }
  if (!hasItems) {
    return <tr><td colSpan={columns} className="admin-table-state">{emptyText}</td></tr>;
  }
  return null;
}
