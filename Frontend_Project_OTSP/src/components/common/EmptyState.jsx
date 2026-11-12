import { Link } from 'react-router-dom';

/**
 * EmptyState Component chuẩn hóa toàn hệ thống OTSP
 * Thay thế 85 biến thể empty state tự chế rải rác
 */
export default function EmptyState({
  icon = 'bi-inbox',
  title = 'Chưa có dữ liệu',
  description = 'Hiện tại chưa có mục nào trong danh sách này.',
  actionText,
  onAction,
  actionLink,
  extra,
  className = ''
}) {
  return (
    <div className={`text-center py-5 px-3 ${className}`}>
      <div
        className="d-inline-flex align-items-center justify-content-center rounded-circle mb-3"
        style={{
          width: '64px',
          height: '64px',
          backgroundColor: 'var(--otsp-empty-icon-bg, rgba(219, 48, 58, 0.08))',
          color: 'var(--otsp-primary, #DB303A)',
          fontSize: '1.75rem'
        }}
      >
        <i className={`bi ${icon}`} aria-hidden="true" />
      </div>
      <h3 className="h6 fw-bold text-body mb-1">{title}</h3>
      {description && (
        <p className="text-secondary small mb-3 mx-auto" style={{ maxWidth: '420px', lineHeight: 1.5 }}>
          {description}
        </p>
      )}

      {actionLink && actionText && (
        <Link to={actionLink} className="btn btn-sm btn-danger px-3 py-1.5 rounded-3 fw-medium">
          {actionText}
        </Link>
      )}

      {!actionLink && onAction && actionText && (
        <button
          type="button"
          onClick={onAction}
          className="btn btn-sm btn-danger px-3 py-1.5 rounded-3 fw-medium"
        >
          {actionText}
        </button>
      )}

      {extra && <div className="mt-3">{extra}</div>}
    </div>
  );
}

