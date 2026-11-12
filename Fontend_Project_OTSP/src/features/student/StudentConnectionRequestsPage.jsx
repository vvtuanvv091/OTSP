import { useState, useEffect, useMemo } from 'react';
import { hubUrl, resolveAssetUrl } from '../../api/config';
import { Link } from 'react-router-dom';
import * as signalR from '@microsoft/signalr';
import studentApi from '../../api/studentApi';
import { useConfirm } from '../../context/confirmHooks';
import { useToast } from '../../context/toastHooks';
import { getStoredToken } from '../../utils/authStorage';

const formatMoney = (val) =>
  new Intl.NumberFormat('vi-VN').format(val || 0);

export default function StudentConnectionRequestsPage() {
  const confirm = useConfirm();
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [requests, setRequests] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [statusFilter, setStatusFilter] = useState(''); // '' (All), 'Pending', 'Accepted', 'Rejected'
  const [realtimeNotice, setRealtimeNotice] = useState('');
  const [actionMsg, setActionMsg] = useState({ type: '', text: '' });
  const [reloadTick, setReloadTick] = useState(0);

  useEffect(() => {
    let active = true;
    async function loadRequests() {
      try {
        setLoading(true);
        const res = await studentApi.getMyConnectionRequests(statusFilter || undefined);
        if (active && res.data) {
          const list = Array.isArray(res.data)
            ? res.data
            : (res.data.items || res.data.requests || []);
          setRequests(list);
          if (!statusFilter) {
            setTotalCount(res.data.total ?? list.length);
          } else if (res.data.totalAll !== undefined) {
            setTotalCount(res.data.totalAll);
          }
        }
      } catch (err) {
        console.warn('API getMyConnectionRequests error:', err);
      } finally {
        if (active) setLoading(false);
      }
    }
    loadRequests();
    return () => {
      active = false;
    };
  }, [statusFilter, reloadTick]);

  // Lắng nghe sự kiện cập nhật thời gian thực qua SignalR
  useEffect(() => {
    const token = getStoredToken();
    if (!token) return undefined;

    let active = true;
    let hubConnection = null;
    let isConnected = false;

    try {
      hubConnection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl('/hubs/connection-requests'), {
          accessTokenFactory: getStoredToken,
        })
        .withAutomaticReconnect()
        .build();

      hubConnection.on('ConnectionRequestStatusChanged', (update) => {
        if (!active || !update) return;
        setRequests((prev) =>
          prev.map((r) =>
            r.id === update.requestId ? { ...r, status: update.status } : r
          )
        );
        const statusLabel =
          update.status === 'Accepted'
            ? 'Đã chấp nhận'
            : update.status === 'Rejected'
            ? 'Đã từ chối'
            : update.status;
        setRealtimeNotice(`Gia sư vừa cập nhật phản hồi yêu cầu #${update.requestId}: ${statusLabel}`);
        setReloadTick((n) => n + 1);
      });

      hubConnection.on('TrialScheduleConfirmed', (update) => {
        if (!active || !update) return;
        setRealtimeNotice('Gia sư vừa xếp lịch học cho lớp của bạn. Danh sách đã được cập nhật.');
        setReloadTick((n) => n + 1);
      });

      hubConnection
        .start()
        .then(() => {
          isConnected = true;
          if (!active) {
            hubConnection.stop().catch(() => {});
          }
        })
        .catch((err) => {
          if (active) {
            console.debug('SignalR connection-requests start notice:', err?.message || err);
          }
        });
    } catch (err) {
      console.warn('SignalR connection-requests init error:', err);
    }

    return () => {
      active = false;
      if (hubConnection) {
        hubConnection.off('ConnectionRequestStatusChanged');
        if (isConnected && hubConnection.state === signalR.HubConnectionState.Connected) {
          hubConnection.stop().catch(() => {});
        }
      }
    };
  }, []);

  const handleCancel = async (id) => {
    const ok = await confirm({
      title: 'Hủy yêu cầu kết nối',
      message: 'Bạn có chắc chắn muốn hủy yêu cầu kết nối này không?',
      confirmText: 'Hủy yêu cầu',
      variant: 'danger',
    });
    if (!ok) return;
    try {
      const res = await studentApi.cancelConnectionRequest(id);
      if (res.data?.success) {
        toast.success(res.data.message || 'Đã hủy yêu cầu kết nối thành công.');
        setRequests((prev) =>
          prev.map((r) => (r.id === id ? { ...r, status: 'Cancelled' } : r))
        );
      }
    } catch (err) {
      toast.error(err?.response?.data?.message || 'Không thể hủy yêu cầu, vui lòng thử lại sau.');
    }
  };

  const filteredRequests = useMemo(() => {
    if (!statusFilter) return requests;
    return requests.filter((r) => (r.status || 'Pending') === statusFilter);
  }, [requests, statusFilter]);

  const formatDateTime = (dateStr) => {
    if (!dateStr) return '';
    try {
      const d = new Date(dateStr);
      if (isNaN(d.getTime())) return '';
      const day = String(d.getDate()).padStart(2, '0');
      const month = String(d.getMonth() + 1).padStart(2, '0');
      const year = d.getFullYear();
      const hours = String(d.getHours()).padStart(2, '0');
      const minutes = String(d.getMinutes()).padStart(2, '0');
      return `${day}/${month}/${year} ${hours}:${minutes}`;
    } catch {
      return '';
    }
  };

  return (
    <div id="my-requests-page" className="container-fluid py-3 px-1 px-md-3">
      {/* Tiêu đề trang & Đường dẫn điều hướng */}
      <div className="d-flex flex-wrap justify-content-between align-items-center mb-4 gap-2">
        <div>
          <h1 className="h4 fw-bold mb-1 text-body d-flex align-items-center">
            <i className="bi bi-clock-history text-danger me-2"></i> Yêu cầu kết nối đã gửi
          </h1>
          <p className="text-secondary small mb-0">
            Theo dõi tiến độ và phản hồi từ các gia sư bạn đã gửi yêu cầu học tập.
          </p>
        </div>
        <Link to="/search" className="btn btn-sm btn-outline-danger px-3 py-1.5 fw-semibold rounded-2">
          <i className="bi bi-search me-1"></i> Tìm thêm gia sư mới
        </Link>
      </div>

      {actionMsg.text && (
        <div className={`alert alert-${actionMsg.type} alert-dismissible fade show d-flex align-items-center mb-4`} role="alert">
          <i className={`bi ${actionMsg.type === 'success' ? 'bi-check-circle-fill' : 'bi-exclamation-triangle-fill'} fs-5 me-2`}></i>
          <div>{actionMsg.text}</div>
          <button type="button" className="btn-close" onClick={() => setActionMsg({ type: '', text: '' })}></button>
        </div>
      )}

      {realtimeNotice && (
        <div className="alert alert-info alert-dismissible fade show d-flex align-items-center mb-4" role="alert">
          <i className="bi bi-bell-fill me-2 fs-5"></i>
          <div>{realtimeNotice}</div>
          <button type="button" className="btn-close" onClick={() => setRealtimeNotice('')}></button>
        </div>
      )}

      {/* Bộ lọc trạng thái (Matching Image 4 pills) */}
      <div className="d-flex flex-wrap gap-2 mb-4 nav-filter" style={{ gap: '10px' }}>
        <button
          type="button"
          className={`btn btn-sm rounded-pill px-3 py-1.5 fw-semibold ${!statusFilter ? 'btn-danger text-white' : 'btn-outline-secondary'}`}
          onClick={() => setStatusFilter('')}
        >
          Tất cả ({statusFilter ? totalCount : requests.length})
        </button>
        <button
          type="button"
          className={`btn btn-sm rounded-pill px-3 py-1.5 ${statusFilter === 'Pending' ? 'btn-warning text-dark fw-semibold' : 'btn-outline-secondary'}`}
          onClick={() => setStatusFilter('Pending')}
        >
          Chờ phản hồi
        </button>
        <button
          type="button"
          className={`btn btn-sm rounded-pill px-3 py-1.5 ${statusFilter === 'Accepted' ? 'btn-success text-white fw-semibold' : 'btn-outline-secondary'}`}
          onClick={() => setStatusFilter('Accepted')}
        >
          Đã chấp nhận
        </button>
        <button
          type="button"
          className={`btn btn-sm rounded-pill px-3 py-1.5 ${statusFilter === 'Rejected' ? 'btn-secondary text-white fw-semibold' : 'btn-outline-secondary'}`}
          onClick={() => setStatusFilter('Rejected')}
        >
          Đã từ chối
        </button>
      </div>

      {/* Danh sách thẻ yêu cầu kết nối */}
      {loading ? (
        <div className="text-center py-5">
          <div className="spinner-border text-danger" role="status"></div>
          <div className="mt-2 text-secondary small">Đang tải danh sách yêu cầu kết nối...</div>
        </div>
      ) : filteredRequests.length === 0 ? (
        <div className="card border shadow-sm rounded-3 text-center py-5 bg-body">
          <div className="card-body">
            <i className="bi bi-inbox fs-1 text-secondary mb-3 d-block"></i>
            <h5 className="fw-semibold text-body">Chưa có yêu cầu kết nối nào</h5>
            <p className="text-secondary small mb-3">
              Bạn chưa gửi yêu cầu kết nối học tập nào theo tiêu chí này.
            </p>
            <Link to="/search" className="btn btn-sm btn-danger px-3.5 py-2">
              <i className="bi bi-search me-1"></i> Khám phá gia sư ngay
            </Link>
          </div>
        </div>
      ) : (
        <div className="row g-3">
          {filteredRequests.map((item) => (
            <div key={item.id} className="col-12 col-md-6 col-xl-4">
              <div
                className="card h-100 border shadow-sm rounded-3 p-3 d-flex flex-column justify-content-between bg-body"
                style={{ borderRadius: '1rem' }}
              >
                <div>
                  {/* Header thẻ: Avatar + Name + Fee + Status Badge */}
                  <div className="d-flex justify-content-between align-items-start mb-3">
                    <div className="d-flex align-items-center gap-2" style={{ gap: '10px' }}>
                      {item.tutorAvatarUrl ? (
                        <img
                          src={resolveAssetUrl(item.tutorAvatarUrl)}
                          alt={item.tutorName}
                          style={{ width: 48, height: 48, borderRadius: '50%', objectFit: 'cover' }}
                          onError={(e) => {
                            e.target.style.display = 'none';
                          }}
                        />
                      ) : (
                        <div
                          style={{
                            width: 48,
                            height: 48,
                            borderRadius: '50%',
                            background: 'linear-gradient(135deg, #DB303A, #ea580c)',
                            color: '#fff',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            fontWeight: 700,
                            fontSize: 16
                          }}
                        >
                          {item.tutorName ? item.tutorName.charAt(0).toUpperCase() : 'G'}
                        </div>
                      )}
                      <div>
                        <h2 className="h6 fw-bold mb-0 text-body">{item.tutorName}</h2>
                        <span className="small text-danger fw-bold">
                          {formatMoney(item.hourlyFee)} đ/h
                        </span>
                      </div>
                    </div>

                    {/* Huy hiệu trạng thái duyệt */}
                    {item.status === 'Accepted' && (
                      <span
                        className="badge rounded-pill px-2.5 py-1"
                        style={{ backgroundColor: '#dcfce7', color: '#166534', fontWeight: 500, fontSize: 12 }}
                      >
                        Đã được chấp nhận
                      </span>
                    )}
                    {item.status === 'Pending' && (
                      <span className="badge bg-warning-subtle text-warning-emphasis border border-warning-subtle rounded-pill px-2.5 py-1" style={{ fontSize: 12 }}>
                        Chờ phản hồi
                      </span>
                    )}
                    {item.status === 'Rejected' && (
                      <span className="badge bg-secondary-subtle text-secondary border rounded-pill px-2.5 py-1" style={{ fontSize: 12 }}>
                        Đã từ chối
                      </span>
                    )}
                    {item.status === 'Cancelled' && (
                      <span className="badge bg-dark-subtle text-dark border rounded-pill px-2.5 py-1" style={{ fontSize: 12 }}>
                        Đã hủy
                      </span>
                    )}
                  </div>

                  {/* Badges môn học & cấp học */}
                  <div className="mb-2.5 d-flex flex-wrap gap-2" style={{ gap: '8px' }}>
                    <span className="badge bg-body-secondary text-body border" style={{ fontSize: 11, fontWeight: 500 }}>
                      <i className="bi bi-book me-1 text-danger"></i>
                      {item.subjectName} · {item.gradeLevelName}
                    </span>
                    <span className="badge bg-body-secondary text-body border" style={{ fontSize: 11, fontWeight: 500 }}>
                      {item.preferredType === 'OneToOne' || item.preferredType === '0' || item.preferredType === 0 ? '1 kèm 1' : 'Lớp nhóm'}
                    </span>
                  </div>

                  {/* Lớp học */}
                  {item.classroomName && (
                    <div className="small text-secondary mb-2">
                      <i className="bi bi-easel me-1 text-secondary"></i> Lớp: <strong>{item.classroomName}</strong>
                    </div>
                  )}

                  {/* Lời nhắn box */}
                  <div className="bg-body-tertiary p-2.5 rounded-2 small text-secondary mb-3 border">
                    <span className="fw-semibold text-body">Lời nhắn:</span> "{item.message || (item.isTrial ? '[Mục đích: Đăng ký học thử 1 buổi]' : '[Mục đích: Học chính thức]')}"
                  </div>
                </div>

                {/* Chân thẻ: Thời gian gửi & Các nút thao tác */}
                <div className="pt-3 border-top mt-2 d-flex justify-content-between align-items-center small text-secondary">
                  <div className="d-flex align-items-center gap-1.5 text-secondary" style={{ fontSize: 12, gap: '6px' }}>
                    <i className="bi bi-calendar3 text-secondary"></i>
                    <span>{formatDateTime(item.createdAt)}</span>
                  </div>

                  {/* Các nút hành động thao tác */}
                  <div className="card-action-btns d-flex align-items-center" style={{ gap: '10px' }}>
                    <Link
                      to={`/student/booking/${item.tutorId}`}
                      className="btn btn-sm btn-outline-secondary px-2.5 py-1"
                      style={{ minWidth: 36, textAlign: 'center' }}
                      title="Xem hồ sơ gia sư"
                    >
                      <i className="bi bi-eye"></i>
                    </Link>

                    {item.status === 'Accepted' && item.tutorUserId && (
                      <Link
                        to={`/student/chat?contactId=${item.tutorUserId}`}
                        className="btn btn-sm btn-outline-danger px-3 py-1 d-inline-flex align-items-center"
                        style={{ gap: '6px' }}
                        title="Nhắn tin với gia sư"
                      >
                        <i className="bi bi-chat-dots me-1"></i> Nhắn tin
                      </Link>
                    )}

                    {item.status === 'Pending' && (
                      <button
                        type="button"
                        onClick={() => handleCancel(item.id)}
                        className="btn btn-sm btn-outline-danger px-2.5 py-1 d-inline-flex align-items-center"
                        style={{ gap: '6px' }}
                        title="Hủy yêu cầu"
                      >
                        <i className="bi bi-trash me-1"></i> Hủy
                      </button>
                    )}
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
