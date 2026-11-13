import { useState, useEffect, useRef } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import studentApi from '../../api/studentApi';
import { useSignalR, useSignalREvent } from '../../context/signalRHooks';

const formatMoney = (val) =>
  new Intl.NumberFormat('vi-VN').format(val || 0) + ' VNĐ';

export default function PaymentDetailPage() {
  const { paymentId } = useParams();
  const navigate = useNavigate();

  const [loading, setLoading] = useState(true);
  const [payment, setPayment] = useState(null);
  const [errorMsg, setErrorMsg] = useState('');
  const [copiedField, setCopiedField] = useState('');
  const [isPaid, setIsPaid] = useState(false);
  const redirectTimerRef = useRef(null);
  const copyTimerRef = useRef(null);

  useEffect(() => {
    return () => {
      if (redirectTimerRef.current) clearTimeout(redirectTimerRef.current);
      if (copyTimerRef.current) clearTimeout(copyTimerRef.current);
    };
  }, []);

  useEffect(() => {
    let isMounted = true;
    async function loadPayment() {
      try {
        setLoading(true);
        setErrorMsg('');
        setIsPaid(false);
        const res = await studentApi.getPaymentCheckout(paymentId);
        if (isMounted && res.data) {
          setPayment(res.data);
          setIsPaid(Boolean(res.data.isPaid));
        }
      } catch (err) {
        console.warn('API getPaymentCheckout error:', err);
        if (isMounted) {
          setErrorMsg(err?.response?.data?.message || 'Không tìm thấy thông tin khoản thanh toán.');
        }
      } finally {
        if (isMounted) setLoading(false);
      }
    }
    loadPayment();
    return () => {
      isMounted = false;
    };
  }, [paymentId]);

  const { joinPayment, leavePayment } = useSignalR();

  // Gia nhập phòng thanh toán SignalR thời gian thực
  useEffect(() => {
    if (payment?.paymentId) {
      joinPayment(payment.paymentId);
      return () => leavePayment(payment.paymentId);
    }
    return undefined;
  }, [payment?.paymentId, joinPayment, leavePayment]);

  // Nhận tín hiệu thanh toán thành công tức thì từ SignalR (0ms delay)
  useSignalREvent('PaymentStatusChanged', (data) => {
    if (Number(data?.paymentId) === Number(payment?.paymentId) && data?.isPaid) {
      setIsPaid(true);
      if (redirectTimerRef.current) clearTimeout(redirectTimerRef.current);
      redirectTimerRef.current = setTimeout(() => {
        navigate('/student/payments?status=success');
      }, 2500);
    }
  });

  // Tự động kiểm tra trạng thái thanh toán dự phòng (polling mỗi 5 giây)
  useEffect(() => {
    if (!payment?.paymentId || isPaid) return undefined;

    const interval = setInterval(async () => {
      try {
        const res = await studentApi.checkPaymentStatus(payment.paymentId);
        if (res.data?.isPaid) {
          setIsPaid(true);
          clearInterval(interval);
          if (redirectTimerRef.current) clearTimeout(redirectTimerRef.current);
          redirectTimerRef.current = setTimeout(() => {
            navigate('/student/payments?status=success');
          }, 2500);
        }
      } catch {
        // Kiểm tra trạng thái định kỳ ngầm
      }
    }, 5000);

    return () => clearInterval(interval);
  }, [payment?.paymentId, isPaid, navigate]);

  const copyText = (text, fieldName) => {
    navigator.clipboard?.writeText(text || '')
      .then(() => {
        setCopiedField(fieldName);
        if (copyTimerRef.current) clearTimeout(copyTimerRef.current);
        copyTimerRef.current = setTimeout(() => setCopiedField(''), 2000);
      })
      .catch(() => {});
  };

  if (loading) {
    return (
      <div className="container py-5 text-center">
        <div className="spinner-border text-danger" role="status"></div>
        <div className="mt-2 text-secondary small">Đang tải hóa đơn thanh toán...</div>
      </div>
    );
  }

  if (errorMsg || !payment) {
    return (
      <div className="container py-5 text-center" style={{ maxWidth: 600 }}>
        <div className="alert alert-danger mb-3">
          <i className="bi bi-exclamation-triangle-fill me-2"></i>
          {errorMsg || 'Không tìm thấy hóa đơn cần thanh toán.'}
        </div>
        <Link to="/student/payments" className="btn btn-outline-secondary">
          <i className="bi bi-arrow-left me-1"></i> Quay lại danh sách học phí
        </Link>
      </div>
    );
  }

  return (
    <div className="container-fluid py-3 px-1 px-md-3" style={{ width: '100%' }}>
      {/* Tiêu đề trang thanh toán */}
      <div className="d-flex align-items-center justify-content-between mb-4 flex-wrap gap-2">
        <div>
          <Link to="/student/payments" className="btn btn-sm btn-outline-secondary mb-2">
            <i className="bi bi-arrow-left me-1"></i>Quay lại danh sách
          </Link>
          <h1 className="h4 fw-bold mb-1 text-body">Thanh toán học phí qua Ngân hàng</h1>
          <p className="text-secondary small mb-0">
            Quét mã QR VietQR / SePay bằng ứng dụng ngân hàng bất kỳ để tự động ghi nhận học phí
          </p>
        </div>
        <div>
          {isPaid ? (
            <span className="badge bg-success px-3 py-2 fs-6">
              <i className="bi bi-check-circle-fill me-1"></i>Thanh toán thành công!
            </span>
          ) : (
            <span className="badge bg-warning-subtle text-warning border border-warning-subtle px-3 py-2 fs-6">
              <i className="bi bi-clock-history me-1"></i>Đang chờ thanh toán
            </span>
          )}
        </div>
      </div>

      {isPaid && (
        <div className="alert alert-success d-flex align-items-center mb-4 shadow-sm" role="alert">
          <i className="bi bi-check-circle-fill me-2 fs-4"></i>
          <div>
            <strong>Giao dịch đã được xác nhận thành công!</strong> Đang chuyển hướng bạn về lịch sử học phí...
          </div>
        </div>
      )}

      <div className="row g-4">
        {/* Cột trái: Mã QR thanh toán tự động */}
        <div className="col-12 col-md-5">
          <div className="card border shadow-sm rounded-3 p-4 text-center h-100 bg-body">
            <h2 className="h5 fw-bold mb-3 text-danger">
              <i className="bi bi-qr-code me-1"></i> Quét mã để thanh toán
            </h2>

            <div className="qr-container mb-3 p-3 bg-light border border-2 border-dashed rounded-3 text-center">
              {payment.qrImageUrl ? (
                <img
                  src={payment.qrImageUrl}
                  alt="QR Thanh toán SePay"
                  className="img-fluid rounded shadow-sm mb-2"
                  style={{ maxWidth: 240 }}
                />
              ) : (
                <div className="py-4 text-secondary">
                  <i className="bi bi-qr-code fs-1 d-block mb-2"></i>
                  Chưa thể tạo mã QR
                </div>
              )}
              <div className="small text-muted d-flex align-items-center justify-content-center gap-1.5 mt-1">
                <span className="spinner-grow spinner-grow-sm text-danger" role="status"></span>
                Đang lắng nghe giao dịch tự động...
              </div>
            </div>

            <p className="small text-secondary mb-0" style={{ fontSize: 12 }}>
              Sử dụng App ngân hàng (MB, Vietcombank, Techcombank, MoMo...) quét mã QR trên để chuyển khoản tự động chính xác số tiền và nội dung.
            </p>
          </div>
        </div>

        {/* Cột phải: Thông tin chuyển khoản & chi tiết lớp học */}
        <div className="col-12 col-md-7">
          <div className="card border shadow-sm rounded-3 p-4 h-100 bg-body">
            <h3 className="h6 fw-bold mb-3 border-bottom pb-2 text-body">Chi tiết học phí</h3>

            <div className="d-flex justify-content-between py-2 border-bottom">
              <span className="text-secondary small">Lớp học:</span>
              <span className="fw-semibold text-body">{payment.classroomName}</span>
            </div>
            <div className="d-flex justify-content-between py-2 border-bottom">
              <span className="text-secondary small">Môn &amp; Cấp học:</span>
              <span className="fw-semibold text-body">{payment.subjectGrade}</span>
            </div>
            <div className="d-flex justify-content-between py-2 border-bottom">
              <span className="text-secondary small">Gia sư phụ trách:</span>
              <span className="fw-semibold text-body">{payment.tutorName}</span>
            </div>
            <div className="d-flex justify-content-between py-3 border-bottom bg-light px-3 rounded mt-2">
              <span className="fw-bold text-secondary">Số tiền cần đóng:</span>
              <span className="fw-bold text-danger fs-4">{formatMoney(payment.amount)}</span>
            </div>

            <h3 className="h6 fw-bold mt-4 mb-3 border-bottom pb-2 text-body">Thông tin tài khoản nhận</h3>
            <div className="d-flex justify-content-between py-2 border-bottom">
              <span className="text-secondary small">Ngân hàng:</span>
              <span className="fw-bold text-primary">{payment.bankName}</span>
            </div>
            <div className="d-flex justify-content-between py-2 border-bottom align-items-center">
              <span className="text-secondary small">Số tài khoản:</span>
              <span className="fw-semibold font-monospace text-body d-flex align-items-center gap-1">
                {payment.accountNo}
                <button
                  type="button"
                  className="btn btn-sm btn-link p-0 text-primary ms-1"
                  onClick={() => copyText(payment.accountNo, 'accountNo')}
                  title="Sao chép số tài khoản"
                >
                  <i className="bi bi-clipboard"></i>
                </button>
                {copiedField === 'accountNo' && (
                  <span className="badge bg-success" style={{ fontSize: 10 }}>Đã chép!</span>
                )}
              </span>
            </div>
            <div className="d-flex justify-content-between py-2 border-bottom">
              <span className="text-secondary small">Chủ tài khoản:</span>
              <span className="fw-semibold text-body">{payment.accountName}</span>
            </div>
            <div className="d-flex justify-content-between py-2.5 border-bottom bg-warning-subtle px-3 rounded mt-2 align-items-center">
              <span className="text-warning-emphasis fw-bold small">Nội dung chuyển khoản (bắt buộc):</span>
              <span className="font-monospace text-danger fw-bold fs-5 d-flex align-items-center gap-1">
                {payment.transferContent}
                <button
                  type="button"
                  className="btn btn-sm btn-link p-0 text-danger ms-1"
                  onClick={() => copyText(payment.transferContent, 'transferContent')}
                  title="Sao chép nội dung"
                >
                  <i className="bi bi-clipboard fs-6"></i>
                </button>
                {copiedField === 'transferContent' && (
                  <span className="badge bg-success" style={{ fontSize: 10 }}>Đã chép!</span>
                )}
              </span>
            </div>

            <div className="alert alert-info border-0 mt-3 small mb-0 rounded-3">
              <i className="bi bi-info-circle-fill me-1"></i>
              <strong>Lưu ý:</strong> Vui lòng giữ nguyên nội dung chuyển khoản{' '}
              <code>{payment.transferContent}</code> để hệ thống tự động kích hoạt trạng thái đã thanh toán ngay sau 3-5 giây.
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
