import React, { createContext, useContext, useState, useRef, useCallback } from 'react';

const ConfirmDialogContext = createContext(null);

export function ConfirmDialogProvider({ children }) {
  const [dialogState, setDialogState] = useState({
    isOpen: false,
    title: 'Xác nhận hành động',
    message: '',
    confirmText: 'Xác nhận',
    cancelText: 'Hủy',
    variant: 'danger', // 'danger' | 'primary' | 'warning'
  });

  const resolverRef = useRef(null);

  const confirm = useCallback(({
    title = 'Xác nhận thao tác',
    message = 'Bạn có chắc chắn muốn thực hiện hành động này?',
    confirmText = 'Đồng ý',
    cancelText = 'Hủy bỏ',
    variant = 'danger'
  } = {}) => {
    return new Promise((resolve) => {
      resolverRef.current = resolve;
      setDialogState({
        isOpen: true,
        title,
        message,
        confirmText,
        cancelText,
        variant
      });
    });
  }, []);

  const handleConfirm = () => {
    setDialogState((prev) => ({ ...prev, isOpen: false }));
    if (resolverRef.current) {
      resolverRef.current(true);
      resolverRef.current = null;
    }
  };

  const handleCancel = () => {
    setDialogState((prev) => ({ ...prev, isOpen: false }));
    if (resolverRef.current) {
      resolverRef.current(false);
      resolverRef.current = null;
    }
  };

  const btnClass = {
    danger: 'btn-danger',
    primary: 'btn-primary',
    warning: 'btn-warning text-dark',
  }[dialogState.variant] || 'btn-danger';

  const iconClass = {
    danger: 'bi-exclamation-triangle-fill text-danger',
    primary: 'bi-info-circle-fill text-primary',
    warning: 'bi-exclamation-circle-fill text-warning',
  }[dialogState.variant] || 'bi-exclamation-triangle-fill text-danger';

  return (
    <ConfirmDialogContext.Provider value={confirm}>
      {children}

      {dialogState.isOpen && (
        <div
          className="modal fade show d-block"
          tabIndex="-1"
          role="dialog"
          style={{ backgroundColor: 'rgba(15, 23, 42, 0.55)', zIndex: 10050 }}
          aria-modal="true"
        >
          <div className="modal-dialog modal-dialog-centered" style={{ maxWidth: '440px' }}>
            <div className="modal-content border-0 shadow-lg rounded-4 overflow-hidden bg-body text-body">
              <div className="p-4">
                <div className="d-flex align-items-start mb-3">
                  <div
                    className="d-flex align-items-center justify-content-center rounded-circle p-2 me-3 flex-shrink-0"
                    style={{ backgroundColor: 'rgba(219, 48, 58, 0.1)' }}
                  >
                    <i className={`bi ${iconClass} fs-4`} aria-hidden="true" />
                  </div>
                  <div>
                    <h5 className="modal-title fw-bold fs-6 mb-1">{dialogState.title}</h5>
                    <p className="text-secondary small mb-0 lh-base">{dialogState.message}</p>
                  </div>
                </div>

                <div className="d-flex justify-content-end gap-2 pt-2">
                  <button
                    type="button"
                    className="btn btn-sm btn-outline-secondary px-3 py-1.5 rounded-3 fw-medium"
                    onClick={handleCancel}
                  >
                    {dialogState.cancelText}
                  </button>
                  <button
                    type="button"
                    className={`btn btn-sm ${btnClass} px-3 py-1.5 rounded-3 fw-medium`}
                    onClick={handleConfirm}
                    autoFocus
                  >
                    {dialogState.confirmText}
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </ConfirmDialogContext.Provider>
  );
}

export function useConfirm() {
  const context = useContext(ConfirmDialogContext);
  if (!context) {
    // Fallback nếu gọi ngoài ConfirmDialogProvider
    return async ({ message }) => window.confirm(message || 'Bạn có chắc chắn không?');
  }
  return context;
}

