import { useState, useEffect, useMemo } from 'react';
import { Link } from 'react-router-dom';
import studentApi from '../../api/studentApi';
import { useSignalREvent } from '../../context/signalRHooks';
import '../../styles/student/StudentSchedulePage.css';

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

const normalizeHoursValue = (raw) => {
  let num = Number(raw);
  if (!Number.isFinite(num) || num <= 0) return 2.0;
  if (num >= 45) num /= 60;
  else if (num >= 10) num /= 10;
  return Math.min(4.0, Math.max(0.5, num));
};

const minutesToHHmm = (totalMinutes) => {
  const safe = ((Math.round(totalMinutes) % (24 * 60)) + 24 * 60) % (24 * 60);
  const hh = Math.floor(safe / 60);
  const mm = safe % 60;
  return `${String(hh).padStart(2, '0')}:${String(mm).padStart(2, '0')}`;
};

const getVietnamDateTime = (value) => {
  if (!value) return { date: getVietnamToday(), hour: 0, minute: 0 };
  const wallMatch = String(value).match(/^(\d{4}-\d{2}-\d{2})T(\d{2}):(\d{2})/);
  if (wallMatch && !String(value).endsWith('Z')) {
    return {
      date: wallMatch[1],
      hour: Number(wallMatch[2]),
      minute: Number(wallMatch[3])
    };
  }
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return { date: getVietnamToday(), hour: 0, minute: 0 };
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23'
  }).formatToParts(parsed);
  const values = Object.fromEntries(parts.map(({ type, value: partValue }) => [type, partValue]));
  return {
    date: `${values.year}-${values.month}-${values.day}`,
    hour: Number(values.hour) || 0,
    minute: Number(values.minute) || 0
  };
};

export default function StudentSchedulePage() {
  const [weekOffset, setWeekOffset] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [sessions, setSessions] = useState([]);
  const [pendingPayments, setPendingPayments] = useState([]);
  const [reloadKey, setReloadKey] = useState(0);

  // Cập nhật trạng thái buổi học theo thời gian thực (Zero-Reload)
  useSignalREvent('ClassSessionStatusChanged', () => setReloadKey((k) => k + 1));
  useSignalREvent('ClassSessionEnded', () => setReloadKey((k) => k + 1));
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
            const localDateTime = getVietnamDateTime(s.scheduledAtVn || s.scheduledAtUtc || s.scheduledAt);
            const sessionDate = s.date || localDateTime.date;
            const startHour = Number(s.hour ?? localDateTime.hour);
            const startMinute = Number(s.minute ?? localDateTime.minute ?? 0);
            const dayOfWeek = (parseDateOnly(sessionDate).getUTCDay() + 6) % 7;
            const durationHours = normalizeHoursValue(
              s.hoursPerSession ?? s.durationHours ?? (s.durationMinutes ? s.durationMinutes / 60 : 2.0)
            );
            const startMin = startHour * 60 + startMinute;
            const endMin = startMin + Math.round(durationHours * 60);
            const fullRange = s.timeRangeLabel || `${minutesToHHmm(startMin)}–${minutesToHHmm(endMin)}`;

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
              sessionDate,
              dayOfWeek,
              startHour,
              startMinute,
              startMin,
              endMin,
              durationHours,
              timeDisplay: fullRange,
              fullTimeDisplay: fullRange,
              status: statusStr,
              isMakeup: Boolean(s.isMakeup),
              canJoin: Boolean(s.canJoin),
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
      <div
        className="container-fluid py-4 schedule-page student-schedule-page"
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
                        const rowStartMin = row.start * 60;
                        const rowEndMin = row.end * 60;
                        const daySessions = sessions.filter(
                          (s) => s.dayOfWeek === dayOfWeek && s.startMin >= rowStartMin && s.startMin < rowEndMin
                        );
                        const continuingSessions = sessions.filter(
                          (s) =>
                            s.dayOfWeek === dayOfWeek &&
                            s.status !== 'Cancelled' &&
                            !(s.startMin >= rowStartMin && s.startMin < rowEndMin) &&
                            s.startMin < rowEndMin &&
                            s.endMin > rowStartMin
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
                                      key={`${session.sessionId}-${session.startMin}`}
                                      className={`schedule-session-card ${sessionStatusClass}`}
                                      title={`${session.tutorName} · ${session.subjectName} (${session.timeDisplay})`}
                                    >
                                      <div className="d-flex align-items-center justify-content-between gap-1 mb-1">
                                        <span className="session-time" style={{ fontVariantNumeric: 'tabular-nums' }}>
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
                                        {session.isMakeup ? ' · Học bù' : ''}
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
                                            style={{ fontSize: 10.5, lineHeight: 1.4 }}
                                          >
                                            <i className="bi bi-camera-video me-1"></i>Vào lớp
                                          </Link>
                                        ) : (
                                          <span
                                            className="badge bg-light text-muted border w-100 py-1 mt-1 d-block text-center"
                                            style={{ fontSize: 9.5, lineHeight: 1.2 }}
                                            title="Phòng học mở trước giờ bắt đầu 30 phút"
                                          >
                                            Chưa tới giờ mở lớp
                                          </span>
                                        )
                                      )}
                                    </div>
                                  );
                                })}
                              </div>
                            ) : continuingSessions.length > 0 ? (
                              <div className="p-2 rounded bg-light border text-center small text-secondary" style={{ fontSize: 10 }}>
                                <div className="fw-semibold text-dark text-truncate">{continuingSessions[0].classroomName}</div>
                                <div style={{ fontVariantNumeric: 'tabular-nums' }}>Ca nối tiếp ({continuingSessions[0].timeDisplay})</div>
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

          {sessions.some((s) => !PERIOD_ROWS.some((r) => s.startMin >= r.start * 60 && s.startMin < r.end * 60)) && (
            <div className="mt-3 p-3 rounded-3 bg-warning-subtle border border-warning-subtle">
              <div className="fw-semibold small text-dark mb-2">
                <i className="bi bi-clock-history me-1 text-warning-emphasis"></i>
                Buổi học ngoài khung ca lưới chuẩn (ví dụ trưa 12:00–13:00 hoặc sau 23:00):
              </div>
              <div className="d-flex flex-wrap gap-2">
                {sessions
                  .filter((s) => !PERIOD_ROWS.some((r) => s.startMin >= r.start * 60 && s.startMin < r.end * 60))
                  .map((s) => (
                    <div key={`${s.sessionId}-offgrid`} className="px-2 py-1 rounded bg-white border small d-flex align-items-center gap-2">
                      <span className="fw-bold text-danger" style={{ fontVariantNumeric: 'tabular-nums' }}>
                        {s.sessionDate} · {s.timeDisplay}
                      </span>
                      <span className="text-dark fw-medium">{s.classroomName}</span>
                      {s.canJoin && Number(s.sessionId) > 0 && (
                        <Link to={`/room/${s.sessionId}`} className="btn btn-danger btn-xs px-2 py-0 text-white fw-semibold" style={{ fontSize: 11 }}>
                          Vào lớp
                        </Link>
                      )}
                    </div>
                  ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </>
  );
}
