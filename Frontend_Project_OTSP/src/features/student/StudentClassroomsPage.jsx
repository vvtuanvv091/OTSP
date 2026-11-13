import { useState, useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import studentApi from '../../api/studentApi';
import { useToast } from '../../context/toastHooks';
import { useSignalREvent } from '../../context/signalRHooks';
import ComplaintModal from '../../components/common/ComplaintModal';
import OtspPagination from '../../components/common/OtspPagination';
import '../../styles/student/StudentClassroomsPage.css';

export default function StudentClassroomsPage() {
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [classrooms, setClassrooms] = useState([]);
  const [pendingRequests, setPendingRequests] = useState([]);
  const [statusFilter, setStatusFilter] = useState('Active');
  const [expandedClasses, setExpandedClasses] = useState({});
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalItems, setTotalItems] = useState(0);
  const [counts, setCounts] = useState({ Active: 0, Completed: 0, Cancelled: 0, Pending: 0 });
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [reloadKey, setReloadKey] = useState(0);
  const reviewTimerRef = useRef(null);

  // Tự động làm mới danh sách lớp học theo thời gian thực (Zero-Reload)
  useSignalREvent('ConnectionRequestStatusChanged', () => setReloadKey((k) => k + 1));
  useSignalREvent('ClassSessionStatusChanged', () => setReloadKey((k) => k + 1));
  useSignalREvent('ClassroomCapacityUpdated', () => setReloadKey((k) => k + 1));

  // Trạng thái modal khiếu nại
  const [complaintModalOpen, setComplaintModalOpen] = useState(false);
  const [complaintTarget, setComplaintTarget] = useState({ userId: '', name: '', context: '' });

  // Trạng thái modal đánh giá
  const [reviewModalSession, setReviewModalSession] = useState(null);
  const [reviewScore, setReviewScore] = useState(5);
  const [reviewComment, setReviewComment] = useState('');
  const [submittingReview, setSubmittingReview] = useState(false);
  const [reviewSuccess, setReviewSuccess] = useState('');

  useEffect(() => () => {
    if (reviewTimerRef.current) clearTimeout(reviewTimerRef.current);
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => {
      setPage(1);
      setDebouncedSearch(searchQuery.trim());
    }, 300);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  useEffect(() => {
    let active = true;
    async function loadClassrooms() {
      try {
        setLoading(true);
        setError('');
        if (statusFilter === 'Pending') {
          const res = await studentApi.getMyConnectionRequests('Pending', page, 10, debouncedSearch);
          if (!active) return;
          const list = res.data?.items || [];
          setPendingRequests(list);
          setTotalPages(res.data?.totalPages || 1);
          setTotalItems(res.data?.totalItems || 0);
          setCounts((current) => ({ ...current, Pending: res.data?.totalItems || 0 }));
        } else {
          const res = await studentApi.getClassrooms(statusFilter, page, 10, debouncedSearch);
          if (!active) return;
          if (res.data) {
            setClassrooms(res.data.items || res.data.classrooms || []);
            setTotalPages(res.data.totalPages || 1);
            setTotalItems(res.data.totalClasses || 0);
            setCounts((current) => ({
              Active: res.data.activeCount || 0,
              Completed: res.data.completedCount || 0,
              Cancelled: res.data.cancelledCount || 0,
              Pending: current.Pending
            }));
          }
        }
      } catch (err) {
        if (!active) return;
        setError(err.response?.data?.message || 'Không thể tải danh sách lớp. Vui lòng thử lại.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadClassrooms();
    return () => {
      active = false;
    };
  }, [statusFilter, page, debouncedSearch, reloadKey]);

  const isExpanded = (classId) => {
    if (expandedClasses[classId] !== undefined) {
      return expandedClasses[classId];
    }
    return true; // Mặc định mở rộng để học viên thấy ngay danh sách buổi học
  };

  const toggleExpand = (classId) => {
    setExpandedClasses((prev) => ({
      ...prev,
      [classId]: !isExpanded(classId)
    }));
  };

  const formatSessionTime = (dateStr) => {
    if (!dateStr) return '';
    const vietnamWallTime = String(dateStr).match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/);
    if (vietnamWallTime) {
      return `${vietnamWallTime[4]}:${vietnamWallTime[5]} ${Number(vietnamWallTime[3])}/${Number(vietnamWallTime[2])}/${vietnamWallTime[1].slice(-2)}`;
    }
    const date = new Date(dateStr);
    if (Number.isNaN(date.getTime())) return '';
    return new Intl.DateTimeFormat('vi-VN', {
      timeZone: 'Asia/Ho_Chi_Minh',
      hour: '2-digit',
      minute: '2-digit',
      day: 'numeric',
      month: 'numeric',
      year: '2-digit'
    }).format(date);
  };

  const handleOpenReview = (session, cls) => {
    setReviewModalSession({
      ...session,
      classroomId: cls.classroomId || cls.id,
      classroomName: cls.classroomName || cls.name,
      tutorName: cls.tutorName
    });
    setReviewScore(5);
    setReviewComment('');
    setReviewSuccess('');
  };

  const handleSubmitReview = async (e) => {
    e.preventDefault();
    if (!reviewModalSession) return;
    setSubmittingReview(true);
    try {
      await studentApi.submitReview(reviewModalSession.sessionId, {
        score: reviewScore,
        comment: reviewComment
      });
      setReviewSuccess('Đánh giá buổi học thành công! Cảm ơn bạn.');
      setClassrooms((prev) =>
        prev.map((cls) => {
          const cid = cls.classroomId || cls.id;
          if (cid === (reviewModalSession.classroomId || cls.classroomId)) {
            return {
              ...cls,
              sessions: (cls.sessions || []).map((s) =>
                s.sessionId === reviewModalSession.sessionId
                  ? { ...s, hasReviewed: true, reviewScore }
                  : s
              )
            };
          }
          return cls;
        })
      );
      if (reviewTimerRef.current) clearTimeout(reviewTimerRef.current);
      reviewTimerRef.current = setTimeout(() => {
        setReviewModalSession(null);
        setReviewSuccess('');
      }, 1500);
    } catch {
      toast.error('Không thể gửi đánh giá, vui lòng thử lại.');
    } finally {
      setSubmittingReview(false);
    }
  };

  const currentItemCount = statusFilter === 'Pending' ? pendingRequests.length : classrooms.length;

  return (
    <div className="student-classroom-shell py-4 px-3 px-lg-4" style={{ maxWidth: 1400, margin: '0 auto', width: '100%' }}>
      {/* Tiêu đề trang & Tab lọc trạng thái lớp học */}
      <div className="d-flex align-items-center justify-content-between flex-wrap gap-3 mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1 text-body">Lớp của tôi</h1>
          <p className="text-secondary small mb-0">Khu học tập và theo dõi lịch lớp học của học viên/phụ huynh</p>
        </div>

        <div className="nav nav-pills gap-2 p-1 bg-body-tertiary border student-classroom-filters" role="tablist">
          {[
            { key: 'Active', label: 'Đang học', icon: 'bi-play-circle' },
            { key: 'Completed', label: 'Đã hoàn thành', icon: 'bi-check-circle' },
            { key: 'Pending', label: 'Chờ xác nhận', icon: 'bi-hourglass-split' },
            { key: 'Cancelled', label: 'Đã hủy', icon: 'bi-x-circle' }
          ].map((filter) => (
            <button
              key={filter.key}
              type="button"
              className={`nav-link px-3 py-2 small fw-semibold d-flex align-items-center gap-2 ${statusFilter === filter.key ? 'active bg-danger text-white' : 'text-body'}`}
              onClick={() => {
                setPage(1);
                setStatusFilter(filter.key);
              }}
            >
              <i className={`bi ${filter.icon}`}></i>
              <span>{filter.label}</span>
              <span className={`badge ${statusFilter === filter.key ? 'bg-white text-danger' : 'bg-secondary text-white'} rounded-pill`}>
                {counts[filter.key]}
              </span>
            </button>
          ))}
        </div>
      </div>

      <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-3">
        <div className="input-group student-classroom-search">
          <span className="input-group-text bg-body"><i className="bi bi-search" aria-hidden="true"></i></span>
          <input
            type="search"
            className="form-control"
            placeholder="Tìm theo tên lớp, gia sư, môn học..."
            aria-label="Tìm lớp học"
            value={searchQuery}
            onChange={(event) => setSearchQuery(event.target.value)}
          />
        </div>
        <span className="small text-secondary">Tổng cộng: <strong>{totalItems}</strong></span>
      </div>

      {message && (
        <div className="alert alert-success alert-dismissible fade show d-flex align-items-center mb-3" role="alert">
          <i className="bi bi-check-circle-fill me-2 fs-5"></i>
          <div>{message}</div>
          <button type="button" className="btn-close" onClick={() => setMessage('')} aria-label="Đóng"></button>
        </div>
      )}

      {/* Danh sách lớp học */}
      {loading ? (
        <div className="text-center py-5">
          <div className="spinner-border text-danger" role="status"></div>
          <div className="mt-2 text-secondary small">Đang tải danh sách lớp học...</div>
        </div>
      ) : error ? (
        <div className="alert alert-danger d-flex align-items-center justify-content-between gap-3" role="alert">
          <span>{error}</span>
          <button type="button" className="btn btn-outline-danger btn-sm" onClick={() => setReloadKey((k) => k + 1)}>
            Thử lại
          </button>
        </div>
      ) : currentItemCount === 0 ? (
        <div className="card border shadow-sm rounded-3 text-center py-5 bg-body">
          <div className="card-body">
            <i className="bi bi-mortarboard fs-1 text-secondary opacity-50 mb-3 d-block"></i>
            <h2 className="h5 fw-bold text-body">
              {debouncedSearch ? 'Không tìm thấy kết quả phù hợp' : statusFilter === 'Pending' ? 'Không có yêu cầu nào đang chờ xác nhận' : 'Bạn chưa có lớp học nào trong mục này'}
            </h2>
            <p className="text-secondary small mb-3">
              {debouncedSearch ? 'Thử từ khóa khác hoặc xóa nội dung tìm kiếm.' : 'Các lớp học sau khi kết nối gia sư thành công và bắt đầu học sẽ hiển thị tại đây.'}
            </p>
            {debouncedSearch ? (
              <button type="button" className="btn btn-sm btn-outline-secondary" onClick={() => setSearchQuery('')}>Xóa tìm kiếm</button>
            ) : statusFilter !== 'Pending' ? (
              <Link to="/search" className="btn btn-sm btn-danger px-3.5 py-2 fw-semibold">
                <i className="bi bi-search me-1"></i> Tìm kiếm gia sư
              </Link>
            ) : null}
          </div>
        </div>
      ) : statusFilter === 'Pending' ? (
        pendingRequests.map((request) => (
          <div className="card border shadow-sm mb-3 bg-body" key={request.id}>
            <div className="card-body d-flex flex-wrap align-items-center justify-content-between gap-3">
              <div>
                <h2 className="h6 fw-bold mb-1">{request.tutorName} · {request.subjectName}</h2>
                <div className="small text-secondary">{request.gradeLevelName} · {request.preferredType === 'OneToOne' ? 'Lớp 1-1' : 'Lớp nhóm'}</div>
                {request.message && <p className="small mb-0 mt-2">{request.message}</p>}
              </div>
              <div className="d-flex align-items-center gap-2">
                <span className="badge text-bg-warning">Đang chờ xác nhận</span>
                {request.tutorUserId && (
                  <Link to={`/student/chat?contactId=${request.tutorUserId}`} className="btn btn-sm btn-outline-primary">
                    <i className="bi bi-chat-dots me-1"></i>Nhắn tin
                  </Link>
                )}
              </div>
            </div>
          </div>
        ))
      ) : (
        classrooms.map((cls) => {
          const classId = cls.classroomId || cls.id;
          const expanded = isExpanded(classId);
          const sessions = cls.sessions || [];
          const totalSessions = cls.expectedTotalSessions || cls.totalSessionsCount || sessions.length;
          const doneCount = sessions.filter(
            (s) => s.status === 'Done' || s.status === 1
          ).length;
          const hasLive = sessions.some(
            (s) => (s.status === 'InProgress' || s.status === 3) && s.canJoin
          );
          const progressPercent = totalSessions ? Math.round((doneCount / totalSessions) * 100) : 0;
          const isGroup = cls.type === 1 || cls.type === 'Group';

          return (
            <div
              key={classId}
              className="card border shadow-sm rounded-4 mb-4 overflow-hidden bg-white"
            >
              {/* Row 1: Header info (Tên gia sư, Môn, Tên lớp, Badge loại lớp) */}
              <div className="p-3.5 px-4 border-bottom bg-light bg-opacity-40">
                <div className="d-flex flex-wrap justify-content-between align-items-center gap-3">
                  <div>
                    <div className="d-flex align-items-center gap-2 flex-wrap mb-1">
                      <h2 className="h5 fw-bold mb-0 text-dark">
                        {cls.tutorName} · {cls.subjectName}
                      </h2>
                      {(() => {
                        const isGroup = cls.type === 1 || cls.type === 'Group';
                        const groupName = cls.classroomName?.trim() || (isGroup ? `Nhóm ${cls.subjectName}` : null);
                        return groupName ? (
                          <span className="badge bg-danger-subtle text-danger border border-danger-subtle px-2.5 py-1 rounded-pill fw-semibold">
                            <i className="bi bi-collection-play me-1"></i>
                            {groupName}
                          </span>
                        ) : null;
                      })()}
                    </div>
                    <div className="text-secondary small d-flex align-items-center gap-2 flex-wrap">
                      <span><i className="bi bi-person-badge text-danger me-1"></i>Gia sư: <strong>{cls.tutorName}</strong></span>
                      <span>•</span>
                      <span><i className="bi bi-book text-primary me-1"></i>Môn: <strong>{cls.subjectName}</strong></span>
                      {(cls.startDate || cls.endDate) && (
                        <>
                          <span>•</span>
                          <span style={{ fontVariantNumeric: 'tabular-nums' }}>
                            <i className="bi bi-calendar-range text-danger me-1"></i>
                            {cls.startDate ? String(cls.startDate).slice(0, 10).split('-').reverse().join('/') : '—'}
                            {' → '}
                            {cls.endDate ? String(cls.endDate).slice(0, 10).split('-').reverse().join('/') : '—'}
                          </span>
                        </>
                      )}
                      {cls.registrationDeadline && (cls.type === 1 || cls.type === 'Group') && (
                        <>
                          <span>•</span>
                          <span style={{ fontVariantNumeric: 'tabular-nums' }}>
                            Hạn ĐK: <strong>{String(cls.registrationDeadline).slice(0, 10).split('-').reverse().join('/')}</strong>
                          </span>
                        </>
                      )}
                    </div>
                  </div>

                  <div>
                    <span
                      className="badge px-3 py-1.5 rounded-pill shadow-xs"
                      style={{
                        backgroundColor: cls.type === 0 || cls.type === 'OneToOne' ? '#eff6ff' : '#fef2f2',
                        color: cls.type === 0 || cls.type === 'OneToOne' ? '#1d4ed8' : '#b91c1c',
                        border: `1px solid ${cls.type === 0 || cls.type === 'OneToOne' ? '#bfdbfe' : '#fecaca'}`,
                        fontSize: '0.84rem',
                        fontWeight: 600
                      }}
                    >
                      <i className={`bi ${cls.type === 0 || cls.type === 'OneToOne' ? 'bi-person' : 'bi-people-fill'} me-1`}></i>
                      {cls.type === 0 || cls.type === 'OneToOne' ? 'Lớp 1-1' : 'Lớp nhóm'} · {cls.gradeLevelName}
                    </span>
                  </div>
                </div>
              </div>

              {/* Biểu ngữ nhắc nhở học phí chưa thanh toán */}
              {!cls.hasPaid && cls.status === 'Active' && (
                <div className="alert alert-warning py-2.5 px-4 mx-4 mt-3 mb-0 d-flex align-items-center justify-content-between flex-wrap gap-2 rounded-3 border">
                  <div className="small">
                    <i className="bi bi-exclamation-triangle-fill text-warning me-1.5"></i>
                    <strong>Chưa thanh toán học phí:</strong> Vui lòng đóng học phí để hoàn tất kích hoạt lớp học.
                  </div>
                  {cls.pendingPaymentId && (
                    <Link
                      to={`/student/payments/${cls.pendingPaymentId || ''}/checkout`}
                      className="btn btn-sm btn-danger py-1.5 px-3 rounded-pill fw-medium"
                    >
                      <i className="bi bi-credit-card me-1"></i>Thanh toán ngay
                    </Link>
                  )}
                </div>
              )}

              {/* Row 2: Tiến độ học tập & Nhóm nút hành động (Dãn ra xuống hàng riêng, cách viền thông thoáng) */}
              <div className="px-4 py-3 d-flex flex-wrap justify-content-between align-items-center gap-3">
                <div className="d-flex align-items-center gap-3 flex-wrap">
                  <div>
                    <span className="small fw-semibold text-dark d-inline-flex align-items-center gap-1.5">
                      <i className="bi bi-calendar-check text-danger fs-6"></i>
                      Lịch học: <strong className="text-danger">{doneCount}/{totalSessions}</strong> buổi hoàn thành
                    </span>
                    {totalSessions > 0 && (
                      <div className="student-class-progress" style={{ width: 140 }} title={`${progressPercent}% hoàn thành`}>
                        <div className="student-class-progress-value" style={{ width: `${progressPercent}%` }}></div>
                      </div>
                    )}
                  </div>
                  <span
                    className="badge text-white d-inline-flex align-items-center gap-1.5 px-3 py-1.5 rounded-pill shadow-xs"
                    style={{
                      backgroundColor: hasLive
                        ? '#DB303A'
                        : cls.status === 'Completed'
                        ? '#166534'
                        : cls.status === 'Cancelled'
                        ? '#64748b'
                        : '#0284c7',
                      fontSize: '0.78rem'
                    }}
                  >
                    {(hasLive || cls.status === 'Active') && (
                      <span className="live-dot" style={{ width: 6, height: 6, backgroundColor: '#fff', borderRadius: '50%' }}></span>
                    )}
                    {hasLive
                      ? 'Đang diễn ra phòng học'
                      : cls.status === 'Completed'
                      ? 'Đã hoàn thành'
                      : cls.status === 'Cancelled'
                      ? 'Đã hủy'
                      : 'Đang học'}
                  </span>
                </div>

                  <div className="d-flex align-items-center gap-2 flex-wrap">
                  {isGroup && cls.status !== 'Cancelled' && (
                    <Link
                      to={`/student/chat?contactId=class_${classId}&contactName=${encodeURIComponent(cls.classroomName?.trim() || `Nhóm ${cls.subjectName}`)}`}
                      className="btn btn-sm btn-danger px-3 py-1.5 rounded-pill d-inline-flex align-items-center gap-1.5 shadow-xs fw-medium"
                      style={{ fontSize: '0.84rem', backgroundColor: '#DB303A' }}
                      title="Trò chuyện nhóm cùng gia sư và các bạn trong lớp"
                    >
                      <i className="bi bi-people-fill"></i>Chat nhóm lớp
                    </Link>
                  )}

                  {cls.tutorUserId && (
                    <Link
                      to={`/student/chat?contactId=${cls.tutorUserId}`}
                      className="btn btn-sm btn-outline-danger px-3 py-1.5 rounded-pill d-inline-flex align-items-center gap-1.5 shadow-xs fw-medium"
                      style={{ fontSize: '0.84rem' }}
                      title="Nhắn tin riêng cho gia sư"
                    >
                      <i className="bi bi-chat-dots"></i>Nhắn tin
                    </Link>
                  )}

                  <Link
                    to={`/student/documents?classroomId=${classId}`}
                    className="btn btn-sm btn-outline-secondary px-3 py-1.5 rounded-pill d-inline-flex align-items-center gap-1.5 shadow-xs fw-medium"
                    style={{ fontSize: '0.84rem' }}
                    title="Xem tài liệu lớp học này"
                  >
                    <i className="bi bi-folder2-open"></i>Tài liệu lớp
                  </Link>

                  <button
                    type="button"
                    className="btn btn-sm btn-light border px-3 py-1.5 rounded-pill d-inline-flex align-items-center gap-1.5 shadow-xs fw-medium text-secondary"
                    onClick={() => toggleExpand(classId)}
                    style={{ fontSize: '0.84rem' }}
                  >
                    <span>{expanded ? 'Thu gọn lịch' : 'Xem chi tiết lịch học'}</span>
                    <i className={`bi ${expanded ? 'bi-chevron-up' : 'bi-chevron-down'}`}></i>
                  </button>

                  <button
                    type="button"
                    className="btn btn-sm btn-light border px-2.5 py-1.5 rounded-pill d-inline-flex align-items-center gap-1 shadow-xs fw-medium text-danger"
                    style={{ fontSize: '0.84rem' }}
                    title="Khiếu nại hoặc phản ánh lớp học này"
                    onClick={() => {
                      setComplaintTarget({
                        userId: cls.tutorUserId || '',
                        name: `Gia sư ${cls.tutorName} (${cls.subjectName})`,
                        context: `Lớp học: ${cls.classroomName || cls.subjectName} (#${classId})`,
                      });
                      setComplaintModalOpen(true);
                    }}
                  >
                    <i className="bi bi-flag"></i> Khiếu nại
                  </button>
                </div>
              </div>

              {/* Danh sách các buổi học trong lớp (có thể mở rộng / thu gọn) */}
              <div className={`session-list border-top ${expanded ? 'is-open' : 'is-closed'}`} aria-hidden={!expanded}>
                  {sessions.length === 0 ? (
                    <div className="p-4 text-center text-muted small">
                      <em>Chưa có buổi học nào được lên lịch cho lớp này.</em>
                    </div>
                  ) : (
                    sessions.map((s) => {
                      const timeDisplay = formatSessionTime(s.scheduledAtVn || s.scheduledAt);
                      const isCancelled = s.status === 'Cancelled' || s.status === 2;
                      const isLive = s.status === 'InProgress' || s.status === 3;
                      const isDone = !isCancelled && (s.status === 'Done' || s.status === 1);

                      return (
                        <div
                          key={s.sessionId}
                          className="session-row p-3 px-4 d-flex align-items-center justify-content-between border-bottom"
                          style={{ transition: 'background-color 0.15s ease' }}
                        >
                          <div className="d-flex align-items-center flex-wrap gap-2">
                            <span className="session-label fw-semibold text-body" style={{ fontSize: 14 }}>
                              Buổi {s.index}: {timeDisplay}
                            </span>

                            {isDone ? (
                              <span
                                className="badge text-white px-2 py-0.5 rounded"
                                style={{ backgroundColor: '#166534', fontSize: 11, fontWeight: 600 }}
                              >
                                Đã học
                              </span>
                            ) : isCancelled ? (
                              <span className="badge text-bg-dark">Đã hủy</span>
                            ) : isLive ? (
                              <span
                                className="badge bg-primary text-white d-inline-flex align-items-center gap-1 px-2 py-0.5 rounded"
                                style={{ fontSize: 11, fontWeight: 600 }}
                              >
                                <span className="live-dot"></span>Đang diễn ra
                              </span>
                            ) : (
                              <span
                                className="badge text-white px-2 py-0.5 rounded"
                                style={{ backgroundColor: '#475569', fontSize: 11, fontWeight: 600 }}
                              >
                                Sắp tới
                              </span>
                            )}
                          </div>

                          <div className="session-action">
                            {isDone ? (
                              s.hasReviewed ? (
                                <button
                                  type="button"
                                  className="btn btn-sm btn-outline-success px-3"
                                  onClick={() => handleOpenReview(s, cls)}
                                >
                                  <i className="bi bi-star-fill text-warning me-1"></i>
                                  Đã đánh giá{s.reviewScore > 0 ? ` (${s.reviewScore}★)` : ''}
                                </button>
                              ) : (
                                <button
                                  type="button"
                                  className="btn btn-sm btn-danger px-3 py-1 fw-semibold"
                                  onClick={() => handleOpenReview(s, cls)}
                                >
                                  Đánh giá
                                </button>
                              )
                            ) : s.canJoin && !isCancelled ? (
                              <Link
                                to={`/room/${s.sessionId}`}
                                className="btn btn-sm btn-danger px-3.5 py-1.5 fw-semibold d-inline-flex align-items-center gap-1 shadow-sm"
                              >
                                <i className="bi bi-camera-video me-1"></i>Vào lớp
                              </Link>
                            ) : (
                              <button
                                type="button"
                                className="btn btn-sm"
                                disabled
                                style={{
                                  border: '1px solid #cbd5e1',
                                  background: '#f8fafc',
                                  color: '#64748b',
                                  borderRadius: 6,
                                  padding: '5px 16px',
                                  fontSize: 13,
                                  fontWeight: 500,
                                  opacity: 0.85,
                                  cursor: 'not-allowed'
                                }}
                              >
                                Chưa mở lớp
                              </button>
                            )}
                          </div>
                        </div>
                      );
                    })
                  )}
              </div>
            </div>
          );
        })
      )}

      {!loading && !error && totalItems > 0 && (
        <OtspPagination
          page={page}
          totalPages={totalPages}
          total={totalItems}
          pageSize={10}
          onChange={setPage}
          className="my-4 rounded-3 shadow-sm border"
        />
      )}

      {/* Modal gửi đánh giá buổi học */}
      {reviewModalSession && (
        <div className="modal fade show d-block" tabIndex="-1" style={{ backgroundColor: 'rgba(0,0,0,0.5)' }}>
          <div className="modal-dialog modal-dialog-centered">
            <div className="modal-content border-0 shadow">
              <form onSubmit={handleSubmitReview}>
                <div className="modal-header border-bottom">
                  <h5 className="modal-title fw-bold text-body">
                    <i className="bi bi-star-fill text-warning me-2"></i>
                    Đánh giá buổi học #{reviewModalSession.index}
                  </h5>
                  <button
                    type="button"
                    className="btn-close"
                    onClick={() => setReviewModalSession(null)}
                  ></button>
                </div>
                <div className="modal-body p-4">
                  {reviewSuccess && (
                    <div className="alert alert-success d-flex align-items-center mb-3">
                      <i className="bi bi-check-circle-fill me-2 fs-5"></i>
                      <div>{reviewSuccess}</div>
                    </div>
                  )}

                  <div className="mb-3">
                    <div className="small text-secondary mb-1">Lớp học:</div>
                    <div className="fw-bold text-body">{reviewModalSession.classroomName}</div>
                    <div className="small text-secondary">Gia sư: {reviewModalSession.tutorName}</div>
                  </div>

                  <div className="mb-3 text-center py-2 bg-light rounded-3">
                    <label className="form-label d-block small fw-semibold text-secondary mb-2">
                      Mức độ hài lòng của bạn
                    </label>
                    <div className="d-flex justify-content-center gap-2 fs-3">
                      {[1, 2, 3, 4, 5].map((star) => (
                        <i
                          key={star}
                          className={`bi ${star <= reviewScore ? 'bi-star-fill text-warning' : 'bi-star text-muted'} cursor-pointer`}
                          style={{ cursor: 'pointer' }}
                          onClick={() => setReviewScore(star)}
                        ></i>
                      ))}
                    </div>
                    <div className="small fw-semibold text-danger mt-1">
                      {reviewScore === 5 && 'Tuyệt vời, rất hài lòng (5/5)'}
                      {reviewScore === 4 && 'Hài lòng (4/5)'}
                      {reviewScore === 3 && 'Bình thường (3/5)'}
                      {reviewScore === 2 && 'Chưa hài lòng (2/5)'}
                      {reviewScore === 1 && 'Rất tệ (1/5)'}
                    </div>
                  </div>

                  <div className="mb-3">
                    <label className="form-label small fw-semibold text-body">Nhận xét chi tiết (tùy chọn)</label>
                    <textarea
                      className="form-control form-control-sm"
                      rows="3"
                      placeholder="Chia sẻ cảm nhận về phương pháp dạy, kiến thức buổi học..."
                      value={reviewComment}
                      onChange={(e) => setReviewComment(e.target.value)}
                    ></textarea>
                  </div>
                </div>
                <div className="modal-footer border-top">
                  <button
                    type="button"
                    className="btn btn-sm btn-outline-secondary px-3"
                    onClick={() => setReviewModalSession(null)}
                  >
                    Đóng
                  </button>
                  <button
                    type="submit"
                    disabled={submittingReview}
                    className="btn btn-sm btn-danger px-4 fw-semibold"
                  >
                    {submittingReview ? 'Đang gửi...' : 'Gửi đánh giá'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* Modal gửi khiếu nại lớp học */}
      <ComplaintModal
        isOpen={complaintModalOpen}
        onClose={() => setComplaintModalOpen(false)}
        defaultTargetUserId={complaintTarget.userId}
        defaultTargetName={complaintTarget.name}
        contextInfo={complaintTarget.context}
        onSuccess={() => setMessage('Khiếu nại của bạn đã được gửi thành công tới Ban Quản Trị!')}
      />

    </div>
  );
}
