import { useState, useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import tutorApi from '../../api/tutorApi';
import '../../styles/tutor/TutorAvailabilityPage.css';

const TIME_BLOCKS = [
  { start: 6, end: 8, hours: 2, label: '06:00 – 08:00', period: 'Sáng' },
  { start: 8, end: 10, hours: 2, label: '08:00 – 10:00', period: 'Sáng' },
  { start: 10, end: 12, hours: 2, label: '10:00 – 12:00', period: 'Sáng' },
  { start: 13, end: 15, hours: 2, label: '13:00 – 15:00', period: 'Chiều' },
  { start: 15, end: 17, hours: 2, label: '15:00 – 17:00', period: 'Chiều' },
  { start: 17, end: 19, hours: 2, label: '17:00 – 19:00', period: 'Tối' },
  { start: 19, end: 21, hours: 2, label: '19:00 – 21:00', period: 'Tối' },
  { start: 21, end: 23, hours: 2, label: '21:00 – 23:00', period: 'Tối' },
];

const DAYS = [
  { day: 1, label: 'Thứ 2', short: 'T2' },
  { day: 2, label: 'Thứ 3', short: 'T3' },
  { day: 3, label: 'Thứ 4', short: 'T4' },
  { day: 4, label: 'Thứ 5', short: 'T5' },
  { day: 5, label: 'Thứ 6', short: 'T6' },
  { day: 6, label: 'Thứ 7', short: 'T7' },
  { day: 0, label: 'Chủ nhật', short: 'CN' },
];

const parseDayNum = (val) => {
  if (typeof val === 'number') return Number.isFinite(val) ? val : 0;
  const map = {
    Sunday: 0,
    Monday: 1,
    Tuesday: 2,
    Wednesday: 3,
    Thursday: 4,
    Friday: 5,
    Saturday: 6,
  };
  if (map[val] !== undefined) return map[val];
  const num = Number(val);
  return Number.isFinite(num) ? num : 0;
};

export default function TutorAvailabilityPage() {
  const [selectedKeys, setSelectedKeys] = useState(new Set());
  const [busySlotKeys, setBusySlotKeys] = useState(new Set());
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [activeDayMobile, setActiveDayMobile] = useState(1);
  const msgTimerRef = useRef(null);

  useEffect(() => () => {
    if (msgTimerRef.current) clearTimeout(msgTimerRef.current);
  }, []);

  useEffect(() => {
    let active = true;
    async function loadAvailabilities() {
      try {
        setLoading(true);
        const res = await tutorApi.getAvailabilities();
        if (!active) return;
        const busy = new Set(res.data?.busySlotKeys || []);
        setBusySlotKeys(busy);

        const keys = new Set();
        (res.data?.availabilities || []).forEach((av) => {
          const day = parseDayNum(av.dayOfWeek);
          const startHour = parseInt((av.startTime || '0').split(':')[0], 10);
          const endHour = parseInt((av.endTime || '0').split(':')[0], 10);

          TIME_BLOCKS.forEach((b) => {
            if (b.start >= startHour && b.end <= endHour) {
              const k = `${day}:${b.start}`;
              if (!busy.has(k)) {
                keys.add(k);
              }
            }
          });
        });

        setSelectedKeys(keys);
      } catch {
        if (active) setError('Không tải được cấu hình lịch rảnh.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadAvailabilities();
    return () => {
      active = false;
    };
  }, []);

  const toggleSlot = (key) => {
    if (busySlotKeys.has(key)) {
      setMessage('Khung giờ này bạn đã có lớp học được xếp nên không thể thay đổi.');
      if (msgTimerRef.current) clearTimeout(msgTimerRef.current);
      msgTimerRef.current = setTimeout(() => setMessage(''), 4000);
      return;
    }

    setSelectedKeys((prev) => {
      const next = new Set(prev);
      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }
      return next;
    });
  };

  // Bật/tắt tất cả các ô lịch trống trong cùng một ngày
  const toggleDay = (day) => {
    const dayKeys = TIME_BLOCKS.map((b) => `${day}:${b.start}`).filter((k) => !busySlotKeys.has(k));
    const allSelected = dayKeys.every((k) => selectedKeys.has(k));

    setSelectedKeys((prev) => {
      const next = new Set(prev);
      dayKeys.forEach((k) => {
        if (allSelected) {
          next.delete(k);
        } else {
          next.add(k);
        }
      });
      return next;
    });
  };

  // Bật/tắt tất cả các ô lịch trống theo ca học cho toàn bộ các ngày trong tuần
  const toggleBlock = (blockStart) => {
    const blockKeys = DAYS.map((d) => `${d.day}:${blockStart}`).filter((k) => !busySlotKeys.has(k));
    const allSelected = blockKeys.every((k) => selectedKeys.has(k));

    setSelectedKeys((prev) => {
      const next = new Set(prev);
      blockKeys.forEach((k) => {
        if (allSelected) {
          next.delete(k);
        } else {
          next.add(k);
        }
      });
      return next;
    });
  };

  // Các thao tác chọn nhanh lịch rảnh
  const handleQuickAction = (type) => {
    const next = new Set(selectedKeys);
    if (type === 'weeknights') {
      // Thứ 2 đến Thứ 6 từ 17:00 đến 21:00 (ca 17:00 và 19:00)
      [1, 2, 3, 4, 5].forEach((d) => {
        [17, 19].forEach((h) => {
          const k = `${d}:${h}`;
          if (!busySlotKeys.has(k)) next.add(k);
        });
      });
    } else if (type === 'weekend') {
      // Thứ 7 và Chủ nhật từ 08:00 đến 17:00 (ca 8:00, 10:00, 13:00, 15:00)
      [6, 0].forEach((d) => {
        [8, 10, 13, 15].forEach((h) => {
          const k = `${d}:${h}`;
          if (!busySlotKeys.has(k)) next.add(k);
        });
      });
    } else if (type === 'all') {
      DAYS.forEach((d) => {
        TIME_BLOCKS.forEach((b) => {
          const k = `${d.day}:${b.start}`;
          if (!busySlotKeys.has(k)) next.add(k);
        });
      });
    } else if (type === 'clear') {
      next.clear();
    }
    setSelectedKeys(next);
  };

  const handleSave = async (e) => {
    e.preventDefault();
    setSaving(true);
    setMessage('');
    setError('');

    try {
      const res = await tutorApi.saveAvailabilities({
        selectedSlotKeys: Array.from(selectedKeys),
      });
      setMessage(res.data?.message || 'Lưu lịch rảnh thành công! Học viên có thể xem lịch để đặt hẹn.');
    } catch (err) {
      setError(err.response?.data?.message || 'Có lỗi xảy ra khi lưu lịch rảnh.');
    } finally {
      setSaving(false);
    }
  };

  // Tính tổng số ca học và số giờ rảnh trong tuần
  let freeBlockCount = 0;
  let freeHourCount = 0;
  selectedKeys.forEach((k) => {
    if (!busySlotKeys.has(k)) {
      freeBlockCount++;
      freeHourCount += 2;
    }
  });

  return (
    <>
      <div className="container-fluid py-4 availability-page">
        {/* Header */}
        <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-3">
          <div>
            <h1 className="h3 mb-1 fw-bold text-dark">Cài đặt lịch rảnh hàng tuần</h1>
            <p className="text-secondary small mb-0">
              Nhấp vào từng ô để đánh dấu bạn rảnh hoặc bận. Mỗi ô tương ứng với một buổi học 2 tiếng (GMT+7).
            </p>
          </div>
          <div className="d-flex flex-wrap gap-2">
            <Link to="/tutor/schedule" className="btn btn-outline-danger btn-sm px-3 py-2 rounded-2 fw-semibold">
              <i className="bi bi-calendar3 me-1.5" aria-hidden="true"></i>Xem Thời khóa biểu lịch dạy
            </Link>
          </div>
        </div>

        {message && (
          <div className="alert alert-success alert-dismissible fade show py-2.5 mb-3 shadow-sm rounded-3" role="status">
            <i className="bi bi-check-circle-fill me-2 text-success" aria-hidden="true"></i>{message}
            <button type="button" className="btn-close" onClick={() => setMessage('')} aria-label="Đóng"></button>
          </div>
        )}

        {error && (
          <div className="alert alert-danger alert-dismissible fade show py-2.5 mb-3 shadow-sm rounded-3" role="alert">
            <i className="bi bi-exclamation-triangle-fill me-2 text-danger" aria-hidden="true"></i>{error}
            <button type="button" className="btn-close" onClick={() => setError('')} aria-label="Đóng"></button>
          </div>
        )}

        <form onSubmit={handleSave}>
          <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-3">
            <div className="availability-legend">
              <span className="legend-item">
                <span className="legend-swatch is-free"></span>Rảnh - có thể nhận lớp
              </span>
              <span className="legend-item">
                <span className="legend-swatch is-busy"></span>Đã có lớp (cố định)
              </span>
              <span className="legend-item">
                <span className="legend-swatch"></span>Không dạy (bận)
              </span>
            </div>
            <div className="small text-secondary">
              <i className="bi bi-info-circle me-1" aria-hidden="true"></i>Nhấp trực tiếp vào ô để Bật / Tắt trạng thái rảnh. Nhấp tiêu đề Thứ/Giờ để chọn nhanh cả cột/hàng.
            </div>
          </div>

          <div className="availability-layout">
            <section className="availability-card p-3 shadow-sm" aria-label="Bảng lịch rảnh hàng tuần">
              {/* Mobile day picker */}
              <div className="availability-mobile-day-picker" role="group" aria-label="Chọn ngày trong tuần">
                {DAYS.map((d) => (
                  <button
                    key={d.day}
                    type="button"
                    className="availability-day-button"
                    aria-pressed={activeDayMobile === d.day ? 'true' : 'false'}
                    onClick={() => setActiveDayMobile(d.day)}
                  >
                    {d.short}
                  </button>
                ))}
              </div>

              {loading ? (
                <div className="text-center py-5">
                  <div className="spinner-border text-success" role="status"></div>
                  <div className="text-muted small mt-2">Đang tải lịch rảnh...</div>
                </div>
              ) : (
                <div className="availability-table-wrap">
                  <table className="availability-table mb-0">
                    <thead>
                      <tr>
                        <th className="period-column" scope="col">Buổi</th>
                        <th className="time-column" scope="col" title="Nhấp vào từng khung giờ để chọn cả hàng">Khung giờ</th>
                        {DAYS.map((d) => (
                          <th
                            key={d.day}
                            scope="col"
                            data-day-column={d.day}
                            className={`day-header-clickable ${activeDayMobile === d.day ? 'active-mobile-col' : ''}`}
                            onClick={() => toggleDay(d.day)}
                            title={`Nhấp để chọn/bỏ chọn toàn bộ ${d.label}`}
                          >
                            <div>{d.label}</div>
                            <span className="text-secondary" style={{ fontSize: 9, fontWeight: 'normal' }}>Chọn tất cả</span>
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {TIME_BLOCKS.map((block, idx) => {
                        const isFirstPeriod = idx === 0 || TIME_BLOCKS[idx - 1].period !== block.period;
                        const periodSpan = TIME_BLOCKS.filter((b) => b.period === block.period).length;

                        return (
                          <tr key={idx}>
                            {isFirstPeriod && (
                              <th className="period-column" scope="rowgroup" rowSpan={periodSpan}>
                                {block.period}
                              </th>
                            )}
                            <th
                              className="time-column"
                              scope="row"
                              onClick={() => toggleBlock(block.start)}
                              title={`Nhấp để chọn/bỏ chọn khung giờ ${block.label} cho tất cả các ngày`}
                            >
                              <div>{block.label}</div>
                            </th>

                            {DAYS.map((d) => {
                              const slotKey = `${d.day}:${block.start}`;
                              const isBusy = busySlotKeys.has(slotKey);
                              const isSelected = selectedKeys.has(slotKey);
                              const isMobileActive = activeDayMobile === d.day;

                              if (isBusy) {
                                return (
                                  <td
                                    key={d.day}
                                    data-day-column={d.day}
                                    className={isMobileActive ? 'active-mobile-col' : ''}
                                  >
                                    <div
                                      className="availability-slot-busy"
                                      onClick={() => toggleSlot(slotKey)}
                                      title="Khung giờ này bạn đã có lớp học được xếp, không thể hủy rảnh."
                                    >
                                      <span>Có lớp</span>
                                    </div>
                                  </td>
                                );
                              }

                              return (
                                <td
                                  key={d.day}
                                  data-day-column={d.day}
                                  className={isMobileActive ? 'active-mobile-col' : ''}
                                >
                                  <button
                                    type="button"
                                    className={`availability-slot-btn ${isSelected ? 'is-selected' : ''}`}
                                    onClick={() => toggleSlot(slotKey)}
                                    aria-pressed={isSelected}
                                    title={isSelected ? `${d.label} (${block.label}): Đang rảnh (Bấm để hủy)` : `${d.label} (${block.label}): Đang bận (Bấm để đánh dấu rảnh)`}
                                  >
                                    {isSelected ? (
                                      <span><i className="bi bi-check2 me-0.5"></i>Rảnh</span>
                                    ) : (
                                      <span className="text-secondary-subtle">Trống</span>
                                    )}
                                  </button>
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
            </section>

            {/* Sidebar Controls */}
            <aside className="availability-card p-3.5 shadow-sm" aria-label="Tóm tắt lịch rảnh">
              <h2 className="h6 fw-bold text-dark mb-2">Tóm tắt lịch rảnh</h2>

              <div className="p-3 bg-light rounded-3 mb-3 text-center border">
                <div className="summary-hours mb-0.5">{freeBlockCount}</div>
                <div className="small fw-bold text-dark">buổi rảnh / tuần</div>
                <div className="small text-secondary mt-1">
                  tương đương <strong className="text-success">{freeHourCount} giờ</strong> nhận lớp
                </div>
              </div>

              {busySlotKeys.size > 0 && (
                <div className="p-2.5 bg-danger-subtle text-danger rounded-2 small mb-3 border border-danger-subtle d-flex align-items-center gap-2">
                  <i className="bi bi-lock-fill fs-6 flex-shrink-0"></i>
                  <span>Có <strong>{busySlotKeys.size}</strong> buổi đã có lớp cố định (khóa chỉnh sửa).</span>
                </div>
              )}

              <div className="small fw-bold text-dark mb-2">Chọn nhanh khung giờ</div>
              <div className="d-grid gap-2 mb-3">
                <button
                  type="button"
                  className="quick-action"
                  onClick={() => handleQuickAction('weeknights')}
                >
                  <i className="bi bi-moon-stars me-1.5 text-warning"></i>Tối các ngày (17:00 – 21:00)
                </button>
                <button
                  type="button"
                  className="quick-action"
                  onClick={() => handleQuickAction('weekend')}
                >
                  <i className="bi bi-sun me-1.5 text-info"></i>Cuối tuần cả ngày (08:00 – 17:00)
                </button>
                <button
                  type="button"
                  className="quick-action"
                  onClick={() => handleQuickAction('all')}
                >
                  <i className="bi bi-grid-fill me-1.5 text-success"></i>Chọn tất cả các khung giờ
                </button>
                <button
                  type="button"
                  className="quick-action text-danger"
                  onClick={() => handleQuickAction('clear')}
                >
                  <i className="bi bi-trash3 me-1.5"></i>Xóa tất cả lựa chọn
                </button>
              </div>

              <button
                type="submit"
                className="save-action d-flex align-items-center justify-content-center gap-2"
                disabled={saving}
              >
                {saving ? (
                  <>
                    <span className="spinner-border spinner-border-sm" role="status"></span>
                    <span>Đang lưu...</span>
                  </>
                ) : (
                  <>
                    <i className="bi bi-cloud-arrow-up-fill fs-6"></i>
                    <span>Lưu lịch rảnh</span>
                  </>
                )}
              </button>

              {/* Callout box: Sang Thời khóa biểu */}
              <div className="mt-3 p-3 rounded-3 border border-danger-subtle bg-danger-subtle text-dark text-start">
                <div className="fw-bold text-danger small mb-1">
                  <i className="bi bi-calendar-event me-1"></i>Xem lớp dạy thực tế
                </div>
                <p className="small text-secondary mb-2" style={{ fontSize: 11 }}>
                  Lịch rảnh được dùng để học viên chọn giờ kết nối. Để xem các lớp học và vào phòng học, hãy xem <strong>Thời khóa biểu</strong>.
                </p>
                <Link to="/tutor/schedule" className="btn btn-sm btn-danger w-100 fw-semibold" style={{ fontSize: 12 }}>
                  <i className="bi bi-arrow-right-circle me-1"></i>Sang Thời khóa biểu Lịch dạy
                </Link>
              </div>
            </aside>
          </div>
        </form>
      </div>
    </>
  );
}
