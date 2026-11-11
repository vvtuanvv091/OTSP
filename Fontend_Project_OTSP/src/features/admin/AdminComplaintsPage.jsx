import { useEffect, useState } from 'react';
import adminApi from '../../api/adminApi';
import complaintApi from '../../api/complaintApi';
import { resolveAssetUrl } from '../../api/config';
import { useToast } from '../../context/ToastContext';
import { useConfirm } from '../../context/ConfirmDialogContext';
import {
  AdminFeedback,
  AdminLoadingRows,
  AdminPageHeader,
  AdminPagination,
  AdminStats,
} from './AdminPageShared';

const PAGE_SIZE = 10;
const statusLabel = {
  New: 'Mới nhận',
  Processing: 'Đang xử lý',
  Resolved: 'Đã giải quyết',
  Rejected: 'Đã từ chối',
};

export default function AdminComplaintsPage() {
  const toast = useToast();
  const confirm = useConfirm();
  const [data, setData] = useState(null);
  const [search, setSearch] = useState('');
  const [querySearch, setQuerySearch] = useState('');
  const [status, setStatus] = useState('all');
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState(null);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  // Modal xử lý khiếu nại
  const [activeComplaint, setActiveComplaint] = useState(null);
  const [newStatus, setNewStatus] = useState('Processing');
  const [resolutionText, setResolutionText] = useState('');
  const [previewImage, setPreviewImage] = useState(null);

  useEffect(() => {
    const timer = window.setTimeout(() => setQuerySearch(search.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let active = true;
    async function loadComplaints() {
      setLoading(true);
      setError('');

      try {
        const res = await complaintApi.getAdminComplaints({
          status: status === 'all' ? undefined : status,
          search: querySearch || undefined,
          page,
          pageSize: PAGE_SIZE,
        });
        if (active) {
          setData(res.data);
        }
      } catch (err) {
        if (active) {
          setError(err.response?.data?.message || 'Không tải được danh sách khiếu nại.');
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    }

    loadComplaints();
    return () => {
      active = false;
    };
  }, [querySearch, status, page, reload]);

  const handleOpenProcessModal = (complaint) => {
    setActiveComplaint(complaint);
    setNewStatus(complaint.status === 'New' ? 'Processing' : complaint.status);
    setResolutionText(complaint.resolution || '');
  };

  const handleSaveResolution = async (e) => {
    e.preventDefault();
    if (!activeComplaint) return;

    if ((newStatus === 'Resolved' || newStatus === 'Rejected') && !resolutionText.trim()) {
      setError('Vui lòng nhập nội dung giải quyết hoặc lý do từ chối để thông báo cho người dùng.');
      return;
    }

    setBusyId(activeComplaint.id);
    setError('');
    try {
      await complaintApi.updateAdminComplaintStatus(activeComplaint.id, {
        status: newStatus,
        resolution: resolutionText.trim(),
      });
      setMessage(`Đã cập nhật khiếu nại #${activeComplaint.id} thành công.`);
      setActiveComplaint(null);
      setReload((r) => r + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Lỗi khi cập nhật trạng thái khiếu nại.');
    } finally {
      setBusyId(null);
    }
  };

  const handleDeleteComplaint = async (item) => {
    const ok = await confirm({
      title: 'Xóa khiếu nại',
      message: `Bạn có chắc chắn muốn xóa khiếu nại #${item.id}?`,
      confirmText: 'Xóa',
      variant: 'danger',
    });
    if (!ok) return;
    setBusyId(item.id);
    try {
      await adminApi.deleteComplaint(item.id);
      toast.success(`Đã xóa khiếu nại #${item.id} thành công.`);
      setReload((r) => r + 1);
    } catch (err) {
      toast.error(err.response?.data?.message || 'Lỗi khi xóa khiếu nại.');
    } finally {
      setBusyId(null);
    }
  };

  const complaints = data?.items || [];
  const stats = data?.stats || {};

  return (
    <div className="admin-page">
      <AdminPageHeader
        title="Khiếu nại & Hỗ trợ"
        description="Tiếp nhận, kiểm tra minh chứng và giải quyết các khiếu nại từ học viên và gia sư."
      />

      <AdminFeedback
        error={error}
        message={message}
        onDismiss={() => {
          setError('');
          setMessage('');
        }}
      />

      <AdminStats
        items={[
          { label: 'Mới tiếp nhận', value: stats.totalNew ?? 0, note: 'Cần tiếp nhận & phân loại' },
          { label: 'Đang xử lý', value: stats.totalProcessing ?? 0, note: 'Đang làm việc với các bên' },
          { label: 'Đã giải quyết', value: stats.totalResolved ?? 0, note: 'Đã có kết luận thỏa đáng' },
          { label: 'Đã từ chối', value: stats.totalRejected ?? 0, note: 'Khiếu nại không hợp lệ' },
        ]}
      />

      {/* Bộ lọc nhanh trạng thái khiếu nại */}
      <div className="d-flex align-items-center gap-2 mb-3 flex-wrap">
        <button
          type="button"
          className={`btn btn-sm ${status === 'all' ? 'btn-danger' : 'btn-outline-secondary'}`}
          onClick={() => { setStatus('all'); setPage(1); }}
        >
          Tất cả ({stats.totalAll ?? 0})
        </button>
        <button
          type="button"
          className={`btn btn-sm ${status === 'New' ? 'btn-danger' : 'btn-outline-secondary'}`}
          onClick={() => { setStatus('New'); setPage(1); }}
        >
          Mới nhận ({stats.totalNew ?? 0})
        </button>
        <button
          type="button"
          className={`btn btn-sm ${status === 'Processing' ? 'btn-danger' : 'btn-outline-secondary'}`}
          onClick={() => { setStatus('Processing'); setPage(1); }}
        >
          Đang xử lý ({stats.totalProcessing ?? 0})
        </button>
        <button
          type="button"
          className={`btn btn-sm ${status === 'Resolved' ? 'btn-danger' : 'btn-outline-secondary'}`}
          onClick={() => { setStatus('Resolved'); setPage(1); }}
        >
          Đã giải quyết ({stats.totalResolved ?? 0})
        </button>
        <button
          type="button"
          className={`btn btn-sm ${status === 'Rejected' ? 'btn-danger' : 'btn-outline-secondary'}`}
          onClick={() => { setStatus('Rejected'); setPage(1); }}
        >
          Đã từ chối ({stats.totalRejected ?? 0})
        </button>
      </div>

      {/* Ô tìm kiếm khiếu nại */}
      <div className="admin-toolbar mb-3">
        <div className="admin-search-wrapper" style={{ maxWidth: '420px', width: '100%' }}>
          <i className="bi bi-search" aria-hidden="true" />
          <input
            type="search"
            className="form-control form-control-sm"
            placeholder="Tìm theo nội dung, người gửi, người bị phản ánh..."
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </div>
      </div>

      {/* Bảng danh sách khiếu nại */}
      <section className="admin-panel">
        <div className="admin-panel-heading">
          Danh sách khiếu nại <span className="admin-count">{data?.totalCount ?? complaints.length}</span>
        </div>
        <div className="admin-table-scroll">
          <table className="admin-table admin-wide-table">
            <thead>
              <tr>
                <th style={{ width: '70px' }}>Mã</th>
                <th style={{ width: '130px' }}>Ngày gửi</th>
                <th style={{ width: '180px' }}>Người gửi</th>
                <th style={{ width: '180px' }}>Đối tượng</th>
                <th>Nội dung khiếu nại</th>
                <th style={{ width: '120px' }}>Trạng thái</th>
                <th style={{ width: '150px' }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <AdminLoadingRows columns={7} loading={loading} hasItems={complaints.length > 0} emptyText="Không tìm thấy khiếu nại nào phù hợp." />
              {!loading && complaints.map((item) => {
                const isProcessing = busyId === item.id;
                return (
                  <tr key={item.id}>
                    <td>
                      <strong>#{item.id}</strong>
                    </td>
                    <td>
                      <span>{new Date(item.createdAt).toLocaleDateString('vi-VN')}</span>
                      <span className="admin-table-secondary">
                        {new Date(item.createdAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}
                      </span>
                    </td>
                    <td>
                      <span className="admin-table-primary">{item.senderName || 'Người dùng'}</span>
                      <span className="admin-table-secondary">{item.senderEmail}</span>
                    </td>
                    <td>
                      {item.targetUserName ? (
                        <>
                          <span className="admin-table-primary">{item.targetUserName}</span>
                          <span className="admin-table-secondary">{item.targetUserEmail}</span>
                        </>
                      ) : (
                        <span className="admin-subject-tag">Hệ thống chung</span>
                      )}
                    </td>
                    <td>
                      <div className="text-dark small mb-1" style={{ whiteSpace: 'pre-wrap', maxHeight: '80px', overflowY: 'auto' }}>
                        {item.content}
                      </div>
                      {item.evidenceUrl && (
                        <button
                          type="button"
                          className="btn btn-sm btn-link text-danger p-0 small text-decoration-none d-inline-flex align-items-center gap-1"
                          onClick={() => setPreviewImage(resolveAssetUrl(item.evidenceUrl))}
                        >
                          <i className="bi bi-image"></i> Xem minh chứng
                        </button>
                      )}
                      {item.resolution && (
                        <div className="mt-1 small text-success bg-success-subtle p-1.5 rounded border border-success-subtle">
                          <strong>Kết luận:</strong> {item.resolution}
                        </div>
                      )}
                    </td>
                      <td>
                        <span
                          className={`admin-status ${
                            item.status === 'Resolved'
                              ? 'is-active'
                              : item.status === 'Rejected'
                              ? 'is-locked'
                              : 'is-pending'
                          }`}
                        >
                          {statusLabel[item.status] || item.status}
                        </span>
                      </td>
                      <td>
                        <div className="admin-actions-inline">
                          <button
                            type="button"
                            className="admin-action"
                            onClick={() => handleOpenProcessModal(item)}
                            disabled={isProcessing}
                          >
                            <i className="bi bi-pencil-square me-1"></i>Xử lý
                          </button>
                          <button
                            type="button"
                            className="admin-action is-danger"
                            onClick={() => handleDeleteComplaint(item)}
                            disabled={isProcessing}
                          >
                            <i className="bi bi-trash me-1"></i>Xóa
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
            </tbody>
          </table>
        </div>
      </section>

      {/* Phân trang */}
      <AdminPagination
        page={data?.page || page}
        totalPages={data?.totalPages || 1}
        total={data?.totalCount ?? complaints.length}
        pageSize={PAGE_SIZE}
        onChange={(p) => setPage(p)}
      />

      {/* Modal xử lý khiếu nại của Quản trị viên */}
      {activeComplaint && (
        <div
          className="modal fade show d-block"
          tabIndex="-1"
          style={{ backgroundColor: 'rgba(0,0,0,0.5)', zIndex: 1060 }}
        >
          <div className="modal-dialog modal-dialog-centered">
            <div className="modal-content border-0 shadow-lg rounded-3">
              <div className="modal-header border-bottom py-3">
                <h5 className="modal-title fw-bold text-dark d-flex align-items-center gap-2">
                  <i className="bi bi-shield-check text-danger"></i>
                  Xử lý khiếu nại #{activeComplaint.id}
                </h5>
                <button
                  type="button"
                  className="btn-close"
                  onClick={() => setActiveComplaint(null)}
                  disabled={busyId === activeComplaint.id}
                  aria-label="Đóng"
                ></button>
              </div>

              <form onSubmit={handleSaveResolution}>
                <div className="modal-body p-4">
                  <div className="mb-3 bg-light p-3 rounded-2 border">
                    <div className="small text-muted mb-1">Người gửi: <strong>{activeComplaint.senderName}</strong> ({activeComplaint.senderEmail})</div>
                    {activeComplaint.targetUserName && (
                      <div className="small text-muted mb-1">Đối tượng: <strong>{activeComplaint.targetUserName}</strong></div>
                    )}
                    <div className="small text-dark mt-2 pt-2 border-top">
                      <strong>Nội dung:</strong> {activeComplaint.content}
                    </div>
                  </div>

                  <div className="mb-3">
                    <label className="form-label small fw-semibold text-dark">
                      Cập nhật trạng thái <span className="text-danger">*</span>
                    </label>
                    <div className="d-flex flex-wrap gap-2">
                      <button
                        type="button"
                        className={`btn btn-sm ${newStatus === 'Processing' ? 'btn-info text-dark fw-bold' : 'btn-light border'}`}
                        onClick={() => setNewStatus('Processing')}
                      >
                        Đang xử lý
                      </button>
                      <button
                        type="button"
                        className={`btn btn-sm ${newStatus === 'Resolved' ? 'btn-success text-white fw-bold' : 'btn-light border'}`}
                        onClick={() => setNewStatus('Resolved')}
                      >
                        Đã giải quyết
                      </button>
                      <button
                        type="button"
                        className={`btn btn-sm ${newStatus === 'Rejected' ? 'btn-secondary text-white fw-bold' : 'btn-light border'}`}
                        onClick={() => setNewStatus('Rejected')}
                      >
                        Từ chối khiếu nại
                      </button>
                    </div>
                  </div>

                  <div className="mb-3">
                    <label className="form-label small fw-semibold text-dark">
                      Kết luận / Ghi chú phản hồi cho người dùng <span className="text-danger">*</span>
                    </label>
                    <textarea
                      className="form-control"
                      rows="4"
                      placeholder="Ghi rõ phương án giải quyết (ví dụ: đã hoàn tiền buổi học, đã nhắc nhở gia sư, hoặc giải thích lý do từ chối)..."
                      value={resolutionText}
                      onChange={(e) => setResolutionText(e.target.value)}
                      required={newStatus === 'Resolved' || newStatus === 'Rejected'}
                    ></textarea>
                    <div className="form-text small text-muted">
                      Nội dung này sẽ hiển thị trực tiếp cho người gửi khiếu nại và gửi thông báo hệ thống.
                    </div>
                  </div>
                </div>

                <div className="modal-footer border-top py-2 px-4 bg-light rounded-bottom-3 d-flex justify-content-between">
                  <button
                    type="button"
                    className="btn btn-sm btn-outline-secondary px-3"
                    onClick={() => setActiveComplaint(null)}
                    disabled={busyId === activeComplaint.id}
                  >
                    Hủy bỏ
                  </button>
                  <button
                    type="submit"
                    className="btn btn-sm btn-danger px-4 fw-semibold"
                    disabled={busyId === activeComplaint.id}
                  >
                    {busyId === activeComplaint.id ? (
                      <>
                        <span className="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
                        Đang lưu...
                      </>
                    ) : (
                      'Cập nhật kết quả'
                    )}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* Modal phóng to xem ảnh minh chứng */}
      {previewImage && (
        <div
          className="modal fade show d-block"
          tabIndex="-1"
          style={{ backgroundColor: 'rgba(0,0,0,0.8)', zIndex: 1070 }}
          onClick={() => setPreviewImage(null)}
        >
          <div className="modal-dialog modal-dialog-centered modal-lg">
            <div className="modal-content bg-transparent border-0 text-center">
              <div className="position-relative d-inline-block">
                <img
                  src={resolveAssetUrl(previewImage)}
                  alt="Minh chứng"
                  className="rounded shadow-lg"
                  style={{ maxWidth: '90vw', maxHeight: '85vh', objectFit: 'contain' }}
                />
                <button
                  type="button"
                  className="btn btn-close btn-close-white position-absolute top-0 end-0 m-3"
                  onClick={() => setPreviewImage(null)}
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

