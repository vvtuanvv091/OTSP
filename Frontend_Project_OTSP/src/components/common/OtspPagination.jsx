import '../../styles/admin-pages.css';

/**
 * Thanh phân trang chuẩn OTSP dùng chung cho toàn bộ website (Admin, Gia sư, Học viên)
 * Thiết kế viên thuốc bo tròn, trang active hình tròn đỏ #DB303A thương hiệu, hỗ trợ dấu ...
 */
export default function OtspPagination({
  page = 1,
  totalPages = 1,
  total = 0,
  pageSize = 10,
  onChange = () => {},
  showInfo = true,
  className = '',
}) {
  const pages = Math.max(1, totalPages || 1);
  const firstItem = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const lastItem = total === 0 ? 0 : Math.min(page * pageSize, total);

  // Thuật toán hiển thị danh sách trang thông minh: tự động co gọn với dấu '...' khi có nhiều trang
  const getVisiblePages = () => {
    if (pages <= 5) {
      return Array.from({ length: pages }, (_, i) => i + 1);
    }
    if (page <= 3) {
      return [1, 2, 3, 4, '...', pages];
    }
    if (page >= pages - 2) {
      return [1, '...', pages - 3, pages - 2, pages - 1, pages];
    }
    return [1, '...', page - 1, page, page + 1, '...', pages];
  };

  const visiblePages = getVisiblePages();

  return (
    <div className={`admin-pagination ${className}`}>
      {showInfo && (
        <div className="admin-pagination-info">
          Hiển thị <strong>{firstItem}</strong>–<strong>{lastItem}</strong> trong tổng số <strong>{total}</strong> mục
        </div>
      )}
      <div className={`admin-pagination-controls ${!showInfo ? 'mx-auto' : ''}`}>
        <button
          type="button"
          className="admin-page-nav-btn admin-page-prev"
          disabled={page <= 1}
          onClick={() => onChange(page - 1)}
          aria-label="Trang trước"
        >
          <i className="bi bi-chevron-left me-1" aria-hidden="true" />Trước
        </button>

        <div className="admin-pagination-pages">
          {visiblePages.map((p, idx) => {
            if (p === '...') {
              return (
                <span key={`ellipsis-${idx}`} className="admin-page-ellipsis">
                  …
                </span>
              );
            }
            const isActive = p === page;
            return (
              <button
                key={p}
                type="button"
                className={`admin-page-num ${isActive ? 'is-active' : ''}`}
                onClick={() => onChange(p)}
                aria-label={`Trang ${p}`}
                aria-current={isActive ? 'page' : undefined}
              >
                {p}
              </button>
            );
          })}
        </div>

        <button
          type="button"
          className="admin-page-nav-btn admin-page-next"
          disabled={page >= pages}
          onClick={() => onChange(page + 1)}
          aria-label="Trang sau"
        >
          Sau<i className="bi bi-chevron-right ms-1" aria-hidden="true" />
        </button>
      </div>
    </div>
  );
}

