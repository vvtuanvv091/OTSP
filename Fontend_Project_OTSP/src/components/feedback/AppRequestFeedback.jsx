import { useEffect, useRef, useState } from 'react';
import '../../styles/components/AppRequestFeedback.css';

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
    </>
  );
}
