import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import tutorApi from '../../api/tutorApi';

export default function TutorDashboardPage() {
  const [stats, setStats] = useState(null);
  const [recentReviews, setRecentReviews] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    Promise.allSettled([
      tutorApi.getDashboard(),
      tutorApi.getReviews({ page: 1, pageSize: 3 })
    ])
      .then(([dashRes, revRes]) => {
        if (!active) return;
        if (dashRes.status === 'fulfilled') {
          setStats(dashRes.value.data);
        } else {
          console.error('Error fetching tutor stats', dashRes.reason);
          setError('Không tải được thông tin bảng điều khiển.');
        }
        if (revRes.status === 'fulfilled') {
          setRecentReviews(revRes.value.data?.reviews || []);
        }
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
      <div className="d-flex flex-wrap justify-content-between align-items-center gap-3 mb-4 tutor-page-header">
        <div>
          <h1 className="h3 fw-bold mb-1 tutor-page-title">Tổng quan</h1>
          <p className="text-secondary mb-0">Khu làm việc của gia sư</p>
        </div>
        <div>
          <Link to="/tutor/classrooms/create" className="btn btn-danger fw-semibold">
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
            {stats?.rejectionReason && (
              <div className="small fw-semibold text-danger mt-1">Lý do từ chối: {stats.rejectionReason}</div>
            )}
            <div className="small text-secondary mt-1">
              Vui lòng kiểm tra lại thông tin và chứng chỉ hợp lệ, sau đó vào trang hồ sơ để cập nhật và bấm “Gửi xét duyệt lại”.
            </div>
          </div>
          <Link to="/tutor/profile" className="btn btn-sm btn-danger text-white fw-semibold px-3 py-1.5 shadow-sm text-nowrap">
            Cập nhật &amp; gửi duyệt lại
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

      {/* Thẻ thống kê tổng quan với màu đậm chuẩn */}
      <div className="row g-3 mb-4">
        <div className="col-6 col-md-3">
          <div className="tutor-kpi-card h-100 rounded-3 shadow-sm">
            <div className="d-flex align-items-center justify-content-between">
              <div>
                <small className="tutor-kpi-title">BUỔI HÔM NAY</small>
                <h3 className="tutor-kpi-value mt-1 mb-0">{stats?.todaySessionsCount || 0}</h3>
              </div>
              <div className="tutor-kpi-icon is-schedule" aria-hidden="true">
                <i className="bi bi-easel"></i>
              </div>
            </div>
            <Link to="/tutor/schedule" className="small fw-bold text-decoration-none mt-3 d-inline-block" style={{ color: '#B7212B' }}>
              Xem lịch dạy <i className="bi bi-arrow-right"></i>
            </Link>
          </div>
        </div>

        <div className="col-6 col-md-3">
          <div className="tutor-kpi-card h-100 rounded-3 shadow-sm">
            <div className="d-flex align-items-center justify-content-between">
              <div>
                <small className="tutor-kpi-title">LỚP ĐANG DẠY</small>
                <h3 className="tutor-kpi-value mt-1 mb-0">{stats?.activeClassrooms || 0}</h3>
              </div>
              <div className="tutor-kpi-icon is-classroom" aria-hidden="true">
                <i className="bi bi-person-plus"></i>
              </div>
            </div>
            <Link to="/tutor/classrooms" className="small fw-bold text-decoration-none mt-3 d-inline-block" style={{ color: '#0284C7' }}>
              Xem lớp học <i className="bi bi-arrow-right"></i>
            </Link>
          </div>
        </div>

        <div className="col-6 col-md-3">
          <div className="tutor-kpi-card h-100 rounded-3 shadow-sm">
            <div className="d-flex align-items-center justify-content-between">
              <div>
                <small className="tutor-kpi-title">YÊU CẦU CHỜ</small>
                <h3 className="tutor-kpi-value mt-1 mb-0">{stats?.pendingRequests || 0}</h3>
              </div>
              <div className="tutor-kpi-icon is-requests" aria-hidden="true">
                <i className="bi bi-people"></i>
              </div>
            </div>
            <Link to="/tutor/connection-requests" className="small fw-bold text-decoration-none mt-3 d-inline-block" style={{ color: '#4F46E5' }}>
              Xử lý yêu cầu <i className="bi bi-arrow-right"></i>
            </Link>
          </div>
        </div>

        <div className="col-6 col-md-3">
          <div className="tutor-kpi-card h-100 rounded-3 shadow-sm">
            <div className="d-flex align-items-center justify-content-between">
              <div>
                <small className="tutor-kpi-title">ĐÁNH GIÁ</small>
                <h3 className="tutor-kpi-value mt-1 mb-0" style={{ color: '#B45309' }}>
                  <span style={{ color: '#D97706', fontSize: '1.9rem', marginRight: 4 }}>★</span>
                  {stats?.averageRating > 0 ? Number(stats.averageRating).toFixed(1) : '--'}
                </h3>
              </div>
              <div className="tutor-kpi-icon is-rating" aria-hidden="true">
                <i className="bi bi-star-fill"></i>
              </div>
            </div>
            <Link to="/tutor/reviews" className="small fw-bold mt-3 d-inline-block text-decoration-none" style={{ color: '#D97706' }}>
              <i className="bi bi-star-fill me-1" style={{ color: '#D97706' }}></i>Xem nhận xét học viên &rarr;
            </Link>
          </div>
        </div>
      </div>

      {/* Buổi học sắp tới & Nhận xét thu gọn */}
      <div className="row g-4">
        <div className="col-lg-7">
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

        <div className="col-lg-5 d-flex flex-column gap-4">
          {stats?.profileCompletion && (
            <section className="tutor-profile-progress" aria-labelledby="tutor-profile-progress-title">
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

          {/* Khối Nhận xét & Đánh giá thu gọn */}
          <div className="tutor-dash-card p-4 rounded-3 shadow-sm bg-white border">
            <div className="d-flex justify-content-between align-items-center mb-3">
              <h5 className="fw-bold mb-0">
                <i className="bi bi-star-fill me-2" style={{ color: '#D97706' }}></i>
                Nhận xét &amp; Đánh giá
              </h5>
              <Link
                to="/tutor/reviews"
                className="btn btn-sm btn-outline-warning text-dark fw-bold rounded-pill px-3 py-1 shadow-sm"
                style={{ borderColor: '#D97706', fontSize: '0.78rem' }}
              >
                Xem tất cả &rarr;
              </Link>
            </div>

            {/* Hộp điểm trung bình và sao vàng đậm */}
            <div className="p-3 rounded-3 mb-3 d-flex align-items-center justify-content-between" style={{ backgroundColor: '#FFFBEB', border: '1px solid #FDE68A' }}>
              <div>
                <div className="small fw-bold text-secondary text-uppercase" style={{ fontSize: '0.72rem', letterSpacing: '0.5px' }}>
                  ĐIỂM TRUNG BÌNH
                </div>
                <div className="d-flex align-items-baseline gap-1.5 mt-0.5">
                  <span className="fw-bolder fs-2" style={{ color: '#B45309', lineHeight: 1 }}>
                    {stats?.averageRating > 0 ? Number(stats.averageRating).toFixed(1) : '--'}
                  </span>
                  <span className="text-secondary small fw-medium">/ 5.0</span>
                </div>
              </div>
              <div className="text-end">
                <div className="d-flex gap-1 justify-content-end mb-1">
                  {[1, 2, 3, 4, 5].map((star) => (
                    <i
                      key={star}
                      className="bi bi-star-fill"
                      style={{
                        color: star <= Math.round(stats?.averageRating || 5) ? '#D97706' : '#CBD5E1',
                        fontSize: '1.15rem'
                      }}
                    ></i>
                  ))}
                </div>
                <div className="small fw-semibold" style={{ color: '#B45309', fontSize: '0.78rem' }}>
                  {recentReviews.length > 0 ? `${recentReviews.length} nhận xét mới nhất` : 'Đánh giá học viên'}
                </div>
              </div>
            </div>

            {/* Danh sách nhận xét gần nhất */}
            {recentReviews.length > 0 ? (
              <div className="d-flex flex-column gap-2.5">
                {recentReviews.slice(0, 3).map((rev) => (
                  <div key={rev.id} className="p-2.5 rounded-3 border bg-light-subtle">
                    <div className="d-flex justify-content-between align-items-center mb-1">
                      <div className="d-flex align-items-center gap-2">
                        <div
                          className="rounded-circle d-flex align-items-center justify-content-center fw-bold text-white shadow-sm flex-shrink-0"
                          style={{ width: 26, height: 26, backgroundColor: '#0284C7', fontSize: '0.75rem' }}
                        >
                          {!rev.studentName ? 'H' : rev.studentName.trim().charAt(0).toUpperCase()}
                        </div>
                        <span className="fw-bold small text-dark">{rev.studentName}</span>
                      </div>
                      <div className="d-flex gap-0.5">
                        {[1, 2, 3, 4, 5].map((s) => (
                          <i
                            key={s}
                            className="bi bi-star-fill"
                            style={{
                              color: s <= (rev.score || 5) ? '#D97706' : '#E2E8F0',
                              fontSize: '0.75rem'
                            }}
                          ></i>
                        ))}
                      </div>
                    </div>
                    <p className="small text-secondary mb-0 text-truncate ps-1 fst-italic" title={rev.comment}>
                      "{rev.comment || 'Học viên hài lòng với buổi học.'}"
                    </p>
                  </div>
                ))}
              </div>
            ) : (
              <div className="text-center py-3 text-secondary small">
                <i className="bi bi-chat-heart fs-3 text-muted mb-1 d-block"></i>
                Chưa có nhận xét nào từ học viên.
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
