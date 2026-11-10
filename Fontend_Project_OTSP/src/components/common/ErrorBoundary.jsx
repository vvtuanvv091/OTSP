import React from 'react';

/**
 * ErrorBoundary toàn cục cho OTSP
 * Bắt tất cả lỗi render của React, lỗi nạp chunk động (Vite dynamic import) sau khi deploy,
 * và hiển thị giao diện thông báo lỗi trang nhã kèm nút Tải lại trang thay vì màn hình trắng xóa.
 */
export default class ErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
      errorInfo: null,
      isChunkError: false,
    };
  }

  static getDerivedStateFromError(error) {
    const message = error?.message || '';
    const isChunkError =
      message.includes('Failed to fetch dynamically imported module') ||
      message.includes('Loading chunk') ||
      message.includes('error loading dynamically imported module') ||
      message.includes('Importing a module script failed');

    return {
      hasError: true,
      error,
      isChunkError,
    };
  }

  componentDidCatch(error, errorInfo) {
    console.error('[OTSP ErrorBoundary] Phát hiện sự cố giao diện:', error, errorInfo);
    this.setState({ errorInfo });

    // Nếu là lỗi không tải được module JS (do Vercel vừa deploy phiên bản mới làm đổi mã hash)
    // Tự động tải lại trang sạch sẽ sau 500ms (chỉ reload 1 lần để tránh lặp vô tận)
    const message = error?.message || '';
    if (
      message.includes('Failed to fetch dynamically imported module') ||
      message.includes('Loading chunk') ||
      message.includes('error loading dynamically imported module')
    ) {
      const lastReload = sessionStorage.getItem('otsp_chunk_reload_time');
      const now = Date.now();
      if (!lastReload || now - parseInt(lastReload, 10) > 10000) {
        sessionStorage.setItem('otsp_chunk_reload_time', String(now));
        window.location.reload();
      }
    }
  }

  handleReload = () => {
    sessionStorage.removeItem('otsp_chunk_reload_time');
    window.location.reload();
  };

  handleGoHome = () => {
    window.location.href = '/';
  };

  render() {
    if (this.state.hasError) {
      return (
        <div
          className="d-flex align-items-center justify-content-center min-vh-100 p-4"
          style={{ backgroundColor: 'var(--bs-body-bg, #f8fafc)' }}
        >
          <div
            className="card border-0 shadow-lg p-4 p-md-5 text-center"
            style={{ maxWidth: 540, borderRadius: 20, backgroundColor: '#ffffff' }}
          >
            <div
              className="mx-auto mb-3 d-flex align-items-center justify-content-center"
              style={{
                width: 72,
                height: 72,
                borderRadius: '50%',
                backgroundColor: 'rgba(219, 48, 58, 0.1)',
                color: '#DB303A',
                fontSize: 32,
              }}
            >
              <i className="bi bi-exclamation-triangle-fill" aria-hidden="true" />
            </div>

            <h3 className="fw-bold mb-2 text-dark">
              {this.state.isChunkError ? 'Phiên bản mới đã được cập nhật' : 'Đã xảy ra sự cố hiển thị'}
            </h3>

            <p className="text-secondary mb-4 small">
              {this.state.isChunkError
                ? 'Hệ thống vừa hoàn tất bản cập nhật mới nhất. Vui lòng nhấn "Tải lại trang" để đồng bộ dữ liệu giao diện.'
                : 'Trang tạm thời không thể hiển thị do xung đột dữ liệu khởi tạo. Vui lòng tải lại trang để khắc phục ngay.'}
            </p>

            <div className="d-flex flex-wrap justify-content-center gap-3">
              <button
                type="button"
                className="btn btn-danger px-4 py-2 rounded-pill fw-semibold shadow-sm"
                style={{ backgroundColor: '#DB303A', borderColor: '#DB303A' }}
                onClick={this.handleReload}
              >
                <i className="bi bi-arrow-clockwise me-2" aria-hidden="true" />
                Tải lại trang ngay
              </button>
              <button
                type="button"
                className="btn btn-outline-secondary px-4 py-2 rounded-pill fw-semibold"
                onClick={this.handleGoHome}
              >
                <i className="bi bi-house-door me-2" aria-hidden="true" />
                Về trang chủ
              </button>
            </div>

            {process.env.NODE_ENV !== 'production' && this.state.error && (
              <details className="mt-4 text-start p-3 bg-light rounded-3 border small text-danger overflow-auto" style={{ maxHeight: 150 }}>
                <summary className="fw-bold text-secondary cursor-pointer">Chi tiết lỗi kỹ thuật</summary>
                <pre className="mt-2 mb-0" style={{ fontSize: 11 }}>{this.state.error?.toString()}</pre>
              </details>
            )}
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
