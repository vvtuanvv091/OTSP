import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import authApi from '../../api/authApi';

export default function ForgotPasswordPage() {
  const [searchParams] = useSearchParams();
  const emailParam = searchParams.get('email') || '';
  const tokenParam = searchParams.get('token') || '';

  const [email, setEmail] = useState(emailParam);
  const [token, setToken] = useState(tokenParam);
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [step, setStep] = useState(tokenParam ? 2 : 1); // 1: yêu cầu mã xác thực, 2: đặt lại mật khẩu mới
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(
    tokenParam ? 'Đã nhận mã xác thực từ email. Vui lòng nhập mật khẩu mới của bạn.' : ''
  );
  const [showPassword, setShowPassword] = useState(false);
  const redirectTimerRef = useRef(null);

  const navigate = useNavigate();

  useEffect(() => {
    return () => {
      if (redirectTimerRef.current) clearTimeout(redirectTimerRef.current);
    };
  }, []);

  const handleRequestToken = async (e) => {
    e.preventDefault();
    setError('');
    setSuccess('');
    setLoading(true);

    try {
      const res = await authApi.forgotPassword({ email });
      setSuccess('Hệ thống đã gửi liên kết và mã xác thực đến email của bạn. Vui lòng kiểm tra hộp thư đến (hoặc thư rác spam) và nhập mã bên dưới để hoàn tất đổi mật khẩu.');
      if (res.data?.token) {
        setToken(res.data.token);
      }
      setStep(2);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể tạo mã đặt lại mật khẩu. Vui lòng kiểm tra lại email.');
    } finally {
      setLoading(false);
    }
  };

  const handleResetPassword = async (e) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    if (newPassword !== confirmPassword) {
      setError('Mật khẩu mới và xác nhận mật khẩu không khớp.');
      return;
    }

    setLoading(true);

    try {
      const res = await authApi.resetPassword({
        email,
        token,
        newPassword,
        confirmPassword
      });
      setSuccess(res.data?.message || 'Đặt lại mật khẩu thành công!');
      if (redirectTimerRef.current) clearTimeout(redirectTimerRef.current);
      redirectTimerRef.current = setTimeout(() => {
        navigate('/login');
      }, 2000);
    } catch (err) {
      setError(err.response?.data?.message || 'Đặt lại mật khẩu không thành công. Vui lòng kiểm tra lại mã xác thực.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-page py-5" style={{ minHeight: '75vh', display: 'flex', alignItems: 'center' }}>
      <div className="container">
        <div
          className="auth-card shadow-sm border p-4 p-md-5 mx-auto rounded-4"
          style={{ maxWidth: 480, backgroundColor: 'var(--otsp-card-bg, #fff)' }}
        >
          <div className="text-center mb-4">
            <span
              className="d-inline-flex align-items-center justify-content-center rounded-4 mb-3"
              style={{ width: '3.5rem', height: '3.5rem', backgroundColor: '#e8f5e9', color: '#198754', fontSize: '1.75rem' }}
            >
              <i className="bi bi-shield-lock" aria-hidden="true"></i>
            </span>
            <h1 className="h3 mb-2 fw-bold">Quên mật khẩu</h1>
            <p className="text-body-secondary mb-0">
              {step === 1
                ? 'Nhập email liên kết với tài khoản để lấy lại mật khẩu.'
                : 'Nhập mã xác thực và mật khẩu mới của bạn.'}
            </p>
          </div>

          {error && (
            <div className="alert alert-danger alert-dismissible fade show" role="alert">
              <i className="bi bi-exclamation-triangle-fill me-2"></i>
              {error}
              <button type="button" className="btn-close" onClick={() => setError('')} aria-label="Đóng"></button>
            </div>
          )}

          {success && (
            <div className="alert alert-success alert-dismissible fade show" role="alert">
              <i className="bi bi-check-circle-fill me-2"></i>
              {success}
              <button type="button" className="btn-close" onClick={() => setSuccess('')} aria-label="Đóng"></button>
            </div>
          )}

          {step === 1 ? (
            <form onSubmit={handleRequestToken}>
              <div className="mb-4">
                <label className="form-label small fw-semibold" htmlFor="forgotEmail">Email tài khoản</label>
                <div className="input-group">
                  <span className="input-group-text bg-white border-end-0">
                    <i className="bi bi-envelope text-muted"></i>
                  </span>
                  <input
                    id="forgotEmail"
                    type="email"
                    className="form-control border-start-0"
                    placeholder="name@example.com"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    required
                  />
                </div>
              </div>

              <button
                type="submit"
                className="btn btn-success w-100 py-2.5 fw-semibold mb-3 rounded-pill shadow-sm"
                style={{ backgroundColor: '#198754', borderColor: '#198754' }}
                disabled={loading}
              >
                {loading ? (
                  <>
                    <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                    Đang gửi yêu cầu...
                  </>
                ) : (
                  'Tiếp tục'
                )}
              </button>
            </form>
          ) : (
            <form onSubmit={handleResetPassword}>
              <div className="mb-3">
                <label className="form-label small fw-semibold" htmlFor="resetToken">Mã xác thực</label>
                <input
                  id="resetToken"
                  type="text"
                  className="form-control"
                  placeholder="Dán mã xác thực (Reset Token)"
                  value={token}
                  onChange={(e) => setToken(e.target.value)}
                  required
                />
              </div>

              <div className="mb-3">
                <label className="form-label small fw-semibold" htmlFor="newPassword">Mật khẩu mới</label>
                <div className="input-group">
                  <input
                    id="newPassword"
                    type={showPassword ? 'text' : 'password'}
                    className="form-control border-end-0"
                    placeholder="Tối thiểu 6 ký tự"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    required
                  />
                  <button
                    type="button"
                    className="input-group-text bg-white border-start-0 text-muted"
                    onClick={() => setShowPassword(!showPassword)}
                  >
                    <i className={`bi ${showPassword ? 'bi-eye-slash' : 'bi-eye'}`}></i>
                  </button>
                </div>
              </div>

              <div className="mb-4">
                <label className="form-label small fw-semibold" htmlFor="confirmNewPassword">Xác nhận mật khẩu mới</label>
                <input
                  id="confirmNewPassword"
                  type={showPassword ? 'text' : 'password'}
                  className="form-control"
                  placeholder="Nhập lại mật khẩu mới"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  required
                />
              </div>

              <button
                type="submit"
                className="btn btn-success w-100 py-2.5 fw-semibold mb-3 rounded-pill shadow-sm"
                style={{ backgroundColor: '#198754', borderColor: '#198754' }}
                disabled={loading}
              >
                {loading ? (
                  <>
                    <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                    Đang đặt lại mật khẩu...
                  </>
                ) : (
                  'Xác nhận đặt lại mật khẩu'
                )}
              </button>

              <div className="text-center mb-2">
                <button
                  type="button"
                  className="btn btn-link btn-sm text-decoration-none text-muted"
                  onClick={() => setStep(1)}
                >
                  <i className="bi bi-arrow-left me-1"></i>Nhập lại email khác
                </button>
              </div>
            </form>
          )}

          <div className="text-center pt-3 border-top mt-3">
            <Link to="/login" className="small text-secondary fw-semibold text-decoration-none">
              <i className="bi bi-box-arrow-in-left me-1"></i>Quay lại Đăng nhập
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}

