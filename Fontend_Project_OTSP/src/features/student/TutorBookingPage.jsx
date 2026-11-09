import { useState, useEffect, useMemo } from 'react';
import { useParams, useSearchParams, Link } from 'react-router-dom';
import studentApi from '../../api/studentApi';
import { resolveAssetUrl } from '../../api/config';
import { useAuth } from '../../context/AuthContext';

const formatMoney = (val) =>
  new Intl.NumberFormat('vi-VN').format(val || 0);

const formatTeaching = (fmt) => {
  if (fmt === 'Online' || fmt === 0) return 'Trực tuyến';
  if (fmt === 'Offline' || fmt === 1) return 'Trực tiếp / Tại nhà';
  return 'Trực tuyến & Tại nhà';
};

const TIME_BLOCKS = [
  { start: '06:00', end: '08:00', label: '06:00 - 08:00' },
  { start: '08:00', end: '10:00', label: '08:00 - 10:00' },
  { start: '10:00', end: '12:00', label: '10:00 - 12:00' },
  { start: '13:00', end: '15:00', label: '13:00 - 15:00' },
  { start: '15:00', end: '17:00', label: '15:00 - 17:00' },
  { start: '17:00', end: '19:00', label: '17:00 - 19:00' },
  { start: '19:00', end: '21:00', label: '19:00 - 21:00' },
  { start: '21:00', end: '23:00', label: '21:00 - 23:00' }
];

export default function TutorBookingPage() {
  const { tutorId } = useParams();
  const [searchParams] = useSearchParams();
  const { user, isStudent } = useAuth();
  const queryClassroomId = searchParams.get('classroomId');
  const queryType = searchParams.get('type');

  const [loading, setLoading] = useState(true);
  const [data, setData] = useState(null);
  const [errorMsg, setErrorMsg] = useState('');
  const [successMsg, setSuccessMsg] = useState('');
  const [submitting, setSubmitting] = useState(false);

  // Trạng thái biểu mẫu gửi yêu cầu
  const [selectedSubjectKey, setSelectedSubjectKey] = useState(''); // "subjectId_gradeLevelId"
  const [preferredType, setPreferredType] = useState(queryClassroomId || queryType === 'Group' ? 'Group' : 'OneToOne'); // 1 kèm 1 | Lớp nhóm
  const [isTrial, setIsTrial] = useState(true);
  const [selectedClassroomId, setSelectedClassroomId] = useState(queryClassroomId || '');
  const [selectedSlotUtc, setSelectedSlotUtc] = useState('');
  const [selectedSlotText, setSelectedSlotText] = useState('');
  const [allowTutorReschedule, setAllowTutorReschedule] = useState(true);
  const [message, setMessage] = useState('');

  // Trạng thái thời khóa biểu
  const [activeWeekIndex, setActiveWeekIndex] = useState(0);
  const [dayRangeTab, setDayRangeTab] = useState('weekday'); // 'weekday' (5 ngày đầu: T2-T6) | 'weekend' (T7, CN) | 'all' (Cả 7 ngày)

  // Tự động sinh dữ liệu 4 tuần bắt đầu từ tuần hiện tại
  const weeks = useMemo(() => {
    const list = [];
    const now = new Date();
    const dayNames = ['Chủ nhật', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7'];

    for (let w = 0; w < 4; w++) {
      const days = [];
      const weekStartDate = new Date(now);
      weekStartDate.setDate(now.getDate() + w * 7);

      const weekEndDate = new Date(now);
      weekEndDate.setDate(now.getDate() + w * 7 + 6);

      const startStr = `${String(weekStartDate.getDate()).padStart(2, '0')}/${String(weekStartDate.getMonth() + 1).padStart(2, '0')}`;
      const endStr = `${String(weekEndDate.getDate()).padStart(2, '0')}/${String(weekEndDate.getMonth() + 1).padStart(2, '0')}`;

      for (let d = 0; d < 7; d++) {
        const dayDate = new Date(now);
        dayDate.setDate(now.getDate() + w * 7 + d);

        const dayOfWeekNum = dayDate.getDay();
        const dayName = dayNames[dayOfWeekNum];
        const dateStr = `${String(dayDate.getDate()).padStart(2, '0')}/${String(dayDate.getMonth() + 1).padStart(2, '0')}`;
        const fullDateIso = `${dayDate.getFullYear()}-${String(dayDate.getMonth() + 1).padStart(2, '0')}-${String(dayDate.getDate()).padStart(2, '0')}`;

        days.push({
          date: dayDate,
          dayOfWeekNum,
          dayName,
          dateStr,
          fullDateIso,
          label: `${dayName} ${dateStr}`
        });
      }

      list.push({
        weekIndex: w,
        title: `Tuần ${w + 1} (${startStr} - ${endStr})`,
        days
      });
    }
    return list;
  }, []);

  useEffect(() => {
    let isMounted = true;
    async function loadTutor() {
      const requestedTutorId = Number(tutorId);
      if (!Number.isInteger(requestedTutorId) || requestedTutorId <= 0) {
        setErrorMsg('Mã gia sư không hợp lệ.');
        setLoading(false);
        return;
      }

      try {
        setLoading(true);
        setErrorMsg('');
        const res = await studentApi.getTutorDetails(requestedTutorId);
        if (isMounted && res.data) {
          setData(res.data);
          if (res.data.subjects && res.data.subjects.length > 0) {
            const first = res.data.bookingEligibility?.subjects?.find((subject) => subject.canBook)
              || res.data.subjects[0];
            setSelectedSubjectKey(`${first.subjectId}_${first.gradeLevelId}`);
          }
          if (queryClassroomId) {
            setSelectedClassroomId(Number(queryClassroomId));
            setPreferredType('Group');
            const selectedGroup = res.data.bookingEligibility?.groupClassrooms?.find(
              (classroom) => classroom.classroomId === Number(queryClassroomId)
            );
            const selectedGroupSubject = res.data.subjects?.find(
              (subject) => subject.subjectId === selectedGroup?.subjectId
                && subject.gradeLevelId === selectedGroup?.gradeLevelId
            );
            if (selectedGroupSubject) {
              setSelectedSubjectKey(`${selectedGroupSubject.subjectId}_${selectedGroupSubject.gradeLevelId}`);
            }
          } else if (res.data.bookingEligibility?.groupClassrooms?.length > 0) {
            const firstJoinable = res.data.bookingEligibility.groupClassrooms.find((classroom) => classroom.canJoin);
            setSelectedClassroomId(firstJoinable?.classroomId || res.data.bookingEligibility.groupClassrooms[0].classroomId);
          }
        }
      } catch (err) {
        console.warn('API getTutorDetails error:', err);
        if (isMounted) {
          setErrorMsg(err?.response?.data?.message || 'Không thể tải thông tin gia sư.');
        }
      } finally {
        if (isMounted) setLoading(false);
      }
    }
    loadTutor();
    return () => {
      isMounted = false;
    };
  }, [tutorId, queryClassroomId]);

  const tutor = data?.tutor;
  const subjects = data?.subjects || [];
  const qualifications = data?.qualifications || [];
  const availabilities = useMemo(() => data?.availabilities || [], [data?.availabilities]);
  const reviews = data?.reviews || [];
  const classrooms = data?.classrooms || [];
  const availableSlots = useMemo(() => data?.availableSlots || [], [data?.availableSlots]);
  const existingRequest = data?.existingRequest;
  const groupBookingOptions = data?.bookingEligibility?.groupClassrooms || [];

  const groupClasses = groupBookingOptions.length > 0
    ? groupBookingOptions.map((eligibility) => {
      const openClassroom = classrooms.find((classroom) => classroom.id === eligibility.classroomId);
      return {
        ...openClassroom,
        ...eligibility,
        id: eligibility.classroomId,
        name: eligibility.classroomName
      };
    })
    : classrooms
      .filter((classroom) => classroom.type === 1 || classroom.type === 'Group')
      .map((classroom) => ({
        ...classroom,
        canJoin: classroom.canJoin ?? true
      }));

  const selectedSubjectParts = selectedSubjectKey ? selectedSubjectKey.split('_').map(Number) : [];
  const selectedSubjectEligibility = data?.bookingEligibility?.subjects?.find(
    (subject) => subject.subjectId === selectedSubjectParts[0] && subject.gradeLevelId === selectedSubjectParts[1]
  );
  const selectedGroupEligibility = groupBookingOptions.find(
    (classroom) => classroom.classroomId === Number(selectedClassroomId)
  );

  // Tạo bảng tra cứu O(1) cho các slot cụ thể (chỉ tính 1 lần khi availableSlots thay đổi)
  const slotLookupMap = useMemo(() => {
    const map = new Map();
    availableSlots.forEach((slot) => {
      let dateIso = '';
      let slotHour = '';
      if (slot.slotUtc) {
        const d = new Date(slot.slotUtc);
        dateIso = d.toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' });
        slotHour = d.toLocaleTimeString('en-GB', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit' });
      }

      // Giờ bắt đầu chuẩn (ví dụ "17:00 - 19:00" -> "17:00")
      const rangeStart = slot.timeRange ? slot.timeRange.slice(0, 5) : '';
      const startKey = slotHour || rangeStart;

      if (dateIso && startKey) {
        map.set(`${dateIso}_${startKey}`, {
          slotUtc: slot.slotUtc || slot.isoValue,
          displayText: slot.displayText || `${slot.dateLabel || ''} (${slot.timeRange || ''})`,
          timeRange: slot.timeRange
        });
      }
    });
    return map;
  }, [availableSlots]);

  // Tạo bảng tra cứu O(1) cho lịch cố định hàng tuần
  const recurringLookupMap = useMemo(() => {
    const map = new Map();
    availabilities.forEach((av) => {
      const dayNum = Number(av.dayOfWeek);
      const start = av.startTime ? String(av.startTime).slice(0, 5) : (av.timeRange ? String(av.timeRange).slice(0, 5) : '');
      if (start) {
        map.set(`${dayNum}_${start}`, av);
      }
    });
    return map;
  }, [availabilities]);

  // Hàm phụ trợ tìm ô lịch học theo (thứ, ca) - Tốc độ O(1) tức thì
  const getSlotForCell = (day, block) => {
    // 1. Kiểm tra trong danh sách ô lịch cụ thể (O(1))
    const found = slotLookupMap.get(`${day.fullDateIso}_${block.start}`);
    if (found) {
      return {
        slotUtc: found.slotUtc,
        displayText: found.displayText || `${day.dayName}, ${day.dateStr} (${block.label})`,
        timeRange: found.timeRange || block.label
      };
    }

    // 2. Kiểm tra lịch cố định lặp lại hàng tuần (O(1))
    const recurring = recurringLookupMap.get(`${day.dayOfWeekNum}_${block.start}`);
    if (recurring) {
      return {
        slotUtc: new Date(`${day.fullDateIso}T${block.start}:00+07:00`).toISOString(),
        displayText: `${day.dayName}, ${day.dateStr} (${block.label})`,
        timeRange: block.label
      };
    }

    return null;
  };

  const handleSelectSlot = (slot) => {
    if (selectedSlotUtc === slot.slotUtc) {
      setSelectedSlotUtc('');
      setSelectedSlotText('');
    } else {
      setSelectedSlotUtc(slot.slotUtc);
      setSelectedSlotText(slot.displayText || slot.timeRange);
      setPreferredType('OneToOne');
    }
  };

  const handleSelectGroupClass = (cls) => {
    setPreferredType('Group');
    setSelectedClassroomId(cls.id);
    const sub = subjects.find((s) => s.subjectId === cls.subjectId && s.gradeLevelId === cls.gradeLevelId);
    if (sub) {
      setSelectedSubjectKey(`${sub.subjectId}_${sub.gradeLevelId}`);
    }
    const formElem = document.getElementById('booking-form-card');
    if (formElem) {
      formElem.scrollIntoView({ behavior: 'smooth' });
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!selectedSubjectKey) {
      setErrorMsg('Vui lòng chọn môn học và cấp học.');
      return;
    }

    const [subjectId, gradeLevelId] = selectedSubjectKey.split('_').map(Number);
    const requestedTutorId = Number(tutorId);

    if (!Number.isInteger(requestedTutorId) || requestedTutorId <= 0) {
      setErrorMsg('Mã gia sư không hợp lệ.');
      return;
    }
    if (preferredType === 'OneToOne' && selectedSubjectEligibility && !selectedSubjectEligibility.canBook) {
      setErrorMsg(selectedSubjectEligibility.reason || 'Môn học này hiện không thể đăng ký 1 kèm 1.');
      return;
    }
    if (preferredType === 'Group' && selectedGroupEligibility && !selectedGroupEligibility.canJoin) {
      setErrorMsg(selectedGroupEligibility.reason || 'Lớp nhóm này hiện không thể đăng ký.');
      return;
    }

    const payload = {
      tutorId: requestedTutorId,
      subjectId,
      gradeLevelId,
      preferredType: preferredType === 'Group' ? 1 : 0,
      isTrial: preferredType === 'Group' ? false : isTrial,
      classroomId: preferredType === 'Group' && selectedClassroomId ? Number(selectedClassroomId) : null,
      proposedTrialSlotUtc: selectedSlotUtc || null,
      proposedTrialSlotsUtc: selectedSlotUtc ? [selectedSlotUtc] : [],
      message: message.trim() + (allowTutorReschedule ? ' [Gia sư có thể đề xuất khung giờ khác nếu bận]' : '')
    };

    try {
      setSubmitting(true);
      setErrorMsg('');
      const res = await studentApi.sendConnectionRequest(payload);
      if (res.data?.success) {
        setSuccessMsg(res.data.message || 'Đã gửi yêu cầu kết nối tới gia sư thành công!');
        window.scrollTo({ top: 0, behavior: 'smooth' });
      } else {
        setErrorMsg(res.data?.message || 'Không thể gửi yêu cầu.');
      }
    } catch (err) {
      setErrorMsg(err?.response?.data?.message || 'Có lỗi xảy ra khi gửi yêu cầu.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="container py-4 px-3 px-lg-4" style={{ maxWidth: 1400, margin: '0 auto', width: '100%', minHeight: '85vh' }}>
        <div className="placeholder-glow mb-4">
          <span className="placeholder col-3 rounded py-2"></span>
        </div>
        <div className="row g-4">
          <div className="col-12 col-lg-8">
            <div className="card border rounded-3 p-4 mb-4 shadow-sm bg-white placeholder-glow">
              <span className="placeholder col-5 rounded py-2 mb-3"></span>
              <div className="placeholder col-12 rounded" style={{ height: 220 }}></div>
            </div>
            <div className="card border rounded-3 p-4 shadow-sm bg-white placeholder-glow">
              <div className="d-flex justify-content-between mb-3">
                <span className="placeholder col-6 rounded py-2"></span>
                <span className="placeholder col-3 rounded py-2"></span>
              </div>
              <div className="placeholder col-12 rounded" style={{ height: 350 }}></div>
            </div>
          </div>
          <div className="col-12 col-lg-4">
            <div className="card border rounded-3 p-4 shadow-sm bg-white placeholder-glow">
              <span className="placeholder col-8 rounded py-2 mb-3"></span>
              <div className="placeholder col-12 rounded mb-3" style={{ height: 180 }}></div>
              <div className="placeholder col-12 rounded" style={{ height: 120 }}></div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (!tutor) {
    return (
      <div className="container py-5 text-center">
        <div
          className="p-4 mx-auto rounded-3 shadow-sm bg-white text-dark mb-3"
          style={{ maxWidth: 540, border: '1px solid #fed7aa', borderLeft: '4px solid #f59e0b' }}
        >
          <i className="bi bi-exclamation-triangle-fill text-warning fs-3 d-block mb-2"></i>
          Không tìm thấy hồ sơ gia sư này hoặc gia sư chưa được kích hoạt.
        </div>
        <Link to="/search" className="btn btn-outline-danger rounded-pill px-4 py-2 mt-2">
          <i className="bi bi-arrow-left me-1"></i> Quay lại tìm kiếm
        </Link>
      </div>
    );
  }

  const currentWeek = weeks[activeWeekIndex] || weeks[0];
  const displayedDays = dayRangeTab === 'weekday'
    ? currentWeek.days.slice(0, 5)
    : dayRangeTab === 'weekend'
      ? currentWeek.days.slice(5)
      : currentWeek.days;

  return (
    <div className="container py-4 px-3 px-lg-4" style={{ maxWidth: 1400, margin: '0 auto', width: '100%' }}>
      {/* Thanh điều hướng và nút quay lại */}
      <div className="d-flex flex-wrap justify-content-between align-items-center mb-4 gap-2">
        <nav aria-label="breadcrumb">
          <ol className="breadcrumb mb-0">
            <li className="breadcrumb-item">
              <Link to="/search" className="text-decoration-none text-muted">
                <i className="bi bi-search me-1"></i> Tìm gia sư
              </Link>
            </li>
            <li className="breadcrumb-item active fw-semibold text-dark" aria-current="page">
              Hồ sơ: {tutor.fullName}
            </li>
          </ol>
        </nav>
        <div className="d-flex gap-2">
          {isStudent && (
            <Link to="/student/connection-requests" className="btn btn-sm btn-outline-secondary rounded-pill px-3 py-1.5 shadow-xs">
              <i className="bi bi-clock-history me-1"></i> Yêu cầu đã gửi
            </Link>
          )}
          <Link to="/search" className="btn btn-sm btn-outline-secondary rounded-pill px-3 py-1.5 shadow-xs">
            <i className="bi bi-arrow-left me-1"></i> Quay lại tìm kiếm
          </Link>
        </div>
      </div>

      {/* Thông báo hệ thống */}
      {successMsg && (
        <div
          className="d-flex align-items-center justify-content-between p-3 mb-4 rounded-3 shadow-sm bg-white"
          style={{ border: '1px solid #dcfce7', borderLeft: '4px solid #16a34a' }}
          role="alert"
        >
          <div className="d-flex align-items-start">
            <i className="bi bi-check-circle-fill fs-4 me-3 text-success mt-1"></i>
            <div>
              <div className="fw-bold text-dark">
                Thành công! <span className="fw-normal text-secondary">{successMsg}</span>
              </div>
              <div className="mt-2">
                <Link
                  to="/student/connection-requests"
                  className="btn btn-sm btn-danger text-white rounded-pill px-3 shadow-sm"
                  style={{ backgroundColor: '#DB303A' }}
                >
                  Xem danh sách yêu cầu đã gửi <i className="bi bi-arrow-right ms-1"></i>
                </Link>
              </div>
            </div>
          </div>
          <button type="button" className="btn-close ms-2 align-self-start" onClick={() => setSuccessMsg('')} aria-label="Đóng"></button>
        </div>
      )}

      {errorMsg && (
        <div
          className="d-flex align-items-center justify-content-between p-3 mb-4 rounded-3 shadow-sm bg-white"
          style={{ border: '1px solid #fee2e2', borderLeft: '4px solid #DB303A' }}
          role="alert"
        >
          <div className="d-flex align-items-center text-dark">
            <i className="bi bi-exclamation-circle-fill fs-4 me-3 text-danger"></i>
            <span className="fw-medium">{errorMsg}</span>
          </div>
          <button type="button" className="btn-close ms-2" onClick={() => setErrorMsg('')} aria-label="Đóng"></button>
        </div>
      )}

      {existingRequest && (
        <div
          className="d-flex align-items-start gap-3 p-3 mb-4 rounded-3 shadow-sm bg-white"
          style={{ border: '1px solid #fed7aa', borderLeft: '4px solid #f59e0b' }}
        >
          <i className="bi bi-hourglass-split fs-3 text-warning mt-1"></i>
          <div>
            <strong className="d-block mb-1 text-dark">Bạn đã gửi yêu cầu kết nối tới gia sư này!</strong>
            <span className="small text-secondary">
              Yêu cầu môn <strong>{existingRequest.subjectName || 'học tập'}</strong> đang ở trạng thái{' '}
              <strong className="text-dark">{existingRequest.status === 'Accepted' ? 'Đã chấp nhận' : 'Chờ phản hồi'}</strong>.
              Bạn có thể theo dõi trong mục{' '}
              <Link to="/student/connection-requests" className="fw-semibold text-danger text-decoration-underline">
                Yêu cầu đã gửi
              </Link>.
            </span>
          </div>
        </div>
      )}

      {/* Khung bố cục hai cột: Lịch rảnh gia sư & Biểu mẫu đăng ký */}
      <div className="row g-4">
        {/* ================= LEFT COLUMN (~68%) ================= */}
        <div className="col-lg-8">
          <div className="d-flex flex-column gap-4">
            {/* 1. Profile Header Card */}
            <div
              className="card border rounded-3 p-4 shadow-sm bg-white"
              style={{
                borderColor: 'rgba(219, 48, 58, 0.2)'
              }}
            >
              <div className="d-flex flex-column flex-sm-row gap-3 align-items-sm-center">
                {tutor.avatarUrl ? (
                  <img
                    src={resolveAssetUrl(tutor.avatarUrl)}
                    alt={tutor.fullName}
                    style={{
                      width: 80,
                      height: 80,
                      borderRadius: '50%',
                      objectFit: 'cover',
                      border: '3px solid #fff',
                      boxShadow: '0 4px 14px rgba(219, 48, 58, 0.2)'
                    }}
                    onError={(e) => {
                      e.target.style.display = 'none';
                    }}
                  />
                ) : (
                  <div
                    style={{
                      width: 80,
                      height: 80,
                      borderRadius: '50%',
                      background: 'linear-gradient(135deg, #DB303A, #ea580c)',
                      color: '#fff',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      fontSize: '1.8rem',
                      fontWeight: 700,
                      boxShadow: '0 4px 14px rgba(219, 48, 58, 0.2)'
                    }}
                  >
                    {tutor.fullName ? tutor.fullName.charAt(0).toUpperCase() : 'G'}
                  </div>
                )}

                <div className="flex-grow-1">
                  <div className="d-flex flex-wrap align-items-center gap-2 mb-1.5">
                    <h1 className="h4 mb-0 fw-bold text-dark">{tutor.fullName}</h1>
                    <span className="badge bg-success-subtle text-success border border-success-subtle rounded-pill px-2.5 py-1 small fw-semibold">
                      <i className="bi bi-patch-check-fill me-1"></i> Đã xác thực
                    </span>
                  </div>

                  <div className="d-flex flex-wrap align-items-center gap-2 text-secondary small">
                    <div className="d-flex align-items-center text-warning fw-semibold">
                      <i className="bi bi-star-fill me-1" style={{ color: '#f59e0b' }}></i>
                      <span className="text-dark me-1">
                        {tutor.averageRating > 0 ? Number(tutor.averageRating).toFixed(1) : 'Chưa có'}
                      </span>
                      <span className="text-secondary small">({reviews.length} đánh giá)</span>
                    </div>
                    <span>•</span>
                    <span className="badge bg-danger-subtle text-danger border border-danger-subtle px-2.5 py-1 rounded-pill small">
                      <i className="bi bi-laptop me-1"></i> {formatTeaching(tutor.teachingFormat)}
                    </span>
                    <span>•</span>
                    <div className="fs-5 fw-bold text-danger ms-1">
                      {formatMoney(tutor.hourlyFee)} đ <span className="fs-6 fw-normal text-secondary">/ giờ</span>
                    </div>
                  </div>
                </div>
              </div>

              {/* 3 Stats Boxes */}
              <div className="row g-2 mt-3 pt-3 border-top">
                <div className="col-4">
                  <div className="p-2.5 border rounded-3 bg-light d-flex align-items-center gap-2.5">
                    <i className="bi bi-mortarboard fs-3 text-primary"></i>
                    <div>
                      <div className="fw-bold fs-5 text-dark lh-1">{qualifications.length}</div>
                      <div className="text-secondary small" style={{ fontSize: 12 }}>Bằng cấp</div>
                    </div>
                  </div>
                </div>
                <div className="col-4">
                  <div className="p-2.5 border rounded-3 bg-light d-flex align-items-center gap-2.5">
                    <i className="bi bi-check2-circle fs-3 text-success"></i>
                    <div>
                      <div className="fw-bold fs-5 text-dark lh-1">{tutor.totalCompletedSessions || 0}</div>
                      <div className="text-secondary small" style={{ fontSize: 12 }}>Buổi đã dạy</div>
                    </div>
                  </div>
                </div>
                <div className="col-4">
                  <div className="p-2.5 border rounded-3 bg-light d-flex align-items-center gap-2.5">
                    <i className="bi bi-people fs-3 text-info"></i>
                    <div>
                      <div className="fw-bold fs-5 text-dark lh-1">{tutor.totalStudents || 0}</div>
                      <div className="text-secondary small" style={{ fontSize: 12 }}>Học viên</div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* 2. Giới thiệu bản thân */}
            <div className="card border rounded-3 p-4 shadow-sm bg-white">
              <h2 className="h6 fw-bold mb-3 text-dark d-flex align-items-center gap-2">
                <i className="bi bi-person-fill text-danger"></i> Giới thiệu bản thân
              </h2>
              <div className="text-secondary lh-lg" style={{ whiteSpace: 'pre-line' }}>
                {tutor.introduction || tutor.bio || 'Gia sư chưa cập nhật phần giới thiệu.'}
              </div>
            </div>

            {/* 3. Môn học & Cấp học giảng dạy */}
            <div className="card border rounded-3 p-4 shadow-sm bg-white">
              <h2 className="h6 fw-bold mb-3 text-dark d-flex align-items-center gap-2">
                <i className="bi bi-book-fill text-danger"></i> Môn học &amp; Cấp học giảng dạy
              </h2>
              {subjects.length === 0 ? (
                <p className="text-secondary mb-0 small">Chưa có thông tin môn dạy.</p>
              ) : (
                <div className="d-flex flex-wrap gap-3">
                  {subjects.map((s, idx) => (
                    <div
                      key={idx}
                      className="border rounded-3 p-3 text-center bg-white shadow-xs"
                      style={{
                        minWidth: 130,
                        borderColor: '#fecaca',
                        background: 'linear-gradient(180deg, #fff 0%, #fff7f7 100%)'
                      }}
                    >
                      <div className="text-danger fs-4 mb-1">
                        <i className="bi bi-bookmark-fill"></i>
                      </div>
                      <div className="fw-bold text-dark mb-0.5" style={{ fontSize: '0.92rem' }}>
                        {s.subjectName}
                      </div>
                      <div className="text-secondary small">- {s.gradeName} -</div>
                      {s.hourlyFee && (
                        <div className="text-danger fw-semibold mt-1" style={{ fontSize: '0.8rem' }}>
                          {formatMoney(s.hourlyFee)} đ/h
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* 4. Bằng cấp & Chứng chỉ xác thực */}
            <div className="card border rounded-3 p-4 shadow-sm bg-white">
              <h2 className="h6 fw-bold mb-3 text-dark d-flex align-items-center gap-2">
                <i className="bi bi-award-fill text-danger"></i> Bằng cấp &amp; Chứng chỉ xác thực
              </h2>
              {qualifications.length === 0 ? (
                <p className="text-secondary small mb-0">Gia sư chưa tải lên bằng cấp hoặc chứng chỉ.</p>
              ) : (
                <div className="d-flex flex-column gap-2">
                  {qualifications.map((q) => (
                    <div key={q.id} className="p-2.5 border rounded-3 bg-light d-flex justify-content-between align-items-center">
                      <div className="d-flex align-items-center gap-2">
                        <i className="bi bi-patch-check-fill text-success fs-5"></i>
                        <div>
                          <div className="fw-semibold text-dark small">{q.title}</div>
                          <div className="text-secondary small">{q.institution} ({q.year})</div>
                        </div>
                      </div>
                      {q.certificateUrl && (
                        <a href={resolveAssetUrl(q.certificateUrl)} target="_blank" rel="noopener noreferrer" className="btn btn-sm btn-outline-danger rounded-pill px-3">
                          Xem chứng chỉ
                        </a>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* 5. Khung giờ có thể dạy hàng tuần */}
            <div className="card border rounded-3 p-4 shadow-sm bg-white">
              <h2 className="h6 fw-bold mb-1 text-dark d-flex align-items-center gap-2">
                <i className="bi bi-calendar-week-fill text-danger"></i> Khung giờ có thể dạy hàng tuần
              </h2>
              <p className="text-secondary small mb-3">
                Lịch này chỉ để tham khảo. Lịch học thử có thể được thống nhất sau khi kết nối nếu ngày giờ tương lai nào còn trống.
              </p>
              {availabilities.length === 0 ? (
                <p className="text-secondary mb-0 small">Gia sư linh hoạt theo thỏa thuận với học viên.</p>
              ) : (
                <div className="row g-2">
                  {availabilities.map((a) => (
                    <div key={a.id} className="col-6 col-md-4">
                      <div className="p-2 border rounded-2 bg-light d-flex justify-content-between align-items-center small">
                        <span className="fw-semibold text-dark">{a.dayOfWeekName}</span>
                        <span className="text-success fw-medium">{a.timeRange}</span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* 6. Khung lịch rảnh & Đặt giờ học (4 tuần) - MATRIX GRID */}
            <div className="card border rounded-3 p-4 shadow-sm bg-white">
              <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
                <h2 className="h6 fw-bold mb-0 text-dark d-flex align-items-center gap-2">
                  <i className="bi bi-calendar2-check-fill text-danger"></i> Khung lịch rảnh &amp; Đặt giờ học (4 tuần)
                </h2>
                <span className="badge bg-danger-subtle text-danger border border-danger-subtle rounded-pill px-3 py-1.5 small fw-semibold">
                  <i className="bi bi-clock me-1"></i> Mỗi buổi 2 tiếng - Tối đa 3 buổi/tuần (6 tiếng)
                </span>
              </div>

              <p className="text-secondary small mb-3">
                Lịch sinh hoạt 14 ngày tới (đã tự động lọc lịch gia sư bận). <strong>Học thử:</strong> chọn 1 buổi (2 tiếng). <strong>Học chính thức:</strong> chọn tối đa 3 buổi/tuần (6 tiếng) để lên lịch cố định và kích hoạt gói học phí theo tháng.
              </p>

              {/* Thanh chọn tuần xem lịch & Chuyển đổi ngày */}
              <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-3">
                {/* Chọn Tuần (Tuần 1 -> Tuần 4) */}
                <div className="d-flex flex-wrap gap-2 align-items-center">
                  <span className="text-secondary small fw-semibold me-1 d-none d-sm-inline">
                    <i className="bi bi-calendar3 me-1"></i>Chọn tuần:
                  </span>
                  {weeks.map((w, idx) => (
                    <button
                      key={idx}
                      type="button"
                      onClick={() => setActiveWeekIndex(idx)}
                      className={`btn btn-sm px-2.5 py-1.5 rounded-2 fw-semibold transition-all ${activeWeekIndex === idx
                        ? 'btn-danger text-white shadow-xs'
                        : 'btn-outline-secondary'
                        }`}
                      style={activeWeekIndex === idx ? { backgroundColor: '#DB303A', borderColor: '#DB303A' } : {}}
                    >
                      {w.title}
                    </button>
                  ))}
                </div>

                {/* Bộ lọc hiển thị: 5 ngày đầu (Thứ 2 - Thứ 6) | Cuối tuần (T7 - CN) | Cả tuần */}
                <div className="btn-group btn-group-sm bg-light p-1 rounded-pill border shadow-xs" role="group">
                  <button
                    type="button"
                    onClick={() => setDayRangeTab('weekday')}
                    className={`btn btn-sm rounded-pill px-3 py-1 fw-semibold transition-all ${dayRangeTab === 'weekday'
                      ? 'btn-danger text-white shadow-xs'
                      : 'btn-light text-secondary border-0'
                      }`}
                    style={dayRangeTab === 'weekday' ? { backgroundColor: '#DB303A' } : {}}
                    title="Hiển thị gọn gàng 5 ngày đầu (Thứ 2 đến Thứ 6) không cần vuốt"
                  >
                    <i className="bi bi-calendar-check me-1"></i> Thứ 2 - Thứ 6
                  </button>
                  <button
                    type="button"
                    onClick={() => setDayRangeTab('weekend')}
                    className={`btn btn-sm rounded-pill px-3 py-1 fw-semibold transition-all ${dayRangeTab === 'weekend'
                      ? 'btn-danger text-white shadow-xs'
                      : 'btn-light text-secondary border-0'
                      }`}
                    style={dayRangeTab === 'weekend' ? { backgroundColor: '#DB303A' } : {}}
                    title="Xem tiếp các ngày cuối tuần (Thứ 7 & Chủ nhật)"
                  >
                    <i className="bi bi-calendar2-week me-1"></i> Thứ 7 - Chủ nhật
                  </button>
                  <button
                    type="button"
                    onClick={() => setDayRangeTab('all')}
                    className={`btn btn-sm rounded-pill px-2.5 py-1 fw-semibold transition-all ${dayRangeTab === 'all'
                      ? 'btn-danger text-white shadow-xs'
                      : 'btn-light text-secondary border-0'
                      }`}
                    style={dayRangeTab === 'all' ? { backgroundColor: '#DB303A' } : {}}
                    title="Xem trọn vẹn cả 7 ngày"
                  >
                    Cả tuần
                  </button>
                </div>
              </div>

              {dayRangeTab === 'all' && (
                <div className="text-secondary small mb-2 d-flex align-items-center gap-1.5">
                  <i className="bi bi-arrows-expand text-muted"></i> Có thể cuộn ngang để xem trọn vẹn từ Thứ 2 đến Chủ nhật
                </div>
              )}

              {/* Bảng ma trận thời khóa biểu gia sư */}
              <div className="table-responsive border rounded-3 shadow-xs" style={{ WebkitOverflowScrolling: 'touch' }}>
                <table
                  className="table table-bordered mb-0 align-middle text-center"
                  style={{
                    width: '100%',
                    tableLayout: 'fixed',
                    minWidth: dayRangeTab === 'all' ? 860 : '100%'
                  }}
                >
                  <thead className="table-light">
                    <tr>
                      <th style={{ width: 115, minWidth: 115, fontSize: '0.8rem', padding: '10px 6px', whiteSpace: 'nowrap' }}>
                        Khung giờ
                      </th>
                      {displayedDays.map((day, idx) => (
                        <th
                          key={idx}
                          style={{
                            width: `calc((100% - 115px) / ${displayedDays.length})`,
                            fontSize: '0.8rem',
                            padding: '10px 4px',
                            backgroundColor: (dayRangeTab === 'weekday' && idx === 0) || (dayRangeTab === 'all' && idx === 0)
                              ? 'rgba(219, 48, 58, 0.06)'
                              : undefined
                          }}
                        >
                          <div className="fw-bold text-dark text-nowrap">{day.dayName}</div>
                          <div className="text-secondary small text-nowrap">{day.dateStr}</div>
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {TIME_BLOCKS.map((block, bIdx) => (
                      <tr key={bIdx}>
                        <td className="fw-semibold text-secondary small bg-light text-nowrap" style={{ fontSize: '0.78rem', padding: '8px 4px' }}>
                          {block.label}
                        </td>
                        {displayedDays.map((day, dIdx) => {
                          const slot = getSlotForCell(day, block);
                          const isSelected = slot && selectedSlotUtc === slot.slotUtc;

                          return (
                            <td key={dIdx} className="p-1" style={{ height: 56 }}>
                              {slot ? (
                                <button
                                  type="button"
                                  onClick={() => handleSelectSlot(slot)}
                                  className={`btn w-100 h-100 p-1 border rounded-2 d-flex flex-column align-items-center justify-content-center transition-all ${isSelected
                                    ? 'btn-danger text-white border-danger shadow-xs'
                                    : 'border-success bg-success-subtle text-success'
                                    }`}
                                  style={{
                                    fontSize: '0.75rem',
                                    lineHeight: 1.25,
                                    cursor: 'pointer',
                                    minHeight: 46,
                                    whiteSpace: 'nowrap'
                                  }}
                                  title={`Bấm để chọn: ${slot.displayText}`}
                                >
                                  <strong className={`text-nowrap ${isSelected ? 'text-white' : 'text-success'}`} style={{ fontSize: '0.78rem' }}>
                                    {slot.timeRange}
                                  </strong>
                                  <span className={`small text-nowrap ${isSelected ? 'text-white-50' : 'text-success'}`} style={{ fontSize: '0.70rem' }}>
                                    {isSelected ? '✓ Đã chọn' : '(Rảnh)'}
                                  </span>
                                </button>
                              ) : (
                                <div
                                  className="w-100 h-100 rounded-2 d-flex align-items-center justify-content-center"
                                  style={{
                                    border: '1px dashed #e2e8f0',
                                    backgroundColor: '#fafafa',
                                    minHeight: 46
                                  }}
                                >
                                  <span className="text-muted small" style={{ fontSize: '0.7rem' }}>—</span>
                                </div>
                              )}
                            </td>
                          );
                        })}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>

            {/* 7. Các lớp học nhóm đang tuyển sinh */}
            <div className="card border rounded-3 p-4 shadow-sm bg-white">
              <div className="d-flex align-items-center justify-content-between mb-3">
                <h2 className="h6 fw-bold mb-0 text-dark d-flex align-items-center gap-2">
                  <i className="bi bi-easel-fill text-danger"></i> Các lớp học nhóm đang tuyển sinh
                </h2>
                <span className="badge bg-secondary-subtle text-secondary rounded-pill">
                  {groupClasses.length} lớp học
                </span>
              </div>

              {groupClasses.length === 0 ? (
                <p className="text-secondary small mb-0">Gia sư hiện chưa có lớp nhóm đang tuyển sinh.</p>
              ) : (
                <div className="row g-3">
                  {groupClasses.map((cls) => (
                    <div key={cls.id} className="col-md-6">
                      <div className="card border rounded-3 h-100 overflow-hidden shadow-xs">
                        <img
                          src={resolveAssetUrl(cls.coverImageUrl) || '/assets/img/course-1.jpg'}
                          alt={cls.name}
                          className="w-100"
                          style={{ height: 160, objectFit: 'cover' }}
                          onError={(e) => {
                            e.currentTarget.onerror = null;
                            e.currentTarget.src = '/assets/img/course-1.jpg';
                          }}
                        />
                        <div className="card-body p-3 d-flex flex-column justify-content-between">
                          <div>
                            <div className="d-flex justify-content-between align-items-center mb-2">
                              <span className="badge bg-danger text-white rounded-pill px-2.5 py-1 small">
                                {cls.subjectName} · {cls.gradeLevelName}
                              </span>
                              <span className="fw-bold text-danger">{formatMoney(cls.fee)} đ</span>
                            </div>
                            <h3 className="h6 fw-bold mb-1 text-dark">{cls.name}</h3>
                            <p className="text-secondary small mb-2">{cls.description || 'Chưa có mô tả chi tiết cho lớp học này.'}</p>
                            {cls.scheduleSummary && (
                              <div className="p-2 border rounded-2 bg-light small mb-2 text-dark">
                                <i className="bi bi-calendar-event text-danger me-1"></i>
                                <strong>Lịch học:</strong> {cls.scheduleSummary}
                              </div>
                            )}
                          </div>

                          <div>
                            <div className="d-flex justify-content-between align-items-center pt-2 border-top small text-secondary mb-2">
                              <span>
                                <i className="bi bi-people me-1"></i> {cls.studentCount ?? 0}/{cls.maxStudents || '—'} học viên
                              </span>
                              <span className={cls.canJoin !== false ? 'text-success fw-semibold' : 'text-danger fw-semibold'}>
                                {cls.canJoin !== false
                                  ? (cls.availableSeats !== undefined
                                    ? `Còn ${cls.availableSeats} chỗ`
                                    : (cls.maxStudents ? `Còn ${Math.max(0, cls.maxStudents - (cls.studentCount || 0))} chỗ` : 'Đang tuyển sinh'))
                                  : cls.reason}
                              </span>
                            </div>
                            <button
                              type="button"
                              className="btn btn-sm btn-outline-danger w-100 rounded-pill fw-medium"
                              onClick={() => handleSelectGroupClass(cls)}
                            >
                              <i className="bi bi-eye me-1"></i> Xem chi tiết &amp; Đăng ký
                            </button>
                          </div>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* 8. Đánh giá từ học viên */}
            <div className="card border rounded-3 p-4 shadow-sm bg-white">
              <div className="d-flex justify-content-between align-items-center mb-3">
                <h2 className="h6 fw-bold mb-0 text-dark d-flex align-items-center gap-2">
                  <i className="bi bi-chat-quote-fill text-danger"></i> Đánh giá từ học viên
                </h2>
                <span className="badge bg-light text-secondary border px-2.5 py-1 rounded-pill">
                  {reviews.length} lượt đánh giá
                </span>
              </div>

              {reviews.length === 0 ? (
                <p className="text-secondary small mb-0">Chưa có đánh giá nào cho gia sư này.</p>
              ) : (
                <div className="d-flex flex-column gap-3">
                  {reviews.map((rev) => (
                    <div key={rev.id} className="p-3 border rounded-3 bg-light">
                      <div className="d-flex justify-content-between align-items-center mb-1.5">
                        <div className="d-flex align-items-center gap-2">
                          <div
                            style={{
                              width: 32,
                              height: 32,
                              borderRadius: '50%',
                              background: '#cbd5e1',
                              color: '#334155',
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'center',
                              fontWeight: 600,
                              fontSize: 13
                            }}
                          >
                            {rev.studentName ? rev.studentName.charAt(0).toUpperCase() : 'H'}
                          </div>
                          <span className="fw-semibold small text-dark">{rev.studentName}</span>
                        </div>
                        <div className="text-warning small">
                          {[1, 2, 3, 4, 5].map((star) => (
                            <i
                              key={star}
                              className={`bi ${star <= (rev.score || rev.rating) ? 'bi-star-fill text-warning' : 'bi-star text-muted'}`}
                            ></i>
                          ))}
                        </div>
                      </div>
                      <p className="mb-0 text-secondary small">
                        {rev.comment || 'Học viên không để lại lời nhận xét.'}
                      </p>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* ================= RIGHT COLUMN (~32%) ================= */}
        <div className="col-lg-4">
          <div className="d-flex flex-column gap-4 sticky-top" style={{ top: 88, zIndex: 10 }}>
            {/* Card 1: Gửi yêu cầu kết nối */}
            <div id="booking-form-card" className="card border rounded-3 p-4 shadow-sm bg-white" style={{ borderColor: 'rgba(219, 48, 58, 0.25)' }}>
              <div className="d-flex align-items-center gap-2 mb-3 pb-2 border-bottom">
                <i className="bi bi-send-check-fill text-danger fs-4"></i>
                <div>
                  <h2 className="h6 mb-0 fw-bold text-dark">Gửi yêu cầu kết nối</h2>
                  <small className="text-secondary">Đăng ký học trực tiếp với gia sư</small>
                </div>
              </div>

              {/* Stepper bar (1 Gửi yêu cầu —— 2 Đặt lịch thử —— 3 Vào lớp) */}
              <div className="d-flex align-items-center justify-content-between mb-3.5 pb-2.5 border-bottom">
                <div className="d-flex align-items-center gap-1.5">
                  <span
                    className="d-inline-flex align-items-center justify-content-center rounded-circle fw-bold"
                    style={{
                      width: 22,
                      height: 22,
                      fontSize: '0.75rem',
                      border: '1.5px solid #DB303A',
                      backgroundColor: '#fff1f2',
                      color: '#DB303A'
                    }}
                  >
                    1
                  </span>
                  <span className="fw-bold text-danger" style={{ fontSize: '0.84rem' }}>
                    Gửi yêu cầu
                  </span>
                </div>

                <div className="flex-grow-1 mx-2" style={{ height: 1, backgroundColor: '#e2e8f0' }}></div>

                <div className="d-flex align-items-center gap-1.5">
                  <span
                    className="d-inline-flex align-items-center justify-content-center rounded-circle fw-semibold"
                    style={{
                      width: 22,
                      height: 22,
                      fontSize: '0.75rem',
                      backgroundColor: selectedSlotUtc ? '#fff1f2' : '#f8fafc',
                      border: selectedSlotUtc ? '1.5px solid #DB303A' : '1px solid #e2e8f0',
                      color: selectedSlotUtc ? '#DB303A' : '#64748b'
                    }}
                  >
                    2
                  </span>
                  <span
                    className={selectedSlotUtc ? 'fw-bold text-danger' : 'text-secondary'}
                    style={{ fontSize: '0.84rem' }}
                  >
                    Đặt lịch thử
                  </span>
                </div>

                <div className="flex-grow-1 mx-2" style={{ height: 1, backgroundColor: '#e2e8f0' }}></div>

                <div className="d-flex align-items-center gap-1.5">
                  <span
                    className="d-inline-flex align-items-center justify-content-center rounded-circle fw-semibold text-secondary"
                    style={{
                      width: 22,
                      height: 22,
                      fontSize: '0.75rem',
                      backgroundColor: '#f8fafc',
                      border: '1px solid #e2e8f0',
                      color: '#64748b'
                    }}
                  >
                    3
                  </span>
                  <span className="text-secondary" style={{ fontSize: '0.84rem' }}>
                    Vào lớp
                  </span>
                </div>
              </div>

              {!user ? (
                <div className="p-3 border rounded-3 bg-light text-center">
                  <i className="bi bi-person-lock fs-3 text-danger d-block mb-2"></i>
                  <p className="small text-secondary mb-3">
                    Vui lòng đăng nhập tài khoản Học viên để gửi yêu cầu kết nối và đặt lịch học với gia sư.
                  </p>
                  <Link
                    to={`/login?returnUrl=${encodeURIComponent(`/tutors/${tutorId}`)}`}
                    className="btn btn-danger btn-sm w-100 rounded-pill py-2 fw-semibold"
                    style={{ backgroundColor: '#DB303A', borderColor: '#DB303A' }}
                  >
                    <i className="bi bi-box-arrow-in-right me-1"></i> Đăng nhập để đặt lịch
                  </Link>
                </div>
              ) : !isStudent ? (
                <div className="alert alert-info small mb-0 rounded-3">
                  Chỉ tài khoản Học viên / Phụ huynh mới có thể gửi yêu cầu kết nối tới gia sư.
                </div>
              ) : subjects.length === 0 ? (
                <div className="alert alert-warning small mb-0 rounded-3">
                  Gia sư này chưa cập nhật môn học cụ thể nên tạm thời chưa nhận yêu cầu.
                </div>
              ) : (
                <form onSubmit={handleSubmit}>
                  {/* Chọn Môn học & Cấp học */}
                  <div className="mb-3">
                    <label className="form-label small fw-semibold text-dark">
                      Môn học &amp; Cấp học <span className="text-danger">*</span>
                    </label>
                    <select
                      className="form-select form-select-sm rounded-2"
                      value={selectedSubjectKey}
                      onChange={(e) => setSelectedSubjectKey(e.target.value)}
                      required
                    >
                      {subjects.map((s) => {
                        const eligibility = data?.bookingEligibility?.subjects?.find(
                          (item) => item.subjectId === s.subjectId && item.gradeLevelId === s.gradeLevelId
                        );
                        return (
                          <option key={`${s.subjectId}_${s.gradeLevelId}`} value={`${s.subjectId}_${s.gradeLevelId}`}>
                            {s.subjectName} · {s.gradeName} ({formatMoney(s.hourlyFee)} đ/h)
                            {eligibility && !eligibility.canBook ? ' — Đã có lớp/yêu cầu' : ''}
                          </option>
                        );
                      })}
                    </select>
                    {preferredType === 'OneToOne' && selectedSubjectEligibility && !selectedSubjectEligibility.canBook && (
                      <div className="alert alert-warning py-2 px-3 small mt-2 mb-0 rounded-2">
                        {selectedSubjectEligibility.reason}
                      </div>
                    )}
                  </div>

                  {/* Hình thức học */}
                  <div className="mb-3">
                    <label className="form-label small fw-semibold text-dark">Hình thức học mong muốn</label>
                    <div className="d-flex gap-2">
                      <button
                        type="button"
                        className={`btn btn-sm flex-fill rounded-pill py-1.5 fw-medium ${preferredType === 'OneToOne' ? 'btn-danger text-white' : 'btn-outline-secondary'
                          }`}
                        style={preferredType === 'OneToOne' ? { backgroundColor: '#DB303A', borderColor: '#DB303A' } : {}}
                        onClick={() => setPreferredType('OneToOne')}
                      >
                        <i className="bi bi-person me-1"></i> 1 kèm 1
                      </button>
                      {groupBookingOptions.length > 0 && (
                        <button
                          type="button"
                          className={`btn btn-sm flex-fill rounded-pill py-1.5 fw-medium ${preferredType === 'Group' ? 'btn-danger text-white' : 'btn-outline-secondary'
                            }`}
                          style={preferredType === 'Group' ? { backgroundColor: '#DB303A', borderColor: '#DB303A' } : {}}
                          onClick={() => setPreferredType('Group')}
                        >
                          <i className="bi bi-people me-1"></i> Lớp nhóm ({groupBookingOptions.length})
                        </button>
                      )}
                    </div>
                  </div>

                  {/* Nếu chọn 1 kèm 1: Mục đích kết nối */}
                  {preferredType === 'OneToOne' && (
                    <div className="mb-3">
                      <label className="form-label small fw-semibold text-dark">Mục đích kết nối</label>
                      <div className="d-flex flex-column gap-2">
                        <div
                          className={`p-2.5 border rounded-3 cursor-pointer transition-all ${isTrial ? 'border-danger bg-danger-subtle' : 'bg-light'
                            }`}
                          onClick={() => setIsTrial(true)}
                          style={{ cursor: 'pointer' }}
                        >
                          <div className="d-flex align-items-start gap-2">
                            <input
                              type="radio"
                              name="trialOption"
                              checked={isTrial}
                              onChange={() => setIsTrial(true)}
                              className="mt-1"
                            />
                            <div>
                              <span className="d-block fw-bold small text-dark">
                                <i className="bi bi-mortarboard text-danger me-1"></i> Đăng ký Học thử 1 buổi
                              </span>
                              <span className="text-secondary small" style={{ fontSize: 11 }}>
                                Trải nghiệm 1 buổi học trực tuyến cùng gia sư với mức giá ưu đãi/ngay lập tức
                              </span>
                            </div>
                          </div>
                        </div>

                        <div
                          className={`p-2.5 border rounded-3 cursor-pointer transition-all ${!isTrial ? 'border-danger bg-danger-subtle' : 'bg-light'
                            }`}
                          onClick={() => setIsTrial(false)}
                          style={{ cursor: 'pointer' }}
                        >
                          <div className="d-flex align-items-start gap-2">
                            <input
                              type="radio"
                              name="trialOption"
                              checked={!isTrial}
                              onChange={() => setIsTrial(false)}
                              className="mt-1"
                            />
                            <div>
                              <span className="d-block fw-bold small text-dark">
                                <i className="bi bi-lightning-charge text-danger me-1"></i> Vào học chính thức (Gói tối đa 3 buổi/tuần = 6 tiếng/tuần)
                              </span>
                              <span className="text-secondary small" style={{ fontSize: 11 }}>
                                Chọn các khung giờ cố định trong tuần để học lâu dài và kích hoạt gói học phí tháng
                              </span>
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>
                  )}

                  {/* Chọn giờ học (1 kèm 1) */}
                  {preferredType === 'OneToOne' && (
                    <div className="mb-3">
                      <div className="d-flex justify-content-between align-items-center mb-1">
                        <label className="form-label small fw-semibold text-dark mb-0">
                          {isTrial ? 'Chọn giờ học thử 01 buổi (1 buổi)' : 'Chọn khung giờ học'}
                        </label>
                        {selectedSlotUtc && (
                          <button
                            type="button"
                            className="btn btn-link btn-sm text-danger text-decoration-none p-0 small"
                            onClick={() => {
                              setSelectedSlotUtc('');
                              setSelectedSlotText('');
                            }}
                          >
                            Xóa chọn
                          </button>
                        )}
                      </div>

                      {/* Ô hiển thị thông tin ca học đã chọn */}
                      <div className="p-3 border rounded-3 bg-light text-center mb-2">
                        {selectedSlotUtc ? (
                          <div className="text-start">
                            <div className="d-flex align-items-center gap-2 text-success fw-bold small mb-1">
                              <i className="bi bi-calendar-check-fill fs-5"></i>
                              <span>Đã chọn khung giờ:</span>
                            </div>
                            <div className="fw-semibold text-dark ps-4 small">
                              {selectedSlotText || selectedSlotUtc}
                            </div>
                          </div>
                        ) : (
                          <div>
                            <i className="bi bi-calendar-x text-danger fs-3 d-block mb-1"></i>
                            <div className="fw-bold text-dark small mb-0.5">Chưa chọn lịch học thử</div>
                            <div className="text-secondary small" style={{ fontSize: 11 }}>
                              Bạn chọn 1 khung giờ rảnh trên lịch bên trái hoặc chọn từ danh sách bên dưới.
                            </div>
                          </div>
                        )}
                      </div>

                      {/* Hộp chọn nhanh ca học khả dụng */}
                      {availableSlots.length > 0 && (
                        <select
                          className="form-select form-select-sm rounded-2"
                          value={selectedSlotUtc}
                          onChange={(e) => {
                            const val = e.target.value;
                            setSelectedSlotUtc(val);
                            const found = availableSlots.find((s) => (s.slotUtc || s.isoValue) === val);
                            setSelectedSlotText(found?.displayText || found?.timeRange || '');
                          }}
                        >
                          <option value="">-- Hoặc chọn nhanh từ danh sách rảnh ({availableSlots.length}) --</option>
                          {availableSlots.map((slot) => (
                            <option key={slot.slotUtc || slot.isoValue} value={slot.slotUtc || slot.isoValue}>
                              {slot.displayText || `${slot.dateLabel} (${slot.timeRange})`}
                            </option>
                          ))}
                        </select>
                      )}

                      {/* Checkbox: Gia sư có thể tự đề xuất khung giờ học nếu lịch trên tạm thời kín */}
                      <div className="form-check mt-2.5">
                        <input
                          className="form-check-input"
                          type="checkbox"
                          id="allowRescheduleCheck"
                          checked={allowTutorReschedule}
                          onChange={(e) => setAllowTutorReschedule(e.target.checked)}
                        />
                        <label className="form-check-label text-secondary small" htmlFor="allowRescheduleCheck" style={{ fontSize: 12 }}>
                          Gia sư có thể tự đề xuất khung giờ học nếu lịch trên tạm thời kín
                        </label>
                      </div>
                    </div>
                  )}

                  {/* Nếu chọn Lớp nhóm: Chọn lớp */}
                  {preferredType === 'Group' && groupBookingOptions.length > 0 && (
                    <div className="mb-3">
                      <label className="form-label small fw-semibold text-dark">Chọn lớp nhóm tham gia</label>
                      <select
                        className="form-select form-select-sm rounded-2"
                        value={selectedClassroomId}
                        onChange={(e) => {
                          const classroomId = Number(e.target.value);
                          const classroom = groupBookingOptions.find((item) => item.classroomId === classroomId);
                          setSelectedClassroomId(classroomId);
                          const subject = subjects.find(
                            (item) => item.subjectId === classroom?.subjectId && item.gradeLevelId === classroom?.gradeLevelId
                          );
                          if (subject) setSelectedSubjectKey(`${subject.subjectId}_${subject.gradeLevelId}`);
                        }}
                      >
                        {groupBookingOptions.map((cls) => (
                          <option key={cls.classroomId} value={cls.classroomId} disabled={!cls.canJoin}>
                            {cls.classroomName} · {cls.subjectName} ({formatMoney(cls.fee)} đ)
                            {!cls.canJoin ? ` — ${cls.reason}` : ` — Còn ${cls.availableSeats} chỗ`}
                          </option>
                        ))}
                      </select>
                      {selectedGroupEligibility && !selectedGroupEligibility.canJoin && (
                        <div className="alert alert-warning py-2 px-3 small mt-2 mb-0 rounded-2">{selectedGroupEligibility.reason}</div>
                      )}
                    </div>
                  )}

                  {/* Lời nhắn / Mục tiêu học tập */}
                  <div className="mb-3">
                    <label className="form-label small fw-semibold text-dark">Lời nhắn / Mục tiêu học tập</label>
                    <textarea
                      className="form-control form-control-sm rounded-2"
                      rows="3"
                      placeholder="Ví dụ: Em muốn ôn thi vào lớp 10 môn Toán, mục tiêu 8.5+, mong muốn học 2 buổi/tuần vào các buổi tối thứ 3 và thứ 5..."
                      value={message}
                      onChange={(e) => setMessage(e.target.value)}
                    ></textarea>
                  </div>

                  <button
                    type="submit"
                    disabled={
                      submitting ||
                      Boolean(successMsg) ||
                      Boolean(preferredType === 'OneToOne' && selectedSubjectEligibility && !selectedSubjectEligibility.canBook) ||
                      Boolean(preferredType === 'Group' && selectedGroupEligibility && !selectedGroupEligibility.canJoin)
                    }
                    className="btn btn-danger w-100 py-2.5 fw-semibold d-flex align-items-center justify-content-center gap-2 shadow-sm rounded-pill"
                    style={{ backgroundColor: '#DB303A', borderColor: '#DB303A' }}
                  >
                    {submitting ? (
                      <>
                        <span className="spinner-border spinner-border-sm" role="status"></span>
                        <span>Đang gửi...</span>
                      </>
                    ) : (
                      <>
                        <i className="bi bi-send-fill"></i>
                        <span>Gửi yêu cầu ngay</span>
                      </>
                    )}
                  </button>

                  <div className="text-secondary small mt-3 d-flex align-items-center gap-1.5" style={{ fontSize: 11 }}>
                    <i className="bi bi-shield-check text-success fs-6"></i>
                    <span>Thông tin kết nối được bảo mật an toàn. Bạn sẽ nhận được thông báo khi gia sư chấp nhận.</span>
                  </div>
                </form>
              )}
            </div>

            {/* Card 2: Trò chuyện (Widget) */}
            <div className="card border rounded-3 p-4 shadow-sm bg-white">
              <div className="d-flex align-items-center justify-content-between mb-2">
                <h3 className="h6 fw-bold mb-0 text-dark d-flex align-items-center gap-2">
                  <i className="bi bi-chat-dots-fill text-danger"></i> Trò chuyện
                </h3>
              </div>
              <p className="text-secondary small mb-3" style={{ fontSize: 12 }}>
                Bạn có thể trò chuyện sau khi gia sư chấp nhận yêu cầu.
              </p>

              {/* Vùng xem trước tin nhắn */}
              <div
                className="border rounded-3 p-3 d-flex flex-column align-items-center justify-content-center text-secondary bg-light mb-2"
                style={{ minHeight: 120 }}
              >
                <i className="bi bi-chat-square-text fs-3 opacity-50 mb-1"></i>
                <span className="small">Chưa có tin nhắn</span>
              </div>

              {/* Ô nhập tin nhắn tạm khóa */}
              <div className="input-group input-group-sm">
                <input
                  type="text"
                  className="form-control rounded-start-pill"
                  placeholder="Nhập tin nhắn..."
                  disabled
                />
                <button className="btn btn-outline-secondary rounded-end-pill px-3" type="button" disabled>
                  <i className="bi bi-send"></i>
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
