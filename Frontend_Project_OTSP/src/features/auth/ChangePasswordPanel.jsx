import { useId, useState } from 'react';
import authApi from '../../api/authApi';
import CollapsibleProfileSection from '../../components/common/CollapsibleProfileSection';
import '../../styles/change-password.css';

const initialPasswords = {
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
};

export default function ChangePasswordPanel() {
  const id = useId();
  const [passwords, setPasswords] = useState(initialPasswords);
  const [visiblePasswords, setVisiblePasswords] = useState({ newPassword: false, confirmPassword: false });
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  const updatePassword = (field) => (event) => {
    setPasswords((current) => ({ ...current, [field]: event.target.value }));
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setMessage('');
    setError('');

    if (passwords.newPassword !== passwords.confirmPassword) {
      setError('Mật khẩu mới và xác nhận mật khẩu không khớp.');
      return;
    }

    setSaving(true);
    try {
      const response = await authApi.changePassword(passwords);
      setMessage(response.data?.message || 'Đổi mật khẩu thành công.');
      setPasswords(initialPasswords);
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Không thể đổi mật khẩu. Vui lòng kiểm tra mật khẩu hiện tại.');
    } finally {
      setSaving(false);
    }
  };

  const fields = [
    { key: 'currentPassword', label: 'Mật khẩu hiện tại', icon: 'bi-key' },
    { key: 'newPassword', label: 'Mật khẩu mới', icon: 'bi-shield-lock' },
    { key: 'confirmPassword', label: 'Xác nhận mật khẩu mới', icon: 'bi-shield-check' },
  ];

  return (
    <CollapsibleProfileSection label="Đổi mật khẩu">
      <section className="password-panel" aria-labelledby={`${id}-title`}>
      <div className="password-panel__header">
        <div className="password-panel__icon" aria-hidden="true">
          <i className="bi bi-shield-lock-fill" />
        </div>
        <div>
          <div className="password-panel__eyebrow">BẢO MẬT TÀI KHOẢN</div>
          <h2 className="password-panel__title" id={`${id}-title`}>Đổi mật khẩu</h2>
          <p className="password-panel__description mb-0">
            Xác minh mật khẩu hiện tại để bảo vệ tài khoản của bạn.
          </p>
        </div>
        <span className="password-panel__badge">
          <i className="bi bi-lock-fill me-1" aria-hidden="true" />Riêng tư
        </span>
      </div>

      <form className="password-panel__form" onSubmit={handleSubmit}>
        {message && <div className="alert alert-success py-2" role="status">{message}</div>}
        {error && <div className="alert alert-danger py-2" role="alert">{error}</div>}

        <div className="row g-3">
          {fields.map((field) => (
            <div className="col-12" key={field.key}>
              <label className="form-label password-panel__label" htmlFor={`${id}-${field.key}`}>
                {field.label}
              </label>
              <div className="input-group password-panel__input-group">
                <span className="input-group-text" aria-hidden="true">
                  <i className={`bi ${field.icon}`} />
                </span>
                <input
                  id={`${id}-${field.key}`}
                  className="form-control"
                  type={visiblePasswords[field.key] ? 'text' : 'password'}
                  value={passwords[field.key]}
                  onChange={updatePassword(field.key)}
                  autoComplete={field.key === 'currentPassword' ? 'current-password' : 'new-password'}
                  required
                />
                {(field.key === 'newPassword' || field.key === 'confirmPassword') && (
                  <button
                    className="btn btn-outline-secondary password-panel__visibility"
                    type="button"
                    onClick={() => setVisiblePasswords((current) => ({
                      ...current,
                      [field.key]: !current[field.key],
                    }))}
                    aria-label={visiblePasswords[field.key] ? `Ẩn ${field.label.toLowerCase()}` : `Hiện ${field.label.toLowerCase()}`}
                    title={visiblePasswords[field.key] ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                  >
                    <i className={`bi ${visiblePasswords[field.key] ? 'bi-eye-slash' : 'bi-eye'}`} aria-hidden="true" />
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>

        <div className="password-panel__footer">
          <p className="password-panel__hint mb-0">
            <i className="bi bi-info-circle me-1" aria-hidden="true" />
            Mật khẩu mới cần đáp ứng chính sách bảo mật của hệ thống.
          </p>
          <button className="btn password-panel__submit" type="submit" disabled={saving}>
            {saving ? (
              <><span className="spinner-border spinner-border-sm me-2" role="status" />Đang cập nhật…</>
            ) : (
              <><i className="bi bi-check2-circle me-2" aria-hidden="true" />Cập nhật mật khẩu</>
            )}
          </button>
        </div>
      </form>
      </section>
    </CollapsibleProfileSection>
  );
}
