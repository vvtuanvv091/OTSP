import { useState, useEffect, useMemo } from 'react';
import { Link } from 'react-router-dom';
import studentApi from '../../api/studentApi';

const PERIOD_ROWS = [
  // Sáng (3 blocks)
  { period: 'Sáng', start: 6, end: 8, label: '06:00 – 08:00', isFirst: true, span: 3 },
  { period: 'Sáng', start: 8, end: 10, label: '08:00 – 10:00', isFirst: false },
  { period: 'Sáng', start: 10, end: 12, label: '10:00 – 12:00', isFirst: false },
  // Chiều (2 blocks)
  { period: 'Chiều', start: 13, end: 15, label: '13:00 – 15:00', isFirst: true, span: 2 },
  { period: 'Chiều', start: 15, end: 17, label: '15:00 – 17:00', isFirst: false },
  // Tối (3 blocks)
  { period: 'Tối', start: 17, end: 19, label: '17:00 – 19:00', isFirst: true, span: 3 },
  { period: 'Tối', start: 19, end: 21, label: '19:00 – 21:00', isFirst: false },
  { period: 'Tối', start: 21, end: 23, label: '21:00 – 23:00', isFirst: false }
];

const DAY_TITLES = ['Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7', 'Chủ nhật'];
const DAY_SHORTS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

const parseDateOnly = (value) => {
  if (!value) return new Date();
  const [year, month, day] = value.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day));
};

const formatDateOnly = (date) => [
  date.getUTCFullYear(),
  String(date.getUTCMonth() + 1).padStart(2, '0'),
  String(date.getUTCDate()).padStart(2, '0')
].join('-');

const getVietnamToday = () => {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit'
  }).formatToParts(new Date());
  const values = Object.fromEntries(parts.map(({ type, value }) => [type, value]));
  return `${values.year}-${values.month}-${values.day}`;
};

const getVietnamDateTime = (value) => {
  if (!value) return { date: getVietnamToday(), hour: 0 };
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    hourCycle: 'h23'
  }).formatToParts(new Date(value));
  const values = Object.fromEntries(parts.map(({ type, value: partValue }) => [type, partValue]));
  return {
    date: `${values.year}-${values.month}-${values.day}`,
    hour: Number(values.hour)
  };
};

export default function StudentSchedulePage() {
  const [weekOffset, setWeekOffset] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [sessions, setSessions] = useState([]);
  const [pendingPayments, setPendingPayments] = useState([]);
  const [reloadKey, setReloadKey] = useState(0);
  const [activeDayMobile, setActiveDayMobile] = useState(
    () => (((parseDateOnly(getVietnamToday()).getUTCDay() + 6) % 7) + 1) % 7
  );

  const { monday, sunday, weekDates, todayDayIndex } = useMemo(() => {
    const today = parseDateOnly(getVietnamToday());
    const todayIndex = (today.getUTCDay() + 6) % 7;
    const baseMonday = new Date(today);
    baseMonday.setUTCDate(baseMonday.getUTCDate() - todayIndex + weekOffset * 7);
    const dates = Array.from({ length: 7 }, (_, index) => {
      const date = new Date(baseMonday);
      date.setUTCDate(date.getUTCDate() + index);
      return date;
    });

    return {
      monday: baseMonday,
      sunday: dates[6],
      weekDates: dates,
      todayDayIndex: weekOffset === 0 ? todayIndex : -1
    };
  }, [weekOffset]);

  useEffect(() => {
    let active = true;
    async function loadSchedule() {
      try {
        setLoading(true);
        setError('');
        const res = await studentApi.getSchedule(formatDateOnly(monday), formatDateOnly(sunday));
        if (!active) return;
        if (res.data) {
          setPendingPayments(res.data.pendingPayments || []);
          const rawSlots = res.data.calendarSlots || res.data.sessions || [];
          const mapped = rawSlots.map((s) => {
            const vietnamWallTime = typeof s.scheduledAtVn === 'string'
              ? s.scheduledAtVn.match(/^(\d{4}-\d{2}-\d{2})T(\d{2}):/)
              : null;
            const localDateTime = vietnamWallTime
              ? { date: vietnamWallTime[1], hour: Number(vietnamWallTime[2]) }
              : getVietnamDateTime(s.scheduledAtUtc || s.scheduledAt);
            const sessionDate = s.date || localDateTime.date;
            const sessionHour = s.hour ?? localDateTime.hour;
            const dayOfWeek = (parseDateOnly(sessionDate).getUTCDay() + 6) % 7;
            const startHour = Number(sessionHour);

            const statusStr = typeof s.status === 'string'
              ? s.status
              : (s.status === 3 || s.status === 'InProgress'
                  ? 'InProgress'
                  : (s.status === 1 || s.status === 'Done'
                    ? 'Done'
                    : (s.status === 2 || s.status === 'Cancelled' ? 'Cancelled' : 'Scheduled')));

            return {
              sessionId: s.sessionId || s.id,
              classroomId: s.classroomId,
              classroomName: s.classroomName,
              subjectName: s.subjectName,
              tutorName: s.tutorName,
              classroomType: s.classroomType === 0 || s.classroomType === 'OneToOne' ? 'OneToOne' : 'Group',
              dayOfWeek,
              startHour,
              timeDisplay: `${String(startHour).padStart(2, '0')}:00`,
              fullTimeDisplay: `${String(startHour).padStart(2, '0')}:00 – ${String(startHour + 2).padStart(2, '0')}:00`,
              status: statusStr,
              canJoin: s.canJoin ?? (statusStr === 'InProgress'),
              isPaid: s.hasPaid ?? true,
              pendingPaymentId: s.pendingPaymentId,
              videoRoom: s.videoRoom
            };
          });
          setSessions(mapped);
        }
      } catch (err) {
        if (!active) return;
        setError(err.response?.data?.message || 'Không thể tải lịch học. Vui lòng thử lại.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadSchedule();
    return () => {
      active = false;
    };
  }, [monday, sunday, reloadKey]);

  const formatDateDisplay = (dateObj) => {
    const day = String(dateObj.getUTCDate()).padStart(2, '0');
    const month = String(dateObj.getUTCMonth() + 1).padStart(2, '0');
    return `${day}/${month}`;
  };

  const formatDateFull = (dateObj) => {
    const day = String(dateObj.getUTCDate()).padStart(2, '0');
    const month = String(dateObj.getUTCMonth() + 1).padStart(2, '0');
    const year = dateObj.getUTCFullYear();
    return `${day}/${month}/${year}`;
  };

  const handleJumpDateChange = (e) => {
    if (!e.target.value) return;
    const target = parseDateOnly(e.target.value);
    const current = parseDateOnly(getVietnamToday());
    const targetMonday = new Date(target);
    const currentMonday = new Date(current);
    targetMonday.setUTCDate(targetMonday.getUTCDate() - ((targetMonday.getUTCDay() + 6) % 7));
    currentMonday.setUTCDate(currentMonday.getUTCDate() - ((currentMonday.getUTCDay() + 6) % 7));
    setWeekOffset(Math.round((targetMonday - currentMonday) / (7 * 24 * 60 * 60 * 1000)));
  };

  return (
    <>
      {/* Định dạng CSS hệ thống thời khóa biểu OTSP */}
      <style>{`
        .schedule-page {
          --availability-accent: #DB303A;
          --availability-accent-text: #B7212B;
          --availability-accent-soft: #FDE8E8;
          --availability-accent-border: #FCA5A5;
          --availability-border: var(--bs-border-color, #E5E7EB);
          --availability-busy-bg: #374151;
          --availability-busy-border: #1F2937;
          --availability-busy-text: #FFFFFF;
          width: 100%;
          max-width: 100%;
        }

        .schedule-card {
          background: var(--bs-body-bg, #ffffff);
          border: 1px solid var(--availability-border, #E5E7EB);
          border-radius: 8px;
        }

        .schedule-table-wrap {
          overflow-x: auto;
          width: 100%;
          border: 1px solid var(--availability-border, #E5E7EB);
          border-radius: 8px;
          background: var(--bs-body-bg, #ffffff);
          box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
        }

        .schedule-table {
          width: 100%;
          min-width: 860px;
          table-layout: fixed;
          border-collapse: collapse;
          font-size: 12px;
        }

        .schedule-table thead th {
          position: sticky;
          top: 0;
          z-index: 5;
          background: var(--bs-tertiary-bg, #f8fafc);
        }

        .schedule-table th,
        .schedule-table td {
          border: 1px solid var(--availability-border, #E5E7EB);
        }

        .schedule-table th {
          padding: 8px 6px;
          color: var(--bs-body-color, #1e293b);
          font-weight: 500;
          text-align: center;
          white-space: nowrap;
        }

        .schedule-table th.period-column {
          width: 52px;
        }

        .schedule-table th.time-column {
          width: 110px;
          font-size: 11px;
          font-weight: 600;
          color: #475569;
        }

        .schedule-table td {
          padding: 4px;
          text-align: center;
          vertical-align: top;
          min-height: 56px;
          background: var(--bs-body-bg, #ffffff);
          transition: background-color .15s ease;
        }

        .schedule-table td:hover {
          background: rgba(0, 0, 0, 0.015);
        }

        .schedule-period-label {
          color: var(--availability-accent-text, #B7212B);
          font-size: 11px;
          font-weight: 700;
          letter-spacing: .5px;
          text-align: center;
          vertical-align: middle;
          background: rgba(219, 48, 58, 0.04);
        }

        .schedule-time-label {
          color: var(--bs-secondary-color, #64748B);
          font-size: 11px;
          white-space: nowrap;
          font-weight: 500;
          text-align: center;
          vertical-align: middle;
          background: rgba(0, 0, 0, 0.02);
        }

        .schedule-table th.day-column {
          background: var(--bs-tertiary-bg, #f8fafc);
          padding: 8px 4px;
        }

        .schedule-table th.day-column.is-today {
          background: var(--availability-accent-soft, #FDE8E8);
          color: var(--availability-accent-text, #B7212B);
          border-bottom: 2px solid #DB303A;
        }

        .day-title {
          font-size: 12.5px;
          font-weight: 700;
          line-height: 1.2;
          color: var(--bs-body-color, #0f172a);
        }

        .day-date {
          font-size: 11px;
          color: var(--bs-secondary-color, #64748B);
        }

        .is-today .day-title {
          color: #B7212B;
        }

        .is-today .day-date {
          color: #B7212B;
          font-weight: 600;
        }

        /* Clean rounded slot box */
        .availability-slot {
          position: relative;
          display: flex;
          width: 100%;
          min-height: 48px;
          height: 100%;
          align-items: center;
          justify-content: center;
          border-radius: 6px;
          background: transparent;
          font-size: 11px;
          line-height: 1;
          transition: all .15s ease-in-out;
        }

        .availability-slot.is-free {
          border: 1px solid var(--availability-accent-border, #FCA5A5);
          background: var(--availability-accent-soft, #FDE8E8);
          color: var(--availability-accent-text, #B7212B);
          font-weight: 600;
          cursor: pointer;
        }

        .availability-slot.is-free:hover {
          filter: brightness(.95);
          transform: translateY(-1px);
          box-shadow: 0 2px 4px rgba(219, 48, 58, .15);
        }

        .availability-slot.is-empty {
          background: transparent;
          min-height: 48px;
          height: 100%;
          width: 100%;
        }

        /* Session Card inside Cell */
        .schedule-session-card {
          border: 1px solid var(--availability-border, #E5E7EB);
          border-left: 3px solid #DB303A;
          border-radius: 6px;
          padding: 6px 7px;
          background: var(--bs-body-bg, #ffffff);
          text-align: left;
          cursor: pointer;
          transition: transform .15s ease, box-shadow .15s ease;
          box-shadow: 0 1px 3px rgba(0, 0, 0, .05);
        }

        .schedule-session-card:hover {
          transform: translateY(-1px);
          box-shadow: 0 4px 10px rgba(0, 0, 0, .1);
        }

        .schedule-session-card.status-inprogress {
          border-left-color: #dc2626;
          background: #fef2f2;
        }

        .schedule-session-card.status-done {
          border-left-color: #166534;
          background: #f0fdf4;
        }

        .schedule-session-card.status-scheduled {
          border-left-color: #0284c7;
          background: #f0f9ff;
        }

        .schedule-session-card.status-cancelled {
          border-left-color: #64748b;
          background: #f8fafc;
        }

        /* Dark mode overrides for high contrast and beautiful UI */
        [data-bs-theme="dark"] .schedule-card {
          background: #1B1C38 !important;
          border-color: #2D305D !important;
        }

        [data-bs-theme="dark"] .schedule-table th {
          color: #E2E8F0 !important;
        }

        [data-bs-theme="dark"] .schedule-time-label {
          color: #94A3B8 !important;
        }

        [data-bs-theme="dark"] .availability-slot.is-empty {
          background: #14152D !important;
          border-color: #2D305D !important;
        }

        [data-bs-theme="dark"] .schedule-session-card {
          background: #202244 !important;
          border-color: #2D305D !important;
          color: #F8FAFC !important;
        }

        [data-bs-theme="dark"] .schedule-session-card .session-student {
          color: #FFFFFF !important;
        }

        [data-bs-theme="dark"] .schedule-session-card .session-subject {
          color: #94A3B8 !important;
        }

        [data-bs-theme="dark"] .schedule-session-card.status-inprogress {
          background: #7f1d1d33 !important;
          border-left-color: #f87171 !important;
        }

        [data-bs-theme="dark"] .schedule-session-card.status-done {
          background: #14532d33 !important;
          border-left-color: #4ade80 !important;
        }

        [data-bs-theme="dark"] .schedule-session-card.status-scheduled {
          background: #1e293b !important;
          border-left-color: #38bdf8 !important;
        }

        [data-bs-theme="dark"] .schedule-session-card.status-cancelled {
          background: #334155 !important;
          border-left-color: #94a3b8 !important;
        }

        [data-bs-theme="dark"] .schedule-session-card .badge.bg-light {
          background-color: #14152D !important;
          color: #94A3B8 !important;
          border-color: #2D305D !important;
        }

        .schedule-session-card .session-time {
          font-size: 10px;
          font-weight: 700;
          line-height: 1;
        }

        .schedule-session-card .session-student {
          font-size: 11px;
          font-weight: 600;
          line-height: 1.2;
        }

        .schedule-session-card .session-subject {
          font-size: 10px;
          color: var(--bs-secondary-color, #64748B);
          line-height: 1.2;
        }

        /* Legend styling */
        .availability-legend {
          display: flex;
          flex-wrap: wrap;
          gap: 16px;
          color: var(--bs-secondary-color, #64748B);
          font-size: 12px;
          align-items: center;
        }

        .legend-item {
          display: inline-flex;
          align-items: center;
          gap: 6px;
        }

        .legend-swatch {
          width: 14px;
          height: 14px;
          border: 1px solid var(--availability-border, #E5E7EB);
          border-radius: 3px;
          background: var(--bs-body-bg, #ffffff);
        }

        .legend-swatch.legend-inprogress {
          border-color: #0284c7;
          background: #0284c7;
        }

        .legend-swatch.legend-scheduled {
          border-color: #dc2626;
          background: #dc2626;
        }

        .legend-swatch.legend-done {
          border-color: #166534;
          background: #166534;
        }

        .legend-swatch.legend-cancelled {
          border-color: #64748b;
          background: #64748b;
        }

        /* Mobile day picker */
        .availability-mobile-day-picker {
          display: none;
        }

        .availability-day-button {
          border: 1px solid var(--availability-border, #E5E7EB);
          border-radius: 6px;
          background: var(--bs-body-bg, #ffffff);
          color: var(--bs-body-color, #1e293b);
          padding: .45rem .6rem;
          font-size: .85rem;
        }

        .availability-day-button[aria-pressed="true"] {
          border-color: var(--availability-accent, #DB303A);
          background: var(--availability-accent, #DB303A);
          color: #fff;
        }

        @media (max-width: 767.98px) {
          .availability-mobile-day-picker {
            display: flex;
            gap: .3rem;
            overflow-x: auto;
            padding: .1rem .1rem .5rem;
          }
          .availability-day-button {
            flex: 1 0 auto;
            min-width: 2.35rem;
            padding: .4rem .35rem;
          }
          .schedule-table {
            min-width: 0 !important;
            width: 100% !important;
          }
          .schedule-table th[data-day-column],
          .schedule-table td[data-day-column] {
            display: none;
          }
          .schedule-page[data-active-day="1"] .schedule-table [data-day-column="1"],
          .schedule-page[data-active-day="2"] .schedule-table [data-day-column="2"],
          .schedule-page[data-active-day="3"] .schedule-table [data-day-column="3"],
          .schedule-page[data-active-day="4"] .schedule-table [data-day-column="4"],
          .schedule-page[data-active-day="5"] .schedule-table [data-day-column="5"],
          .schedule-page[data-active-day="6"] .schedule-table [data-day-column="6"],
          .schedule-page[data-active-day="0"] .schedule-table [data-day-column="0"] {
            display: table-cell;
          }
        }
      `}</style>

      <div
        className="container-fluid py-4 schedule-page"
        data-active-day={activeDayMobile}
      >
        {/* Tiêu đề lịch học */}
        <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-3">
          <div>
            <h1 className="h3 mb-1 fw-bold text-body">Lịch học sinh viên</h1>
            <p className="text-secondary mb-0 small">
              Thời khóa biểu học tập của bạn theo giờ Việt Nam (GMT+7). Mỗi buổi học là một khối 2 tiếng.
            </p>
          </div>
          <div className="d-flex flex-wrap gap-2">
            <Link
              to="/student/classrooms"
              className="btn btn-outline-danger btn-sm px-3 py-1.5 fw-semibold rounded-2 d-inline-flex align-items-center gap-1.5"
            >
              <i className="bi bi-mortarboard me-1"></i>Xem lớp của tôi
            </Link>
            <Link
              to="/search"
              className="btn btn-danger btn-sm px-3 py-1.5 fw-semibold rounded-2 d-inline-flex align-items-center gap-1.5"
              style={{ backgroundColor: '#DB303A', borderColor: '#DB303A' }}
            >
              <i className="bi bi-search me-1"></i>Tìm gia sư
            </Link>
          </div>
        </div>

        {/* Cảnh báo học phí chưa hoàn tất nếu có */}
        {pendingPayments.map((p) => (
          <div
            key={p.paymentId}
            className="alert alert-warning border border-warning-subtle shadow-sm p-3 mb-3 rounded-3 d-flex align-items-center justify-content-between flex-wrap gap-2"
          >
            <div className="d-flex align-items-center gap-3">
              <div
                className="rounded-circle bg-warning-subtle p-2 text-warning d-flex align-items-center justify-content-center"
                style={{ width: 44, height: 44 }}
              >
                <i className="bi bi-wallet2 fs-5"></i>
              </div>
              <div>
                <h6 className="mb-1 fw-bold text-dark">Lớp học chính thức đang chờ thanh toán học phí</h6>
                <div className="small text-muted">
                  Lớp <strong>{p.classroomName}</strong> ({p.subjectName} - Gia sư {p.tutorName}). Vui lòng thanh toán để mở khóa lịch học!
                </div>
              </div>
            </div>
            <Link to={`/student/payments/${p.paymentId}/checkout`} className="btn btn-danger btn-sm text-nowrap">
              <i className="bi bi-qr-code me-1"></i>Thanh toán ngay
            </Link>
          </div>
        ))}

        {error && (
          <div className="alert alert-danger d-flex align-items-center justify-content-between gap-3 mb-3" role="alert">
            <span>{error}</span>
            <button type="button" className="btn btn-outline-danger btn-sm" onClick={() => setReloadKey((k) => k + 1)}>
              Thử lại
            </button>
          </div>
        )}

        {/* Khung thời khóa biểu tuần */}
        <div className="schedule-card p-3 mb-4 shadow-sm">
          {/* Thanh điều khiển tuần & Chọn ngày */}
          <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-3 pb-3 border-bottom">
            <div className="d-flex align-items-center gap-2">
              <button
                type="button"
                className="btn btn-outline-secondary btn-sm rounded-pill px-3 py-1"
                onClick={() => setWeekOffset((w) => w - 1)}
              >
                &lt; Tuần trước
              </button>
              <button
                type="button"
                className={`btn btn-sm rounded-pill px-3 py-1 fw-semibold ${weekOffset === 0 ? 'btn-danger text-white' : 'btn-outline-danger'}`}
                style={weekOffset === 0 ? { backgroundColor: '#DB303A', borderColor: '#DB303A' } : {}}
                onClick={() => {
                  setWeekOffset(0);
                  setActiveDayMobile((((parseDateOnly(getVietnamToday()).getUTCDay() + 6) % 7) + 1) % 7);
                }}
              >
                Tuần này
              </button>
              <button
                type="button"
                className="btn btn-outline-secondary btn-sm rounded-pill px-3 py-1"
                onClick={() => setWeekOffset((w) => w + 1)}
              >
                Tuần sau &gt;
              </button>
            </div>

            <div className="d-flex align-items-center gap-2">
              <i className="bi bi-calendar-week text-danger fs-5" aria-hidden="true"></i>
              <span className="h6 mb-0 fw-bold">
                {formatDateFull(monday)} – {formatDateFull(sunday)}
              </span>
              {loading && (
                <span className="spinner-border spinner-border-sm text-danger ms-1" role="status" aria-label="Đang tải lịch"></span>
              )}
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
                value={formatDateOnly(monday)}
                onChange={handleJumpDateChange}
                title="Chọn ngày xem lịch"
              />
            </div>
          </div>

          {/* Chú thích màu sắc thời khóa biểu */}
          <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-3">
            <div className="availability-legend">
              <span className="legend-item">
                <span className="legend-swatch legend-inprogress" aria-hidden="true"></span>Đang diễn ra
              </span>
              <span className="legend-item">
                <span className="legend-swatch legend-scheduled" aria-hidden="true"></span>Sắp tới
              </span>
              <span className="legend-item">
                <span className="legend-swatch legend-done" aria-hidden="true"></span>Đã học
              </span>
              <span className="legend-item">
                <span className="legend-swatch legend-cancelled" aria-hidden="true"></span>Đã hủy
              </span>
            </div>
            <div className="small text-secondary">
              <i className="bi bi-info-circle me-1" aria-hidden="true"></i>Nhấp vào buổi học để xem chi tiết bên dưới.
            </div>
          </div>

          {/* Bộ chọn ngày trên thiết bị di động */}
          <div className="availability-mobile-day-picker mb-2" role="group" aria-label="Chọn ngày trong tuần">
            {weekDates.map((dayDate, i) => {
              const dayCode = (i + 1) % 7;
              const isToday = i === todayDayIndex;
              const active = activeDayMobile === dayCode;
              return (
                <button
                  key={i}
                  type="button"
                  className="availability-day-button"
                  data-mobile-day={dayCode}
                  aria-pressed={active}
                  onClick={() => setActiveDayMobile(dayCode)}
                >
                  {DAY_SHORTS[i]} {formatDateDisplay(dayDate)} {isToday ? '•' : ''}
                </button>
              );
            })}
          </div>

          {/* Lưới thời khóa biểu hàng tuần */}
          <div className="mobile-scroll-hint px-2 pt-1 pb-2 text-secondary small d-md-none">
            <i className="bi bi-arrows-expand me-1"></i>Vuốt ngang để xem đủ các ngày trong tuần
          </div>
          <div className="schedule-table-wrap">
            <table className="schedule-table mb-0" aria-label="Thời khóa biểu lịch học trong tuần">
              <thead>
                <tr>
                  <th className="period-column" scope="col"></th>
                  <th className="time-column" scope="col">Khung giờ</th>
                  {weekDates.map((dayDate, i) => {
                    const isToday = i === todayDayIndex;
                    const dayTitle = DAY_TITLES[i];
                    return (
                      <th
                        key={i}
                        scope="col"
                        className={`day-column ${isToday ? 'is-today' : ''}`}
                        data-day-column={(i + 1) % 7}
                      >
                        <div className="day-title">{dayTitle}</div>
                        <div className="day-date">{formatDateDisplay(dayDate)}</div>
                      </th>
                    );
                  })}
                </tr>
              </thead>
              <tbody>
                {PERIOD_ROWS.map((row, r) => {
                  return (
                    <tr key={r}>
                      {row.isFirst && (
                        <th
                          className="schedule-period-label"
                          scope="rowgroup"
                          rowSpan={row.span}
                        >
                          {row.period}
                        </th>
                      )}
                      <th className="schedule-time-label" scope="row">
                        {row.label}
                      </th>

                      {weekDates.map((dayDate, i) => {
                        const dayOfWeek = i; // 0 = Monday, ..., 6 = Sunday
                        const daySessions = sessions.filter(
                          (s) => s.dayOfWeek === dayOfWeek && s.startHour >= row.start && s.startHour < row.end
                        );

                        return (
                          <td key={i} data-day-column={(i + 1) % 7}>
                            {daySessions.length > 0 ? (
                              <div className="d-flex flex-column gap-1">
                                {daySessions.map((session) => {
                                  const isLive = session.status === 'InProgress';
                                  const isDone = session.status === 'Done';
                                  const isCancelled = session.status === 'Cancelled';

                                  const sessionStatusClass = isLive
                                    ? 'status-inprogress'
                                    : isDone
                                    ? 'status-done'
                                    : isCancelled
                                    ? 'status-cancelled'
                                    : 'status-scheduled';

                                  const sessionBadgeClass = isLive
                                    ? 'text-bg-danger'
                                    : isDone
                                    ? 'text-bg-success'
                                    : isCancelled
                                    ? 'text-bg-secondary'
                                    : 'text-bg-primary';

                                  const sessionBadgeLabel = isLive
                                    ? 'Đang diễn ra'
                                    : isDone
                                    ? 'Đã học'
                                    : isCancelled
                                    ? 'Đã hủy'
                                    : 'Sắp tới';

                                  return (
                                    <div
                                      key={`${session.sessionId}-${session.startHour}`}
                                      className={`schedule-session-card ${sessionStatusClass}`}
                                      title={`${session.tutorName} · ${session.subjectName} (${session.timeDisplay})`}
                                    >
                                      <div className="d-flex align-items-center justify-content-between gap-1 mb-1">
                                        <span className="session-time">
                                          <i className="bi bi-clock me-1"></i>
                                          {session.timeDisplay}
                                        </span>
                                        <span className={`badge ${sessionBadgeClass}`} style={{ fontSize: 8, padding: '2px 4px' }}>
                                          {sessionBadgeLabel}
                                        </span>
                                      </div>
                                      <div className="session-student text-truncate fw-bold">
                                        {session.tutorName}
                                      </div>
                                      <div className="session-subject text-truncate">
                                        {session.subjectName} · {session.classroomName || (session.classroomType === 'OneToOne' ? '1-1' : 'Lớp nhóm')}
                                      </div>
                                      {Number(session.sessionId) > 0 && !isDone && !isCancelled && (
                                        !session.isPaid && session.pendingPaymentId ? (
                                          <Link
                                            to={`/student/payments/${session.pendingPaymentId}/checkout`}
                                            className="btn btn-warning btn-sm w-100 py-0.5 mt-1 d-block text-center fw-semibold"
                                            style={{ fontSize: 10, lineHeight: 1.4 }}
                                          >
                                            <i className="bi bi-qr-code me-1"></i>Đóng học phí
                                          </Link>
                                         ) : session.canJoin ? (
                                           <Link
                                             to={`/room/${session.sessionId}`}
                                             className="btn btn-danger btn-sm w-100 py-0.5 mt-1 d-block text-center text-white fw-semibold"
                                             style={{ fontSize: 10.5, lineHeight: 1.4, backgroundColor: '#dc2626' }}
                                           >
                                             <i className="bi bi-camera-video me-1"></i>Vào lớp
                                           </Link>
                                         ) : (
                                           <span
                                             className="badge bg-light text-muted border w-100 py-1 mt-1 d-block text-center"
                                             style={{ fontSize: 9.5, lineHeight: 1.2 }}
                                           >
                                             Chưa mở
                                           </span>
                                         )
                                      )}
                                    </div>
                                  );
                                })}
                              </div>
                            ) : (
                              <div className="availability-slot is-empty"></div>
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
        </div>
      </div>
    </>
  );
}
