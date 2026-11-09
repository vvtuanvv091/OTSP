import { useEffect, useRef, useState } from 'react';
import adminApi from '../../api/adminApi';
import { resolveAssetUrl } from '../../api/config';
import { useAuth } from '../../context/AuthContext';
import CollapsibleProfileSection from '../../components/common/CollapsibleProfileSection';
import ChangePasswordPanel from '../auth/ChangePasswordPanel';
import compressImageFile from '../../utils/imageCompressor';

export default function AdminProfilePage() {
  const { updateUser } = useAuth();
  const fileInputRef = useRef(null);
  const [profile, setProfile] = useState({ fullName: '', email: '', phoneNumber: '', avatarUrl: '' });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  useEffect(() => {
    let active = true;
    adminApi.getProfile()
      .then((response) => {
        if (active) setProfile((current) => ({ ...current, ...(response.data || {}) }));
      })
      .catch((requestError) => {
        if (active) setError(requestError?.response?.data?.message || 'Không tải được hồ sơ quản trị viên.');
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  const handleSave = async (event) => {
    event.preventDefault();
    setError('');
    setMessage('');
    setSaving(true);
    try {
      const response = await adminApi.updateProfile({ fullName: profile.fullName, phoneNumber: profile.phoneNumber });
      const updated = response.data?.profile || {};
      setProfile((current) => ({ ...current, ...updated }));
      updateUser(updated);
      setMessage(response.data?.message || 'Đã cập nhật hồ sơ.');
    } catch (requestError) {
      setError(requestError?.response?.data?.message || 'Không thể cập nhật hồ sơ.');
    } finally {
      setSaving(false);
    }
  };

  const handleAvatarChange = async (event) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      setError('Vui lòng chọn tệp ảnh.');
      return;
    }
    setError('');
    setMessage('');
    setUploading(true);
    try {
      const optimized = await compressImageFile(file, { maxWidth: 600, maxHeight: 600, quality: 0.85 });
      const response = await adminApi.uploadAvatar(optimized);
      const avatarUrl = response.data?.avatarUrl;
      if (avatarUrl) {
        setProfile((current) => ({ ...current, avatarUrl }));
        updateUser({ avatarUrl });
      }
      setMessage(response.data?.message || 'Đã cập nhật ảnh đại diện.');
    } catch (requestError) {
      setError(requestError?.response?.data?.message || 'Không thể tải ảnh đại diện lên.');
    } finally {
      setUploading(false);
    }
  };

  if (loading) return <div className="container-fluid py-4"><div className="text-secondary"><span className="spinner-border spinner-border-sm me-2" />Đang tải hồ sơ…</div></div>;

  return (
    <div className="container-fluid py-4 admin-profile-page">
      <div className="mb-4">
        <div className="small text-secondary mb-1">Trang của tôi › Quản trị hệ thống</div>
        <h1 className="h3 fw-bold mb-1">Hồ sơ quản trị viên</h1>
        <p className="text-secondary mb-0">Cập nhật thông tin hiển thị và ảnh đại diện tài khoản admin.</p>
      </div>

      {error && <div className="alert alert-danger" role="alert">{error}</div>}
      {message && <div className="alert alert-success" role="status">{message}</div>}

      <CollapsibleProfileSection label="Thông tin hồ sơ">
        <form className="card border-0 shadow-sm" onSubmit={handleSave}>
        <div className="card-body p-4">
          <div className="d-flex flex-wrap align-items-center gap-4 pb-4 mb-4 border-bottom">
            <div className="admin-profile-avatar-wrap">
              <img src={resolveAssetUrl(profile.avatarUrl) || '/assets/img/default-avatar.svg'} alt="Ảnh đại diện quản trị viên" onError={(event) => { event.currentTarget.src = '/assets/img/default-avatar.svg'; }} />
              <button type="button" className="admin-profile-avatar-edit" onClick={() => fileInputRef.current?.click()} disabled={uploading} aria-label="Đổi ảnh đại diện">
                <i className={`bi ${uploading ? 'bi-arrow-repeat' : 'bi-camera-fill'}`} aria-hidden="true" />
              </button>
              <input ref={fileInputRef} className="visually-hidden" type="file" accept="image/*" onChange={handleAvatarChange} />
            </div>
            <div>
              <h2 className="h5 fw-bold mb-1">{profile.fullName || 'Quản trị viên OTSP'}</h2>
              <div className="text-secondary">{profile.email}</div>
              <button type="button" className="btn btn-outline-danger btn-sm mt-2" onClick={() => fileInputRef.current?.click()} disabled={uploading}>
                {uploading ? 'Đang tải ảnh…' : 'Thay ảnh đại diện'}
              </button>
              <div className="small text-secondary mt-1">Ảnh JPG, PNG hoặc WEBP; tối đa 5 MB.</div>
            </div>
          </div>

          <div className="row g-3">
            <div className="col-md-6">
              <label className="form-label fw-semibold" htmlFor="admin-profile-name">Họ và tên</label>
              <input id="admin-profile-name" className="form-control" value={profile.fullName || ''} maxLength={150} required onChange={(event) => setProfile((current) => ({ ...current, fullName: event.target.value }))} />
            </div>
            <div className="col-md-6">
              <label className="form-label fw-semibold" htmlFor="admin-profile-email">Email đăng nhập</label>
              <input id="admin-profile-email" className="form-control" value={profile.email || ''} readOnly disabled />
            </div>
            <div className="col-md-6">
              <label className="form-label fw-semibold" htmlFor="admin-profile-phone">Số điện thoại</label>
              <input id="admin-profile-phone" className="form-control" type="tel" value={profile.phoneNumber || ''} maxLength={30} onChange={(event) => setProfile((current) => ({ ...current, phoneNumber: event.target.value }))} />
            </div>
          </div>
        </div>
        <div className="card-footer bg-transparent d-flex justify-content-end p-3">
          <button type="submit" className="btn btn-danger" disabled={saving || uploading}>
            {saving ? <><span className="spinner-border spinner-border-sm me-2" />Đang lưu…</> : <><i className="bi bi-check2 me-1" />Lưu thay đổi</>}
          </button>
        </div>
        </form>
      </CollapsibleProfileSection>

      <ChangePasswordPanel />

      <style>{`
        .admin-profile-avatar-wrap { position: relative; width: 112px; height: 112px; flex: 0 0 112px; }
        .admin-profile-avatar-wrap > img { width: 100%; height: 100%; border: 3px solid #f4c7cb; border-radius: 50%; object-fit: cover; background: #fff; }
        .admin-profile-avatar-edit { position: absolute; right: 0; bottom: 0; display: grid; width: 36px; height: 36px; place-items: center; border: 2px solid #fff; border-radius: 50%; color: #fff; background: #c92536; }
        .admin-profile-avatar-edit:disabled { opacity: .7; }
      `}</style>
    </div>
  );
}
