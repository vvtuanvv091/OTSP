import React, { createContext, useContext, useState, useCallback, useMemo } from 'react';

const ToastContext = createContext(null);

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);

  const removeToast = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const addToast = useCallback(({ message, type = 'info', title = '', duration = 4000 }) => {
    const id = Date.now() + Math.random().toString(36).substring(2, 9);
    const newToast = { id, message, type, title, duration };

    setToasts((prev) => [...prev, newToast]);

    if (duration > 0) {
      setTimeout(() => {
        removeToast(id);
      }, duration);
    }
    return id;
  }, [removeToast]);

  const toast = useMemo(() => ({
    show: (msg, type, title, duration) => addToast({ message: msg, type, title, duration }),
    success: (msg, title) => addToast({ message: msg, type: 'success', title: title || 'Thành công' }),
    error: (msg, title) => addToast({ message: msg, type: 'error', title: title || 'Có lỗi xảy ra' }),
    warning: (msg, title) => addToast({ message: msg, type: 'warning', title: title || 'Cảnh báo' }),
    info: (msg, title) => addToast({ message: msg, type: 'info', title: title || 'Thông báo' }),
    remove: removeToast
  }), [addToast, removeToast]);

  return (
    <ToastContext.Provider value={toast}>
      {children}
      {/* Toast Notification Container */}
      <div
        className="toast-container position-fixed top-0 end-0 p-3"
        style={{ zIndex: 9999, maxWidth: '420px', width: '100%', pointerEvents: 'none' }}
        aria-live="polite"
        aria-atomic="true"
      >
        {toasts.map((t) => {
          const config = {
            success: {
              icon: 'bi-check-circle-fill',
              borderClass: 'border-success',
              iconClass: 'text-success',
              badgeClass: 'bg-success text-white',
            },
            error: {
              icon: 'bi-exclamation-triangle-fill',
              borderClass: 'border-danger',
              iconClass: 'text-danger',
              badgeClass: 'bg-danger text-white',
            },
            warning: {
              icon: 'bi-exclamation-circle-fill',
              borderClass: 'border-warning',
              iconClass: 'text-warning',
              badgeClass: 'bg-warning text-dark',
            },
            info: {
              icon: 'bi-info-circle-fill',
              borderClass: 'border-primary',
              iconClass: 'text-primary',
              badgeClass: 'bg-primary text-white',
            },
          }[t.type] || {
            icon: 'bi-info-circle-fill',
            borderClass: 'border-primary',
            iconClass: 'text-primary',
            badgeClass: 'bg-primary text-white',
          };

          return (
            <div
              key={t.id}
              className={`toast show mb-2 border shadow-sm rounded-3 bg-body ${config.borderClass}`}
              style={{
                pointerEvents: 'auto',
                animation: 'otsp-slide-in-right 0.25s cubic-bezier(0.16, 1, 0.3, 1)',
                borderWidth: '1px',
              }}
              role="alert"
            >
              <div className="d-flex align-items-center p-3">
                <i className={`bi ${config.icon} fs-4 me-3 ${config.iconClass}`} aria-hidden="true" />
                <div className="flex-grow-1 me-2 text-body">
                  {t.title && <div className="fw-semibold small">{t.title}</div>}
                  <div className="small text-secondary">{t.message}</div>
                </div>
                <button
                  type="button"
                  className="btn-close ms-auto"
                  onClick={() => removeToast(t.id)}
                  aria-label="Đóng"
                />
              </div>
            </div>
          );
        })}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const context = useContext(ToastContext);
  if (!context) {
    // Trả về fallback an toàn nếu component được render ngoài ToastProvider
    return {
      show: (msg) => console.log('Toast:', msg),
      success: (msg) => console.log('Toast Success:', msg),
      error: (msg) => console.error('Toast Error:', msg),
      warning: (msg) => console.warn('Toast Warning:', msg),
      info: (msg) => console.log('Toast Info:', msg),
      remove: () => {},
    };
  }
  return context;
}

