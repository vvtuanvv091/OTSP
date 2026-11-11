import { useState, useEffect } from 'react';
import tutorApi from '../../api/tutorApi';
import { resolveAssetUrl } from '../../api/config';
import { useAuth } from '../../context/AuthContext';
import { useToast } from '../../context/ToastContext';
import { useConfirm } from '../../context/ConfirmDialogContext';
import CollapsibleProfileSection from '../../components/common/CollapsibleProfileSection';
import ChangePasswordPanel from '../auth/ChangePasswordPanel';
import compressImageFile from '../../utils/imageCompressor';

export default function TutorProfilePage() {
  const { user, updateUser } = useAuth();
  const toast = useToast();
  const confirm = useConfirm();
  const [profile, setProfile] = useState(null);
  const [tutorSubjects, setTutorSubjects] = useState([]);
  const [qualifications, setQualifications] = useState([]);
  const [allSubjects, setAllSubjects] = useState([]);
  const [allGradeLevels, setAllGradeLevels] = useState([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [uploadingAvatar, setUploadingAvatar] = useState(false);
  const [showQualUpload, setShowQualUpload] = useState(false);
  const [uploadingQual, setUploadingQual] = useState(false);
  const [addingSubject, setAddingSubject] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [reloadKey, setReloadKey] = useState(0);


  // Các trường thông tin hồ sơ gia sư
  const [displayName, setDisplayName] = useState('');
  const [citizenIdNumber, setCitizenIdNumber] = useState('');
  const [residenceAddress, setResidenceAddress] = useState('');
  const [introduction, setIntroduction] = useState('');
  const [selectedSubjectId, setSelectedSubjectId] = useState('');
  const [selectedGradeLevelId, setSelectedGradeLevelId] = useState('');
  const [qualTitle, setQualTitle] = useState('');
  const [qualFile, setQualFile] = useState(null);

  useEffect(() => {
    let active = true;
    async function loadProfileData() {
      try {
        setLoading(true);
        const res = await tutorApi.getProfile();
        if (!active) return;
        if (res.data) {
          const p = res.data.profile || {};
          setProfile(p);
          setDisplayName(p.fullName || '');
          setCitizenIdNumber(p.citizenIdNumber || '');
          setResidenceAddress(p.residenceAddress || '');
          setIntroduction(p.introduction || '');
          setTutorSubjects(res.data.subjects || []);
          setQualifications(res.data.qualifications || []);
          const subs = res.data.allSubjects || [];
          const grades = res.data.allGradeLevels || [];
          setAllSubjects(subs);
          setAllGradeLevels(grades);
          if (subs.length > 0) setSelectedSubjectId((prev) => prev || subs[0].id);
          if (grades.length > 0) setSelectedGradeLevelId((prev) => prev || grades[0].id);

          // Tự động đồng bộ ảnh đại diện và họ tên với AuthContext (Header, Sidebar)
          if (p.avatarUrl && p.avatarUrl !== user?.avatarUrl) {
            updateUser({ avatarUrl: p.avatarUrl });
          }
          if (p.fullName && p.fullName !== user?.fullName) {
            updateUser({ fullName: p.fullName });
          }
        }
      } catch {
        if (active) setError('Không tải được thông tin hồ sơ gia sư.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadProfileData();
    return () => {
      active = false;
    };
  }, [reloadKey]);

  const handleUpdateProfile = async (e) => {
    e.preventDefault();
    setSaving(true);
    setMessage('');
    setError('');

    try {
      await tutorApi.updateProfile({
        displayName,
        citizenIdNumber,
        residenceAddress,
        introduction,
        hourlyFee: profile?.hourlyFee,
      });

      setProfile((prev) => ({
        ...prev,
        fullName: displayName,
        citizenIdNumber,
        residenceAddress,
        introduction,
      }));

      if (displayName) {
        updateUser({ fullName: displayName });
      }
      toast.success('Cập nhật hồ sơ thành công!');
      setMessage('Cập nhật hồ sơ thành công!');
    } catch (err) {
      setError(err.response?.data?.message || 'Có lỗi xảy ra khi cập nhật hồ sơ.');
    } finally {
      setSaving(false);
    }
  };

  const handleAvatarChange = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploadingAvatar(true);
    setMessage('');
    setError('');

    try {
      // Tối ưu nén ảnh avatar phía client trước khi upload
      const optimizedFile = await compressImageFile(file, { maxWidth: 600, maxHeight: 600, quality: 0.85 });
      const res = await tutorApi.uploadAvatar(optimizedFile);
      if (res.data?.avatarUrl) {
        setProfile((prev) => ({ ...prev, avatarUrl: res.data.avatarUrl }));
        updateUser({ avatarUrl: res.data.avatarUrl });
      }
      setMessage(res.data?.message || 'Cập nhật ảnh đại diện thành công!');
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể tải ảnh lên.');
    } finally {
      setUploadingAvatar(false);
      e.target.value = '';
    }
  };

  const handleAddSubject = async (e) => {
    e.preventDefault();
    if (!selectedSubjectId || !selectedGradeLevelId) return;

    setAddingSubject(true);
    setMessage('');
    setError('');

    try {
      const res = await tutorApi.addSubject({
        subjectId: Number(selectedSubjectId),
        gradeLevelId: Number(selectedGradeLevelId),
        hourlyFee: profile?.hourlyFee || 150000,
      });
      setMessage(res.data?.message || 'Đã thêm môn dạy thành công.');
      setReloadKey((k) => k + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể thêm môn dạy.');
    } finally {
      setAddingSubject(false);
    }
  };

  const handleRemoveSubject = async (id) => {
    const ok = await confirm({
      title: 'Xóa môn dạy',
      message: 'Bạn có chắc chắn muốn xóa môn dạy này khỏi danh sách?',
      confirmText: 'Xóa',
      variant: 'danger',
    });
    if (!ok) return;

    try {
      const res = await tutorApi.removeSubject(id);
      toast.success(res.data?.message || 'Đã xóa môn dạy thành công.');
      setTutorSubjects((prev) => prev.filter((item) => item.id !== id));
    } catch (err) {
      toast.error(err.response?.data?.message || 'Không thể xóa môn dạy.');
    }
  };

  const handleUploadQualification = async (e) => {
    e.preventDefault();
    if (!qualTitle.trim() || !qualFile) {
      setError('Vui lòng nhập tên bằng cấp và chọn tệp đính kèm.');
      return;
    }

    setUploadingQual(true);
    setMessage('');
    setError('');

    try {
      // Tối ưu nén ảnh bằng cấp phía client (chuẩn 1920px giữ nguyên từng con số, dấu mộc, chữ ký)
      const optimizedFile = await compressImageFile(qualFile, { maxWidth: 1920, maxHeight: 1920, quality: 0.82 });
      const formData = new FormData();
      formData.append('title', qualTitle.trim());
      formData.append('file', optimizedFile);

      const res = await tutorApi.uploadQualification(formData);
      setMessage(res.data?.message || 'Đã tải lên bằng cấp thành công. Vui lòng chờ quản trị viên xác minh.');
      setQualTitle('');
      setQualFile(null);
      setShowQualUpload(false);
      setReloadKey((k) => k + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể tải lên bằng cấp.');
    } finally {
      setUploadingQual(false);
    }
  };

  const handleRemoveQualification = async (id) => {
    const ok = await confirm({
      title: 'Xóa bằng cấp',
      message: 'Bạn có chắc chắn muốn xóa bằng cấp này khỏi hồ sơ?',
      confirmText: 'Xóa',
      variant: 'danger',
    });
    if (!ok) return;

    try {
      await tutorApi.deleteQualification(id);
      toast.success('Đã xóa bằng cấp thành công.');
      setQualifications((prev) => prev.filter((q) => q.id !== id));
    } catch (err) {
      toast.error(err.response?.data?.message || 'Không thể xóa bằng cấp.');
    }
  };

  const handleOpenQualificationFile = async (q) => {
    if (!q?.fileUrl) return;
    if (/^https?:\/\//i.test(q.fileUrl) || q.fileUrl.startsWith('/uploads/')) {
      window.open(resolveAssetUrl(q.fileUrl), '_blank', 'noopener,noreferrer');
      return;
    }
    const preview = window.open('', '_blank');
    if (!preview) {
      setError('Trình duyệt đang chặn cửa sổ bật lên. Vui lòng cho phép mở tab mới rồi thử lại.');
      return;
    }
    preview.document.title = q.title || 'Chứng chỉ gia sư';
    preview.document.body.innerHTML = '<p style="font:14px sans-serif;padding:20px">Đang tải tệp chứng chỉ…</p>';
    try {
      const res = await tutorApi.getQualificationFile(q.fileUrl);
      const blobUrl = URL.createObjectURL(res.data);
      preview.location.href = blobUrl;
      window.setTimeout(() => URL.revokeObjectURL(blobUrl), 60_000);
    } catch (err) {
      preview.close();
      setError(err.response?.data?.message || 'Không mở được tệp bằng cấp/chứng chỉ.');
    }
  };

  if (loading) {
    return (
      <div className="container profile-wrap text-center py-5">
        <div className="spinner-border text-danger" role="status"></div>
      </div>
    );
  }

  const avatarUrl = resolveAssetUrl(profile?.avatarUrl) || '/assets/img/default-avatar.svg';
  const isApproved = profile?.verificationStatus === 'Approved';
  const isRejected = profile?.verificationStatus === 'Rejected';
  const statusClass = isApproved ? 'status-approved' : isRejected ? 'status-rejected' : 'status-pending';
  const statusIcon = isApproved ? 'bi-patch-check-fill' : isRejected ? 'bi-x-octagon-fill' : 'bi-hourglass-split';
  const statusLabel = isApproved ? 'Đã xác minh' : isRejected ? 'Bị từ chối' : 'Chờ xác nhận';

  return (
    <>
      <style>{`
        .profile-wrap { padding-top: 1.5rem; padding-bottom: 3.5rem; }
        .pro-card {
          background: var(--bs-body-bg);
          border: 1px solid var(--bs-border-color);
          border-radius: 1.25rem;
          box-shadow: 0 10px 30px rgba(0, 0, 0, .06);
          padding: 1.75rem;
        }
        @media (min-width: 992px) {
          .sticky-side { position: sticky; top: 90px; }
        }
        .avatar-wrapper {
          position: relative;
          width: 136px;
          height: 136px;
          margin: 0 auto 1.25rem;
          display: block;
        }
        .avatar-ring {
          width: 100%;
          height: 100%;
          padding: 4px;
          border-radius: 50%;
          background: linear-gradient(135deg, #DB303A, #ff7a85);
          box-shadow: 0 8px 24px rgba(219, 48, 58, .22);
          display: flex;
          align-items: center;
          justify-content: center;
          overflow: hidden;
          transition: transform .25s ease, box-shadow .25s ease;
        }
        .avatar-wrapper:hover .avatar-ring {
          transform: translateY(-2px);
          box-shadow: 0 12px 28px rgba(219, 48, 58, .28);
        }
        .avatar-ring img {
          width: 100%;
          height: 100%;
          aspect-ratio: 1 / 1;
          object-fit: cover;
          object-position: center;
          border-radius: 50%;
          border: 3.5px solid var(--bs-body-bg);
          background-color: var(--bs-tertiary-bg);
          display: block;
          transition: transform .25s ease;
        }
        .avatar-wrapper:hover .avatar-ring img {
          transform: scale(1.05);
        }
        .avatar-upload-btn {
          position: absolute;
          bottom: 2px;
          right: 2px;
          width: 38px;
          height: 38px;
          border-radius: 50%;
          background: #DB303A;
          color: #fff !important;
          display: inline-flex;
          align-items: center;
          justify-content: center;
          cursor: pointer;
          border: 3px solid var(--bs-body-bg);
          box-shadow: 0 4px 12px rgba(0, 0, 0, .18);
          transition: transform .2s ease, background-color .2s ease;
          z-index: 2;
        }
        .avatar-upload-btn:hover {
          transform: scale(1.12);
          background: #B7212B;
          color: #fff !important;
        }
        .avatar-loading-overlay {
          position: absolute;
          inset: 0;
          border-radius: 50%;
          background: rgba(0, 0, 0, .55);
          backdrop-filter: blur(2px);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 3;
        }
        .status-pill {
          display: inline-flex;
          align-items: center;
          gap: .4rem;
          padding: .4rem 1rem;
          border-radius: 999px;
          font-size: .85rem;
          font-weight: 600;
        }
        .status-approved {
          background: #166534;
          color: #ffffff;
        }
        .status-rejected {
          background: rgba(219, 48, 58, .14);
          color: #DB303A;
        }
        .status-pending {
          background: rgba(245, 158, 11, .14);
          color: #d97706;
        }
        .rating-line {
          font-size: .9rem;
          color: var(--bs-secondary-color);
        }
        .section-head {
          display: flex;
          align-items: center;
          justify-content: space-between;
          margin-bottom: 1.25rem;
        }
        .section-title {
          font-size: 1.15rem;
          font-weight: 700;
          margin-bottom: 0;
          display: flex;
          align-items: center;
          gap: .5rem;
        }
        .section-icon {
          color: #DB303A;
          font-size: 1.3rem;
        }
        .subject-chip {
          display: inline-flex;
          align-items: center;
          gap: .4rem;
          background: rgba(219, 48, 58, .09);
          color: #B7212B;
          border: 1px solid rgba(219, 48, 58, .2);
          border-radius: 999px;
          padding: .35rem .75rem;
          font-size: .85rem;
          font-weight: 500;
          margin: 0 .4rem .4rem 0;
        }
        .empty-box {
          text-align: center;
          padding: 2rem 1rem;
          border: 1px dashed var(--bs-border-color);
          border-radius: .9rem;
          color: var(--bs-secondary-color);
          font-size: .9rem;
        }
        .empty-box i {
          display: block;
          font-size: 1.75rem;
          margin-bottom: .5rem;
          color: var(--bs-secondary-color);
        }
        .qual-item {
          display: flex;
          justify-content: space-between;
          align-items: center;
          gap: 1rem;
          padding: 1rem 1.1rem;
          border: 1px solid var(--bs-border-color);
          border-radius: .9rem;
          transition: border-color .2s, box-shadow .2s, transform .2s;
        }
        .qual-item + .qual-item { margin-top: .75rem; }
        .qual-item:hover {
          transform: translateY(-2px);
          border-color: #DB303A;
          box-shadow: 0 10px 24px rgba(0,0,0,.08);
        }
        .qual-icon {
          width: 46px;
          height: 46px;
          border-radius: 12px;
          background: rgba(219, 48, 58, .12);
          color: #DB303A;
          display: inline-flex;
          align-items: center;
          justify-content: center;
          font-size: 1.25rem;
          flex-shrink: 0;
        }
        .btn-brand {
          background-color: #DB303A;
          border-color: #DB303A;
          color: #fff;
        }
        .btn-brand:hover {
          background-color: #B7212B;
          border-color: #B7212B;
          color: #fff;
        }
        .save-bar {
          display: flex;
          justify-content: flex-end;
          margin: .5rem 0 1.5rem;
        }
      `}</style>

      <div className="container profile-wrap">
        {message && (
          <div className="alert alert-success alert-dismissible fade show shadow-sm" role="alert">
            <i className="bi bi-check-circle me-2"></i>{message}
            <button type="button" className="btn-close" onClick={() => setMessage('')} aria-label="Đóng"></button>
          </div>
        )}

        {error && (
          <div className="alert alert-danger alert-dismissible fade show shadow-sm" role="alert">
            <i className="bi bi-exclamation-triangle me-2"></i>{error}
            <button type="button" className="btn-close" onClick={() => setError('')} aria-label="Đóng"></button>
          </div>
        )}

        {!isApproved && (
          <div className={`alert ${isRejected ? 'alert-danger' : 'alert-warning'} border rounded-3 p-3 mb-4 d-flex align-items-center gap-3 shadow-sm`}>
            <i className={`bi ${statusIcon} fs-2 flex-shrink-0`}></i>
            <div>
              <div className="fw-bold fs-6">
                {isRejected ? 'Hồ sơ của bạn đã bị từ chối phê duyệt.' : 'Hồ sơ gia sư của bạn đang chờ Quản trị viên xét duyệt!'}
              </div>
              <div className="small text-muted mt-1">
                {isRejected
                  ? 'Vui lòng kiểm tra lại tính chính xác của Số CCCD, Bằng cấp/Chứng chỉ và cập nhật lại thông tin để gửi yêu cầu xét duyệt lại.'
                  : 'Gia sư mới đăng ký cần được Quản trị viên đối soát CCCD và Bằng cấp/Chứng chỉ hợp lệ trước khi kích hoạt hiển thị công khai trên hệ thống.'}
              </div>
            </div>
          </div>
        )}

        <div className="row g-4">
          {/* Cột trái: Thẻ ảnh đại diện cố định */}
          <aside className="col-lg-4">
            <div className="sticky-side">
              <CollapsibleProfileSection label="Ảnh đại diện và tóm tắt">
                <div className="pro-card text-center">
                <div className="avatar-wrapper">
                  <div className="avatar-ring">
                    <img
                      id="tutorAvatarImg"
                      src={avatarUrl}
                      alt="Ảnh đại diện gia sư"
                      width="120"
                      height="120"
                      onError={(e) => { e.currentTarget.src = '/assets/img/default-avatar.svg'; }}
                    />
                  </div>
                  {uploadingAvatar && (
                    <div className="avatar-loading-overlay">
                      <div className="spinner-border spinner-border-sm text-light" role="status"></div>
                    </div>
                  )}
                  <label htmlFor="avatarFileInput" className="avatar-upload-btn" title="Đổi ảnh đại diện">
                    <i className="bi bi-camera-fill"></i>
                  </label>
                  <input
                    type="file"
                    id="avatarFileInput"
                    accept="image/jpeg,image/png,image/webp"
                    className="d-none"
                    onChange={handleAvatarChange}
                  />
                </div>

                <h2 className="h5 fw-bold mb-2">{profile?.fullName}</h2>

                <span className={`status-pill ${statusClass}`}>
                  <i className={`bi ${statusIcon}`}></i>{statusLabel}
                </span>

                <div className="rating-line mt-3">
                  {Number(profile?.averageRating) > 0 ? (
                    <span>
                      <i className="bi bi-star-fill text-warning me-1"></i>
                      {Number(profile.averageRating).toFixed(1)} / 5
                    </span>
                  ) : (
                    <span>Chưa có đánh giá</span>
                  )}
                </div>
              </div>
              </CollapsibleProfileSection>
            </div>
          </aside>
          <main className="col-lg-8">
            <form onSubmit={handleUpdateProfile}>
              {/* Section 1: Thông tin cá nhân */}
              <CollapsibleProfileSection label="Thông tin cá nhân">
                <section className="pro-card mb-4">
                <div className="section-head">
                  <h2 className="section-title">
                    <span className="section-icon"><i className="bi bi-person-vcard"></i></span>
                    Thông tin cá nhân
                  </h2>
                </div>

                <div className="row g-3">
                  <div className="col-md-6">
                    <label htmlFor="DisplayName" className="form-label">Họ và tên</label>
                    <input
                      id="DisplayName"
                      name="DisplayName"
                      className="form-control"
                      value={displayName}
                      maxLength={150}
                      required
                      onChange={(e) => setDisplayName(e.target.value)}
                    />
                  </div>
                  <div className="col-md-6">
                    <label htmlFor="citizenIdNumber" className="form-label">Số CCCD (12 chữ số)</label>
                    <input
                      id="citizenIdNumber"
                      name="citizenIdNumber"
                      className="form-control"
                      inputMode="numeric"
                      value={citizenIdNumber}
                      maxLength={12}
                      pattern="[0-9]{12}"
                      required
                      onChange={(e) => setCitizenIdNumber(e.target.value)}
                    />
                  </div>
                  <div className="col-12">
                    <label htmlFor="residenceAddress" className="form-label">Nơi ở hiện tại (thông tin riêng tư)</label>
                    <input
                      id="residenceAddress"
                      name="residenceAddress"
                      className="form-control"
                      value={residenceAddress}
                      maxLength={500}
                      required
                      onChange={(e) => setResidenceAddress(e.target.value)}
                    />
                  </div>
                  <div className="col-md-6">
                    <label htmlFor="Email" className="form-label">Email</label>
                    <input id="Email" className="form-control" value={profile?.email || ''} readOnly />
                  </div>
                  <div className="col-md-6">
                    <label htmlFor="PhoneNumber" className="form-label">Số điện thoại</label>
                    <input id="PhoneNumber" className="form-control" value={profile?.phoneNumber || ''} readOnly />
                  </div>
                  <div className="col-md-6">
                    <label htmlFor="DateOfBirth" className="form-label">Ngày sinh</label>
                    <input
                      id="DateOfBirth"
                      type="date"
                      className="form-control"
                      value={profile?.dateOfBirth ? profile.dateOfBirth.substring(0, 10) : ''}
                      readOnly
                    />
                  </div>
                  <div className="col-12">
                    <label htmlFor="Address" className="form-label">Địa chỉ</label>
                    <input id="Address" className="form-control" value={profile?.address || ''} readOnly />
                  </div>
                </div>
                </section>
              </CollapsibleProfileSection>

              {/* Section 2: Giới thiệu bản thân */}
              <CollapsibleProfileSection label="Giới thiệu bản thân">
                <section className="pro-card mb-4">
                <div className="section-head">
                  <h2 className="section-title">
                    <span className="section-icon"><i className="bi bi-chat-quote"></i></span>
                    Giới thiệu bản thân
                  </h2>
                </div>

                <textarea
                  id="Introduction"
                  name="Introduction"
                  className="form-control"
                  rows={5}
                  placeholder="Giới thiệu kinh nghiệm, phương pháp giảng dạy và môn học của bạn..."
                  value={introduction}
                  onChange={(e) => setIntroduction(e.target.value)}
                ></textarea>
                </section>
              </CollapsibleProfileSection>

              <div className="save-bar">
                <button type="submit" className="btn btn-brand" disabled={saving}>
                  <i className="bi bi-check2-circle me-2"></i>
                  {saving ? 'Đang lưu...' : 'Lưu hồ sơ'}
                </button>
              </div>
            </form>

            {/* Section 3: Môn dạy và học phí */}
            <CollapsibleProfileSection label="Môn dạy và học phí">
              <section className="pro-card mb-4">
              <div className="section-head">
                <h2 className="section-title">
                  <span className="section-icon"><i className="bi bi-cash-stack"></i></span>
                  Môn dạy và học phí
                </h2>
              </div>

              <div id="subjectList" className="mb-3">
                {tutorSubjects.map((item) => (
                  <span key={item.id} className="subject-chip">
                    <i className="bi bi-bookmark-fill"></i>
                    {item.subjectName} · {item.gradeLevelName || item.gradeName}
                    <button
                      type="button"
                      className="btn-close ms-1"
                      aria-label="Xóa"
                      onClick={() => handleRemoveSubject(item.id)}
                    ></button>
                  </span>
                ))}
              </div>

              {tutorSubjects.length === 0 && (
                <div id="emptySubjects" className="empty-box mb-3">
                  <i className="bi bi-journal-plus"></i>
                  <span>Bạn chưa thêm môn và cấp học giảng dạy.</span>
                </div>
              )}

              <form onSubmit={handleAddSubject} className="row g-2 align-items-end mb-4">
                <div className="col-md-5">
                  <label className="form-label">Môn học</label>
                  <select
                    name="subjectId"
                    className="form-select"
                    value={selectedSubjectId}
                    onChange={(e) => setSelectedSubjectId(e.target.value)}
                    required
                  >
                    {allSubjects.map((s) => (
                      <option key={s.id} value={s.id}>{s.name}</option>
                    ))}
                  </select>
                </div>
                <div className="col-md-4">
                  <label className="form-label">Cấp học</label>
                  <select
                    name="gradeLevelId"
                    className="form-select"
                    value={selectedGradeLevelId}
                    onChange={(e) => setSelectedGradeLevelId(e.target.value)}
                    required
                  >
                    {allGradeLevels.map((g) => (
                      <option key={g.id} value={g.id}>{g.name}</option>
                    ))}
                  </select>
                </div>
                <div className="col-md-3 d-grid">
                  <button type="submit" className="btn btn-brand" disabled={addingSubject}>
                    {addingSubject ? 'Đang thêm...' : 'Thêm'}
                  </button>
                </div>
              </form>

              <label htmlFor="HourlyFee" className="form-label">Học phí mỗi giờ</label>
              <div className="input-group">
                <input
                  id="HourlyFee"
                  className="form-control"
                  value={new Intl.NumberFormat('vi-VN').format(profile?.hourlyFee || 0)}
                  readOnly
                />
                <span className="input-group-text">VNĐ/giờ</span>
              </div>
              </section>
            </CollapsibleProfileSection>

            {/* Section 4: Bằng cấp / Chứng chỉ */}
            <CollapsibleProfileSection label="Bằng cấp và chứng chỉ">
              <section className="pro-card mb-4">
              <div className="section-head">
                <h2 className="section-title">
                  <span className="section-icon"><i className="bi bi-patch-check"></i></span>
                  Bằng cấp / Chứng chỉ
                </h2>
                <button
                  className="btn btn-brand btn-sm px-3"
                  type="button"
                  onClick={() => setShowQualUpload(!showQualUpload)}
                >
                  <i className="bi bi-cloud-arrow-up me-1"></i>Tải bằng / chứng chỉ
                </button>
              </div>

              {showQualUpload && (
                <div className="mb-4">
                  <form onSubmit={handleUploadQualification} className="p-3 border rounded-3 bg-light">
                    <div className="row g-3 align-items-end">
                      <div className="col-md-5">
                        <label htmlFor="qualificationTitle" className="form-label">Tên bằng cấp / chứng chỉ</label>
                        <input
                          id="qualificationTitle"
                          name="title"
                          className="form-control"
                          maxLength={200}
                          required
                          placeholder="Ví dụ: Chứng chỉ IELTS"
                          value={qualTitle}
                          onChange={(e) => setQualTitle(e.target.value)}
                        />
                      </div>
                      <div className="col-md-5">
                        <label htmlFor="qualificationFile" className="form-label">Tệp (PDF, JPG, PNG; tối đa 10 MB)</label>
                        <input
                          id="qualificationFile"
                          name="file"
                          type="file"
                          className="form-control"
                          accept=".pdf,.jpg,.jpeg,.png"
                          required
                          onChange={(e) => setQualFile(e.target.files?.[0] || null)}
                        />
                      </div>
                      <div className="col-md-2 d-grid">
                        <button type="submit" className="btn btn-brand" disabled={uploadingQual}>
                          {uploadingQual ? 'Đang tải...' : 'Tải lên'}
                        </button>
                      </div>
                    </div>
                  </form>
                </div>
              )}

              <div id="qualList">
                {qualifications.map((q) => {
                  const statusText = q.status === 'Approved' ? 'Đã duyệt' : q.status === 'Rejected' ? 'Bị từ chối' : 'Chờ xác minh';
                  const canRemove = q.status === 'Pending';
                  return (
                    <div key={q.id} className="qual-item">
                      <div className="d-flex align-items-center gap-3">
                        <span className="qual-icon"><i className="bi bi-award"></i></span>
                        <div>
                          <div className="fw-semibold">{q.title}</div>
                          <small className="text-secondary">Trạng thái: {statusText}</small>
                        </div>
                      </div>

                      <div className="d-flex align-items-center gap-2">
                        {q.fileUrl && (
                          <button
                            type="button"
                            className="btn btn-outline-secondary btn-sm px-3"
                            onClick={() => handleOpenQualificationFile(q)}
                          >
                            Xem tệp
                          </button>
                        )}

                        {canRemove && (
                          <button
                            type="button"
                            className="btn btn-outline-danger btn-sm px-3"
                            onClick={() => handleRemoveQualification(q.id)}
                          >
                            Xóa
                          </button>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>

              {qualifications.length === 0 && (
                <div id="emptyQuals" className="empty-box">
                  <i className="bi bi-file-earmark-arrow-up"></i>
                  <span>Bạn chưa tải lên bằng cấp hoặc chứng chỉ. Thêm bằng cấp giúp hồ sơ đáng tin hơn.</span>
                </div>
              )}
              </section>
            </CollapsibleProfileSection>

            <ChangePasswordPanel />
          </main>
        </div>
      </div>
    </>
  );
}
