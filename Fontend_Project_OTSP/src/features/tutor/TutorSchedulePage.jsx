import { useState, useEffect, useMemo } from 'react';
import { Link } from 'react-router-dom';
import tutorApi from '../../api/tutorApi';

const TIME_BLOCKS = [
  { start: 6, end: 8, label: '06:00 – 08:00', period: 'Sáng' },
  { start: 8, end: 10, label: '08:00 – 10:00', period: 'Sáng' },
  { start: 10, end: 12, label: '10:00 – 12:00', period: 'Sáng' },
  { start: 13, end: 15, label: '13:00 – 15:00', period: 'Chiều' },
  { start: 15, end: 17, label: '15:00 – 17:00', period: 'Chiều' },
  { start: 17, end: 19, label: '17:00 – 19:00', period: 'Tối' },
  { start: 19, end: 21, label: '19:00 – 21:00', period: 'Tối' },
  { start: 21, end: 23, label: '21:00 – 23:00', period: 'Tối' },
];

const DAYS = [
  { dayIndex: 0, dayOfWeek: 1, name: 'Thứ 2', short: 'T2' },
  { dayIndex: 1, dayOfWeek: 2, name: 'Thứ 3', short: 'T3' },
  { dayIndex: 2, dayOfWeek: 3, name: 'Thứ 4', short: 'T4' },
  { dayIndex: 3, dayOfWeek: 4, name: 'Thứ 5', short: 'T5' },
  { dayIndex: 4, dayOfWeek: 5, name: 'Thứ 6', short: 'T6' },
  { dayIndex: 5, dayOfWeek: 6, name: 'Thứ 7', short: 'T7' },
  { dayIndex: 6, dayOfWeek: 0, name: 'Chủ nhật', short: 'CN' },
];

const parseDateOnly = (val) => {
  if (!val) return new Date();
  const [y, m, d] = String(val).split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d));
};

const formatDateOnly = (d) => [
  d.getUTCFullYear(),
  String(d.getUTCMonth() + 1).padStart(2, '0'),
  String(d.getUTCDate()).padStart(2, '0')
].join('-');

const formatDisplayDateShort = (d) =>
  `${String(d.getUTCDate()).padStart(2, '0')}/${String(d.getUTCMonth() + 1).padStart(2, '0')}`;

const formatDisplayDateFull = (d) =>
  `${String(d.getUTCDate()).padStart(2, '0')}/${String(d.getUTCMonth() + 1).padStart(2, '0')}/${d.getUTCFullYear()}`;

const getVietnamTodayStr = () => {
  const nowParts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit'
  }).formatToParts(new Date());
  const map = Object.fromEntries(nowParts.map(({ type, value }) => [type, value]));
  return `${map.year}-${map.month}-${map.day}`;
};

export default function TutorSchedulePage() {
  const [scheduleData, setScheduleData] = useState(null);
  const [weekOffset, setWeekOffset] = useState(0);
  const [jumpDate, setJumpDate] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [reloadKey, setReloadKey] = useState(0);
  const [activeDayMobile, setActiveDayMobile] = useState(
    () => (parseDateOnly(getVietnamTodayStr()).getUTCDay() + 6) % 7
  ); // 0 = Thứ 2, ..., 6 = Chủ nhật

  // Modal: Tạo buổi học / dạy bù
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [selectedClassroomId, setSelectedClassroomId] = useState('');
  const [sessionDate, setSessionDate] = useState('');
  const [sessionHour, setSessionHour] = useState(19);
  const [creating, setCreating] = useState(false);

  // Modal: Xem chi tiết và hủy buổi học
  const [selectedSession, setSelectedSession] = useState(null);
  const [cancelling, setCancelling] = useState(false);

  useEffect(() => {
    let active = true;
    async function loadSchedule() {
      try {
        setLoading(true);
        const res = await tutorApi.getSchedule(weekOffset, jumpDate);
        if (!active) return;
        setScheduleData(res.data);
        const rawMonday = res.data?.selectedMonday;
        const vnToday = res.data?.todayVn || getVietnamTodayStr();
        if (rawMonday) {
          const baseMon = parseDateOnly(rawMonday);
          for (let i = 0; i < 7; i++) {
            const d = new Date(baseMon);
            d.setUTCDate(d.getUTCDate() + i);
            if (formatDateOnly(d) === vnToday) {
              setActiveDayMobile(i);
              break;
            }
          }
        }
      } catch {
        if (active) setError('Không tải được thời khóa biểu lịch dạy.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadSchedule();
    return () => {
      active = false;
    };
  }, [weekOffset, jumpDate, reloadKey]);

  // Tính toán danh sách ngày trong tuần dựa trên ngày Thứ 2 đã chọn
  const { mondayDate, weekDates, todayStr } = useMemo(() => {
    const rawMonday = scheduleData?.selectedMonday;
    const baseMonday = rawMonday ? parseDateOnly(rawMonday) : parseDateOnly(formatDateOnly(new Date()));
    const dates = Array.from({ length: 7 }, (_, i) => {
      const d = new Date(baseMonday);
      d.setUTCDate(d.getUTCDate() + i);
      return d;
    });

    return {
      mondayDate: baseMonday,
      weekDates: dates,
      todayStr: scheduleData?.todayVn || getVietnamTodayStr()
    };
  }, [scheduleData]);

  // Chuẩn hóa dữ liệu buổi học
  const calendarSessions = useMemo(() => {
    const raw = scheduleData?.calendarSessions || [];
    return raw.map((s) => {
      let dateStr = s.date;
      let hourNum = s.hour;

      if (!dateStr || hourNum === undefined) {
        const rawTime = s.scheduledAtVn || s.scheduledAtUtc;
        if (rawTime) {
          const match = String(rawTime).match(/^(\d{4}-\d{2}-\d{2})T(\d{2}):/);
          if (match) {
            dateStr = match[1];
            hourNum = parseInt(match[2], 10);
          }
        }
      }

      const status = s.status === 'InProgress' || s.status === 3
        ? 'InProgress'
        : (s.status === 'Done' || s.status === 1
          ? 'Done'
          : (s.status === 'Cancelled' || s.status === 2 ? 'Cancelled' : 'Scheduled'));

      return {
        ...s,
        date: dateStr,
        hour: hourNum,
        normalizedStatus: status,
        sessionId: s.sessionId || s.id,
        canJoin: s.canJoin ?? (status === 'InProgress')
      };
    });
  }, [scheduleData]);

  // Danh sách buổi học đang diễn ra trực tuyến
  const liveSessions = useMemo(() => {
    return calendarSessions.filter((s) => s.normalizedStatus === 'InProgress' && Number(s.sessionId) > 0);
  }, [calendarSessions]);

  // Thống kê số lượng buổi học trong tuần
  const stats = useMemo(() => {
    const total = calendarSessions.length;
    const upcoming = calendarSessions.filter((s) => s.normalizedStatus === 'Scheduled').length;
    const inProgress = calendarSessions.filter((s) => s.normalizedStatus === 'InProgress').length;
    const done = calendarSessions.filter((s) => s.normalizedStatus === 'Done').length;
    return { total, upcoming, inProgress, done };
  }, [calendarSessions]);

  // Tự động điền thông tin khi bấm vào một ô trống trên lịch
  const handleSlotClick = (dateStr, hour) => {
    setSessionDate(dateStr);
    setSessionHour(hour);
    if (scheduleData?.activeClassrooms?.length > 0 && !selectedClassroomId) {
      setSelectedClassroomId(String(scheduleData.activeClassrooms[0].id));
    }
    setShowCreateModal(true);
  };

  // Xử lý tạo buổi học
  const handleCreateSession = async (e) => {
    e.preventDefault();
    if (!selectedClassroomId || !sessionDate) {
      setError('Vui lòng chọn lớp học và ngày học.');
      return;
    }

    setCreating(true);
    setMessage('');
    setError('');

    try {
      // Giờ Việt Nam là UTC+7 -> Giờ UTC = sessionHour - 7
      const utcDate = new Date(`${sessionDate}T00:00:00Z`);
      utcDate.setUTCHours(Number(sessionHour) - 7);

      await tutorApi.createSession({
        classroomId: Number(selectedClassroomId),
        scheduledAtUtc: utcDate.toISOString(),
      });
      setMessage('Tạo buổi học thành công!');
      setShowCreateModal(false);
      setSelectedClassroomId('');
      setSessionDate('');
      setReloadKey((k) => k + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Có lỗi xảy ra khi tạo buổi học.');
    } finally {
      setCreating(false);
    }
  };

  // Xử lý hủy buổi học
  const handleCancelSession = async (sessionId) => {
    if (!window.confirm('Bạn có chắc chắn muốn hủy buổi học này không?')) return;
    setCancelling(true);
    try {
      await tutorApi.cancelSession(sessionId);
      setMessage('Đã hủy buổi học thành công.');
      setSelectedSession(null);
      setReloadKey((k) => k + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể hủy buổi học.');
    } finally {
      setCancelling(false);
    }
  };

  return (
    <>
      <style>{`
        .schedule-page {
          --bs-primary: #DB303A;
          --bs-primary-rgb: 219, 48, 58;
          --tutor-accent: #DB303A;
          --tutor-accent-soft: #FEE2E2;
          --tutor-accent-border: #FCA5A5;
          --tutor-border: var(--bs-border-color, #E5E7EB);
        }

        .schedule-card {
          background: var(--bs-body-bg);
          border: 1px solid var(--tutor-border);
          border-radius: 12px;
        }

        .schedule-table-wrap {
          overflow-x: auto;
          width: 100%;
        }

        .schedule-table {
          width: 100%;
          min-width: 820px;
          table-layout: fixed;
          border-collapse: separate;
          border-spacing: 4px;
          font-size: 12px;
        }

        .schedule-table th {
          padding: 8px 4px;
          text-align: center;
          white-space: nowrap;
          border-radius: 6px;
        }

        .schedule-table th.period-column {
          width: 48px;
          color: var(--tutor-accent);
          font-size: 11px;
          font-weight: 700;
          text-transform: uppercase;
          background: rgba(219, 48, 58, 0.04);
          vertical-align: middle;
        }

        .schedule-table th.time-column {
          width: 105px;
          font-size: 11px;
          font-weight: 600;
          color: var(--bs-secondary-color);
          background: rgba(0, 0, 0, 0.02);
          vertical-align: middle;
        }

        .schedule-table th.day-column {
          background: var(--bs-tertiary-bg);
          border: 1px solid transparent;
        }

        .schedule-table th.day-column.is-today {
          background: var(--tutor-accent-soft);
          color: var(--tutor-accent);
          border-color: var(--tutor-accent-border);
          font-weight: 700;
        }

        .schedule-table td {
          padding: 0;
          vertical-align: top;
          height: 84px;
        }

        .day-title {
          font-size: 13px;
          font-weight: 700;
          line-height: 1.2;
        }

        .day-date {
          font-size: 11px;
          color: var(--bs-secondary-color);
        }

        .is-today .day-date {
          color: var(--tutor-accent);
          font-weight: 600;
        }

        /* Clean slot box */
        .schedule-cell-slot {
          position: relative;
          display: flex;
          flex-direction: column;
          width: 100%;
          height: 100%;
          min-height: 84px;
          padding: 4px;
          border: 1px dashed var(--tutor-border);
          border-radius: 8px;
          background: var(--bs-body-bg);
          transition: all .15s ease-in-out;
        }

        .schedule-cell-slot:hover {
          border-color: var(--tutor-accent-border);
          background: rgba(219, 48, 58, 0.02);
        }

        .schedule-cell-slot.has-session {
          border: none;
          padding: 0;
          background: transparent;
        }

        .empty-slot-btn {
          opacity: 0;
          transition: opacity .15s ease;
          width: 100%;
          height: 100%;
          display: flex;
          align-items: center;
          justify-content: center;
          gap: 4px;
          color: var(--bs-secondary-color);
          font-size: 11px;
          cursor: pointer;
          border: none;
          background: transparent;
        }

        .schedule-cell-slot:hover .empty-slot-btn {
          opacity: 1;
          color: var(--tutor-accent);
        }

        /* Teaching Session Card */
        .session-card-item {
          display: flex;
          flex-direction: column;
          gap: 3px;
          padding: 6px 8px;
          border-radius: 8px;
          text-align: left;
          cursor: pointer;
          transition: transform .15s ease, box-shadow .15s ease;
          box-shadow: 0 1px 3px rgba(0, 0, 0, 0.06);
          border-left: 4px solid #DB303A;
          background: var(--bs-body-bg);
          height: 100%;
        }

        .session-card-item:hover {
          transform: translateY(-2px);
          box-shadow: 0 6px 12px rgba(0, 0, 0, 0.12);
        }

        .session-card-item.status-scheduled {
          border-color: #E2E8F0;
          border-left-color: #3B82F6;
          background: #F8FAFC;
        }

        .session-card-item.status-inprogress {
          border-color: #FCA5A5;
          border-left-color: #DC2626;
          background: #FEF2F2;
          animation: pulseBorder 2s infinite;
        }

        .session-card-item.status-done {
          border-color: #E2E8F0;
          border-left-color: #10B981;
          background: #F0FDF4;
          opacity: 0.9;
        }

        .session-card-item.status-cancelled {
          border-color: #E2E8F0;
          border-left-color: #64748B;
          background: #F1F5F9;
          opacity: 0.7;
        }

        @keyframes pulseBorder {
          0% { box-shadow: 0 0 0 0 rgba(220, 38, 38, 0.4); }
          70% { box-shadow: 0 0 0 6px rgba(220, 38, 38, 0); }
          100% { box-shadow: 0 0 0 0 rgba(220, 38, 38, 0); }
        }

        .session-title {
          font-weight: 700;
          font-size: 11px;
          line-height: 1.25;
          color: #0F172A;
        }

        .session-meta {
          font-size: 10px;
          color: #475569;
          display: flex;
          align-items: center;
          gap: 4px;
        }

        /* Mobile day tabs */
        .availability-mobile-day-picker { display: none; }
        @media (max-width: 767.98px) {
          .availability-mobile-day-picker {
            display: flex;
            gap: .35rem;
            overflow-x: auto;
            padding: .2rem 0 .6rem;
          }
          .availability-day-button {
            flex: 1 0 auto;
            min-width: 2.8rem;
            padding: .45rem .4rem;
            border-radius: 8px;
            border: 1px solid var(--tutor-border);
            background: var(--bs-body-bg);
            font-size: .82rem;
            text-align: center;
          }
          .availability-day-button[aria-pressed="true"] {
            border-color: var(--tutor-accent);
            background: var(--tutor-accent);
            color: #fff;
            font-weight: 700;
          }
          .schedule-table {
            min-width: 0 !important;
            width: 100% !important;
            table-layout: fixed;
          }
          .schedule-table th.time-column,
          .schedule-table td.time-cell {
            width: 82px !important;
            min-width: 82px !important;
            padding: 0.45rem !important;
            font-size: 0.75rem;
          }
          .schedule-table th.day-column,
          .schedule-table td.day-cell {
            display: none;
            width: auto !important;
            min-width: 0 !important;
          }
          .schedule-page[data-active-day="0"] .schedule-table [data-day-col="0"],
          .schedule-page[data-active-day="1"] .schedule-table [data-day-col="1"],
          .schedule-page[data-active-day="2"] .schedule-table [data-day-col="2"],
          .schedule-page[data-active-day="3"] .schedule-table [data-day-col="3"],
          .schedule-page[data-active-day="4"] .schedule-table [data-day-col="4"],
          .schedule-page[data-active-day="5"] .schedule-table [data-day-col="5"],
          .schedule-page[data-active-day="6"] .schedule-table [data-day-col="6"] {
            display: table-cell;
          }
        }
      `}</style>

      <div className="container-fluid py-4 schedule-page" data-active-day={activeDayMobile}>
        {/* Tiêu đề trang */}
        <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-3">
          <div>
            <h1 className="h3 mb-1 fw-bold text-dark">Lịch dạy & Buổi học</h1>
            <p className="text-secondary mb-0 small">
              Thời khóa biểu giảng dạy theo giờ Việt Nam (GMT+7). Quản lý lịch dạy, vào phòng học trực tuyến và xếp lịch học bù.
            </p>
          </div>
          <div className="d-flex flex-wrap gap-2">
            <button
              type="button"
              className="btn btn-danger btn-sm px-3 py-2 fw-semibold rounded-2 d-inline-flex align-items-center gap-1.5 shadow-sm"
              style={{ backgroundColor: '#DB303A', borderColor: '#DB303A' }}
              onClick={() => {
                setSessionDate(todayStr);
                setSessionHour(19);
                if (scheduleData?.activeClassrooms?.length > 0) {
                  setSelectedClassroomId(String(scheduleData.activeClassrooms[0].id));
                }
                setShowCreateModal(true);
              }}
            >
              <i className="bi bi-calendar-plus me-1" aria-hidden="true"></i>+ Tạo buổi học / Dạy bù
            </button>
            <Link to="/tutor/availability" className="btn btn-outline-secondary btn-sm px-3 py-2 rounded-2">
              <i className="bi bi-clock-history me-1" aria-hidden="true"></i>Cài đặt lịch rảnh
            </Link>
            <Link to="/tutor/connection-requests" className="btn btn-outline-danger btn-sm px-3 py-2 rounded-2">
              <i className="bi bi-person-plus me-1" aria-hidden="true"></i>Yêu cầu kết nối
            </Link>
          </div>
        </div>

        {/* Thông báo hệ thống */}
        {message && (
          <div className="alert alert-success alert-dismissible fade show mb-3 shadow-sm rounded-3" role="status">
            <i className="bi bi-check-circle-fill me-2" aria-hidden="true"></i>{message}
            <button type="button" className="btn-close" onClick={() => setMessage('')} aria-label="Đóng"></button>
          </div>
        )}

        {error && (
          <div className="alert alert-warning alert-dismissible fade show mb-3 shadow-sm rounded-3" role="alert">
            <i className="bi bi-exclamation-triangle-fill me-2" aria-hidden="true"></i>{error}
            <button type="button" className="btn-close" onClick={() => setError('')} aria-label="Đóng"></button>
          </div>
        )}

        {/* Biểu ngữ buổi học đang diễn ra */}
        {liveSessions.length > 0 && (
          <section className="alert alert-danger border-danger-subtle p-3 mb-4 rounded-3 shadow-sm" aria-labelledby="live-heading">
            <div className="d-flex align-items-center justify-content-between flex-wrap gap-2 mb-2">
              <div className="d-flex align-items-center gap-2">
                <span className="spinner-grow spinner-grow-sm text-danger" role="status"></span>
                <h2 id="live-heading" className="h6 fw-bold mb-0 text-danger">Buổi học đang diễn ra</h2>
                <span className="badge text-bg-danger">{liveSessions.length}</span>
              </div>
            </div>
            <div className="d-flex flex-column gap-2">
              {liveSessions.map((session) => (
                <div key={session.sessionId} className="d-flex align-items-center justify-content-between bg-white p-2.5 px-3 rounded-2 border border-danger-subtle shadow-sm flex-wrap gap-2">
                  <div>
                    <div className="fw-bold text-dark">{session.classroomName} · {session.studentName}</div>
                    <div className="small text-secondary">{session.subjectName} · {session.date} lúc {String(session.hour).padStart(2, '0')}:00</div>
                  </div>
                  <Link to={`/room/${session.sessionId}`} className="btn btn-danger btn-sm px-3 fw-bold shadow-sm">
                    <i className="bi bi-camera-video-fill me-1" aria-hidden="true" />Vào phòng học ngay
                  </Link>
                </div>
              ))}
            </div>
          </section>
        )}

        {/* Thống kê tóm tắt số buổi học */}
        <div className="row g-3 mb-4">
          <div className="col-6 col-md-3">
            <div className="card border-0 shadow-sm rounded-3 p-3 bg-white">
              <div className="d-flex align-items-center justify-content-between">
                <div>
                  <div className="text-secondary small fw-medium">Tổng buổi tuần này</div>
                  <div className="h4 fw-bold mb-0 mt-1 text-dark">{stats.total}</div>
                </div>
                <div className="rounded-circle bg-primary-subtle text-primary p-2.5 d-flex align-items-center justify-content-center" style={{ width: 44, height: 44 }}>
                  <i className="bi bi-calendar3 fs-5"></i>
                </div>
              </div>
            </div>
          </div>
          <div className="col-6 col-md-3">
            <div className="card border-0 shadow-sm rounded-3 p-3 bg-white">
              <div className="d-flex align-items-center justify-content-between">
                <div>
                  <div className="text-secondary small fw-medium">Buổi sắp tới</div>
                  <div className="h4 fw-bold mb-0 mt-1 text-primary">{stats.upcoming}</div>
                </div>
                <div className="rounded-circle bg-info-subtle text-info p-2.5 d-flex align-items-center justify-content-center" style={{ width: 44, height: 44 }}>
                  <i className="bi bi-clock-history fs-5"></i>
                </div>
              </div>
            </div>
          </div>
          <div className="col-6 col-md-3">
            <div className="card border-0 shadow-sm rounded-3 p-3 bg-white">
              <div className="d-flex align-items-center justify-content-between">
                <div>
                  <div className="text-secondary small fw-medium">Đang diễn ra</div>
                  <div className="h4 fw-bold mb-0 mt-1 text-danger">{stats.inProgress}</div>
                </div>
                <div className="rounded-circle bg-danger-subtle text-danger p-2.5 d-flex align-items-center justify-content-center" style={{ width: 44, height: 44 }}>
                  <i className="bi bi-broadcast fs-5"></i>
                </div>
              </div>
            </div>
          </div>
          <div className="col-6 col-md-3">
            <div className="card border-0 shadow-sm rounded-3 p-3 bg-white">
              <div className="d-flex align-items-center justify-content-between">
                <div>
                  <div className="text-secondary small fw-medium">Đã hoàn thành</div>
                  <div className="h4 fw-bold mb-0 mt-1 text-success">{stats.done}</div>
                </div>
                <div className="rounded-circle bg-success-subtle text-success p-2.5 d-flex align-items-center justify-content-center" style={{ width: 44, height: 44 }}>
                  <i className="bi bi-check2-circle fs-5"></i>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Thẻ thời khóa biểu */}
        <div className="schedule-card p-3 mb-4 shadow-sm">
          {/* Thanh điều khiển tuần */}
          <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-3 pb-3 border-bottom">
            <div className="d-flex align-items-center gap-2">
              <button
                type="button"
                className="btn btn-outline-secondary btn-sm rounded-2"
                onClick={() => setWeekOffset(weekOffset - 1)}
                title="Tuần trước"
              >
                <i className="bi bi-chevron-left me-1" aria-hidden="true"></i>Tuần trước
              </button>
              <button
                type="button"
                className={`btn ${weekOffset === 0 ? 'btn-danger' : 'btn-outline-danger'} btn-sm rounded-2 fw-semibold`}
                style={weekOffset === 0 ? { backgroundColor: '#DB303A', borderColor: '#DB303A' } : {}}
                onClick={() => { setWeekOffset(0); setJumpDate(''); }}
              >
                Tuần này
              </button>
              <button
                type="button"
                className="btn btn-outline-secondary btn-sm rounded-2"
                onClick={() => setWeekOffset(weekOffset + 1)}
                title="Tuần sau"
              >
                Tuần sau<i className="bi bi-chevron-right ms-1" aria-hidden="true"></i>
              </button>
            </div>

            <div className="d-flex align-items-center gap-2">
              <i className="bi bi-calendar-week text-danger fs-5" aria-hidden="true"></i>
              <span className="h6 mb-0 fw-bold text-dark">
                {formatDisplayDateFull(mondayDate)} – {formatDisplayDateFull(weekDates[6])}
              </span>
              {weekOffset !== 0 && (
                <span className="badge bg-secondary-subtle text-secondary border">
                  {weekOffset > 0 ? `+${weekOffset} tuần` : `${weekOffset} tuần`}
                </span>
              )}
            </div>

            <div className="d-flex align-items-center gap-2">
              <input
                id="jumpDateInput"
                type="date"
                className="form-control form-control-sm rounded-2"
                style={{ maxWidth: 150 }}
                value={jumpDate || formatDateOnly(mondayDate)}
                onChange={(e) => {
                  setJumpDate(e.target.value);
                  setWeekOffset(0);
                }}
                title="Chọn ngày xem lịch"
              />
            </div>
          </div>

          {/* Chú thích màu sắc trên lịch */}
          <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-3">
            <div className="d-flex flex-wrap align-items-center gap-3 small text-secondary">
              <span className="d-inline-flex align-items-center gap-1.5">
                <span className="badge bg-primary text-white" style={{ width: 12, height: 12, padding: 0, borderRadius: 3 }}> </span>
                Lịch dạy sắp tới
              </span>
              <span className="d-inline-flex align-items-center gap-1.5">
                <span className="badge bg-danger text-white" style={{ width: 12, height: 12, padding: 0, borderRadius: 3 }}> </span>
                Đang diễn ra (Vào lớp ngay)
              </span>
              <span className="d-inline-flex align-items-center gap-1.5">
                <span className="badge bg-success text-white" style={{ width: 12, height: 12, padding: 0, borderRadius: 3 }}> </span>
                Đã học xong
              </span>
              <span className="d-inline-flex align-items-center gap-1.5">
                <span className="border border-dashed border-secondary-subtle" style={{ width: 12, height: 12, borderRadius: 3, display: 'inline-block' }}></span>
                Trống (Bấm để tạo buổi dạy bù)
              </span>
            </div>
            <div className="small text-secondary">
              <i className="bi bi-info-circle me-1" aria-hidden="true"></i>Bấm vào buổi học để xem chi tiết / Bấm vào ô trống để tạo lịch bù.
            </div>
          </div>

          {/* Bộ chọn ngày trên thiết bị di động */}
          <div className="availability-mobile-day-picker mb-2" role="group" aria-label="Chọn ngày trong tuần">
            {weekDates.map((d, idx) => {
              const isToday = formatDateOnly(d) === todayStr;
              return (
                <button
                  key={idx}
                  type="button"
                  className="availability-day-button"
                  aria-pressed={activeDayMobile === idx}
                  onClick={() => setActiveDayMobile(idx)}
                >
                  <div>{DAYS[idx].short}</div>
                  <div className="small">{formatDisplayDateShort(d)} {isToday ? '•' : ''}</div>
                </button>
              );
            })}
          </div>

          {/* Bảng thời khóa biểu chính */}
          {loading ? (
            <div className="text-center py-5">
              <div className="spinner-border text-danger" role="status"></div>
              <div className="small text-secondary mt-2">Đang tải lịch giảng dạy...</div>
            </div>
          ) : (
            <div className="schedule-table-wrap">
              <table className="schedule-table mb-0">
                <thead>
                  <tr>
                    <th className="period-column" scope="col">Buổi</th>
                    <th className="time-column" scope="col">Khung giờ</th>
                    {weekDates.map((curDate, idx) => {
                      const curDateStr = formatDateOnly(curDate);
                      const isToday = curDateStr === todayStr;
                      return (
                        <th
                          key={idx}
                          className={`day-column ${isToday ? 'is-today' : ''}`}
                          data-day-col={idx}
                          scope="col"
                        >
                          <div className="day-title">{DAYS[idx].name}</div>
                          <div className="day-date">{formatDisplayDateShort(curDate)}</div>
                        </th>
                      );
                    })}
                  </tr>
                </thead>
                <tbody>
                  {TIME_BLOCKS.map((block, bIdx) => {
                    const isFirstPeriod = bIdx === 0 || TIME_BLOCKS[bIdx - 1].period !== block.period;
                    const periodCount = TIME_BLOCKS.filter((b) => b.period === block.period).length;

                    return (
                      <tr key={bIdx}>
                        {isFirstPeriod && (
                          <th className="period-column" scope="rowgroup" rowSpan={periodCount}>
                            {block.period}
                          </th>
                        )}
                        <th className="time-column" scope="row">
                          {block.label}
                        </th>

                        {weekDates.map((curDate, dIdx) => {
                          const curDateStr = formatDateOnly(curDate);

                          // Tìm các buổi học trùng ngày và trùng ca học này
                          const slotSessions = calendarSessions.filter((s) => {
                            if (s.date !== curDateStr) return false;
                            const h = Number(s.hour);
                            return h >= block.start && h < block.end;
                          });

                          return (
                            <td key={dIdx} className="day-cell" data-day-col={dIdx}>
                              {slotSessions.length > 0 ? (
                                <div className="schedule-cell-slot has-session d-flex flex-column gap-1">
                                  {slotSessions.map((session) => {
                                    const isDone = session.normalizedStatus === 'Done';
                                    const isInProgress = session.normalizedStatus === 'InProgress';
                                    const isCancelled = session.normalizedStatus === 'Cancelled';

                                    const cardClass = isInProgress
                                      ? 'status-inprogress'
                                      : isDone
                                      ? 'status-done'
                                      : isCancelled
                                      ? 'status-cancelled'
                                      : 'status-scheduled';

                                    const badgeClass = isInProgress
                                      ? 'bg-danger text-white'
                                      : isDone
                                      ? 'bg-success text-white'
                                      : isCancelled
                                      ? 'bg-secondary text-white'
                                      : 'bg-primary text-white';

                                    const statusText = isInProgress
                                      ? 'Đang diễn ra'
                                      : isDone
                                      ? 'Đã học'
                                      : isCancelled
                                      ? 'Đã hủy'
                                      : 'Sắp tới';

                                    return (
                                      <div
                                        key={session.sessionId || session.id}
                                        className={`session-card-item ${cardClass}`}
                                        onClick={() => setSelectedSession(session)}
                                        title="Bấm để xem chi tiết buổi học"
                                      >
                                        <div className="d-flex align-items-center justify-content-between gap-1">
                                          <span className="small fw-bold text-dark">
                                            <i className="bi bi-clock me-1 text-secondary" style={{ fontSize: 10 }}></i>
                                            {String(session.hour).padStart(2, '0')}:00
                                          </span>
                                          <span className={`badge ${badgeClass}`} style={{ fontSize: 9, padding: '2px 5px' }}>
                                            {statusText}
                                          </span>
                                        </div>

                                        <div className="session-title text-truncate" title={session.classroomName}>
                                          {session.classroomName}
                                        </div>

                                        <div className="session-meta text-truncate">
                                          <i className="bi bi-person me-0.5"></i>
                                          <span>{session.studentName || 'Học viên'}</span>
                                        </div>

                                        <div className="d-flex align-items-center justify-content-between gap-1 mt-auto pt-1">
                                          <span className="badge bg-light text-secondary border" style={{ fontSize: 9 }}>
                                            {session.subjectName}
                                          </span>

                                          {Number(session.sessionId) > 0 && !isDone && !isCancelled && (
                                            session.canJoin ? (
                                              <Link
                                                to={`/room/${session.sessionId}`}
                                                className={`btn btn-sm py-0 px-1.5 fw-semibold ${isInProgress ? 'btn-danger animate-pulse' : 'btn-outline-danger'}`}
                                                style={{ fontSize: 10 }}
                                                onClick={(e) => e.stopPropagation()}
                                              >
                                                <i className="bi bi-camera-video me-1" aria-hidden="true" />Vào lớp
                                              </Link>
                                            ) : (
                                              <span className="badge bg-light text-muted border py-0 px-1" style={{ fontSize: 9 }}>
                                                Chưa mở
                                              </span>
                                            )
                                          )}
                                        </div>
                                      </div>
                                    );
                                  })}
                                </div>
                              ) : (
                                <div
                                  className="schedule-cell-slot"
                                  onClick={() => handleSlotClick(curDateStr, block.start)}
                                  title={`Bấm để tạo buổi dạy bù lúc ${block.label} ngày ${formatDisplayDateShort(curDate)}`}
                                >
                                  <button type="button" className="empty-slot-btn">
                                    <i className="bi bi-plus-circle" aria-hidden="true"></i>
                                    <span>Lên lịch</span>
                                  </button>
                                </div>
                              )}
                            </td>
                          );
                        })}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Bảng danh sách buổi học trong tuần */}
        <div className="card border-0 shadow-sm rounded-3 p-3 bg-white">
          <div className="d-flex align-items-center justify-content-between mb-3">
            <h2 className="h5 fw-bold mb-0 text-dark">
              <i className="bi bi-card-checklist me-2 text-danger"></i>Danh sách buổi học trong tuần
            </h2>
            <span className="badge bg-secondary-subtle text-secondary">{calendarSessions.length} buổi</span>
          </div>

          {calendarSessions.length === 0 ? (
            <div className="text-center py-4 text-secondary">
              <i className="bi bi-calendar-x fs-2 d-block mb-2 text-secondary-subtle"></i>
              Chưa có buổi học nào được xếp trong tuần này. Bấm <strong>"+ Tạo buổi học / Dạy bù"</strong> hoặc chọn một ô trống trên lịch để lên lịch giảng dạy.
            </div>
          ) : (
            <div className="table-responsive">
              <table className="table align-middle table-hover mb-0">
                <thead className="table-light">
                  <tr>
                    <th>Thời gian</th>
                    <th>Lớp học</th>
                    <th>Học viên</th>
                    <th>Môn học</th>
                    <th>Hình thức</th>
                    <th>Trạng thái</th>
                    <th className="text-end">Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {calendarSessions.map((s) => {
                    const isDone = s.normalizedStatus === 'Done';
                    const isInProgress = s.normalizedStatus === 'InProgress';
                    const isCancelled = s.normalizedStatus === 'Cancelled';

                    return (
                      <tr key={s.sessionId || s.id}>
                        <td className="fw-semibold text-danger">
                          <i className="bi bi-clock me-1 text-secondary"></i>
                          {s.date} lúc {String(s.hour).padStart(2, '0')}:00
                        </td>
                        <td className="fw-bold">{s.classroomName}</td>
                        <td>{s.studentName || 'Chưa xếp'}</td>
                        <td>
                          <span className="badge bg-light text-dark border">{s.subjectName}</span>
                        </td>
                        <td>
                          <span className="badge bg-secondary-subtle text-secondary">
                            {s.classroomType === 'OneToOne' || s.classroomType === 0 ? '1-1' : 'Lớp nhóm'}
                          </span>
                        </td>
                        <td>
                          <span
                            className={`badge ${
                              isInProgress
                                ? 'bg-danger'
                                : isDone
                                ? 'bg-success'
                                : isCancelled
                                ? 'bg-secondary'
                                : 'bg-primary'
                            }`}
                          >
                            {isInProgress ? 'Đang diễn ra' : isDone ? 'Đã học' : isCancelled ? 'Đã hủy' : 'Sắp tới'}
                          </span>
                        </td>
                        <td className="text-end">
                          <div className="d-inline-flex gap-1">
                            {Number(s.sessionId) > 0 && !isDone && !isCancelled && (
                              s.canJoin ? (
                                <Link to={`/room/${s.sessionId}`} className="btn btn-sm btn-danger px-2.5">
                                  <i className="bi bi-camera-video me-1" aria-hidden="true" />Vào lớp
                                </Link>
                              ) : (
                                <button type="button" className="btn btn-sm btn-outline-secondary px-2" disabled title="Chưa đến giờ mở phòng">
                                  Chưa mở
                                </button>
                              )
                            )}
                            <button
                              type="button"
                              className="btn btn-sm btn-outline-secondary px-2"
                              onClick={() => setSelectedSession(s)}
                              title="Xem chi tiết"
                            >
                              <i className="bi bi-eye"></i>
                            </button>
                            {!isDone && !isCancelled && (
                              <button
                                type="button"
                                className="btn btn-sm btn-outline-danger px-2"
                                onClick={() => handleCancelSession(s.sessionId)}
                                title="Hủy buổi học"
                              >
                                <i className="bi bi-x-circle"></i>
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Modal: Tự tạo lịch học / Dạy bù */}
        {showCreateModal && (
          <div className="modal fade show d-block" style={{ backgroundColor: 'rgba(0,0,0,0.5)' }} role="dialog">
            <div className="modal-dialog modal-dialog-centered">
              <div className="modal-content border-0 shadow-lg rounded-3">
                <form onSubmit={handleCreateSession}>
                  <div className="modal-header border-bottom">
                    <h5 className="modal-title fw-bold text-dark">
                      <i className="bi bi-calendar-plus text-danger me-2"></i>Tự tạo lịch học / Dạy bù
                    </h5>
                    <button type="button" className="btn-close" onClick={() => setShowCreateModal(false)}></button>
                  </div>
                  <div className="modal-body">
                    <div className="mb-3">
                      <label className="form-label fw-semibold">Chọn lớp học <span className="text-danger">*</span></label>
                      <select
                        className="form-select"
                        value={selectedClassroomId}
                        onChange={(e) => setSelectedClassroomId(e.target.value)}
                        required
                      >
                        <option value="">-- Chọn lớp đang dạy --</option>
                        {(scheduleData?.activeClassrooms || []).map((c) => (
                          <option key={c.id} value={c.id}>
                            {c.name} · {c.subjectName} ({c.type})
                          </option>
                        ))}
                      </select>
                      {(!scheduleData?.activeClassrooms || scheduleData.activeClassrooms.length === 0) && (
                        <div className="small text-danger mt-1">
                          Bạn chưa có lớp học nào đang hoạt động. Vui lòng tạo lớp học trong Quản lý lớp trước.
                        </div>
                      )}
                    </div>

                    <div className="mb-3">
                      <label className="form-label fw-semibold">Ngày học <span className="text-danger">*</span></label>
                      <input
                        type="date"
                        className="form-control"
                        value={sessionDate}
                        onChange={(e) => setSessionDate(e.target.value)}
                        required
                      />
                    </div>

                    <div className="mb-3">
                      <label className="form-label fw-semibold">Khung giờ bắt đầu <span className="text-danger">*</span></label>
                      <select
                        className="form-select"
                        value={sessionHour}
                        onChange={(e) => setSessionHour(Number(e.target.value))}
                      >
                        {TIME_BLOCKS.map((b) => (
                          <option key={b.start} value={b.start}>
                            {b.label} ({b.period})
                          </option>
                        ))}
                      </select>
                      <div className="form-text small text-secondary">
                        Mỗi buổi học theo chuẩn kéo dài 2 tiếng (GMT+7).
                      </div>
                    </div>
                  </div>
                  <div className="modal-footer border-top">
                    <button type="button" className="btn btn-secondary rounded-2" onClick={() => setShowCreateModal(false)}>
                      Hủy bỏ
                    </button>
                    <button
                      type="submit"
                      className="btn btn-danger rounded-2 px-3 fw-semibold shadow-sm"
                      style={{ backgroundColor: '#DB303A', borderColor: '#DB303A' }}
                      disabled={creating || !selectedClassroomId}
                    >
                      {creating ? (
                        <>
                          <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                          Đang tạo...
                        </>
                      ) : (
                        'Xác nhận tạo lịch'
                      )}
                    </button>
                  </div>
                </form>
              </div>
            </div>
          </div>
        )}

        {/* Modal: Xem chi tiết buổi học */}
        {selectedSession && (
          <div className="modal fade show d-block" style={{ backgroundColor: 'rgba(0,0,0,0.5)' }} role="dialog">
            <div className="modal-dialog modal-dialog-centered">
              <div className="modal-content border-0 shadow-lg rounded-3">
                <div className="modal-header border-bottom">
                  <h5 className="modal-title fw-bold text-dark">
                    <i className="bi bi-info-circle text-primary me-2"></i>Chi tiết buổi học
                  </h5>
                  <button type="button" className="btn-close" onClick={() => setSelectedSession(null)}></button>
                </div>
                <div className="modal-body">
                  <div className="mb-3 p-3 bg-light rounded-3">
                    <div className="h6 fw-bold text-dark mb-1">{selectedSession.classroomName}</div>
                    <div className="small text-secondary mb-2">Môn học: <strong className="text-dark">{selectedSession.subjectName}</strong></div>
                    <div className="d-flex align-items-center gap-2">
                      <span className="badge bg-secondary-subtle text-secondary">
                        {selectedSession.classroomType === 'OneToOne' || selectedSession.classroomType === 0 ? 'Lớp 1-1' : 'Lớp nhóm'}
                      </span>
                      <span
                        className={`badge ${
                          selectedSession.normalizedStatus === 'InProgress'
                            ? 'bg-danger'
                            : selectedSession.normalizedStatus === 'Done'
                            ? 'bg-success'
                            : selectedSession.normalizedStatus === 'Cancelled'
                            ? 'bg-secondary'
                            : 'bg-primary'
                        }`}
                      >
                        {selectedSession.normalizedStatus === 'InProgress' ? 'Đang diễn ra' : selectedSession.normalizedStatus === 'Done' ? 'Đã học' : selectedSession.normalizedStatus === 'Cancelled' ? 'Đã hủy' : 'Sắp diễn ra'}
                      </span>
                    </div>
                  </div>

                  <div className="row g-2 mb-3">
                    <div className="col-6">
                      <div className="small text-secondary">Thời gian</div>
                      <div className="fw-semibold text-danger">
                        {selectedSession.date} lúc {String(selectedSession.hour).padStart(2, '0')}:00
                      </div>
                    </div>
                    <div className="col-6">
                      <div className="small text-secondary">Học viên</div>
                      <div className="fw-semibold text-dark">{selectedSession.studentName || 'Chưa xếp'}</div>
                    </div>
                  </div>

                  {selectedSession.videoRoom && (
                    <div className="mb-3">
                      <div className="small text-secondary mb-1">Mã phòng học trực tuyến</div>
                      <code className="p-2 bg-light border rounded d-block text-break font-monospace small">
                        {selectedSession.videoRoom}
                      </code>
                    </div>
                  )}
                </div>
                <div className="modal-footer border-top d-flex justify-content-between">
                  <div>
                    {!['Done', 'Cancelled'].includes(selectedSession.normalizedStatus) && (
                      <button
                        type="button"
                        className="btn btn-outline-danger btn-sm"
                        disabled={cancelling}
                        onClick={() => handleCancelSession(selectedSession.sessionId)}
                      >
                        {cancelling ? 'Đang hủy...' : 'Hủy buổi học này'}
                      </button>
                    )}
                  </div>
                  <div className="d-flex gap-2">
                    <button type="button" className="btn btn-secondary btn-sm" onClick={() => setSelectedSession(null)}>
                      Đóng
                    </button>
                    {Number(selectedSession.sessionId) > 0 && !['Done', 'Cancelled'].includes(selectedSession.normalizedStatus) && (
                      selectedSession.canJoin ? (
                        <Link
                          to={`/room/${selectedSession.sessionId}`}
                          className="btn btn-danger btn-sm px-3 fw-semibold"
                          style={{ backgroundColor: '#DB303A', borderColor: '#DB303A' }}
                        >
                          <i className="bi bi-camera-video me-1" aria-hidden="true" />Vào phòng học
                        </Link>
                      ) : (
                        <button
                          type="button"
                          className="btn btn-outline-secondary btn-sm px-3 fw-semibold"
                          disabled
                        >
                          Phòng chưa mở
                        </button>
                      )
                    )}
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </>
  );
}
