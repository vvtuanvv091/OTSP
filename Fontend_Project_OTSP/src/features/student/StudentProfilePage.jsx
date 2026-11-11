import { useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';
import studentApi from '../../api/studentApi';
import { resolveAssetUrl } from '../../api/config';
import { useAuth } from '../../context/AuthContext';
import { useToast } from '../../context/ToastContext';
import { useConfirm } from '../../context/ConfirmDialogContext';
import CollapsibleProfileSection from '../../components/common/CollapsibleProfileSection';
import ChangePasswordPanel from '../auth/ChangePasswordPanel';
import compressImageFile from '../../utils/imageCompressor';

export default function StudentProfilePage() {
  const { user, updateUser } = useAuth();
  const toast = useToast();
  const confirm = useConfirm();
  const location = useLocation();

  const [loading, setLoading] = useState(true);
  const [profile, setProfile] = useState({
    fullName: user?.fullName || '',
    email: user?.email || '',
    phoneNumber: '',
    dateOfBirth: '',
    citizenIdNumber: '',
    residenceAddress: '',
    interestedSubjects: '',
    area: '',
    freeSchedule: '',
    avatarUrl: user?.avatarUrl || '/assets/img/default-avatar.svg',
    isMinor: false
  });

  const [guardians, setGuardians] = useState([]);
  const [showOptionalGuardianForm, setShowOptionalGuardianForm] = useState(false);
  const [isGuardianSectionExpanded, setIsGuardianSectionExpanded] = useState(true);

  // Biểu mẫu thêm người giám hộ mới
  const [newGuardian, setNewGuardian] = useState({
    fullName: '',
    contactInfo: '',
    citizenIdNumber: '',
    residenceAddress: ''
  });

  const [message, setMessage] = useState({ type: '', text: '' });
  const [uploadingAvatar, setUploadingAvatar] = useState(false);
  const [savingProfile, setSavingProfile] = useState(false);

  useEffect(() => {
    let isMounted = true;
    async function fetchProfile() {
      try {
        const res = await studentApi.getProfile();
        if (isMounted && res.data) {
          const prof = res.data.profile || {};
          setProfile({
            fullName: prof.fullName || user?.fullName || '',
            email: prof.email || user?.email || '',
            phoneNumber: prof.phoneNumber || '',
            dateOfBirth: prof.dateOfBirth ? String(prof.dateOfBirth).split('T')[0] : '',
            citizenIdNumber: prof.citizenIdNumber || '',
            residenceAddress: prof.residenceAddress || '',
            interestedSubjects: prof.interestedSubjects || '',
            area: prof.area || '',
            freeSchedule: prof.freeSchedule || '',
            avatarUrl: prof.avatarUrl || user?.avatarUrl || '/assets/img/default-avatar.svg',
            isMinor: !!prof.isMinor
          });

          if (Array.isArray(res.data.guardians)) {
            setGuardians(res.data.guardians);
          }

          // Đồng bộ với AuthContext cho Header và Sidebar
          if (prof.avatarUrl && prof.avatarUrl !== user?.avatarUrl) {
            updateUser({ avatarUrl: prof.avatarUrl });
          }
          if (prof.fullName && prof.fullName !== user?.fullName) {
            updateUser({ fullName: prof.fullName });
          }
        }
      } catch (err) {
        console.warn('Error fetching profile from API:', err);
      } finally {
        if (isMounted) setLoading(false);
      }
    }
    fetchProfile();
    return () => {
      isMounted = false;
    };
  }, [user?.id]);

  useEffect(() => {
    if (location.hash === '#guardians') {
      const timer = setTimeout(() => {
        document.getElementById('guardians')?.scrollIntoView({ behavior: 'smooth' });
      }, 200);
      return () => clearTimeout(timer);
    }
    return undefined;
  }, [location.hash]);

  const updateField = (field) => (event) => {
    setProfile((current) => ({ ...current, [field]: event.target.value }));
  };

  const handleSaveProfile = async (event) => {
    event.preventDefault();
    setSavingProfile(true);
    setMessage({ type: '', text: '' });
    try {
      await studentApi.updateProfile({
        fullName: profile.fullName,
        citizenIdNumber: profile.citizenIdNumber,
        residenceAddress: profile.residenceAddress,
        phoneNumber: profile.phoneNumber,
        interestedSubjects: profile.interestedSubjects,
        area: profile.area,
        freeSchedule: profile.freeSchedule
      });
      if (profile.fullName) {
        updateUser({ fullName: profile.fullName });
      }
      toast.success('Cập nhật hồ sơ cá nhân thành công!');
      setMessage({ type: 'success', text: 'Cập nhật hồ sơ cá nhân thành công!' });
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch (err) {
      setMessage({
        type: 'danger',
        text: err?.response?.data?.message || 'Không thể cập nhật hồ sơ, vui lòng thử lại.'
      });
    } finally {
      setSavingProfile(false);
    }
  };

  const handleAvatarChange = async (event) => {
    const file = event.target.files?.[0];
    if (!file) return;

    setUploadingAvatar(true);
    setMessage({ type: '', text: '' });
    try {
      const optimized = await compressImageFile(file, { maxWidth: 600, maxHeight: 600, quality: 0.85 });
      const formData = new FormData();
      formData.append('avatarFile', optimized);

      const res = await studentApi.uploadAvatar(formData);
      const newUrl = res.data?.avatarUrl || res.data?.url;
      if (newUrl) {
        setProfile((prev) => ({ ...prev, avatarUrl: newUrl }));
        updateUser({ avatarUrl: newUrl });
      }
      setMessage({ type: 'success', text: 'Cập nhật ảnh đại diện thành công!' });
    } catch {
      setMessage({ type: 'danger', text: 'Không thể tải ảnh đại diện lên, vui lòng thử lại.' });
    } finally {
      setUploadingAvatar(false);
    }
  };

  const handleAddGuardian = async (e) => {
    e.preventDefault();
    if (!newGuardian.fullName || !newGuardian.contactInfo) {
      toast.warning('Vui lòng nhập họ tên và thông tin liên hệ của người giám hộ.');
      return;
    }

    try {
      const res = await studentApi.addGuardian(newGuardian);
      if (res.data) {
        setGuardians((prev) => [...prev, res.data]);
        setNewGuardian({ fullName: '', contactInfo: '', citizenIdNumber: '', residenceAddress: '' });
        setShowOptionalGuardianForm(false);
        toast.success('Thêm người giám hộ thành công!');
      }
    } catch (err) {
      toast.error(err?.response?.data?.message || 'Không thể thêm người giám hộ.');
    }
  };

  const handleRemoveGuardian = async (id) => {
    const ok = await confirm({
      title: 'Xóa người giám hộ',
      message: 'Bạn có chắc chắn muốn xóa người giám hộ này không?',
      confirmText: 'Xóa',
      variant: 'danger',
    });
    if (!ok) return;
    try {
      await studentApi.removeGuardian(id);
      setGuardians((prev) => prev.filter((g) => g.id !== id));
      toast.success('Đã xóa người giám hộ.');
    } catch {
      toast.error('Không thể xóa người giám hộ.');
    }
  };

  // Định dạng ngày hiển thị theo chuẩn ngày/tháng/năm
  const formatDateDisplay = (dateString) => {
    if (!dateString) return '—';
    try {
      const parts = dateString.split('-');
      if (parts.length === 3) {
        return `${parts[2]}/${parts[1]}/${parts[0]}`;
      }
      return dateString;
    } catch {
      return dateString;
    }
  };

  if (loading) {
    return (
      <div className="container py-5 text-center">
        <div className="spinner-border text-danger" role="status"></div>
        <div className="mt-2 text-secondary small">Đang tải hồ sơ học viên...</div>
      </div>
    );
  }

  return (
    <div className="container-fluid profile-wrap py-3 px-1 px-md-3" style={{ width: '100%' }}>
      {message.text && (
        <div
          className={`alert alert-${message.type} alert-dismissible fade show shadow-sm mb-4 d-flex align-items-center`}
          role="alert"
        >
          <i
            className={`bi ${message.type === 'success' ? 'bi-check-circle-fill' : 'bi-exclamation-triangle-fill'} me-2 fs-5`}
          ></i>
          <div>{message.text}</div>
          <button type="button" className="btn-close" onClick={() => setMessage({ type: '', text: '' })}></button>
        </div>
      )}

      <div className="row g-4">
        {/* Cột trái: Ảnh đại diện & Tóm tắt hồ sơ */}
        <aside className="col-lg-4">
          <div className="profile-sidebar-wrapper" style={{ position: 'sticky', top: 'calc(var(--student-header-offset, 140px) + 20px)', zIndex: 10 }}>
            <CollapsibleProfileSection label="Ảnh đại diện và tóm tắt">
              <div className="pro-card text-center p-4">
                <div className="avatar-wrapper mb-3 position-relative d-inline-block">
                  <div
                    className="avatar-ring"
                    style={{
                      width: 136,
                      height: 136,
                      padding: 4,
                      borderRadius: '50%',
                      background: 'linear-gradient(135deg, #DB303A, #ff7a85)',
                      boxShadow: '0 8px 24px rgba(219, 48, 58, .22)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      overflow: 'hidden'
                    }}
                  >
                    <img
                      id="studentAvatarImg"
                      src={resolveAssetUrl(profile.avatarUrl) || '/assets/img/default-avatar.svg'}
                      alt="Ảnh đại diện"
                      style={{
                        width: '100%',
                        height: '100%',
                        aspectRatio: '1 / 1',
                        objectFit: 'cover',
                        borderRadius: '50%',
                        border: '3.5px solid #fff',
                        backgroundColor: '#f1f5f9',
                        display: 'block'
                      }}
                      onError={(e) => {
                        e.target.src = '/assets/img/default-avatar.svg';
                      }}
                    />
                  </div>

                  {uploadingAvatar && (
                    <div
                      className="position-absolute inset-0 rounded-circle d-flex align-items-center justify-content-center"
                      style={{
                        top: 0,
                        left: 0,
                        width: 136,
                        height: 136,
                        background: 'rgba(0,0,0,0.55)',
                        zIndex: 3
                      }}
                    >
                      <div className="spinner-border spinner-border-sm text-light" role="status"></div>
                    </div>
                  )}

                  <label
                    htmlFor="student-avatar-input"
                    className="avatar-upload-btn"
                    title="Đổi ảnh đại diện"
                    style={{
                      position: 'absolute',
                      bottom: 2,
                      right: 2,
                      width: 38,
                      height: 38,
                      borderRadius: '50%',
                      backgroundColor: '#DB303A',
                      color: '#fff',
                      display: 'inline-flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      cursor: 'pointer',
                      border: '3px solid #fff',
                      boxShadow: '0 4px 12px rgba(0,0,0,0.18)',
                      zIndex: 2
                    }}
                  >
                    <i className="bi bi-camera-fill" aria-hidden="true"></i>
                  </label>
                  <input
                    id="student-avatar-input"
                    type="file"
                    accept="image/jpeg,image/png,image/webp"
                    className="visually-hidden"
                    onChange={handleAvatarChange}
                  />
                </div>

                <h2 className="h5 fw-bold mb-2 text-body">{profile.fullName || 'Học viên'}</h2>

                {profile.isMinor ? (
                  <span className="badge rounded-pill bg-info-subtle text-info-emphasis px-3 py-2 border border-info-subtle">
                    Học viên dưới 18 tuổi
                  </span>
                ) : (
                  <span className="badge rounded-pill bg-secondary-subtle text-secondary-emphasis px-3 py-2 border">
                    Học viên / Phụ huynh
                  </span>
                )}

                <div className="small text-secondary mt-3 pt-3 border-top d-flex justify-content-between">
                  <span>Người giám hộ:</span>
                  <strong>{guardians.length}</strong>
                </div>
              </div>
            </CollapsibleProfileSection>
          </div>
        </aside>

        {/* Cột phải: Các biểu mẫu thông tin cá nhân & học tập */}
        <main className="col-lg-8">
          <form onSubmit={handleSaveProfile}>
            {/* Section 1: Thông tin cá nhân */}
            <CollapsibleProfileSection label="Thông tin cá nhân">
              <section className="pro-card mb-4 p-4 shadow-sm">
                <div className="section-head mb-3 d-flex align-items-center gap-2">
                  <span
                    style={{
                      width: 36,
                      height: 36,
                      borderRadius: 10,
                      display: 'inline-flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      backgroundColor: 'rgba(219, 48, 58, .12)',
                      color: '#DB303A'
                    }}
                  >
                    <i className="bi bi-person-vcard fs-5"></i>
                  </span>
                  <h2 className="h5 fw-bold mb-0 text-body">Thông tin cá nhân</h2>
                </div>

                <div className="row g-3">
                  <div className="col-md-6">
                    <label className="form-label small fw-semibold text-secondary">Họ và tên</label>
                    <input
                      type="text"
                      className="form-control"
                      value={profile.fullName}
                      onChange={updateField('fullName')}
                      maxLength="150"
                      required
                    />
                  </div>
                  <div className="col-md-6">
                    <label className="form-label small fw-semibold text-secondary">Số CCCD (12 chữ số)</label>
                    <input
                      type="text"
                      className="form-control"
                      placeholder="Nhập đúng 12 chữ số"
                      value={profile.citizenIdNumber}
                      onChange={updateField('citizenIdNumber')}
                      maxLength="12"
                      pattern="[0-9]{12}"
                    />
                  </div>
                  <div className="col-12">
                    <label className="form-label small fw-semibold text-secondary">Nơi ở hiện tại</label>
                    <input
                      type="text"
                      className="form-control"
                      placeholder="Địa chỉ cư trú"
                      value={profile.residenceAddress}
                      onChange={updateField('residenceAddress')}
                      maxLength="500"
                    />
                  </div>
                  <div className="col-md-6">
                    <label className="form-label small fw-semibold text-secondary">Email</label>
                    <input
                      type="email"
                      className="form-control"
                      value={profile.email}
                      readOnly
                      style={{ backgroundColor: 'var(--bs-tertiary-bg, #f8f9fa)', cursor: 'not-allowed' }}
                      title="Email tài khoản không thể chỉnh sửa"
                    />
                  </div>
                  <div className="col-md-6">
                    <label className="form-label small fw-semibold text-secondary">Số điện thoại</label>
                    <input
                      type="tel"
                      className="form-control"
                      placeholder="Nhập số điện thoại"
                      value={profile.phoneNumber}
                      onChange={updateField('phoneNumber')}
                      maxLength="20"
                      title="Số điện thoại tài khoản"
                    />
                  </div>
                  <div className="col-md-6">
                    <label className="form-label small fw-semibold text-secondary">Ngày sinh</label>
                    <input
                      type="text"
                      className="form-control"
                      value={formatDateDisplay(profile.dateOfBirth)}
                      readOnly
                      style={{ backgroundColor: 'var(--bs-tertiary-bg, #f8f9fa)', cursor: 'not-allowed' }}
                    />
                  </div>
                </div>
              </section>
            </CollapsibleProfileSection>

            {/* Section 2: Nhu cầu học tập */}
            <CollapsibleProfileSection label="Nhu cầu học tập">
              <section className="pro-card mb-4 p-4 shadow-sm">
                <div className="section-head mb-3 d-flex align-items-center gap-2">
                  <span
                    style={{
                      width: 36,
                      height: 36,
                      borderRadius: 10,
                      display: 'inline-flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      backgroundColor: 'rgba(219, 48, 58, .12)',
                      color: '#DB303A'
                    }}
                  >
                    <i className="bi bi-search fs-5"></i>
                  </span>
                  <h2 className="h5 fw-bold mb-0 text-body">Nhu cầu học tập</h2>
                </div>

                <div className="row g-3">
                  <div className="col-12">
                    <label className="form-label small fw-semibold text-secondary">Môn học quan tâm</label>
                    <input
                      type="text"
                      className="form-control"
                      placeholder="Ví dụ: Toán, Tiếng Anh"
                      value={profile.interestedSubjects}
                      onChange={updateField('interestedSubjects')}
                      maxLength="500"
                    />
                  </div>
                  <div className="col-md-6">
                    <label className="form-label small fw-semibold text-secondary">Khu vực học mong muốn</label>
                    <input
                      type="text"
                      className="form-control"
                      placeholder="Ví dụ: Biên Hòa, Đồng Nai"
                      value={profile.area}
                      onChange={updateField('area')}
                      maxLength="200"
                    />
                  </div>
                  <div className="col-md-6">
                    <label className="form-label small fw-semibold text-secondary">Lịch rảnh</label>
                    <input
                      type="text"
                      className="form-control"
                      placeholder="Ví dụ: Thứ 3, Thứ 5 buổi tối"
                      value={profile.freeSchedule}
                      onChange={updateField('freeSchedule')}
                      maxLength="500"
                    />
                  </div>
                </div>

                <div className="d-flex justify-content-end mt-4">
                  <button
                    type="submit"
                    className="btn btn-brand"
                    disabled={savingProfile}
                    style={{
                      background: 'linear-gradient(120deg, #DB303A, #B7212B)',
                      color: '#fff',
                      border: 'none',
                      borderRadius: 999,
                      padding: '0.7rem 1.8rem',
                      fontWeight: 600,
                      boxShadow: '0 8px 20px rgba(219, 48, 58, .22)'
                    }}
                  >
                    <i className="bi bi-check2-circle me-2"></i>
                    {savingProfile ? 'Đang lưu...' : 'Lưu hồ sơ'}
                  </button>
                </div>
              </section>
            </CollapsibleProfileSection>
          </form>

          {/* Section 3: Người giám hộ */}
          <section className="pro-card mb-4 p-4 shadow-sm" id="guardians">
            <div className="section-head mb-3 d-flex align-items-center justify-content-between flex-wrap gap-2">
              <div className="d-flex align-items-center gap-2 flex-wrap">
                <span
                  style={{
                    width: 36,
                    height: 36,
                    borderRadius: 10,
                    display: 'inline-flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    backgroundColor: 'rgba(219, 48, 58, .12)',
                    color: '#DB303A'
                  }}
                >
                  <i className="bi bi-people fs-5"></i>
                </span>
                <h2 className="h5 fw-bold mb-0 text-body">Người giám hộ</h2>
                {!profile.isMinor ? (
                  <span className="badge bg-success-subtle text-success border border-success-subtle rounded-pill px-2.5 py-1" style={{ fontSize: 11 }}>
                    Trên 18 tuổi · Tự quản lý
                  </span>
                ) : (
                  <span className="badge bg-warning-subtle text-warning-emphasis border border-warning-subtle rounded-pill px-2.5 py-1" style={{ fontSize: 11 }}>
                    Dưới 18 tuổi · Bắt buộc
                  </span>
                )}
              </div>

              <div className="d-flex align-items-center gap-2 flex-wrap" style={{ gap: '10px' }}>
                {!profile.isMinor && !showOptionalGuardianForm && (
                  <button
                    type="button"
                    className="btn btn-sm btn-outline-danger d-inline-flex align-items-center gap-1.5 px-3 py-1.5 rounded-pill fw-semibold me-2 shadow-xs"
                    style={{ borderColor: '#DB303A', color: '#DB303A' }}
                    onClick={() => {
                      setIsGuardianSectionExpanded(true);
                      setShowOptionalGuardianForm(true);
                    }}
                  >
                    <i className="bi bi-plus-lg me-1"></i>Thêm người giám hộ (tùy chọn)
                  </button>
                )}

                <button
                  type="button"
                  className="profile-collapse-control"
                  onClick={() => setIsGuardianSectionExpanded(!isGuardianSectionExpanded)}
                  title={isGuardianSectionExpanded ? 'Thu gọn phần người giám hộ' : 'Mở rộng phần người giám hộ'}
                >
                  <span className="small">{isGuardianSectionExpanded ? 'Thu gọn' : 'Mở rộng'}</span>
                  <i className={`bi ${isGuardianSectionExpanded ? 'bi-chevron-up' : 'bi-chevron-down'} ms-1`}></i>
                </button>
              </div>
            </div>

            {isGuardianSectionExpanded && (
              <>
                {/* Danh sách người giám hộ (nếu đã có) */}
                {guardians.length > 0 ? (
                  <div className="guardian-list mb-3">
                    {guardians.map((g) => (
                      <div
                        key={g.id}
                        className="guardian-item p-3 border rounded-3 mb-2 d-flex justify-content-between align-items-center gap-2 bg-body"
                      >
                        <div>
                          <div className="fw-bold text-body">{g.fullName}</div>
                          <div className="small text-secondary">
                            <i className="bi bi-telephone me-1 text-danger"></i>Liên hệ: {g.contactInfo} ·{' '}
                            <i className="bi bi-card-text me-1 text-danger"></i>CCCD: {g.citizenIdNumber || '—'}
                          </div>
                          {g.residenceAddress && (
                            <div className="small text-muted mt-0.5">
                              <i className="bi bi-geo-alt me-1 text-danger"></i>{g.residenceAddress}
                            </div>
                          )}
                        </div>
                        <div>
                          <button
                            type="button"
                            onClick={() => handleRemoveGuardian(g.id)}
                            className="btn btn-outline-danger btn-sm px-3"
                            title="Xóa người giám hộ"
                          >
                            Xóa
                          </button>
                        </div>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="empty-box mb-3 d-flex align-items-center gap-2.5 p-3 border border-dashed rounded-3 text-secondary small bg-body">
                    <i className="bi bi-shield-check fs-4 text-success"></i>
                    <span>
                      {profile.isMinor
                        ? 'Học viên dưới 18 tuổi cần thêm ít nhất một người giám hộ hợp pháp.'
                        : 'Bạn đã trên 18 tuổi và tự quản lý tài khoản. Thông tin người giám hộ là hoàn toàn không bắt buộc.'}
                    </span>
                  </div>
                )}

                {/* Form thêm người giám hộ:
                    - BẮT BUỘC hiển thị khi isMinor = true
                    - Chỉ hiển thị khi showOptionalGuardianForm = true nếu đã đủ 18 tuổi */}
                {(profile.isMinor || showOptionalGuardianForm) && (
                  <form
                    onSubmit={handleAddGuardian}
                    className="mt-3 p-3.5 border border-dashed rounded-3"
                    style={{
                      backgroundColor: 'rgba(219, 48, 58, .04)',
                      borderColor: '#DB303A'
                    }}
                  >
                    <div className="d-flex justify-content-between align-items-center mb-2">
                      <h3 className="h6 fw-bold mb-0 text-danger">
                        <i className="bi bi-person-plus-fill me-1"></i>
                        {profile.isMinor ? 'Thêm người giám hộ (Bắt buộc đối với học viên < 18 tuổi)' : 'Thêm người giám hộ (Tùy chọn)'}
                      </h3>
                      {!profile.isMinor && (
                        <button
                          type="button"
                          className="btn btn-sm btn-outline-secondary py-0 px-2"
                          onClick={() => setShowOptionalGuardianForm(false)}
                          style={{ fontSize: 12 }}
                        >
                          Thu gọn form
                        </button>
                      )}
                    </div>

                    <div className="row g-3 align-items-end mt-1">
                      <div className="col-md-6">
                        <label className="form-label small fw-semibold text-secondary">Họ và tên người giám hộ</label>
                        <input
                          type="text"
                          className="form-control form-control-sm"
                          placeholder="Ví dụ: Trần Văn Phụ Huynh"
                          value={newGuardian.fullName}
                          onChange={(e) => setNewGuardian({ ...newGuardian, fullName: e.target.value })}
                          required
                        />
                      </div>
                      <div className="col-md-6">
                        <label className="form-label small fw-semibold text-secondary">Số điện thoại hoặc email</label>
                        <input
                          type="text"
                          className="form-control form-control-sm"
                          placeholder="0900000000 hoặc email"
                          value={newGuardian.contactInfo}
                          onChange={(e) => setNewGuardian({ ...newGuardian, contactInfo: e.target.value })}
                          required
                        />
                      </div>
                      <div className="col-md-6">
                        <label className="form-label small fw-semibold text-secondary">CCCD (12 chữ số)</label>
                        <input
                          type="text"
                          maxLength="12"
                          pattern="[0-9]{12}"
                          className="form-control form-control-sm"
                          placeholder="079178000999"
                          value={newGuardian.citizenIdNumber}
                          onChange={(e) => setNewGuardian({ ...newGuardian, citizenIdNumber: e.target.value })}
                        />
                      </div>
                      <div className="col-md-6">
                        <label className="form-label small fw-semibold text-secondary">Nơi ở</label>
                        <input
                          type="text"
                          className="form-control form-control-sm"
                          placeholder="Địa chỉ thường trú"
                          value={newGuardian.residenceAddress}
                          onChange={(e) => setNewGuardian({ ...newGuardian, residenceAddress: e.target.value })}
                        />
                      </div>
                      <div className="col-12 d-grid mt-3">
                        <button
                          type="submit"
                          className="btn btn-danger py-2 fw-semibold"
                          style={{
                            background: 'linear-gradient(120deg, #DB303A, #B7212B)',
                            border: 'none',
                            borderRadius: 999
                          }}
                        >
                          Thêm
                        </button>
                      </div>
                    </div>
                  </form>
                )}
              </>
            )}
          </section>

          <ChangePasswordPanel />
        </main>
      </div>

      <style>{`
        .pro-card {
          background: var(--bs-card-bg, #ffffff);
          border: 1px solid var(--bs-border-color, #e5e7eb);
          border-radius: 14px;
        }
        [data-bs-theme="dark"] .pro-card {
          background-color: #1e293b;
          border-color: #334155;
        }
      `}</style>
    </div>
  );
}
