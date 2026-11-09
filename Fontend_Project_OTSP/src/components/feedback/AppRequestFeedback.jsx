import { useEffect, useRef, useState } from 'react';

export default function AppRequestFeedback() {
  const [pendingRequests, setPendingRequests] = useState(0);
  const [visible, setVisible] = useState(false);
  const [slow, setSlow] = useState(false);
  const [forbiddenMessage, setForbiddenMessage] = useState('');
  const pendingRef = useRef(0);
  const visibleTimer = useRef(null);
  const slowTimer = useRef(null);
  const forbiddenTimer = useRef(null);

  useEffect(() => {
    const start = () => {
      if (pendingRef.current === 0) {
        clearTimeout(visibleTimer.current);
        clearTimeout(slowTimer.current);
        visibleTimer.current = window.setTimeout(() => setVisible(true), 350);
        slowTimer.current = window.setTimeout(() => setSlow(true), 7000);
        setSlow(false);
      }
      pendingRef.current += 1;
      setPendingRequests(pendingRef.current);
    };
    const end = () => {
      pendingRef.current = Math.max(0, pendingRef.current - 1);
      setPendingRequests(pendingRef.current);
      if (pendingRef.current === 0) {
        clearTimeout(visibleTimer.current);
        clearTimeout(slowTimer.current);
        setVisible(false);
        setSlow(false);
      }
    };
    const forbidden = (event) => {
      setForbiddenMessage(event.detail?.message || 'Bạn không có quyền thực hiện thao tác này.');
      clearTimeout(forbiddenTimer.current);
      forbiddenTimer.current = window.setTimeout(() => setForbiddenMessage(''), 9000);
    };

    window.addEventListener('otsp:request-start', start);
    window.addEventListener('otsp:request-end', end);
    window.addEventListener('otsp:forbidden', forbidden);
    return () => {
      window.removeEventListener('otsp:request-start', start);
      window.removeEventListener('otsp:request-end', end);
      window.removeEventListener('otsp:forbidden', forbidden);
      clearTimeout(visibleTimer.current);
      clearTimeout(slowTimer.current);
      clearTimeout(forbiddenTimer.current);
    };
  }, []);

  return (
    <>
      {visible && pendingRequests > 0 && (
        <div className="otsp-request-status" role="status" aria-live="polite">
          <span className="spinner-border spinner-border-sm" aria-hidden="true" />
          <span>{slow ? 'Máy chủ đang phản hồi chậm, vui lòng chờ…' : 'Đang tải dữ liệu…'}</span>
        </div>
      )}
      {forbiddenMessage && (
        <div className="otsp-permission-notice" role="alert">
          <i className="bi bi-shield-lock-fill" aria-hidden="true" />
          <span>{forbiddenMessage}</span>
          <button type="button" aria-label="Đóng thông báo" onClick={() => setForbiddenMessage('')}>×</button>
        </div>
      )}
      <style>{`
        .otsp-request-status { position: fixed; right: 18px; bottom: 18px; z-index: 2000; display: flex; align-items: center; gap: 10px; max-width: min(460px, calc(100vw - 28px)); padding: 12px 16px; color: #263448; background: #fff; border: 1px solid #cbd5e1; border-radius: 10px; box-shadow: 0 8px 28px rgba(15,23,42,.18); font-size: 14px; }
        .otsp-request-status .spinner-border { color: #dc303a; }
        .otsp-permission-notice { position: fixed; top: 18px; right: 18px; z-index: 2010; display: flex; align-items: center; gap: 10px; width: min(460px, calc(100vw - 28px)); padding: 13px 15px; color: #7f1d1d; background: #fff1f2; border: 1px solid #fda4af; border-radius: 9px; box-shadow: 0 8px 28px rgba(15,23,42,.16); font-size: 14px; }
        .otsp-permission-notice > i { color: #be123c; }
        .otsp-permission-notice button { margin-left: auto; border: 0; color: inherit; background: transparent; font-size: 22px; line-height: 1; }
        [data-bs-theme="dark"] .otsp-request-status { color: #eef2f7; background: #1f2937; border-color: #536176; }
        [data-bs-theme="dark"] .otsp-permission-notice { color: #ffe4e6; background: #3f2028; border-color: #a94051; }
        @media (max-width: 575.98px) { .otsp-request-status { right: 10px; bottom: 10px; left: 10px; max-width: none; } .otsp-permission-notice { top: 10px; right: 10px; } }
      `}</style>
    </>
  );
}
