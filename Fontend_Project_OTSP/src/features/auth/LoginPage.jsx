import { useState } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import GoogleSignInButton from '../../components/auth/GoogleSignInButton';

export default function LoginPage() {
  const [email, setEmail] = useState(() => localStorage.getItem('otsp_remember_email') || '');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(() => Boolean(localStorage.getItem('otsp_remember_email')));
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [googleRole, setGoogleRole] = useState('Student_Parent');
  const [sessionNotice, setSessionNotice] = useState(() => {
    const notice = sessionStorage.getItem('otsp_auth_notice');
    if (notice) {
      sessionStorage.removeItem('otsp_auth_notice');
      return notice;
    }
    return '';
  });

  const { login, loginWithGoogle } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const routeUserByRole = (userInfo) => {
    const roles = [
      ...(Array.isArray(userInfo?.roles) ? userInfo.roles : []),
      ...(Array.isArray(userInfo?.Roles) ? userInfo.Roles : []),
      userInfo?.role,
      userInfo?.Role,
    ].filter(Boolean).map((role) => String(role).trim().toLowerCase());
    const from = location.state?.from?.pathname;
    const isAdmin = roles.includes('admin') || userInfo?.isAdmin === true;
    const isTutor = roles.includes('tutor');
    const isStudent = roles.includes('student') || roles.includes('student_parent');

    // Kiểm tra tính hợp lệ của đường dẫn chuyển hướng 'from' theo từng vai trò
    let isFromValidForRole = false;
    if (from && from !== '/login' && from !== '/access-denied') {
      if (isAdmin && from.startsWith('/admin')) {
        isFromValidForRole = true;
      } else if (isTutor && from.startsWith('/tutor')) {
        isFromValidForRole = true;
      } else if (isStudent && from.startsWith('/student')) {
        isFromValidForRole = true;
      } else if (!from.startsWith('/admin') && !from.startsWith('/tutor') && !from.startsWith('/student')) {
        // Trang công khai (như /search, /news, /classrooms, ...)
        isFromValidForRole = true;
      }
    }

    if (isFromValidForRole && from) {
      navigate(from, { replace: true });
    } else if (isAdmin) {
      navigate('/admin/dashboard', { replace: true });
    } else if (isTutor) {
      navigate('/tutor/dashboard', { replace: true });
    } else {
      navigate('/student/dashboard', { replace: true });
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSessionNotice('');
    setError('');
    setLoading(true);

    try {
      if (rememberMe) {
        localStorage.setItem('otsp_remember_email', email);
      } else {
        localStorage.removeItem('otsp_remember_email');
      }

      const userInfo = await login(email, password, rememberMe);
      routeUserByRole(userInfo);
    } catch (err) {
      setError(err.response?.data?.message || 'Đăng nhập không thành công. Vui lòng kiểm tra lại email hoặc mật khẩu.');
    } finally {
      setLoading(false);
    }
  };

  const handleGoogleCredential = async (credential) => {
    setError('');
    setLoading(true);

    try {
      const userInfo = await loginWithGoogle({ credential, role: googleRole });
      routeUserByRole(userInfo);
    } catch (err) {
      setError(err.response?.data?.message || 'Đăng nhập Google không thành công.');
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
              style={{ width: '3.5rem', height: '3.5rem', backgroundColor: '#fee2e2', color: '#dc2626', fontSize: '1.75rem' }}
            >
              <i className="bi bi-person-lock" aria-hidden="true"></i>
            </span>
            <h1 className="h3 mb-2 fw-bold">Chào mừng bạn trở lại</h1>
            <p className="text-body-secondary mb-0">Đăng nhập để tiếp tục sử dụng OTSP.</p>
          </div>

          {error && (
            <div
              className="d-flex align-items-center justify-content-between p-3 mb-3 rounded-3 shadow-sm alert alert-danger"
              role="alert"
            >
              <div className="d-flex align-items-center">
                <i className="bi bi-exclamation-circle-fill text-danger fs-5 me-2" aria-hidden="true"></i>
                <span className="small fw-medium">{error}</span>
              </div>
              <button
                type="button"
                className="btn-close ms-2"
                onClick={() => setError('')}
                aria-label="Đóng"
              ></button>
            </div>
          )}

          {sessionNotice && (
            <div
              className="d-flex align-items-center justify-content-between p-3 mb-3 rounded-3 shadow-sm alert alert-warning"
              role="status"
            >
              <div className="d-flex align-items-center">
                <i className="bi bi-info-circle-fill text-warning fs-5 me-2" aria-hidden="true"></i>
                <span className="small fw-medium">{sessionNotice}</span>
              </div>
              <button
                type="button"
                className="btn-close ms-2"
                onClick={() => setSessionNotice('')}
                aria-label="Đóng"
              ></button>
            </div>
          )}

          {location.state?.authRequired && !location.state?.loggedOut && !sessionNotice && (
            <div
              className="d-flex align-items-center p-3 mb-3 rounded-3 shadow-sm alert alert-danger"
              role="status"
            >
              <i className="bi bi-shield-lock-fill text-danger fs-5 me-2" aria-hidden="true"></i>
              <span className="small fw-medium">
                Vui lòng đăng nhập để tiếp tục đến trang bạn đã chọn.
              </span>
            </div>
          )}

          <form onSubmit={handleSubmit}>
            <div className="mb-3">
              <label className="form-label small fw-semibold" htmlFor="loginEmail">Email</label>
              <div className="input-group">
                <span className="input-group-text bg-body border-end-0">
                  <i className="bi bi-envelope text-muted"></i>
                </span>
                <input
                  id="loginEmail"
                  type="email"
                  className="form-control border-start-0"
                  placeholder="name@example.com"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                />
              </div>
            </div>

            <div className="mb-3">
              <div className="d-flex justify-content-between">
                <label className="form-label small fw-semibold" htmlFor="loginPassword">Mật khẩu</label>
                <Link to="/forgot-password" className="small text-danger text-decoration-none">Quên mật khẩu?</Link>
              </div>
              <div className="input-group">
                <span className="input-group-text bg-white border-end-0">
                  <i className="bi bi-key text-muted"></i>
                </span>
                <input
                  id="loginPassword"
                  type={showPassword ? 'text' : 'password'}
                  className="form-control border-start-0 border-end-0"
                  placeholder="Nhập mật khẩu của bạn"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
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

            <div className="form-check mb-4">
              <input
                className="form-check-input"
                type="checkbox"
                id="rememberMe"
                checked={rememberMe}
                onChange={(e) => setRememberMe(e.target.checked)}
              />
              <label className="form-check-label small text-body-secondary" htmlFor="rememberMe">
                Ghi nhớ đăng nhập
              </label>
            </div>

            <button
              type="submit"
              className="btn btn-danger w-100 py-2.5 fw-semibold mb-3 rounded-pill shadow-sm"
              style={{ backgroundColor: '#DB303A' }}
              disabled={loading}
            >
              {loading ? (
                <>
                  <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                  Đang đăng nhập...
                </>
              ) : (
                'Đăng nhập'
              )}
            </button>

            <div className="d-flex align-items-center my-3">
              <hr className="flex-grow-1" />
              <span className="px-2 small text-muted">hoặc đăng nhập nhanh</span>
              <hr className="flex-grow-1" />
            </div>

            <div className="mb-3 p-3 rounded-4 border bg-light-subtle">
              <div className="d-flex align-items-center justify-content-between flex-wrap gap-2 mb-2">
                <span className="small text-secondary fw-medium">Vai trò (nếu tạo tài khoản mới):</span>
                <div className="btn-group btn-group-sm" role="group" aria-label="Chọn vai trò Google">
                  <button
                    type="button"
                    className={`btn rounded-start-pill px-3 ${googleRole === 'Student_Parent' ? 'btn-danger' : 'btn-outline-secondary'}`}
                    onClick={() => setGoogleRole('Student_Parent')}
                  >
                    Học viên / PH
                  </button>
                  <button
                    type="button"
                    className={`btn rounded-end-pill px-3 ${googleRole === 'Tutor' ? 'btn-danger' : 'btn-outline-secondary'}`}
                    onClick={() => setGoogleRole('Tutor')}
                  >
                    Gia sư
                  </button>
                </div>
              </div>
              <GoogleSignInButton mode="signin" onCredential={handleGoogleCredential} disabled={loading} />
            </div>

            <div className="text-center">
              <span className="small text-body-secondary">Chưa có tài khoản? </span>
              <Link to="/register" className="small text-danger fw-semibold text-decoration-none">
                Đăng ký ngay
              </Link>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}

