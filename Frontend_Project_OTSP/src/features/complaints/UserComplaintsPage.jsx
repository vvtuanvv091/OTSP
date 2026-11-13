import { useState, useEffect, useRef } from 'react';
import { resolveAssetUrl } from '../../api/config';
import complaintApi from '../../api/complaintApi';
import { useSignalREvent } from '../../context/signalRHooks';
import ComplaintModal from '../../components/common/ComplaintModal';

export default function UserComplaintsPage() {
  const [complaints, setComplaints] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filterStatus, setFilterStatus] = useState('all');
  const [modalOpen, setModalOpen] = useState(false);
  const [selectedImage, setSelectedImage] = useState(null);
  const [alertMsg, setAlertMsg] = useState('');
  const [errorMsg, setErrorMsg] = useState('');
  const [reloadKey, setReloadKey] = useState(0);

  // Tự động làm mới danh sách khiếu nại khi Admin phân xử theo thời gian thực (Zero-Reload)
  useSignalREvent('ComplaintStatusChanged', () => setReloadKey((k) => k + 1));

  const alertTimerRef = useRef(null);

  useEffect(() => {
    let active = true;
    async function loadComplaints() {
      setLoading(true);
      setErrorMsg('');
      try {
        const res = await complaintApi.getMyAppComplaints();
        if (active) setComplaints(res.data || []);
      } catch (err) {
        if (active) setErrorMsg(err.response?.data?.message || 'Không thể tải danh sách khiếu nại.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadComplaints();
    return () => { active = false; };
  }, [reloadKey]);

  useEffect(() => () => {
    if (alertTimerRef.current) clearTimeout(alertTimerRef.current);
  }, []);

  const handleModalSuccess = () => {
    setAlertMsg('Khiếu nại của bạn đã được gửi thành công tới Ban Quản Trị!');
    setReloadKey((k) => k + 1);
    if (alertTimerRef.current) clearTimeout(alertTimerRef.current);
    alertTimerRef.current = setTimeout(() => setAlertMsg(''), 6000);
  };

  const filtered = complaints.filter((c) => {
    if (filterStatus === 'all') return true;
    return c.status === filterStatus;
  });

  const getStatusBadge = (status) => {
    switch (status) {
      case 'New':
        return <span className="badge bg-warning text-dark border"><i className="bi bi-clock me-1"></i>Mới gửi</span>;
      case 'Processing':
        return <span className="badge bg-info text-dark border"><i className="bi bi-gear me-1"></i>Đang xử lý</span>;
      case 'Resolved':
        return <span className="badge bg-success text-white"><i className="bi bi-check-circle me-1"></i>Đã giải quyết</span>;
      case 'Rejected':
        return <span className="badge bg-secondary text-white"><i className="bi bi-x-circle me-1"></i>Từ chối</span>;
      default:
        return <span className="badge bg-light text-dark">{status}</span>;
    }
  };

  return (
    <div className="container-fluid py-4 px-3 px-md-4">
      {/* Tiêu đề trang & Nút tạo khiếu nại */}
      <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1 text-dark d-flex align-items-center gap-2">
            <i className="bi bi-flag text-danger"></i>
            Khiếu nại & Hỗ trợ
          </h1>
          <p className="text-secondary mb-0">
            Theo dõi tiến độ và phản hồi từ Ban Quản trị đối với các yêu cầu khiếu nại của bạn.
          </p>
        </div>
        <button
          type="button"
          className="btn btn-danger d-flex align-items-center gap-2 px-3 py-2 shadow-sm"
          onClick={() => setModalOpen(true)}
        >
          <i className="bi bi-plus-lg"></i>
          <span>Gửi khiếu nại mới</span>
        </button>
      </div>

      {alertMsg && (
        <div className="alert alert-success alert-dismissible fade show rounded-3 shadow-sm mb-4" role="alert">
          <i className="bi bi-check-circle-fill me-2"></i>
          {alertMsg}
          <button type="button" className="btn-close" onClick={() => setAlertMsg('')} aria-label="Đóng"></button>
        </div>
      )}

      {errorMsg && (
        <div className="alert alert-danger alert-dismissible fade show rounded-3 shadow-sm mb-4" role="alert">
          <i className="bi bi-exclamation-triangle-fill me-2"></i>
          {errorMsg}
          <button type="button" className="btn-close" onClick={() => setErrorMsg('')} aria-label="Đóng"></button>
        </div>
      )}

      {/* Tab lọc trạng thái khiếu nại */}
      <div className="card border-0 shadow-sm rounded-3 mb-4">
        <div className="card-body py-2 px-3">
          <div className="d-flex flex-wrap align-items-center gap-2">
            <span className="small text-secondary fw-semibold me-2">
              <i className="bi bi-funnel me-1"></i>Lọc trạng thái:
            </span>
            <button
              type="button"
              className={`btn btn-sm ${filterStatus === 'all' ? 'btn-danger' : 'btn-light border'} rounded-pill px-3`}
              onClick={() => setFilterStatus('all')}
            >
              Tất cả ({complaints.length})
            </button>
            <button
              type="button"
              className={`btn btn-sm ${filterStatus === 'New' ? 'btn-warning text-dark' : 'btn-light border'} rounded-pill px-3`}
              onClick={() => setFilterStatus('New')}
            >
              Mới gửi ({complaints.filter((c) => c.status === 'New').length})
            </button>
            <button
              type="button"
              className={`btn btn-sm ${filterStatus === 'Processing' ? 'btn-info text-dark' : 'btn-light border'} rounded-pill px-3`}
              onClick={() => setFilterStatus('Processing')}
            >
              Đang xử lý ({complaints.filter((c) => c.status === 'Processing').length})
            </button>
            <button
              type="button"
              className={`btn btn-sm ${filterStatus === 'Resolved' ? 'btn-success text-white' : 'btn-light border'} rounded-pill px-3`}
              onClick={() => setFilterStatus('Resolved')}
            >
              Đã giải quyết ({complaints.filter((c) => c.status === 'Resolved').length})
            </button>
            <button
              type="button"
              className={`btn btn-sm ${filterStatus === 'Rejected' ? 'btn-secondary text-white' : 'btn-light border'} rounded-pill px-3`}
              onClick={() => setFilterStatus('Rejected')}
            >
              Từ chối ({complaints.filter((c) => c.status === 'Rejected').length})
            </button>
          </div>
        </div>
      </div>

      {/* Danh sách khiếu nại phản ánh */}
      {loading ? (
        <div className="card border-0 shadow-sm rounded-4 p-5 text-center">
          <div className="spinner-border text-danger mx-auto" role="status"></div>
          <span className="small text-muted mt-2">Đang tải danh sách khiếu nại...</span>
        </div>
      ) : filtered.length === 0 ? (
        <div className="card border-0 shadow-sm rounded-4 p-5 text-center text-secondary">
          <i className="bi bi-shield-check fs-1 text-muted mb-2"></i>
          <h3 className="h5 fw-bold text-dark">Không có khiếu nại nào</h3>
          <p className="small text-muted mb-3">
            {filterStatus === 'all'
              ? 'Bạn chưa gửi khiếu nại hoặc phản ánh nào.'
              : 'Không có khiếu nại nào ở trạng thái này.'}
          </p>
          <div>
            <button
              type="button"
              className="btn btn-sm btn-outline-danger px-3"
              onClick={() => setModalOpen(true)}
            >
              <i className="bi bi-plus-lg me-1"></i>Tạo khiếu nại mới
            </button>
          </div>
        </div>
      ) : (
        <div className="d-flex flex-column gap-3">
          {filtered.map((item) => (
            <div key={item.id} className="card border-0 shadow-sm rounded-3 p-3 p-md-4">
              <div className="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-2 pb-2 border-bottom">
                <div className="d-flex align-items-center gap-2">
                  <span className="fw-bold text-dark">Khiếu nại #{item.id}</span>
                  {getStatusBadge(item.status)}
                </div>
                <span className="small text-muted">
                  <i className="bi bi-clock me-1"></i>
                  {new Date(item.createdAt).toLocaleString('vi-VN')}
                </span>
              </div>

              {item.targetUserName && (
                <div className="small text-secondary mb-2">
                  <span className="fw-semibold text-dark">Đối tượng liên quan:</span>{' '}
                  <span className="badge bg-light text-dark border">
                    <i className="bi bi-person me-1"></i>
                    {item.targetUserName}
                  </span>
                </div>
              )}

              <div className="text-dark mb-3" style={{ whiteSpace: 'pre-wrap', lineHeight: 1.6 }}>
                {item.content}
              </div>

              {item.evidenceUrl && (
                <div className="mb-3">
                  <span className="small text-muted d-block mb-1">
                    <i className="bi bi-image me-1"></i>Minh chứng đính kèm:
                  </span>
                  <img
                    src={resolveAssetUrl(item.evidenceUrl)}
                    alt="Minh chứng"
                    className="rounded border shadow-sm cursor-pointer"
                    style={{ maxHeight: '120px', maxWidth: '200px', objectFit: 'cover', cursor: 'pointer' }}
                    onClick={() => setSelectedImage(resolveAssetUrl(item.evidenceUrl))}
                  />
                </div>
              )}

              {/* Hộp thông tin phản hồi giải quyết từ Quản trị viên */}
              {item.resolution && (
                <div className="alert alert-success border-0 bg-success-subtle p-3 rounded-3 mt-2 mb-0">
                  <div className="fw-bold text-success-emphasis d-flex align-items-center gap-1 mb-1">
                    <i className="bi bi-shield-check"></i>
                    Phản hồi từ Ban Quản Trị {item.adminName ? `(${item.adminName})` : ''}:
                  </div>
                  <div className="text-dark small" style={{ whiteSpace: 'pre-wrap' }}>
                    {item.resolution}
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {/* Modal tạo khiếu nại mới */}
      <ComplaintModal
        isOpen={modalOpen}
        onClose={() => setModalOpen(false)}
        onSuccess={handleModalSuccess}
      />

      {/* Modal xem trước ảnh bằng chứng phóng to */}
      {selectedImage && (
        <div
          className="modal fade show d-block"
          tabIndex="-1"
          style={{ backgroundColor: 'rgba(0,0,0,0.8)', zIndex: 1070 }}
          onClick={() => setSelectedImage(null)}
        >
          <div className="modal-dialog modal-dialog-centered modal-lg">
            <div className="modal-content bg-transparent border-0 text-center">
              <div className="position-relative d-inline-block">
                <img
                  src={resolveAssetUrl(selectedImage)}
                  alt="Ảnh minh chứng lớn"
                  className="rounded shadow-lg"
                  style={{ maxWidth: '90vw', maxHeight: '85vh', objectFit: 'contain' }}
                />
                <button
                  type="button"
                  className="btn btn-close btn-close-white position-absolute top-0 end-0 m-3"
                  onClick={() => setSelectedImage(null)}
                  aria-label="Đóng"
                ></button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

