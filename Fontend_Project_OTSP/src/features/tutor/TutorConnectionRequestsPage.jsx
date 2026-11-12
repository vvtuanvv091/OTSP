import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import tutorApi from '../../api/tutorApi';
import { resolveAssetUrl } from '../../api/config';
import { useConfirm } from '../../context/confirmHooks';
import OtspPagination from '../../components/common/OtspPagination';

export default function TutorConnectionRequestsPage() {
  const confirm = useConfirm();
  const [requests, setRequests] = useState([]);
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [actionLoading, setActionLoading] = useState(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;
    async function loadRequests() {
      try {
        setLoading(true);
        const params = { page, pageSize: 10 };
        if (statusFilter) params.status = statusFilter;
        const res = await tutorApi.getConnectionRequests(params);
        if (!active) return;
        setRequests(res.data?.items || []);
        setPage(res.data?.page || 1);
        setTotalPages(res.data?.totalPages || 1);
        setTotal(res.data?.total ?? res.data?.totalItems ?? 0);
      } catch {
        if (active) setError('Không tải được danh sách yêu cầu kết nối.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadRequests();
    return () => {
      active = false;
    };
  }, [page, statusFilter, reloadKey]);

  const handleTabChange = (status) => {
    setStatusFilter(status);
    setPage(1);
  };

  const handleAccept = async (id, studentName) => {
    setActionLoading(id);
    setMessage('');
    setError('');

    try {
      const res = await tutorApi.acceptConnectionRequest(id);
      setMessage(res.data?.message || `Đã chấp nhận yêu cầu của ${studentName || 'học viên'}.`);
      setReloadKey((k) => k + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Có lỗi xảy ra khi chấp nhận yêu cầu.');
    } finally {
      setActionLoading(null);
    }
  };

  const handleReject = async (id) => {
    const reason = await confirm({
      title: 'Từ chối yêu cầu kết nối',
      message: 'Bạn có chắc muốn từ chối yêu cầu kết nối này?',
      confirmText: 'Từ chối yêu cầu',
      variant: 'danger',
      input: {
        label: 'Lý do từ chối (tùy chọn)',
        placeholder: 'Nhập lý do từ chối để học viên được biết...',
        required: false,
      },
    });
    if (reason === null) return;

    setActionLoading(id);
    setMessage('');
    setError('');

    try {
      const res = await tutorApi.rejectConnectionRequest(id, reason);
      setMessage(res.data?.message || 'Đã từ chối yêu cầu kết nối.');
      setReloadKey((k) => k + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Có lỗi xảy ra khi từ chối yêu cầu.');
    } finally {
      setActionLoading(null);
    }
  };

  const formatSingleSlot = (raw) => {
    const d = new Date(raw);
    if (Number.isNaN(d.getTime())) return String(raw);
    const days = ['Chủ nhật', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7'];
    const dayLabel = days[d.getDay()];
    const dateStr = `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`;
    const startHour = String(d.getHours()).padStart(2, '0');
    const startMin = String(d.getMinutes()).padStart(2, '0');
    const endH = new Date(d);
    endH.setHours(endH.getHours() + 2);
    const endHour = String(endH.getHours()).padStart(2, '0');
    const endMin = String(endH.getMinutes()).padStart(2, '0');
    return `${dayLabel}, ${dateStr} · ${startHour}:${startMin}-${endHour}:${endMin}`;
  };

  const formatSlotTime = (slotUtc, slotsUtc) => {
    const unique = [];
    const seen = new Set();
    [...(slotsUtc || []), slotUtc].filter(Boolean).forEach((s) => {
      if (!seen.has(s)) {
        seen.add(s);
        unique.push(s);
      }
    });
    if (unique.length === 0) return 'Lịch linh hoạt';
    return unique.map(formatSingleSlot).join(' | ');
  };

  const formatLifecycleNote = (request) => {
    const parts = [];
    if (request.durationWeeks) parts.push(`Gói ${request.durationWeeks} tuần`);
    if (request.requestedStartDate || request.requestedEndDate) {
      const fmt = (iso) => {
        if (!iso) return '—';
        const [y, m, d] = String(iso).slice(0, 10).split('-');
        return y && m && d ? `${d}/${m}/${y}` : iso;
      };
      parts.push(`${fmt(request.requestedStartDate)} → ${fmt(request.requestedEndDate)}`);
    }
    return parts.join(' · ');
  };

  const resolveIsTrial = (request) => {
    if (typeof request.isTrial === 'boolean') return request.isTrial;
    if (request.durationWeeks) return false;
    if (request.proposedTrialSlotsUtc && request.proposedTrialSlotsUtc.length > 1) return false;
    return Boolean(
      (request.message && request.message.includes('[Mục đích: Đăng ký học thử 1 buổi]')) ||
      request.proposedTrialSlotUtc ||
      (request.proposedTrialSlotsUtc && request.proposedTrialSlotsUtc.length === 1)
    );
  };

  return (
    <div className="container-fluid py-4" id="tutor-requests-page">
      {/* Header (Matching 5079) */}
      <div className="d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3 mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1 text-dark">Yêu cầu kết nối</h1>
          <p className="text-secondary mb-0">Học viên gửi yêu cầu kèm khung giờ học thử. Hệ thống đối chiếu với lịch rảnh của bạn.</p>
        </div>
        <div className="d-flex gap-2">
          <Link to="/tutor/schedule" className="btn btn-sm btn-outline-secondary d-inline-flex align-items-center">
            <i className="bi bi-calendar3 me-1"></i> Lịch dạy
          </Link>
          <Link to="/tutor/students" className="btn btn-sm btn-outline-danger d-inline-flex align-items-center">
            <i className="bi bi-people me-1"></i> Học viên của tôi
          </Link>
        </div>
      </div>

      {/* Tab lọc trạng thái yêu cầu */}
      <div className="mb-4">
        <ul className="nav nav-pills gap-2 bg-light p-1.5 rounded-3 border d-inline-flex flex-wrap" role="tablist">
          <li className="nav-item">
            <button
              type="button"
              className={`nav-link py-1.5 px-3 fw-semibold small ${statusFilter === '' ? 'active bg-danger text-white shadow-sm' : 'text-secondary'}`}
              style={statusFilter === '' ? { backgroundColor: '#DB303A' } : {}}
              onClick={() => handleTabChange('')}
            >
              <i className="bi bi-collection me-1"></i> Tất cả
            </button>
          </li>
          <li className="nav-item">
            <button
              type="button"
              className={`nav-link py-1.5 px-3 fw-semibold small ${statusFilter === 'Pending' ? 'active bg-danger text-white shadow-sm' : 'text-secondary'}`}
              style={statusFilter === 'Pending' ? { backgroundColor: '#DB303A' } : {}}
              onClick={() => handleTabChange('Pending')}
            >
              <i className="bi bi-hourglass-split me-1"></i> Chờ duyệt
            </button>
          </li>
          <li className="nav-item">
            <button
              type="button"
              className={`nav-link py-1.5 px-3 fw-semibold small ${statusFilter === 'Accepted' ? 'active bg-danger text-white shadow-sm' : 'text-secondary'}`}
              style={statusFilter === 'Accepted' ? { backgroundColor: '#DB303A' } : {}}
              onClick={() => handleTabChange('Accepted')}
            >
              <i className="bi bi-check-circle me-1"></i> Đã chấp nhận
            </button>
          </li>
          <li className="nav-item">
            <button
              type="button"
              className={`nav-link py-1.5 px-3 fw-semibold small ${statusFilter === 'Rejected' ? 'active bg-danger text-white shadow-sm' : 'text-secondary'}`}
              style={statusFilter === 'Rejected' ? { backgroundColor: '#DB303A' } : {}}
              onClick={() => handleTabChange('Rejected')}
            >
              <i className="bi bi-x-circle me-1"></i> Đã từ chối
            </button>
          </li>
        </ul>
      </div>

      {/* Thông báo hệ thống */}
      {message && (
        <div className="alert alert-success alert-dismissible fade show shadow-sm" role="alert">
          <i className="bi bi-check-circle-fill me-2"></i>{message}
          <button type="button" className="btn-close" onClick={() => setMessage('')} aria-label="Đóng"></button>
        </div>
      )}

      {error && (
        <div className="alert alert-warning alert-dismissible fade show shadow-sm" role="alert">
          <i className="bi bi-exclamation-triangle-fill me-2"></i>{error}
          <button type="button" className="btn-close" onClick={() => setError('')} aria-label="Đóng"></button>
        </div>
      )}

      {loading ? (
        <div className="card border-0 shadow-sm rounded-3 p-5 text-center">
          <div className="spinner-border text-danger" role="status"></div>
        </div>
      ) : requests.length === 0 ? (
        <div className="card border-0 shadow-sm rounded-3 p-5 text-center">
          <i className="bi bi-person-x fs-1 text-muted d-block mb-3"></i>
          <h5 className="fw-semibold">Chưa có yêu cầu kết nối nào</h5>
          <p className="text-secondary small mb-0">Khi có học viên gửi đề xuất học tập, thông tin sẽ hiển thị tại đây.</p>
        </div>
      ) : (
        <>
          {/* ==================== DESKTOP TABLE VIEW (>= 768px) ==================== */}
          <div className="d-none d-md-block card border-0 shadow-sm rounded-3 overflow-hidden">
            <table className="table table-hover align-middle mb-0">
              <thead className="table-light">
                <tr>
                  <th scope="col" style={{ width: '22%' }}>Học viên</th>
                  <th scope="col" style={{ width: '22%' }}>Môn &amp; Lớp</th>
                  <th scope="col" style={{ width: '20%' }}>Giờ học đề xuất</th>
                  <th scope="col" style={{ width: '14%' }}>Trạng thái</th>
                  <th scope="col" style={{ minWidth: 210, width: '22%', textAlign: 'right', whiteSpace: 'nowrap' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {requests.map((request) => {
                  const isTrial = resolveIsTrial(request);
                  const lifecycleNote = formatLifecycleNote(request);

                  const isPending = request.status === 'Pending';
                  const isAccepted = request.status === 'Accepted';
                  const isRejected = request.status === 'Rejected';

                  const formattedSlot = formatSlotTime(request.proposedTrialSlotUtc, request.proposedTrialSlotsUtc);

                  // Tách nội dung ghi chú sạch loại bỏ tiền tố hệ thống
                  let studentNote = null;
                  if (request.message) {
                    const cleanLines = request.message
                      .split(/[\r\n]+/)
                      .map((l) => l.trim())
                      .filter((l) => !l.startsWith('[Mục đích:') && !l.startsWith('[Lịch học đề xuất'));
                    const joined = cleanLines.join(' ').trim();
                    if (joined) studentNote = joined;
                  }

                  return (
                    <tr key={request.id} data-request-row={request.id}>
                      <td>
                        <div className="d-flex align-items-center gap-2.5">
                          {request.studentAvatarUrl ? (
                            <img
                              src={resolveAssetUrl(request.studentAvatarUrl)}
                              alt={request.studentName}
                              className="rounded-circle object-fit-cover shadow-sm"
                              style={{ width: 38, height: 38, flexShrink: 0 }}
                            />
                          ) : (
                            <div
                              style={{
                                width: 38,
                                height: 38,
                                fontSize: 14,
                                borderRadius: '50%',
                                background: '#fee2e2',
                                color: '#dc2626',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                fontWeight: 700,
                                flexShrink: 0
                              }}
                            >
                              {request.studentName ? request.studentName.trim().charAt(0).toUpperCase() : 'H'}
                            </div>
                          )}
                          <div className="min-w-0 ms-2">
                            <div className="fw-bold text-dark text-truncate" style={{ fontSize: 14 }} title={request.studentName}>
                              {request.studentName}
                            </div>
                            <div className="small mt-0.5">
                              {isTrial ? (
                                <span className="text-danger fw-semibold" style={{ fontSize: '11.5px' }}>
                                  <i className="bi bi-lightning-charge-fill me-1"></i>Học thử 1 buổi
                                </span>
                              ) : (
                                <span className="text-primary fw-semibold" style={{ fontSize: '11.5px' }}>
                                  <i className="bi bi-mortarboard-fill me-1"></i>Học chính thức
                                </span>
                              )}
                            </div>
                            {studentNote && (
                              <div className="text-secondary small mt-0.5 text-truncate" style={{ maxWidth: 240 }} title={studentNote}>
                                <i className="bi bi-chat-left-dots text-muted me-1"></i>"{studentNote}"
                              </div>
                            )}
                          </div>
                        </div>
                      </td>

                      <td>
                        <div className="fw-bold text-dark" style={{ fontSize: 14 }}>{request.subjectName}</div>
                        <div className="small text-secondary mt-0.5">
                          {request.gradeLevelName && <span>{request.gradeLevelName}</span>}
                          <span className="mx-1">·</span>
                          {request.preferredType === 'OneToOne' || request.preferredType === 0 ? (
                            <span>1 kèm 1</span>
                          ) : (
                            <span>Lớp nhóm{request.classroomName ? `: ${request.classroomName}` : ''}</span>
                          )}
                        </div>
                      </td>

                      <td>
                        <div className="fw-semibold text-dark" style={{ fontSize: 13, fontVariantNumeric: 'tabular-nums' }}>
                          <i className="bi bi-clock text-muted me-1"></i>{formattedSlot}
                        </div>
                        {lifecycleNote && (
                          <div className="small text-secondary mt-0.5" style={{ fontVariantNumeric: 'tabular-nums' }}>
                            <i className="bi bi-calendar-range me-1"></i>{lifecycleNote}
                          </div>
                        )}
                        {request.matchesAvailability && (
                          <div className="small text-success fw-semibold mt-0.5">
                            <i className="bi bi-check2-circle me-1"></i>Khớp lịch rảnh
                          </div>
                        )}
                      </td>

                      <td>
                        {isPending ? (
                          <span className="badge rounded-pill px-2.5 py-1 fw-semibold" style={{ background: '#fef3c7', color: '#b45309', border: '1px solid #fcd34d', fontSize: 12 }}>
                            <i className="bi bi-hourglass-split me-1"></i>Chờ duyệt
                          </span>
                        ) : isAccepted ? (
                          <span className="badge rounded-pill px-2.5 py-1 fw-semibold" style={{ background: '#15803d', color: '#ffffff', fontSize: 12 }}>
                            <i className="bi bi-check-circle-fill me-1"></i>Đã chấp nhận
                          </span>
                        ) : isRejected ? (
                          <span className="badge rounded-pill px-2.5 py-1 fw-semibold" style={{ background: '#64748b', color: '#ffffff', fontSize: 12 }}>
                            <i className="bi bi-x-circle-fill me-1"></i>Đã từ chối
                          </span>
                        ) : (
                          <span className="badge rounded-pill px-2.5 py-1 fw-semibold" style={{ background: '#dc2626', color: '#ffffff', fontSize: 12 }}>
                            <i className="bi bi-slash-circle-fill me-1"></i>Đã hủy
                          </span>
                        )}
                      </td>

                      <td style={{ textAlign: 'right', whiteSpace: 'nowrap', minWidth: 210 }}>
                        {isPending ? (
                          <div className="d-inline-flex align-items-center justify-content-end gap-2" style={{ flexWrap: 'nowrap' }}>
                            <button
                              type="button"
                              className="btn btn-sm btn-success d-inline-flex align-items-center gap-1 px-2.5 py-1 shadow-sm text-nowrap"
                              style={{ fontSize: '12.5px', fontWeight: 500, whiteSpace: 'nowrap', flexShrink: 0 }}
                              disabled={actionLoading === request.id}
                              onClick={() => handleAccept(request.id, request.studentName)}
                            >
                              <i className="bi bi-check-lg"></i> Chấp nhận
                            </button>
                            <button
                              type="button"
                              className="btn btn-sm btn-outline-danger d-inline-flex align-items-center gap-1 px-2.5 py-1 text-nowrap"
                              style={{ fontSize: '12.5px', fontWeight: 500, whiteSpace: 'nowrap', flexShrink: 0 }}
                              disabled={actionLoading === request.id}
                              onClick={() => handleReject(request.id)}
                            >
                              <i className="bi bi-x-lg"></i> Từ chối
                            </button>
                          </div>
                        ) : isAccepted ? (
                          <div className="d-inline-flex align-items-center gap-1.5 justify-content-end" style={{ flexWrap: 'nowrap' }}>
                            {request.studentUserId && (
                              <Link
                                to={`/tutor/chat?contactId=${request.studentUserId}`}
                                className="btn btn-sm btn-outline-danger d-inline-flex align-items-center gap-1 px-2.5 py-1 text-nowrap"
                                style={{ fontSize: '12.5px', fontWeight: 500, whiteSpace: 'nowrap', flexShrink: 0 }}
                                title="Nhắn tin với học viên"
                              >
                                <i className="bi bi-chat-dots-fill"></i> Nhắn tin
                              </Link>
                            )}
                            <Link
                              to="/tutor/schedule"
                              className="btn btn-sm btn-outline-secondary d-inline-flex align-items-center gap-1 px-2 py-1 text-nowrap"
                              style={{ fontSize: '12.5px', whiteSpace: 'nowrap', flexShrink: 0 }}
                              title="Xem lịch dạy"
                            >
                              <i className="bi bi-calendar3"></i>
                            </Link>
                          </div>
                        ) : isRejected ? (
                          <span className="text-muted small d-inline-flex align-items-center gap-1">
                            <i className="bi bi-dash-circle"></i> Đã từ chối
                          </span>
                        ) : (
                          <span className="text-muted small d-inline-flex align-items-center gap-1">
                            <i className="bi bi-info-circle"></i> Học viên đã hủy
                          </span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {/* ==================== MOBILE CARD VIEW (< 768px) ==================== */}
          <div className="d-md-none d-flex flex-column gap-3">
            {requests.map((request) => {
              const isTrial = resolveIsTrial(request);
              const lifecycleNote = formatLifecycleNote(request);
              const isPending = request.status === 'Pending';
              const isAccepted = request.status === 'Accepted';
              const isRejected = request.status === 'Rejected';
              const formattedSlot = formatSlotTime(request.proposedTrialSlotUtc, request.proposedTrialSlotsUtc);

              return (
                <div key={request.id} className="card border shadow-sm rounded-3 p-3 bg-white">
                  <div className="d-flex justify-content-between align-items-start mb-2">
                    <div className="d-flex align-items-center gap-2">
                      <div
                        style={{
                          width: 34,
                          height: 34,
                          borderRadius: '50%',
                          background: '#fee2e2',
                          color: '#dc2626',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          fontWeight: 700,
                          fontSize: 13
                        }}
                      >
                        {request.studentName ? request.studentName.trim().charAt(0).toUpperCase() : 'H'}
                      </div>
                      <div>
                        <div className="fw-bold text-dark small">{request.studentName}</div>
                        <span className={isTrial ? 'text-danger small' : 'text-primary small'}>
                          <i className={`bi ${isTrial ? 'bi-lightning-charge-fill' : 'bi-mortarboard-fill'} me-1`}></i>
                          {isTrial ? 'Học thử' : 'Học chính thức'}
                        </span>
                      </div>
                    </div>
                    {isPending ? (
                      <span className="badge rounded-pill bg-warning-subtle text-warning-emphasis border border-warning-subtle">
                        Chờ duyệt
                      </span>
                    ) : isAccepted ? (
                      <span className="badge rounded-pill bg-success text-white">Đã chấp nhận</span>
                    ) : isRejected ? (
                      <span className="badge rounded-pill bg-secondary text-white">Đã từ chối</span>
                    ) : (
                      <span className="badge rounded-pill bg-danger text-white">Đã hủy</span>
                    )}
                  </div>

                  <div className="small text-secondary mb-1">
                    <i className="bi bi-book me-1"></i><strong>{request.subjectName}</strong> · {request.gradeLevelName}
                  </div>
                  <div className="small text-secondary mb-2" style={{ fontVariantNumeric: 'tabular-nums' }}>
                    <i className="bi bi-clock me-1"></i>{formattedSlot}
                    {lifecycleNote && (
                      <div className="mt-1"><i className="bi bi-calendar-range me-1"></i>{lifecycleNote}</div>
                    )}
                  </div>

                  {request.message && (
                    <p className="small text-muted mb-3 p-2 bg-light rounded">"{request.message}"</p>
                  )}

                  <div className="d-flex justify-content-end gap-2 pt-2 border-top">
                    {isPending ? (
                      <>
                        <button
                          type="button"
                          className="btn btn-sm btn-success px-3"
                          disabled={actionLoading === request.id}
                          onClick={() => handleAccept(request.id, request.studentName)}
                        >
                          Chấp nhận
                        </button>
                        <button
                          type="button"
                          className="btn btn-sm btn-outline-danger px-3"
                          disabled={actionLoading === request.id}
                          onClick={() => handleReject(request.id)}
                        >
                          Từ chối
                        </button>
                      </>
                    ) : isAccepted && request.studentUserId ? (
                      <Link
                        to={`/tutor/chat?contactId=${request.studentUserId}`}
                        className="btn btn-sm btn-outline-danger px-3"
                      >
                        <i className="bi bi-chat-dots-fill me-1"></i> Nhắn tin
                      </Link>
                    ) : null}
                  </div>
                </div>
              );
            })}
          </div>

          {/* Phân trang danh sách yêu cầu */}
          {total > 0 && (
            <OtspPagination
              page={page}
              totalPages={totalPages}
              total={total}
              pageSize={10}
              onChange={setPage}
              className="mt-4 rounded-3 shadow-sm border"
            />
          )}
        </>
      )}
    </div>
  );
}
