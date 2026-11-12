import { useState, useRef, useCallback, useEffect } from 'react';
import { ConfirmDialogContext } from './confirmHooks';

const DEFAULT_DIALOG = {
  isOpen: false,
  title: 'Xác nhận hành động',
  message: '',
  confirmText: 'Xác nhận',
  cancelText: 'Hủy',
  variant: 'danger', // 'danger' | 'primary' | 'warning'
  input: null,
};

export function ConfirmDialogProvider({ children }) {
  const [dialogState, setDialogState] = useState(DEFAULT_DIALOG);
  const [inputValue, setInputValue] = useState('');
  const [inputError, setInputError] = useState('');

  const resolverRef = useRef(null);

  const confirm = useCallback(({
    title = 'Xác nhận thao tác',
    message = 'Bạn có chắc chắn muốn thực hiện hành động này?',
    confirmText = 'Đồng ý',
    cancelText = 'Hủy bỏ',
    variant = 'danger',
    input = null,
  } = {}) => {
    return new Promise((resolve) => {
      resolverRef.current = resolve;
      setInputValue(input?.defaultValue ?? '');
      setInputError('');
      setDialogState({
        isOpen: true,
        title,
        message,
        confirmText,
        cancelText,
        variant,
        input,
      });
    });
  }, []);

  const handleCancel = useCallback(() => {
    const isInputMode = Boolean(dialogState.input);
    setDialogState((prev) => ({ ...prev, isOpen: false }));
    setInputError('');
    if (resolverRef.current) {
      resolverRef.current(isInputMode ? null : false);
      resolverRef.current = null;
    }
  }, [dialogState.input]);

  const handleConfirm = useCallback(() => {
    const inputCfg = dialogState.input;
    if (inputCfg) {
      const trimmed = inputValue.trim();
      const minLen = Number(inputCfg.minLength || 0);
      if (inputCfg.required && !trimmed) {
        setInputError(inputCfg.requiredMessage || 'Vui lòng nhập nội dung.');
        return;
      }
      if (minLen > 0 && trimmed.length < minLen) {
        setInputError(inputCfg.minLengthMessage || `Vui lòng nhập tối thiểu ${minLen} ký tự.`);
        return;
      }
    }

    setDialogState((prev) => ({ ...prev, isOpen: false }));
    setInputError('');
    if (resolverRef.current) {
      resolverRef.current(inputCfg ? inputValue : true);
      resolverRef.current = null;
    }
  }, [dialogState.input, inputValue]);

  useEffect(() => {
    if (!dialogState.isOpen) return undefined;
    const onKeyDown = (event) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        handleCancel();
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [dialogState.isOpen, handleCancel]);

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
          aria-labelledby="otsp-confirm-dialog-title"
          onClick={(event) => {
            if (event.target === event.currentTarget) handleCancel();
          }}
        >
          <div className="modal-dialog modal-dialog-centered" style={{ maxWidth: '460px' }}>
            <div className="modal-content border-0 shadow-lg rounded-4 overflow-hidden bg-body text-body">
              <form
                onSubmit={(event) => {
                  event.preventDefault();
                  handleConfirm();
                }}
              >
                <div className="p-4">
                  <div className="d-flex align-items-start mb-3">
                    <div
                      className="d-flex align-items-center justify-content-center rounded-circle p-2 me-3 flex-shrink-0"
                      style={{ backgroundColor: 'rgba(219, 48, 58, 0.1)' }}
                    >
                      <i className={`bi ${iconClass} fs-4`} aria-hidden="true" />
                    </div>
                    <div className="flex-grow-1">
                      <h5 id="otsp-confirm-dialog-title" className="modal-title fw-bold fs-6 mb-1">
                        {dialogState.title}
                      </h5>
                      {dialogState.message && (
                        <p className="text-secondary small mb-0 lh-base">{dialogState.message}</p>
                      )}
                    </div>
                  </div>

                  {dialogState.input && (
                    <div className="mb-3">
                      {dialogState.input.label && (
                        <label htmlFor="otsp-confirm-dialog-input" className="form-label small fw-semibold mb-1">
                          {dialogState.input.label}
                        </label>
                      )}
                      <textarea
                        id="otsp-confirm-dialog-input"
                        rows={3}
                        className={`form-control form-control-sm rounded-3 ${inputError ? 'is-invalid' : ''}`}
                        placeholder={dialogState.input.placeholder || 'Nhập lý do hoặc ghi chú...'}
                        value={inputValue}
                        onChange={(event) => {
                          setInputValue(event.target.value);
                          if (inputError) setInputError('');
                        }}
                        autoFocus
                      />
                      {inputError && (
                        <div className="invalid-feedback d-block small" role="alert">
                          {inputError}
                        </div>
                      )}
                    </div>
                  )}

                  <div className="d-flex justify-content-end gap-2 pt-2">
                    <button
                      type="button"
                      className="btn btn-sm btn-outline-secondary px-3 py-1.5 rounded-3 fw-medium"
                      onClick={handleCancel}
                    >
                      {dialogState.cancelText}
                    </button>
                    <button
                      type="submit"
                      className={`btn btn-sm ${btnClass} px-3 py-1.5 rounded-3 fw-medium`}
                      autoFocus={!dialogState.input}
                    >
                      {dialogState.confirmText}
                    </button>
                  </div>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </ConfirmDialogContext.Provider>
  );
}
