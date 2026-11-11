import { useState, useEffect, useMemo } from 'react';
import { Link } from 'react-router-dom';
import studentApi from '../../api/studentApi';
import { useAuth } from '../../context/AuthContext';
import OtspPagination from '../../components/common/OtspPagination';

const formatMoney = (value) =>
  new Intl.NumberFormat('vi-VN').format(value || 0) + ' đ';

export default function StudentDashboardPage() {
  const { user } = useAuth();
  const [loading, setLoading] = useState(true);
  const [dashboardData, setDashboardData] = useState(null);
  const [profileData, setProfileData] = useState(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [isScheduleCollapsed, setIsScheduleCollapsed] = useState(() => {
    return localStorage.getItem('otsp_student_schedule_collapsed') === 'true';
  });
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 5;

  useEffect(() => {
    let isMounted = true;
    async function fetchDashboard() {
      try {
        setLoading(true);
        const [dashRes, profRes] = await Promise.allSettled([
          studentApi.getDashboard(),
          studentApi.getProfile()
        ]);

        if (isMounted) {
          if (dashRes.status === 'fulfilled' && dashRes.value.data) {
            setDashboardData(dashRes.value.data);
          }
          if (profRes.status === 'fulfilled' && profRes.value.data) {
            setProfileData(profRes.value.data);
          }
        }
      } catch (err) {
        console.warn('Error loading dashboard from Neon API:', err);
      } finally {
        if (isMounted) setLoading(false);
      }
    }
    fetchDashboard();
    return () => {
      isMounted = false;
    };
  }, []);

  const toggleCollapse = () => {
    setIsScheduleCollapsed((prev) => {
      const next = !prev;
      localStorage.setItem('otsp_student_schedule_collapsed', String(next));
      return next;
    });
  };

  // Dữ liệu thực tế lấy trực tiếp từ cơ sở dữ liệu
  const dashboard = useMemo(() => {
    const isMinor = profileData?.profile?.isMinor ?? dashboardData?.isMinor ?? user?.isMinor ?? false;
    const guardianName = profileData?.guardians?.[0]?.fullName || profileData?.profile?.guardianName || dashboardData?.guardianName || '';

    const rawUpcoming = dashboardData?.upcomingSessions || [];
    const rawPending = dashboardData?.pendingPayments || [];

    const upcomingSessions = rawUpcoming.map((s) => ({
      id: s.id,
      title: s.classroomName || s.title || 'Lớp học',
      tutor: s.tutorName || s.tutor || 'Gia sư',
      scheduledAt: s.displayTime || (s.scheduledAt
        ? new Date(s.scheduledAt).toLocaleString('vi-VN', {
            timeZone: 'Asia/Ho_Chi_Minh',
            hour: '2-digit',
            minute: '2-digit',
            weekday: 'long',
            day: '2-digit',
            month: '2-digit',
            year: 'numeric'
          })
        : ''),
      joinUrl: s.id > 0 ? `/room/${s.id}` : '/student/schedule',
      canJoinNow: s.canJoinNow ?? false,
      isToday: s.isToday ?? false
    }));

    const pendingPayments = rawPending.map((p) => ({
      id: p.id,
      classroomName: p.classroomName || 'Lớp học',
      amount: p.amount || 0,
      dueDate: p.dueDate
        ? new Date(p.dueDate).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })
        : (p.paidAt ? new Date(p.paidAt).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : '')
    }));

    const activeClassesCount = dashboardData?.activeClassroomsCount ?? 0;
    const pendingRequestsCount = dashboardData?.pendingRequestsCount ?? 0;
    const totalRemainingFee = dashboardData?.totalRemainingFee ?? pendingPayments.reduce((acc, p) => acc + (p.amount || 0), 0);
    const todaySessionsCount = dashboardData?.todaySessionsCount ?? upcomingSessions.filter((s) => s.isToday).length;

    const nextSession = dashboardData?.nextSession || (upcomingSessions.length > 0 ? {
      title: upcomingSessions[0].title,
      subtitle: `${upcomingSessions[0].scheduledAt} · Gia sư: ${upcomingSessions[0].tutor}`,
      joinUrl: upcomingSessions[0].joinUrl
    } : null);

    return {
      isMinor,
      guardianName,
      nextSession,
      todaySessionsCount,
      activeClassesCount,
      totalRemainingFee,
      pendingRequestsCount,
      upcomingSessions,
      pendingPayments
    };
  }, [dashboardData, profileData, user]);

  // Lọc danh sách lịch học sắp tới
  const filteredSessions = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    if (!q) return dashboard.upcomingSessions;
    return dashboard.upcomingSessions.filter(
      (s) =>
        s.title.toLowerCase().includes(q) || s.tutor.toLowerCase().includes(q)
    );
  }, [dashboard.upcomingSessions, searchQuery]);

  // Phân trang danh sách lịch học
  const totalPages = Math.ceil(filteredSessions.length / pageSize) || 1;
  const pagedSessions = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredSessions.slice(start, start + pageSize);
  }, [filteredSessions, currentPage, pageSize]);

  return (
    <div className="container-fluid py-4 student-dashboard-container">
      {/* Tiêu đề bảng điều khiển */}
      <div className="mb-4">
        <h1 className="h3 fw-bold mb-1" style={{ color: 'var(--bs-body-color, #0f172a)' }}>
          Tổng quan
        </h1>
        <p className="text-secondary mb-0" style={{ fontSize: '0.92rem' }}>
          Khu học tập của học viên / phụ huynh
        </p>
      </div>

      {/* 1. Banner Người Giám Hộ (Hiển thị nếu học viên dưới 18 tuổi hoặc đã có người giám hộ từ Neon) */}
      {(dashboard.isMinor || dashboard.guardianName) && (
        <div className="student-guardian-alert-card mb-4 shadow-sm d-flex align-items-center justify-content-between flex-wrap gap-2">
          <div>
            {dashboard.isMinor && <span>Bạn dưới 18 tuổi. </span>}
            {dashboard.guardianName ? (
              <span>
                Người giám hộ: <strong>{dashboard.guardianName}</strong>
              </span>
            ) : (
              <span className="text-secondary">Chưa đăng ký thông tin người giám hộ.</span>
            )}
          </div>
          <Link
            to="/student/profile#guardians"
            className="btn btn-sm btn-outline-warning text-dark fw-medium rounded-pill px-3 py-1"
          >
            Chi tiết
          </Link>
        </div>
      )}

      {/* 2. Banner: Buổi học tiếp theo */}
      <div className="student-banner-card mb-4 shadow-sm">
        {loading ? (
          <div className="d-flex align-items-center gap-2 py-2 text-secondary">
            <span className="spinner-border spinner-border-sm text-danger" role="status" aria-hidden="true"></span>
            <span>Đang tải dữ liệu tổng quan...</span>
          </div>
        ) : dashboard.nextSession ? (
          <div className="d-flex flex-wrap align-items-center justify-content-between gap-3">
            <div>
              <div className="student-banner-label">Buổi học tiếp theo</div>
              <div className="student-banner-title">{dashboard.nextSession.title}</div>
              <div className="student-banner-meta">{dashboard.nextSession.subtitle}</div>
            </div>
            <div>
              <Link to={dashboard.nextSession.joinUrl} className="btn-red-action">
                <i className="bi bi-camera-video me-2" aria-hidden="true"></i>Vào lớp
              </Link>
            </div>
          </div>
        ) : (
          <div className="d-flex flex-wrap align-items-center justify-content-between gap-3">
            <div>
              <div className="student-banner-label">Buổi học tiếp theo</div>
              <div className="student-banner-title">Hôm nay bạn không có lịch học</div>
              <div className="student-banner-meta" style={{ color: '#64748b' }}>
                Kiểm tra thời khóa biểu hoặc tìm thêm gia sư mới
              </div>
            </div>
            <div>
              <Link to="/student/schedule" className="btn btn-outline-danger rounded-pill px-4">
                Xem thời khóa biểu
              </Link>
            </div>
          </div>
        )}
      </div>

      {/* 3. 4 KPI Metric Cards (Đổ từ Neon Database) */}
      <div className="row g-3 mb-4">
        {/* Card 1: Buổi hôm nay */}
        <div className="col-6 col-md-3">
          <div className="student-kpi-card shadow-sm">
            <div className="student-kpi-title">BUỔI HÔM NAY</div>
            <div className="student-kpi-value">{dashboard.todaySessionsCount}</div>
            <div className="student-kpi-icon-box kpi-box-pink d-flex align-items-center justify-content-center">
              <i className="bi bi-calendar-check text-danger" style={{ fontSize: '1.15rem' }}></i>
            </div>
          </div>
        </div>

        {/* Card 2: Lớp đang học */}
        <div className="col-6 col-md-3">
          <div className="student-kpi-card shadow-sm">
            <div className="student-kpi-title">LỚP ĐANG HỌC</div>
            <div className="student-kpi-value">{dashboard.activeClassesCount}</div>
            <div className="student-kpi-icon-box kpi-box-blue d-flex align-items-center justify-content-center">
              <i className="bi bi-easel text-primary" style={{ fontSize: '1.15rem' }}></i>
            </div>
          </div>
        </div>

        {/* Card 3: Học phí còn lại */}
        <div className="col-6 col-md-3">
          <div className="student-kpi-card shadow-sm">
            <div className="student-kpi-title">HỌC PHÍ CÒN LẠI</div>
            <div className="student-kpi-value" style={{ fontSize: '1.45rem' }}>
              {formatMoney(dashboard.totalRemainingFee)}
            </div>
            <div className="student-kpi-icon-box kpi-box-yellow d-flex align-items-center justify-content-center">
              <i className="bi bi-credit-card-2-front text-warning" style={{ fontSize: '1.15rem' }}></i>
            </div>
          </div>
        </div>

        {/* Card 4: Yêu cầu đang chờ */}
        <div className="col-6 col-md-3">
          <div className="student-kpi-card shadow-sm">
            <div className="student-kpi-title">YÊU CẦU ĐANG CHỜ</div>
            <div className="student-kpi-value">{dashboard.pendingRequestsCount}</div>
            <div className="student-kpi-icon-box kpi-box-purple d-flex align-items-center justify-content-center">
              <i className="bi bi-clock-history" style={{ color: '#9333ea', fontSize: '1.15rem' }}></i>
            </div>
          </div>
        </div>
      </div>

      {/* 4. Hai Cột: Lịch sắp tới (Left) & Học phí cần thanh toán (Right) */}
      <div className="row g-4">
        {/* Cột trái: Lịch sắp tới */}
        <div className="col-12 col-lg-7">
          <div className="student-dash-card shadow-sm p-4 h-100">
            <div className="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-3">
              <div className="d-flex align-items-center gap-2">
                <h2 className="h5 fw-bold mb-0" style={{ color: 'var(--bs-body-color, #0f172a)' }}>
                  Lịch sắp tới
                </h2>
                {filteredSessions.length > 0 && (
                  <span className="badge rounded-pill bg-light text-secondary border fw-semibold" style={{ fontSize: '0.75rem' }}>
                    {filteredSessions.length}
                  </span>
                )}
              </div>

              <div className="d-flex align-items-center gap-2">
                {/* Ô tìm kiếm tức thời */}
                <div className="position-relative" style={{ width: 175 }}>
                  <input
                    type="text"
                    value={searchQuery}
                    onChange={(e) => {
                      setSearchQuery(e.target.value);
                      if (isScheduleCollapsed) setIsScheduleCollapsed(false);
                      setCurrentPage(1);
                    }}
                    placeholder="Tìm môn, gia sư..."
                    className="form-control form-control-sm ps-4 pe-2 rounded-pill"
                    style={{ fontSize: '0.82rem' }}
                  />
                  <i
                    className="bi bi-search position-absolute top-50 start-0 translate-middle-y ms-2 text-muted"
                    style={{ fontSize: '0.75rem' }}
                  ></i>
                </div>

                {/* Nút Thu gọn / Ẩn hiện lịch học */}
                <button
                  type="button"
                  className="btn btn-toggle-schedule rounded-pill d-inline-flex align-items-center gap-1"
                  onClick={toggleCollapse}
                  title="Thu gọn hoặc mở rộng lịch học"
                >
                  <i className={`bi ${isScheduleCollapsed ? 'bi-plus-lg' : 'bi-dash-lg'} toggle-icon`}></i>
                  <span>{isScheduleCollapsed ? 'Mở lịch' : 'Thu gọn'}</span>
                </button>
              </div>
            </div>

            {/* Vùng Lịch có thể Thu gọn */}
            {!isScheduleCollapsed && (
              <div className="schedule-collapse-area">
                {pagedSessions.length > 0 ? (
                  <>
                    <div className="table-responsive">
                      <table className="table upcoming-schedule-table">
                        <tbody>
                          {pagedSessions.map((session) => (
                            <tr key={session.id} className="schedule-row">
                              <td>
                                <div className="schedule-time-text">{session.scheduledAt}</div>
                                <div className="schedule-class-text">
                                  {session.title} · <span className="text-secondary">{session.tutor}</span>
                                </div>
                              </td>
                              <td className="text-end">
                                {session.canJoinNow ? (
                                  <Link to={session.joinUrl} className="btn-red-action-sm">
                                    <i className="bi bi-camera-video me-1"></i>Vào lớp
                                  </Link>
                                ) : session.isToday ? (
                                  <span className="badge-open-later">Chưa mở</span>
                                ) : (
                                  <span className="badge-future-outline">
                                    <i className="bi bi-calendar-event me-1"></i>Sắp tới
                                  </span>
                                )}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>

                    {/* Phân trang chuẩn OTSP */}
                    {filteredSessions.length > 0 && (
                      <OtspPagination
                        page={currentPage}
                        totalPages={totalPages}
                        total={filteredSessions.length}
                        pageSize={pageSize}
                        onChange={setCurrentPage}
                        className="mt-3 border-top pt-2"
                      />
                    )}
                  </>
                ) : (
                  <div className="text-center py-5 text-muted">
                    <i className="bi bi-calendar-x fs-1 d-block mb-2 text-secondary"></i>
                    <p className="mb-0">
                      {searchQuery
                        ? 'Không tìm thấy buổi học nào khớp với từ khóa tìm kiếm.'
                        : 'Không có buổi học nào sắp diễn ra.'}
                    </p>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Cột phải: Học phí cần thanh toán & Truy cập nhanh */}
        <div className="col-12 col-lg-5">
          <div className="student-dash-card shadow-sm p-4 h-100 d-flex flex-column justify-content-between">
            <div>
              <div className="d-flex align-items-center justify-content-between mb-3">
                <h2 className="h5 fw-bold mb-0" style={{ color: 'var(--bs-body-color, #0f172a)' }}>
                  Học phí cần thanh toán
                </h2>
                {dashboard.pendingPayments.length > 0 && (
                  <span className="badge bg-danger rounded-pill px-2 py-1" style={{ fontSize: '0.75rem' }}>
                    {dashboard.pendingPayments.length} khoản
                  </span>
                )}
              </div>

              {dashboard.pendingPayments.length > 0 ? (
                <>
                  <div className="pending-fee-list">
                    {dashboard.pendingPayments.map((fee) => (
                      <div key={fee.id} className="pending-fee-item d-flex align-items-center justify-content-between gap-2">
                        <div>
                          <div className="pending-fee-title">{fee.classroomName}</div>
                          <div className="small text-secondary">Hạn: {fee.dueDate}</div>
                          <div className="pending-fee-amount text-danger">{formatMoney(fee.amount)}</div>
                        </div>
                        <div>
                          <Link to={`/student/payments/${fee.id}/checkout`} className="btn-red-action-sm">
                            <i className="bi bi-wallet2 me-1"></i>Thanh toán
                          </Link>
                        </div>
                      </div>
                    ))}
                  </div>
                  <div className="mt-3 text-end">
                    <Link to="/student/payments" className="text-danger small fw-semibold text-decoration-none">
                      Xem tất cả học phí <i className="bi bi-arrow-right"></i>
                    </Link>
                  </div>
                </>
              ) : (
                <div className="text-center py-4 text-muted">
                  <i className="bi bi-check-circle-fill text-success fs-1 d-block mb-2"></i>
                  <p className="mb-1 fw-medium text-dark">Tất cả học phí đã hoàn tất</p>
                  <p className="small text-secondary mb-3">Bạn không có khoản học phí nào cần thanh toán.</p>
                  <Link to="/student/payments" className="btn btn-sm btn-outline-secondary">
                    Lịch sử thanh toán
                  </Link>
                </div>
              )}
            </div>

            {/* Truy cập nhanh */}
            <div className="pt-3 border-top mt-4">
              <h3 className="h6 fw-bold mb-2 text-secondary">Truy cập nhanh</h3>
              <div className="d-grid gap-2">
                <Link to="/student/classrooms" className="btn btn-sm btn-outline-secondary text-start d-flex align-items-center justify-content-between">
                  <span><i className="bi bi-easel me-2 text-danger"></i>Lớp của tôi</span>
                  <i className="bi bi-chevron-right small text-muted"></i>
                </Link>
                <Link to="/search" className="btn btn-sm btn-outline-secondary text-start d-flex align-items-center justify-content-between">
                  <span><i className="bi bi-search me-2 text-danger"></i>Tìm gia sư mới</span>
                  <i className="bi bi-chevron-right small text-muted"></i>
                </Link>
                <Link to="/student/documents" className="btn btn-sm btn-outline-secondary text-start d-flex align-items-center justify-content-between">
                  <span><i className="bi bi-folder2-open me-2 text-danger"></i>Tài liệu học tập</span>
                  <i className="bi bi-chevron-right small text-muted"></i>
                </Link>
                <Link to="/account/manage/change-password" className="btn btn-sm btn-outline-secondary text-start d-flex align-items-center justify-content-between">
                  <span><i className="bi bi-key me-2 text-danger"></i>Đổi mật khẩu</span>
                  <i className="bi bi-chevron-right small text-muted"></i>
                </Link>
              </div>
            </div>
          </div>
        </div>
      </div>

      <style>{`
        .student-dashboard-container { width: 100%; max-width: 100%; margin: 0; }
        .student-dash-card {
          background-color: var(--bs-card-bg, #ffffff);
          border: 1px solid var(--bs-border-color, #e5e7eb);
          border-radius: 12px;
          transition: transform 0.15s ease, box-shadow 0.15s ease;
        }
        .student-guardian-alert-card {
          background-color: var(--bs-card-bg, #ffffff);
          border: 1px solid var(--bs-warning-border-subtle, #fcd34d);
          border-radius: 10px;
          padding: 16px 20px;
          font-size: 0.95rem;
          font-weight: 500;
          color: var(--bs-body-color, #0f172a);
        }
        .student-banner-card {
          background-color: var(--bs-card-bg, #ffffff);
          border: 1px solid var(--bs-danger-border-subtle, #fca5a5);
          border-radius: 12px;
          padding: 22px 28px;
        }
        .student-banner-label { font-size: 0.82rem; color: #64748b; margin-bottom: 4px; font-weight: 500; }
        .student-banner-title { font-size: 1.35rem; font-weight: 700; color: var(--bs-body-color, #0f172a); margin-bottom: 6px; }
        .student-banner-meta { font-size: 0.88rem; font-weight: 600; color: #dc2626; }
        .btn-red-action {
          background-color: #dc2626;
          color: #ffffff;
          font-weight: 600;
          border: 1px solid #dc2626;
          border-radius: 8px;
          padding: 8px 22px;
          display: inline-flex;
          align-items: center;
          justify-content: center;
          text-decoration: none;
          transition: background-color 0.15s ease, border-color 0.15s ease;
        }
        .btn-red-action:hover { background-color: #b91c1c; border-color: #b91c1c; color: #ffffff; }
        .btn-red-action-sm {
          background-color: #dc2626;
          color: #ffffff;
          font-weight: 600;
          border: none;
          border-radius: 6px;
          padding: 6px 18px;
          font-size: 0.84rem;
          text-decoration: none;
          display: inline-block;
          transition: background-color 0.15s ease;
        }
        .btn-red-action-sm:hover { background-color: #b91c1c; color: #ffffff; }
        .student-kpi-card {
          background-color: var(--bs-card-bg, #ffffff);
          border: 1px solid var(--bs-border-color, #e5e7eb);
          border-radius: 10px;
          padding: 18px 20px;
          height: 100%;
          position: relative;
        }
        .student-kpi-title {
          font-size: 0.72rem;
          font-weight: 700;
          letter-spacing: 0.05em;
          text-transform: uppercase;
          color: #64748b;
          margin-bottom: 8px;
        }
        .student-kpi-value {
          font-size: 1.85rem;
          font-weight: 700;
          color: var(--bs-body-color, #0f172a);
          line-height: 1.2;
        }
        .student-kpi-icon-box {
          width: 36px;
          height: 36px;
          border-radius: 8px;
          position: absolute;
          top: 18px;
          right: 18px;
        }
        .kpi-box-pink { background-color: #fee2e2; }
        .kpi-box-blue { background-color: #dbeafe; }
        .kpi-box-yellow { background-color: #fef3c7; }
        .kpi-box-purple { background-color: #f3e8ff; }
        .badge-open-later {
          background-color: #fee2e2;
          color: #b91c1c;
          border-radius: 6px;
          padding: 6px 14px;
          font-size: 0.82rem;
          font-weight: 600;
          display: inline-block;
        }
        .badge-future-outline {
          border: 1px solid #fca5a5;
          color: #dc2626;
          background-color: transparent;
          border-radius: 6px;
          padding: 5px 14px;
          font-size: 0.82rem;
          font-weight: 500;
          display: inline-block;
        }
        .upcoming-schedule-table td {
          border-top: none;
          border-bottom: 1px solid var(--bs-border-color, #f1f5f9);
          padding: 16px 14px;
          vertical-align: middle;
          font-size: 0.92rem;
        }
        .schedule-time-text { font-size: 0.92rem; font-weight: 700; color: var(--bs-body-color, #0f172a); margin-bottom: 2px; }
        .schedule-class-text { font-size: 0.88rem; color: #475569; }
        .pending-fee-item { border-bottom: 1px solid var(--bs-border-color, #f1f5f9); padding: 14px 0; }
        .pending-fee-item:last-child { border-bottom: none; }
        .pending-fee-title { font-size: 0.92rem; font-weight: 600; color: var(--bs-body-color, #0f172a); margin-bottom: 2px; }
        .pending-fee-amount { font-size: 1rem; font-weight: 700; }
        .btn-toggle-schedule {
          border: 1px solid var(--bs-border-color, #e2e8f0);
          color: #64748b;
          font-size: 0.8rem;
          font-weight: 500;
          padding: 4px 10px;
          background: transparent;
          transition: all 0.15s ease;
        }
        .btn-toggle-schedule:hover {
          background-color: var(--bs-tertiary-bg, #f1f5f9);
          color: #0f172a;
          border-color: #cbd5e1;
        }
        [data-bs-theme="dark"] .kpi-box-pink { background-color: rgba(220, 38, 38, 0.2); }
        [data-bs-theme="dark"] .kpi-box-blue { background-color: rgba(59, 130, 246, 0.2); }
        [data-bs-theme="dark"] .kpi-box-yellow { background-color: rgba(245, 158, 11, 0.2); }
        [data-bs-theme="dark"] .kpi-box-purple { background-color: rgba(168, 85, 247, 0.2); }
        [data-bs-theme="dark"] .badge-future-outline { border-color: rgba(220, 38, 38, 0.45); color: #f87171; }
        [data-bs-theme="dark"] .schedule-class-text { color: #94a3b8; }
        [data-bs-theme="dark"] .btn-toggle-schedule { border-color: #334155; color: #94a3b8; }
      `}</style>
    </div>
  );
}
