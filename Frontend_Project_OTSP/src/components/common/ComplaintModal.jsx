import { useState, useEffect } from 'react';
import complaintApi from '../../api/complaintApi';

export default function ComplaintModal({
  isOpen,
  onClose,
  onSuccess,
  defaultTargetUserId = '',
  defaultTargetName = '',
  contextInfo = '',
}) {
  const [content, setContent] = useState('');
  const [targetUserId, setTargetUserId] = useState(defaultTargetUserId);
  const [targets, setTargets] = useState([]);
  const [file, setFile] = useState(null);
  const [filePreview, setFilePreview] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const [prevOpen, setPrevOpen] = useState(false);
  if (isOpen && !prevOpen) {
    setPrevOpen(true);
    setContent('');
    setFile(null);
    setFilePreview(null);
    setError('');
    setTargetUserId(defaultTargetUserId || '');
  } else if (!isOpen && prevOpen) {
    setPrevOpen(false);
  }

  useEffect(() => {
    if (!isOpen) return undefined;
    let active = true;
    // Tải danh sách các đối tượng gợi ý liên quan
    complaintApi.getTargets()
      .then((res) => {
        if (active) setTargets(res.data || []);
      })
      .catch(() => {});
    return () => {
      active = false;
    };
  }, [isOpen]);

  useEffect(() => {
    return () => {
      if (filePreview) URL.revokeObjectURL(filePreview);
    };
  }, [filePreview]);

  const handleFileChange = (e) => {
    const selected = e.target.files?.[0];
    if (selected) {
      if (selected.size > 5 * 1024 * 1024) {
        setError('Kích thước ảnh minh chứng tối đa là 5MB.');
        return;
      }
      setFile(selected);
      setFilePreview(URL.createObjectURL(selected));
      setError('');
    }
  };

  const handleRemoveFile = () => {
    setFile(null);
    if (filePreview) URL.revokeObjectURL(filePreview);
    setFilePreview(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!content.trim() || content.trim().length < 5) {
      setError('Vui lòng nhập nội dung khiếu nại (tối thiểu 5 ký tự).');
      return;
    }

    setLoading(true);
    setError('');

    try {
      const formData = new FormData();
      formData.append('content', content.trim());
      if (targetUserId) {
        formData.append('targetUserId', targetUserId);
      }
      if (file) {
        formData.append('file', file);
      }

      await complaintApi.createComplaint(formData);
      if (onSuccess) onSuccess();
      onClose();
    } catch (err) {
      setError(err.response?.data?.message || 'Có lỗi xảy ra khi gửi khiếu nại. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div
      className="modal fade show d-block"
      tabIndex="-1"
      style={{ backgroundColor: 'rgba(0,0,0,0.5)', zIndex: 1060 }}
    >
      <div className="modal-dialog modal-dialog-centered">
        <div className="modal-content border-0 shadow-lg rounded-3">
          <div className="modal-header border-bottom py-3">
            <h5 className="modal-title fw-bold text-dark d-flex align-items-center gap-2">
              <i className="bi bi-flag-fill text-danger"></i>
              Gửi khiếu nại &amp; Phản ánh
            </h5>
            <button
              type="button"
              className="btn-close"
              onClick={onClose}
              disabled={loading}
              aria-label="Đóng"
            ></button>
          </div>

          <form onSubmit={handleSubmit}>
            <div className="modal-body p-4">
              {error && (
                <div className="alert alert-danger py-2 px-3 small d-flex align-items-center gap-2 mb-3">
                  <i className="bi bi-exclamation-triangle-fill flex-shrink-0"></i>
                  <span>{error}</span>
                </div>
              )}

              {contextInfo && (
                <div className="alert alert-light border py-2 px-3 small mb-3 text-secondary">
                  <i className="bi bi-info-circle me-1"></i>
                  Liên quan: <strong>{contextInfo}</strong>
                </div>
              )}

              {/* Đối tượng khiếu nại */}
              <div className="mb-3">
                <label className="form-label small fw-semibold text-dark">
                  Đối tượng phản ánh / khiếu nại
                </label>
                {defaultTargetName ? (
                  <div className="form-control bg-light text-dark fw-semibold small">
                    {defaultTargetName}
                  </div>
                ) : (
                  <select
                    className="form-select form-select-sm"
                    value={targetUserId}
                    onChange={(e) => setTargetUserId(e.target.value)}
                  >
                    <option value="">-- Phản ánh chung về hệ thống / khác --</option>
                    {targets.map((t) => (
                      <option key={t.userId || t.UserId} value={t.userId || t.UserId}>
                        {t.fullName || t.FullName} ({t.email || t.Email})
                      </option>
                    ))}
                  </select>
                )}
                <div className="form-text small text-muted">
                  Bạn có thể chọn người dùng liên quan hoặc gửi phản ánh chung tới Ban quản trị.
                </div>
              </div>

              {/* Nội dung khiếu nại */}
              <div className="mb-3">
                <label className="form-label small fw-semibold text-dark">
                  Nội dung chi tiết <span className="text-danger">*</span>
                </label>
                <textarea
                  className="form-control"
                  rows="4"
                  placeholder="Mô tả cụ thể sự việc, ngày giờ, buổi học hoặc lý do bạn gửi khiếu nại..."
                  value={content}
                  onChange={(e) => setContent(e.target.value)}
                  maxLength={2000}
                  required
                ></textarea>
                <div className="d-flex justify-content-between form-text small text-muted mt-1">
                  <span>Tối thiểu 5 ký tự</span>
                  <span>{content.length}/2000</span>
                </div>
              </div>

              {/* Minh chứng (ảnh chụp màn hình) */}
              <div className="mb-2">
                <label className="form-label small fw-semibold text-dark d-flex align-items-center justify-content-between">
                  <span>Ảnh minh chứng (tùy chọn)</span>
                  <span className="text-muted small fw-normal">PNG, JPG, tối đa 5MB</span>
                </label>

                {filePreview ? (
                  <div className="position-relative d-inline-block border rounded-3 p-1 bg-light">
                    <img
                      src={filePreview}
                      alt="Minh chứng"
                      className="rounded"
                      style={{ maxWidth: '100%', maxHeight: '140px', objectFit: 'contain' }}
                    />
                    <button
                      type="button"
                      className="btn btn-sm btn-danger position-absolute top-0 end-0 m-1 rounded-circle p-1 d-flex align-items-center justify-content-center"
                      style={{ width: '24px', height: '24px' }}
                      onClick={handleRemoveFile}
                      title="Xóa ảnh"
                    >
                      <i className="bi bi-x"></i>
                    </button>
                  </div>
                ) : (
                  <input
                    type="file"
                    className="form-control form-control-sm"
                    accept="image/*"
                    onChange={handleFileChange}
                  />
                )}
              </div>
            </div>

            <div className="modal-footer border-top py-2 px-4 bg-light rounded-bottom-3 d-flex justify-content-between">
              <button
                type="button"
                className="btn btn-sm btn-outline-secondary px-3"
                onClick={onClose}
                disabled={loading}
              >
                Hủy bỏ
              </button>
              <button
                type="submit"
                className="btn btn-sm btn-danger px-4 fw-semibold"
                disabled={loading}
              >
                {loading ? (
                  <>
                    <span className="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
                    Đang gửi...
                  </>
                ) : (
                  <>
                    <i className="bi bi-send me-1"></i>
                    Gửi khiếu nại
                  </>
                )}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}

