import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import GoogleSignInButton from '../../components/auth/GoogleSignInButton';
import '../../styles/auth/RegisterPage.css';

export default function RegisterPage() {
  const [role, setRole] = useState('Student_Parent'); // 'Student_Parent' | 'Tutor'
  const [fullName, setFullName] = useState('');
  const [citizenIdNumber, setCitizenIdNumber] = useState('');
  const [residenceAddress, setResidenceAddress] = useState('');
  const [dateOfBirth, setDateOfBirth] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');

  // Gia sư
  const [introduction, setIntroduction] = useState('');

  // Học viên
  const [interestedSubjects, setInterestedSubjects] = useState('');
  const [area, setArea] = useState('');
  const [guardianFullName, setGuardianFullName] = useState('');
  const [guardianContact, setGuardianContact] = useState('');

  // Tài khoản
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [acceptTerms, setAcceptTerms] = useState(false);

  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [googleLoading, setGoogleLoading] = useState(false);

  const { register, loginWithGoogle } = useAuth();
  const navigate = useNavigate();

  const calculateAge = (dobStr) => {
    if (!dobStr) return null;
    const birth = new Date(dobStr);
    if (Number.isNaN(birth.getTime())) return null;
    const today = new Date();
    let age = today.getFullYear() - birth.getFullYear();
    const m = today.getMonth() - birth.getMonth();
    if (m < 0 || (m === 0 && today.getDate() < birth.getDate())) {
      age--;
    }
    return age;
  };

  const age = calculateAge(dateOfBirth);
  const isMinor = age !== null && age < 18;

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');

    if (!acceptTerms) {
      setError('Vui lòng đồng ý với Điều khoản sử dụng.');
      return;
    }

    if (password.length < 6) {
      setError('Mật khẩu phải có ít nhất 6 ký tự.');
      return;
    }

    if (password !== confirmPassword) {
      setError('Mật khẩu và xác nhận mật khẩu không khớp.');
      return;
    }

    if (citizenIdNumber.trim() && !/^[0-9]{12}$/.test(citizenIdNumber.trim())) {
      setError('Số CCCD phải gồm đúng 12 chữ số (hoặc để trống nếu chưa có).');
      return;
    }

    if (role === 'Tutor' && isMinor) {
      setError('Gia sư phải đủ 18 tuổi trở lên.');
      return;
    }

    setLoading(true);

    try {
      const payload = {
        role,
        fullName: fullName.trim(),
        citizenIdNumber: citizenIdNumber.trim() || undefined,
        residenceAddress: residenceAddress.trim() || undefined,
        dateOfBirth: dateOfBirth || undefined,
        phoneNumber: phoneNumber.trim() || undefined,
        email: email.trim(),
        password,
        confirmPassword,
        acceptTerms,
        introduction: role === 'Tutor' ? introduction.trim() : undefined,
        interestedSubjects: role === 'Student_Parent' ? interestedSubjects.trim() : undefined,
        area: role === 'Student_Parent' ? (area.trim() || residenceAddress.trim()) : undefined,
        guardianFullName: role === 'Student_Parent' && isMinor
          ? (guardianFullName.trim() || `Phụ huynh của ${fullName.trim()}`)
          : undefined,
        guardianContact: role === 'Student_Parent' && isMinor
          ? (guardianContact.trim() || phoneNumber.trim() || email.trim())
          : undefined,
      };

      await register(payload);

      if (role === 'Tutor') {
        navigate('/tutor/dashboard', { replace: true });
      } else {
        navigate('/student/dashboard', { replace: true });
      }
    } catch (err) {
      const msg = err.response?.data?.message ||
        (err.response?.data?.errors ? Object.values(err.response.data.errors).flat().join(', ') : 'Đăng ký không thành công. Vui lòng kiểm tra lại thông tin.');
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const handleGoogleCredential = async (credential) => {
    setError('');
    setGoogleLoading(true);
    try {
      const userInfo = await loginWithGoogle({ credential, role });
      const roles = [ ...(userInfo?.roles || []), userInfo?.role ].filter(Boolean).map((value) => String(value).toLowerCase());
      if (roles.includes('admin') || userInfo?.isAdmin) navigate('/admin/profile', { replace: true });
      else if (roles.includes('tutor')) navigate('/tutor/dashboard', { replace: true });
      else navigate('/student/dashboard', { replace: true });
    } catch (requestError) {
      setError(requestError?.response?.data?.message || 'Không thể đăng ký bằng Google. Vui lòng thử lại.');
    } finally {
      setGoogleLoading(false);
    }
  };

  return (
    <section className="register-section py-5" style={{ minHeight: '85vh', backgroundColor: 'var(--bs-tertiary-bg, #f8fafc)' }}>
      <div className="container">
        <div className="row justify-content-center">
          <div className="col-12 col-md-9 col-lg-7 col-xl-6">
            <div className="register-card">
              <h1>Tạo tài khoản</h1>
              <p className="sub">Chọn bạn là ai để chúng tôi hiển thị đúng thông tin cần điền.</p>

              {error && (
                <div className="alert alert-danger py-2.5 px-3 small mb-4 rounded-3 d-flex align-items-center" role="alert">
                  <i className="bi bi-exclamation-triangle-fill me-2 fs-5 flex-shrink-0"></i>
                  <div>{error}</div>
                </div>
              )}

              <form onSubmit={handleSubmit}>
                {/* 1. Chọn vai trò */}
                <div className="role-options">
                  <div className="role-option">
                    <input
                      type="radio"
                      id="roleStudent"
                      name="rolePicker"
                      checked={role === 'Student_Parent'}
                      onChange={() => setRole('Student_Parent')}
                    />
                    <label htmlFor="roleStudent">
                      <i className="bi bi-mortarboard"></i>
                      <strong>Học viên / Phụ huynh</strong>
                      <small>Tôi muốn tìm gia sư</small>
                    </label>
                  </div>
                  <div className="role-option">
                    <input
                      type="radio"
                      id="roleTutor"
                      name="rolePicker"
                      checked={role === 'Tutor'}
                      onChange={() => setRole('Tutor')}
                    />
                    <label htmlFor="roleTutor">
                      <i className="bi bi-person-workspace"></i>
                      <strong>Gia sư</strong>
                      <small>Tôi muốn nhận dạy</small>
                    </label>
                  </div>
                </div>

                {/* 2. Thông tin chung (Floating labels chuẩn Bootstrap) */}
                <div className="form-floating mb-3">
                  <input
                    type="text"
                    className="form-control"
                    id="regFullName"
                    placeholder="Họ và tên"
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    required
                  />
                  <label htmlFor="regFullName">Họ và tên</label>
                </div>

                <div className="form-floating mb-3">
                  <input
                    type="text"
                    className="form-control"
                    id="regCitizenId"
                    placeholder="Số CCCD (12 chữ số)"
                    maxLength={12}
                    value={citizenIdNumber}
                    onChange={(e) => setCitizenIdNumber(e.target.value)}
                  />
                  <label htmlFor="regCitizenId">Số CCCD (12 chữ số)</label>
                </div>

                <div className="form-floating mb-3">
                  <input
                    type="text"
                    className="form-control"
                    id="regAddress"
                    placeholder="Nơi ở hiện tại"
                    value={residenceAddress}
                    onChange={(e) => setResidenceAddress(e.target.value)}
                  />
                  <label htmlFor="regAddress">Nơi ở hiện tại</label>
                </div>

                {/* Row: Ngày sinh + Số điện thoại (khoảng cách g-3 chuẩn Bootstrap) */}
                <div className="row g-3 mb-3">
                  <div className="col-md-6">
                    <div className="form-floating">
                      <input
                        type="date"
                        className="form-control"
                        id="regDob"
                        placeholder="Ngày sinh"
                        value={dateOfBirth}
                        onChange={(e) => setDateOfBirth(e.target.value)}
                      />
                      <label htmlFor="regDob">Ngày sinh</label>
                    </div>
                  </div>
                  <div className="col-md-6">
                    <div className="form-floating">
                      <input
                        type="tel"
                        className="form-control"
                        id="regPhone"
                        placeholder="Số điện thoại"
                        value={phoneNumber}
                        onChange={(e) => setPhoneNumber(e.target.value)}
                      />
                      <label htmlFor="regPhone">Số điện thoại</label>
                    </div>
                  </div>
                </div>

                {/* 3. Dành riêng cho Gia sư */}
                {role === 'Tutor' && (
                  <div className="mb-3">
                    <div className="form-section-title">Thông tin giảng dạy</div>
                    <div className="form-floating mb-3">
                      <textarea
                        className="form-control"
                        id="regIntro"
                        placeholder="Giới thiệu bản thân và kinh nghiệm dạy"
                        style={{ height: '110px' }}
                        value={introduction}
                        onChange={(e) => setIntroduction(e.target.value)}
                      ></textarea>
                      <label htmlFor="regIntro">Giới thiệu bản thân và kinh nghiệm dạy</label>
                    </div>

                    <div
                      className="alert alert-info py-2.5 px-3 small rounded-3"
                      style={{
                        backgroundColor: '#e0f2fe',
                        borderColor: '#bae6fd',
                        color: '#0369a1',
                        border: '1px solid #bae6fd'
                      }}
                    >
                      Sau khi đăng ký, hồ sơ gia sư sẽ ở trạng thái <strong>chờ duyệt</strong>. Bạn bổ sung bằng cấp, môn dạy trong trang hồ sơ để quản trị viên xác minh.
                    </div>
                  </div>
                )}

                {/* 4. Dành riêng cho Học viên / Phụ huynh */}
                {role === 'Student_Parent' && (
                  <div className="mb-3">
                    <div className="form-section-title">Nhu cầu học tập (không bắt buộc)</div>
                    <div className="form-floating mb-3">
                      <input
                        type="text"
                        className="form-control"
                        id="regSubjects"
                        placeholder="Môn học quan tâm"
                        value={interestedSubjects}
                        onChange={(e) => setInterestedSubjects(e.target.value)}
                      />
                      <label htmlFor="regSubjects">Môn học quan tâm (ví dụ: Toán, Tiếng Anh)</label>
                    </div>

                    <div className="form-floating mb-3">
                      <input
                        type="text"
                        className="form-control"
                        id="regArea"
                        placeholder="Khu vực"
                        value={area}
                        onChange={(e) => setArea(e.target.value)}
                      />
                      <label htmlFor="regArea">Khu vực (ví dụ: Đồng Nai)</label>
                    </div>

                    {isMinor && (
                      <div className="p-3 rounded-3 border bg-light-subtle mt-2">
                        <div className="small fw-semibold text-success mb-2">
                          <i className="bi bi-shield-check me-1"></i>
                          Thông tin Phụ huynh / Người giám hộ (dành cho học viên dưới 18 tuổi)
                        </div>
                        <div className="row g-2">
                          <div className="col-md-6">
                            <div className="form-floating">
                              <input
                                type="text"
                                className="form-control"
                                id="regGuardianName"
                                placeholder="Họ tên phụ huynh"
                                value={guardianFullName}
                                onChange={(e) => setGuardianFullName(e.target.value)}
                              />
                              <label htmlFor="regGuardianName">Họ tên Phụ huynh / Giám hộ</label>
                            </div>
                          </div>
                          <div className="col-md-6">
                            <div className="form-floating">
                              <input
                                type="text"
                                className="form-control"
                                id="regGuardianContact"
                                placeholder="SĐT phụ huynh"
                                value={guardianContact}
                                onChange={(e) => setGuardianContact(e.target.value)}
                              />
                              <label htmlFor="regGuardianContact">SĐT / Liên hệ Phụ huynh</label>
                            </div>
                          </div>
                        </div>
                      </div>
                    )}
                  </div>
                )}

                {/* 5. Tài khoản đăng nhập */}
                <div className="form-section-title">Tài khoản đăng nhập</div>
                <div className="form-floating mb-3">
                  <input
                    type="email"
                    className="form-control"
                    id="regEmail"
                    placeholder="name@example.com"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    required
                  />
                  <label htmlFor="regEmail">Email</label>
                </div>

                <div className="form-floating has-toggle mb-3">
                  <input
                    type={showPassword ? 'text' : 'password'}
                    className="form-control"
                    id="regPassword"
                    placeholder="Mật khẩu"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                  />
                  <label htmlFor="regPassword">Mật khẩu</label>
                  <button
                    type="button"
                    className="password-toggle"
                    onClick={() => setShowPassword(!showPassword)}
                    aria-label="Ẩn hiện mật khẩu"
                  >
                    <i className={`bi ${showPassword ? 'bi-eye-slash' : 'bi-eye'}`}></i>
                  </button>
                </div>

                <div className="form-floating has-toggle mb-3">
                  <input
                    type={showConfirmPassword ? 'text' : 'password'}
                    className="form-control"
                    id="regConfirmPassword"
                    placeholder="Nhập lại mật khẩu"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    required
                  />
                  <label htmlFor="regConfirmPassword">Nhập lại mật khẩu</label>
                  <button
                    type="button"
                    className="password-toggle"
                    onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                    aria-label="Ẩn hiện mật khẩu"
                  >
                    <i className={`bi ${showConfirmPassword ? 'bi-eye-slash' : 'bi-eye'}`}></i>
                  </button>
                </div>

                {/* 6. Điều khoản */}
                <div className="form-check mb-4 mt-3">
                  <input
                    className="form-check-input"
                    type="checkbox"
                    id="acceptTerms"
                    checked={acceptTerms}
                    onChange={(e) => setAcceptTerms(e.target.checked)}
                    required
                  />
                  <label className="form-check-label small" htmlFor="acceptTerms">
                    Tôi đồng ý với <Link to="/terms" className="text-decoration-none fw-semibold">Điều khoản sử dụng</Link> và <Link to="/privacy" className="text-decoration-none fw-semibold">Chính sách bảo mật</Link>
                  </label>
                </div>

                {/* 7. Nút Đăng ký */}
                <button
                  type="submit"
                  className="btn-register shadow-sm"
                  disabled={loading}
                >
                  {loading ? (
                    <>
                      <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                      Đang đăng ký...
                    </>
                  ) : (
                    'Đăng ký'
                  )}
                </button>

                <div className="d-flex align-items-center my-3">
                  <hr className="flex-grow-1" />
                  <span className="px-2 small text-muted">hoặc</span>
                  <hr className="flex-grow-1" />
                </div>

                <GoogleSignInButton mode="signup" onCredential={handleGoogleCredential} disabled={loading || googleLoading} />
                <p className="small text-secondary text-center mt-2 mb-0">Google tự điền email và tên; loại tài khoản lấy theo lựa chọn học viên/gia sư phía trên.</p>

                {/* 8. Link Đăng nhập */}
                <p className="text-center mt-3 mb-0 small text-secondary">
                  Đã có tài khoản?{' '}
                  <Link to="/login" className="fw-semibold text-decoration-none">
                    Đăng nhập
                  </Link>
                </p>
              </form>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
