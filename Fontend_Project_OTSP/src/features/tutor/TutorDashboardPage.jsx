import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import tutorApi from '../../api/tutorApi';

export default function TutorDashboardPage() {
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    tutorApi.getDashboard()
      .then((res) => {
        if (active) setStats(res.data);
      })
      .catch((err) => {
        console.error('Error fetching tutor stats', err);
        if (active) setError('Không tải được thông tin bảng điều khiển.');
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, []);

  if (loading) {
    return (
      <div className="container py-4 text-center">
        <div className="spinner-border text-danger" role="status">
          <span className="visually-hidden">Đang tải dữ liệu...</span>
        </div>
      </div>
    );
  }

  const nextSession = stats?.upcomingSessions?.[0];

  return (
    <div className="container-fluid py-4 tutor-dashboard-container">
      {/* Tiêu đề trang */}
      <div className="d-flex justify-content-between align-items-center mb-4">
        <div>
          <h1 className="h3 fw-bold mb-1">Tổng quan</h1>
          <p className="text-secondary mb-0">Khu làm việc của gia sư</p>
        </div>
        <div>
          <Link to="/tutor/classrooms/create" className="btn btn-danger fw-semibold" style={{ backgroundColor: '#DB303A' }}>
            <i className="bi bi-plus-circle me-1"></i>Tạo lớp học mới
          </Link>
        </div>
      </div>

      {error && <div className="alert alert-danger py-2 small">{error}</div>}

      {stats?.verificationStatus === 'Pending' && (
        <div className="alert alert-warning border border-warning-subtle rounded-3 p-3 mb-4 d-flex flex-wrap align-items-center gap-3 shadow-sm">
          <i className="bi bi-hourglass-split fs-2 text-warning flex-shrink-0"></i>
          <div className="flex-grow-1 min-w-0">
            <div className="fw-bold fs-6 text-dark">Hồ sơ gia sư của bạn đang chờ Quản trị viên xét duyệt!</div>
            <div className="small text-secondary mt-0.5">
              Tài khoản gia sư mới đăng ký cần được Admin xác minh CCCD và Bằng cấp/Chứng chỉ trước khi hồ sơ được kích hoạt công khai và nhận học viên.
            </div>
          </div>
          <Link to="/tutor/profile" className="btn btn-sm btn-warning text-dark fw-semibold px-3 py-1.5 shadow-sm text-nowrap">
            Xem hồ sơ của tôi
          </Link>
        </div>
      )}

      {stats?.verificationStatus === 'Rejected' && (
        <div className="alert alert-danger border border-danger-subtle rounded-3 p-3 mb-4 d-flex flex-wrap align-items-center gap-3 shadow-sm">
          <i className="bi bi-x-octagon-fill fs-2 text-danger flex-shrink-0"></i>
          <div className="flex-grow-1 min-w-0">
            <div className="fw-bold fs-6 text-dark">Hồ sơ gia sư của bạn đã bị từ chối xét duyệt.</div>
            <div className="small text-secondary mt-0.5">
              Vui lòng kiểm tra lại thông tin và chứng chỉ hợp lệ để cập nhật lại hồ sơ gửi Admin phê duyệt lại.
            </div>
          </div>
          <Link to="/tutor/profile" className="btn btn-sm btn-danger text-white fw-semibold px-3 py-1.5 shadow-sm text-nowrap">
            Cập nhật hồ sơ
          </Link>
        </div>
      )}

      <section className="tutor-banner-card mb-4" aria-label="Buổi học tiếp theo">
        <div className="d-flex flex-wrap align-items-center justify-content-between gap-3">
          <div>
            <div className="tutor-banner-label">Buổi học tiếp theo</div>
            <div className="tutor-banner-title fw-bold">
              {nextSession?.classroomName || 'Chưa có buổi học nào sắp diễn ra'}
            </div>
            {nextSession ? (
              <div className="tutor-banner-meta d-flex align-items-center gap-2 mt-1">
                <span>
                  {nextSession.displayTime || new Date(nextSession.scheduledAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}
                </span>
                {nextSession.canJoin ? (
                  <span className="badge bg-danger">Đang mở</span>
                ) : nextSession.isToday ? (
                  <span className="badge bg-warning text-dark">{nextSession.openAtText || 'Chưa mở'}</span>
                ) : (
                  <span className="badge bg-secondary">Sắp tới</span>
                )}
              </div>
            ) : (
              <p className="text-secondary small mb-0">
                Mở thêm lịch rảnh hoặc thiết lập lịch lớp học để bắt đầu.
              </p>
            )}
          </div>
          {nextSession?.canJoin ? (
            <Link to={`/room/${nextSession.id}`} className="btn btn-danger px-4">
              <i className="bi bi-camera-video-fill me-1"></i>Vào lớp
            </Link>
          ) : (
            <Link to="/tutor/schedule" className="btn btn-outline-secondary px-3 py-2">
              <i className="bi bi-calendar3 me-1"></i>Xem lịch dạy
            </Link>
          )}
        </div>
      </section>

      {/* Thẻ thống kê tổng quan */}
      <div className="row g-3 mb-4">
        <div className="col-6 col-md-3">
          <div className="tutor-kpi-card h-100">
            <div className="d-flex align-items-center justify-content-between">
              <div>
                <small className="tutor-kpi-title">BUỔI HÔM NAY</small>
                <h3 className="tutor-kpi-value mt-1 mb-0">{stats?.todaySessionsCount || 0}</h3>
              </div>
              <div className="tutor-kpi-icon is-schedule" aria-hidden="true">
                <i className="bi bi-easel"></i>
              </div>
            </div>
            <Link to="/tutor/schedule" className="text-danger small fw-semibold text-decoration-none mt-3 d-inline-block">
              Xem lịch dạy <i className="bi bi-arrow-right"></i>
            </Link>
          </div>
        </div>

        <div className="col-6 col-md-3">
          <div className="tutor-kpi-card h-100">
            <div className="d-flex align-items-center justify-content-between">
              <div>
                <small className="tutor-kpi-title">LỚP ĐANG DẠY</small>
                <h3 className="tutor-kpi-value mt-1 mb-0">{stats?.activeClassrooms || 0}</h3>
              </div>
              <div className="tutor-kpi-icon is-classroom" aria-hidden="true">
                <i className="bi bi-person-plus"></i>
              </div>
            </div>
            <Link to="/tutor/classrooms" className="text-danger small fw-semibold text-decoration-none mt-3 d-inline-block">
              Xem lớp học <i className="bi bi-arrow-right"></i>
            </Link>
          </div>
        </div>

        <div className="col-6 col-md-3">
          <div className="tutor-kpi-card h-100">
            <div className="d-flex align-items-center justify-content-between">
              <div>
                <small className="tutor-kpi-title">YÊU CẦU CHỜ</small>
                <h3 className="tutor-kpi-value mt-1 mb-0">{stats?.pendingRequests || 0}</h3>
              </div>
              <div className="tutor-kpi-icon is-requests" aria-hidden="true">
                <i className="bi bi-people"></i>
              </div>
            </div>
            <Link to="/tutor/connection-requests" className="text-danger small fw-semibold text-decoration-none mt-3 d-inline-block">
              Xử lý yêu cầu <i className="bi bi-arrow-right"></i>
            </Link>
          </div>
        </div>

        <div className="col-6 col-md-3">
          <div className="tutor-kpi-card h-100">
            <div className="d-flex align-items-center justify-content-between">
              <div>
                <small className="tutor-kpi-title">ĐÁNH GIÁ</small>
                <h3 className="tutor-kpi-value mt-1 mb-0">
                  ★ {stats?.averageRating > 0 ? Number(stats.averageRating).toFixed(1) : '--'}
                </h3>
              </div>
              <div className="tutor-kpi-icon is-rating" aria-hidden="true">
                <i className="bi bi-star"></i>
              </div>
            </div>
            <span className="text-success small fw-semibold mt-3 d-inline-block">
              <i className="bi bi-star-fill me-1"></i>Đánh giá từ học viên
            </span>
          </div>
        </div>
      </div>

      {/* Buổi học sắp tới & Lối tắt nhanh */}
      <div className="row g-4">
        <div className="col-lg-8">
          <div className="tutor-dash-card p-4 h-100">
            <div className="d-flex justify-content-between align-items-center mb-3">
              <h5 className="fw-bold mb-0">
                <i className="bi bi-calendar-event me-2 text-danger"></i>Buổi học sắp tới
              </h5>
              <Link to="/tutor/schedule" className="btn btn-sm btn-outline-danger rounded-pill">
                Xem toàn bộ lịch
              </Link>
            </div>

            {stats?.upcomingSessions && stats.upcomingSessions.length > 0 ? (
              <div className="list-group list-group-flush">
                {stats.upcomingSessions.map((session) => (
                  <div key={session.id} className="list-group-item px-0 py-3 d-flex flex-wrap gap-2 justify-content-between align-items-center">
                    <div>
                      <h6 className="fw-bold mb-1">{session.classroomName}</h6>
                      <small className="text-secondary">
                        <i className="bi bi-clock me-1 text-danger"></i>
                        {session.displayTime || new Date(session.scheduledAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}
                      </small>
                    </div>
                    <div>
                      {session.canJoin ? (
                        <Link
                          to={`/room/${session.id}`}
                          className="btn btn-sm btn-danger rounded-pill px-3 shadow-sm"
                          style={{ backgroundColor: '#DB303A' }}
                        >
                          <i className="bi bi-camera-video me-1"></i>Vào phòng
                        </Link>
                      ) : session.isToday ? (
                        <span className="badge bg-warning-subtle text-warning-emphasis border border-warning-subtle px-2.5 py-1.5 rounded-pill">
                          <i className="bi bi-clock me-1"></i>{session.openAtText || 'Chưa mở'}
                        </span>
                      ) : (
                        <span className="badge bg-light text-secondary border px-2.5 py-1.5 rounded-pill">
                          <i className="bi bi-calendar-event me-1"></i>Sắp tới
                        </span>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="text-center py-4 text-secondary">
                <i className="bi bi-calendar-check fs-1 text-muted mb-2 d-block"></i>
                <p className="mb-0">Không có buổi học nào sắp diễn ra trong 7 ngày tới.</p>
              </div>
            )}
          </div>
        </div>

        <div className="col-lg-4">
          {stats?.profileCompletion && (
            <section className="tutor-profile-progress h-100" aria-labelledby="tutor-profile-progress-title">
              <div className="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-2">
                <h2 id="tutor-profile-progress-title" className="h5 mb-0">
                  Hoàn thiện hồ sơ <span className="text-secondary fw-normal">· {stats.profileCompletion.percentage}%</span>
                </h2>
                <span className="small fw-semibold text-secondary">
                  {stats.profileCompletion.completedCount}/{stats.profileCompletion.totalCount} mục đã xong
                </span>
              </div>
              <div className="progress tutor-profile-progress-bar mb-3" role="progressbar"
                aria-valuenow={stats.profileCompletion.percentage} aria-valuemin="0" aria-valuemax="100"
                aria-label="Mức độ hoàn thiện hồ sơ">
                <div className="progress-bar bg-danger" style={{ width: `${stats.profileCompletion.percentage}%` }} />
              </div>
              <ul className="tutor-profile-checklist list-unstyled mb-0">
                {stats.profileCompletion.items.map((item) => (
                  <li key={item.key} className={item.complete ? 'is-complete' : 'is-incomplete'}>
                    <i className={`bi ${item.complete ? 'bi-check-circle-fill' : 'bi-circle'}`} aria-hidden="true" />
                    {item.complete ? <span>{item.label}</span> : <Link to={item.href || '/tutor/profile'}>{item.label}</Link>}
                  </li>
                ))}
              </ul>
            </section>
          )}
        </div>
      </div>
    </div>
  );
}
