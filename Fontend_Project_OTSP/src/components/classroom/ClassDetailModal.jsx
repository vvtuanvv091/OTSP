import { useState, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import commonApi from '../../api/commonApi';
import studentApi from '../../api/studentApi';
import { resolveAssetUrl } from '../../api/config';

const formatPrice = (val) => new Intl.NumberFormat('vi-VN').format(val || 0);

export default function ClassDetailModal({ classId, initialData = null, onClose, onRegistered }) {
  const { user, isStudent } = useAuth();
  const navigate = useNavigate();

  const [classData, setClassData] = useState(initialData);
  const [loading, setLoading] = useState(Boolean(classId) && (!initialData || !initialData.weeklySchedules));
  const [errorMsg, setErrorMsg] = useState('');
  const [successMsg, setSuccessMsg] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState('');

  const [prevClassId, setPrevClassId] = useState(classId);
  if (prevClassId !== classId) {
    setPrevClassId(classId);
    setClassData(initialData);
    setLoading(Boolean(classId) && (!initialData || !initialData.weeklySchedules));
    setErrorMsg('');
    setSuccessMsg('');
  }

  useEffect(() => {
    if (!classId) return undefined;
    let isMounted = true;

    commonApi.getClassDetails(classId)
      .then((res) => {
        if (isMounted && res.data) {
          setClassData(res.data);
        }
      })
      .catch((err) => {
        console.warn('Could not fetch class details from API:', err);
      })
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [classId]);

  if (!classId && !initialData) return null;

  const c = classData || initialData || {};
  const isStudentRole = isStudent || user?.role === 'Student_Parent' || user?.role === 'Student';
  const isEnrolled = Boolean(c.isEnrolled);
  const isPending = Boolean(c.isPending);
  const studentCount = Number(c.studentCount) || 0;
  const maxStudents = Number(c.maxStudents) || 0;
  const isFull = maxStudents > 0 && studentCount >= maxStudents;
  const percentFilled = maxStudents > 0 ? Math.min(100, Math.round((studentCount / maxStudents) * 100)) : 0;
  const tutorRating = Number(c.tutor?.rating ?? c.tutor?.averageRating ?? c.tutorRating ?? 0);
  const tutorContactId = c.tutor?.userId || c.tutorUserId || '';

  const handleEnrollSubmit = async (e) => {
    e.preventDefault();
    if (!user) {
      navigate('/login');
      return;
    }
    if (!isStudentRole) {
      setErrorMsg('Chỉ tài khoản Học viên / Phụ huynh mới có thể đăng ký lớp học.');
      return;
    }

    try {
      setSubmitting(true);
      setErrorMsg('');

      const payload = {
        tutorId: c.tutor?.id || c.tutorId,
        subjectId: c.subjectId,
        gradeLevelId: c.gradeLevelId,
        preferredType: 1, // Lớp học nhóm
        isTrial: false,
        classroomId: c.id,
        message: message.trim() || 'Em muốn đăng ký tham gia lớp học nhóm này.'
      };

      const res = await studentApi.sendConnectionRequest(payload);
      if (res.data?.success) {
        setSuccessMsg(res.data.message || 'Đã gửi yêu cầu đăng ký lớp học thành công! Gia sư sẽ sớm duyệt yêu cầu.');
        setClassData((prev) => ({ ...prev, isPending: true }));
        if (onRegistered) onRegistered(c.id);
      } else {
        setErrorMsg(res.data?.message || 'Không thể gửi yêu cầu đăng ký.');
      }
    } catch (err) {
      setErrorMsg(err?.response?.data?.message || 'Có lỗi xảy ra khi gửi yêu cầu đăng ký.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div
      className="modal fade show d-block"
      tabIndex="-1"
      style={{ backgroundColor: 'rgba(15, 23, 42, 0.65)', backdropFilter: 'blur(3px)', zIndex: 1055 }}
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div className="modal-dialog modal-dialog-centered modal-lg modal-dialog-scrollable">
        <div className="modal-content border-0 shadow-lg rounded-4 overflow-hidden">
          {/* Header */}
          <div className="modal-header border-bottom px-4 py-3 bg-light d-flex align-items-center justify-content-between">
            <div className="d-flex align-items-center gap-2">
              <span
                style={{
                  width: 38,
                  height: 38,
                  borderRadius: 10,
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  backgroundColor: 'rgba(219, 48, 58, 0.12)',
                  color: '#DB303A'
                }}
              >
                <i className="bi bi-mortarboard-fill fs-5"></i>
              </span>
              <div>
                <h5 className="modal-title fw-bold text-body mb-0">Chi tiết lớp học &amp; Đăng ký</h5>
                <small className="text-secondary">Thông tin lớp học chuẩn hóa OTSP</small>
              </div>
            </div>
            <button
              type="button"
              className="btn-close"
              aria-label="Đóng"
              onClick={onClose}
            ></button>
          </div>

          {/* Thân Modal */}
          <div className="modal-body p-4">
            {loading ? (
              <div className="text-center py-5">
                <div className="spinner-border text-danger" role="status"></div>
                <div className="mt-2 text-secondary small">Đang tải thông tin lớp học...</div>
              </div>
            ) : (
              <>
                {/* Thông báo lỗi & thành công */}
                {errorMsg && (
                  <div className="alert alert-danger py-2 px-3 small rounded-3 mb-3 d-flex align-items-center gap-2">
                    <i className="bi bi-exclamation-triangle-fill flex-shrink-0"></i>
                    <span>{errorMsg}</span>
                  </div>
                )}
                {successMsg && (
                  <div className="alert alert-success py-2 px-3 small rounded-3 mb-3 d-flex align-items-center gap-2">
                    <i className="bi bi-check-circle-fill flex-shrink-0 text-success"></i>
                    <span>{successMsg}</span>
                  </div>
                )}

                {/* Thông tin chính về lớp học */}
                <div className="d-flex flex-wrap gap-2 mb-2">
                  <span className="badge bg-danger-subtle text-danger border border-danger-subtle rounded-pill px-3 py-1.5 fw-semibold">
                    {c.subjectName} · {c.gradeName}
                  </span>
                  <span className="badge bg-secondary-subtle text-secondary rounded-pill px-3 py-1.5">
                    {c.type === 'Group' || c.type === 1 || !c.type ? 'Lớp nhóm tuyển sinh' : 'Lớp kèm 1-1'}
                  </span>
                  {isEnrolled && (
                    <span className="badge bg-success text-white rounded-pill px-3 py-1.5 fw-semibold">
                      <i className="bi bi-check-circle me-1"></i>Bạn đang theo học
                    </span>
                  )}
                  {isPending && !isEnrolled && (
                    <span className="badge bg-warning text-dark rounded-pill px-3 py-1.5 fw-semibold">
                      <i className="bi bi-hourglass-split me-1"></i>Đang chờ gia sư duyệt
                    </span>
                  )}
                </div>

                <h3 className="fw-bold text-body mb-2">{c.name}</h3>

                <p className="text-secondary small mb-4">
                  {c.description || 'Gia sư chưa cập nhật mô tả chi tiết cho lớp học này.'}
                </p>

                {/* Key Metrics Grid */}
                <div className="row g-3 mb-4">
                  <div className="col-6 col-md-3">
                    <div className="p-3 bg-light rounded-3 border text-center h-100">
                      <small className="text-secondary d-block mb-1">Học phí khóa</small>
                      <strong className="text-danger fs-5 d-block">{formatPrice(c.fee)} đ</strong>
                    </div>
                  </div>
                  <div className="col-6 col-md-3">
                    <div className="p-3 bg-light rounded-3 border text-center h-100">
                      <small className="text-secondary d-block mb-1">Sĩ số lớp</small>
                      <strong className="text-body fs-5 d-block">
                        {maxStudents > 0 ? `${studentCount} / ${maxStudents}` : `${studentCount} học viên`}
                      </strong>
                      {maxStudents > 0 && (
                        <div className="progress mt-2" style={{ height: 6 }}>
                          <div
                            className={`progress-bar ${isFull ? 'bg-danger' : 'bg-success'}`}
                            role="progressbar"
                            style={{ width: `${percentFilled}%` }}
                          ></div>
                        </div>
                      )}
                    </div>
                  </div>
                  <div className="col-6 col-md-3">
                    <div className="p-3 bg-light rounded-3 border text-center h-100">
                      <small className="text-secondary d-block mb-1">Số buổi / tuần</small>
                      <strong className="text-body fs-5 d-block">
                        {c.sessionsPerWeek
                          ? `${c.sessionsPerWeek} buổi`
                          : c.weeklySchedules?.length
                            ? `${c.weeklySchedules.length} buổi`
                            : 'Linh hoạt'}
                      </strong>
                      <small className="text-muted" style={{ fontSize: '0.72rem' }}>
                        {c.hoursPerSession ? `~${c.hoursPerSession} giờ / buổi` : 'Theo lịch lớp'}
                      </small>
                    </div>
                  </div>
                  <div className="col-6 col-md-3">
                    <div className="p-3 bg-light rounded-3 border text-center h-100">
                      <small className="text-secondary d-block mb-1">Khai giảng</small>
                      <strong className="text-body fs-6 d-block mt-1">
                        {c.startDate ? c.startDate : 'Nhận học viên ngay'}
                      </strong>
                    </div>
                  </div>
                </div>

                {/* Lịch học hàng tuần */}
                <div className="mb-4 p-3 bg-light rounded-4 border">
                  <h6 className="fw-bold text-body mb-2 d-flex align-items-center gap-2">
                    <i className="bi bi-calendar3 text-danger"></i>Lịch học cố định trong tuần
                  </h6>
                  {c.weeklySchedules && c.weeklySchedules.length > 0 ? (
                    <div className="d-flex flex-wrap gap-2">
                      {c.weeklySchedules.map((ws, i) => (
                        <div
                          key={i}
                          className="px-3 py-1.5 bg-white border rounded-pill small fw-semibold text-body shadow-xs d-flex align-items-center gap-1.5"
                        >
                          <span className="badge bg-danger text-white rounded-circle p-1" style={{ fontSize: '0.65rem' }}>
                            {ws.dayOfWeekName || `Thứ ${ws.dayOfWeek + 1}`}
                          </span>
                          <span className="text-secondary">{ws.timeRange}</span>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <p className="text-muted small mb-0">Lịch học linh hoạt, gia sư sẽ thống nhất cùng nhóm học viên sau khi mở lớp.</p>
                  )}
                </div>

                {/* Thẻ thông tin gia sư phụ trách */}
                <div className="card p-3 rounded-4 border mb-4 shadow-xs" style={{ backgroundColor: '#fff' }}>
                  <div className="d-flex align-items-center justify-content-between flex-wrap gap-3">
                    <div className="d-flex align-items-center gap-3">
                      <img
                        src={resolveAssetUrl(c.tutor?.avatarUrl || c.tutorAvatarUrl) || '/assets/img/default-avatar.svg'}
                        alt={c.tutor?.fullName || c.tutorName}
                        className="rounded-circle border"
                        style={{ width: 54, height: 54, objectFit: 'cover' }}
                        onError={(e) => { e.target.src = '/assets/img/default-avatar.svg'; }}
                      />
                      <div>
                        <div className="small text-secondary">Gia sư phụ trách</div>
                        <h6 className="fw-bold mb-0 text-body">{c.tutor?.fullName || c.tutorName}</h6>
                        <div className="small text-warning fw-semibold mt-0.5">
                          {tutorRating > 0 ? (
                            <>
                              <i className="bi bi-star-fill me-1"></i>
                              {tutorRating.toFixed(1)}
                            </>
                          ) : (
                            <span className="text-secondary fw-normal">Chưa có đánh giá</span>
                          )}
                          <span className="text-secondary fw-normal ms-2">• Hồ sơ xác minh</span>
                        </div>
                      </div>
                    </div>

                    <div className="d-flex align-items-center gap-2">
                      {tutorContactId && (
                        <Link
                          to={`/chat?contactId=${tutorContactId}&classroomId=${c.id || ''}&classTitle=${encodeURIComponent(c.nameClass || c.title || '')}&inquiry=1`}
                          className="btn btn-sm btn-outline-danger rounded-pill px-3 py-1.5 fw-semibold"
                          onClick={onClose}
                        >
                          <i className="bi bi-chat-dots-fill me-1.5"></i>Nhắn tin gia sư
                        </Link>
                      )}
                      <Link
                        to={`/student/booking/${c.tutor?.id || c.tutorId}`}
                        className="btn btn-sm btn-light border rounded-pill px-3 py-1.5 text-secondary"
                        onClick={onClose}
                      >
                        Xem hồ sơ
                      </Link>
                    </div>
                  </div>
                </div>

                {/* Vùng thao tác đăng ký lớp học */}
                <div className="border-top pt-3">
                  {isEnrolled ? (
                    <div className="p-3 bg-success-subtle text-success border border-success-subtle rounded-3 text-center fw-semibold">
                      <i className="bi bi-check-circle-fill me-2 fs-5"></i>
                      Bạn hiện đang theo học lớp học này rồi!
                    </div>
                  ) : isPending ? (
                    <div className="p-3 bg-warning-subtle text-warning-emphasis border border-warning-subtle rounded-3 text-center fw-semibold">
                      <i className="bi bi-hourglass-split me-2 fs-5"></i>
                      Yêu cầu đăng ký của bạn đang chờ gia sư phản hồi và phê duyệt.
                    </div>
                  ) : isFull ? (
                    <div className="p-3 bg-secondary-subtle text-secondary border rounded-3 text-center fw-semibold">
                      <i className="bi bi-people-fill me-2 fs-5"></i>
                      Lớp học này hiện đã đủ sĩ số học viên (sĩ số tối đa: {c.maxStudents}).
                    </div>
                  ) : !user ? (
                    <div className="p-3 bg-light border rounded-3 text-center">
                      <p className="mb-2 text-secondary small">Vui lòng đăng nhập để gửi yêu cầu đăng ký lớp học này.</p>
                      <button
                        type="button"
                        className="btn btn-danger rounded-pill px-4 py-2 fw-semibold"
                        style={{ backgroundColor: '#DB303A' }}
                        onClick={() => {
                          onClose();
                          navigate('/login');
                        }}
                      >
                        <i className="bi bi-box-arrow-in-right me-1.5"></i>Đăng nhập để đăng ký
                      </button>
                    </div>
                  ) : !isStudentRole ? (
                    <div className="p-3 bg-light border rounded-3 text-center text-secondary small">
                      Tài khoản của bạn là Gia sư / Quản trị viên, không thể gửi yêu cầu tham gia lớp học.
                    </div>
                  ) : (
                    <form onSubmit={handleEnrollSubmit} className="d-flex flex-column gap-3">
                      <div>
                        <label className="form-label small fw-semibold text-body mb-1" htmlFor="enrollMessage">
                          Lời nhắn gửi gia sư (tùy chọn)
                        </label>
                        <input
                          id="enrollMessage"
                          type="text"
                          className="form-control"
                          value={message}
                          onChange={(e) => setMessage(e.target.value)}
                          placeholder="Ví dụ: Em muốn đăng ký lớp học này để nâng cao điểm thi cuối kỳ..."
                        />
                      </div>
                      <div className="d-flex justify-content-end gap-2">
                        <button
                          type="button"
                          className="btn btn-light border rounded-pill px-4 py-2 text-secondary"
                          onClick={onClose}
                          disabled={submitting}
                        >
                          Đóng
                        </button>
                        <button
                          type="submit"
                          className="btn btn-danger rounded-pill px-4 py-2 fw-semibold text-white shadow-sm"
                          style={{ backgroundColor: '#DB303A' }}
                          disabled={submitting}
                        >
                          {submitting ? (
                            <>
                              <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                              Đang gửi yêu cầu...
                            </>
                          ) : (
                            <>
                              <i className="bi bi-send-fill me-2"></i>Xác nhận đăng ký lớp này
                            </>
                          )}
                        </button>
                      </div>
                    </form>
                  )}
                </div>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

