import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import tutorApi from '../../api/tutorApi';
import OtspPagination from '../../components/common/OtspPagination';

export default function TutorStudentsPage() {
  const [students, setStudents] = useState([]);
  const [activeTab, setActiveTab] = useState('all');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  useEffect(() => {
    let active = true;
    async function loadStudents() {
      try {
        setLoading(true);
        const res = await tutorApi.getStudents({ tab: activeTab, page, pageSize: 10 });
        if (!active) return;
        setStudents(res.data?.items || []);
        setPage(res.data?.page || 1);
        setTotalPages(res.data?.totalPages || 1);
        setTotal(res.data?.total ?? res.data?.totalItems ?? 0);
      } catch {
        if (active) setError('Không tải được danh sách học viên.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadStudents();
    return () => {
      active = false;
    };
  }, [activeTab, page]);

  const handleTabChange = (tab) => {
    setActiveTab(tab);
    setPage(1);
  };

  return (
    <div className="tutor-page-container">
      {/* Tiêu đề trang */}
      <div className="tutor-page-header d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3">
        <div>
          <h1 className="tutor-page-title">Học viên của tôi</h1>
          <p className="tutor-page-subtitle mb-0">Quản lý danh sách học viên theo từng lớp học và theo dõi tiến độ buổi dạy.</p>
        </div>
        <div className="d-flex gap-2">
          <Link to="/tutor/classrooms" className="btn btn-sm btn-outline-danger">
            <i className="bi bi-easel me-1"></i> Quản lý lớp học
          </Link>
          <Link to="/tutor/schedule" className="btn btn-sm btn-outline-secondary">
            <i className="bi bi-calendar3 me-1"></i> Lịch dạy
          </Link>
        </div>
      </div>

      {/* Tab lọc học viên */}
      <div className="mb-4">
        <ul className="nav nav-pills gap-2 bg-light p-1.5 rounded-3 border d-inline-flex">
          <li className="nav-item">
            <button
              type="button"
              className={`nav-link py-1.5 px-3 fw-semibold small ${activeTab === 'all' ? 'active bg-danger text-white shadow-sm' : 'text-secondary'}`}
              onClick={() => handleTabChange('all')}
            >
              <i className="bi bi-people me-1"></i> Tất cả
            </button>
          </li>
          <li className="nav-item">
            <button
              type="button"
              className={`nav-link py-1.5 px-3 fw-semibold small ${activeTab === 'official' ? 'active bg-danger text-white shadow-sm' : 'text-secondary'}`}
              onClick={() => handleTabChange('official')}
            >
              <i className="bi bi-mortarboard-fill me-1"></i> Học chính thức
            </button>
          </li>
          <li className="nav-item">
            <button
              type="button"
              className={`nav-link py-1.5 px-3 fw-semibold small ${activeTab === 'trial' ? 'active bg-danger text-white shadow-sm' : 'text-secondary'}`}
              onClick={() => handleTabChange('trial')}
            >
              <i className="bi bi-lightning-charge me-1"></i> Học thử
            </button>
          </li>
        </ul>
      </div>

      {/* Thông báo hệ thống */}
      {message && (
        <div className="alert alert-success alert-dismissible fade show shadow-sm" role="alert">
          <i className="bi bi-check-circle-fill me-2"></i>{message}
          <button type="button" className="btn-close" onClick={() => setMessage('')} aria-label="Đóng"></button>
        </div>
      )}

      {error && (
        <div className="alert alert-warning alert-dismissible fade show shadow-sm" role="alert">
          <i className="bi bi-exclamation-triangle-fill me-2"></i>{error}
          <button type="button" className="btn-close" onClick={() => setError('')} aria-label="Đóng"></button>
        </div>
      )}

      {loading ? (
        <div className="tutor-card-table-wrapper p-5 text-center">
          <div className="spinner-border text-danger" role="status"></div>
        </div>
      ) : students.length === 0 ? (
        <div className="tutor-card-table-wrapper p-5 text-center">
          <i className="bi bi-people fs-1 text-muted d-block mb-3"></i>
          <h5 className="fw-semibold">Không tìm thấy học viên nào</h5>
          <p className="text-secondary small mb-0">Khi bạn chấp nhận yêu cầu kết nối học thử hoặc chính thức, học viên sẽ hiển thị tại đây.</p>
        </div>
      ) : (
        <>
          {/* ==================== DESKTOP TABLE VIEW (>= 768px) ==================== */}
          <div className="d-none d-md-block tutor-card-table-wrapper">
            <table className="tutor-custom-table">
              <thead>
                <tr>
                  <th scope="col" style={{ width: '20%' }}>Học viên</th>
                  <th scope="col" style={{ width: '24%' }}>Lớp học</th>
                  <th scope="col" style={{ width: '13%' }}>Môn</th>
                  <th scope="col" style={{ width: '13%' }}>Tiến độ</th>
                  <th scope="col" style={{ width: '14%' }}>Buổi tiếp theo</th>
                  <th scope="col" style={{ minWidth: 210, textAlign: 'right', whiteSpace: 'nowrap' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {students.map((student, idx) => (
                  <tr key={`${student.studentId || 's'}-${student.classroomId || student.requestId || idx}`}>
                    <td>
                      <div className="d-flex align-items-center gap-2">
                        <div
                          className="student-avatar-circle"
                          style={{
                            width: 36,
                            height: 36,
                            fontSize: 13,
                            borderRadius: '50%',
                            background: '#e0e7ff',
                            color: '#3730a3',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            fontWeight: 700,
                          }}
                        >
                          {!student.studentName ? 'H' : student.studentName.trim().charAt(0).toUpperCase()}
                        </div>
                        <div className="cell-student-name fw-semibold">{student.studentName}</div>
                      </div>
                    </td>
                    <td>
                      <div className="fw-medium text-dark">{student.classroomName}</div>
                      {student.isTrial ? (
                        <span className="badge bg-danger-subtle text-danger border border-danger-subtle py-0.5 px-1.5 mt-1" style={{ fontSize: 11 }}>
                          <i className="bi bi-lightning-charge me-1"></i>Học thử
                        </span>
                      ) : student.classroomType === 'Group' || student.classroomType === 1 ? (
                        <span className="badge bg-primary-subtle text-primary border border-primary-subtle py-0.5 px-1.5 mt-1" style={{ fontSize: 11 }}>
                          <i className="bi bi-people me-1"></i>Lớp nhóm
                        </span>
                      ) : (
                        <span className="badge bg-warning-subtle text-warning-emphasis border border-warning-subtle py-0.5 px-1.5 mt-1" style={{ fontSize: 11 }}>
                          <i className="bi bi-person me-1"></i>1 kèm 1
                        </span>
                      )}
                    </td>
                    <td>
                      <span className="fw-medium text-secondary">{student.subjectName}</span>
                    </td>
                    <td>
                      <span className={`fw-semibold ${student.isTrial ? 'text-primary' : 'text-danger'}`}>
                        {student.progress || `${student.totalDoneSessions || 0}/${student.totalSessions || 0} buổi`}
                      </span>
                    </td>
                    <td>
                      <div className="slot-time-text fw-medium" style={{ fontSize: '12.5px' }}>
                        {student.nextSessionFormatted || (student.nextSession?.scheduledAt ? new Date(student.nextSession.scheduledAt).toLocaleString('vi-VN') : 'Chưa có lịch')}
                      </div>
                    </td>
                    <td style={{ minWidth: 210, textAlign: 'right', whiteSpace: 'nowrap' }}>
                      <div className="d-flex justify-content-end align-items-center gap-1 flex-nowrap">
                        {student.isTrial && (
                          <Link
                            to="/tutor/schedule"
                            className="btn btn-sm btn-outline-success py-1 px-2 text-nowrap d-inline-flex align-items-center"
                            style={{ whiteSpace: 'nowrap', height: 32, fontSize: 12, fontWeight: 500 }}
                            title="Nhận vào lớp chính thức (Xếp lịch 6h/tuần)"
                          >
                            <i className="bi bi-mortarboard-fill me-1"></i>Nhận vào lớp chính
                          </Link>
                        )}
                        {student.nextSessionId > 0 && (
                          student.canJoinNextSession ? (
                            <Link
                              to={`/room/${student.nextSessionId}`}
                              className="btn btn-sm btn-success d-inline-flex align-items-center justify-content-center"
                              style={{ width: 32, height: 32, padding: 0 }}
                              title="Vào phòng video học ngay"
                            >
                              <i className="bi bi-camera-video-fill"></i>
                            </Link>
                          ) : (
                            <button
                              type="button"
                              className="btn btn-sm btn-outline-secondary d-inline-flex align-items-center justify-content-center"
                              style={{ width: 32, height: 32, padding: 0, opacity: 0.6, cursor: 'not-allowed' }}
                              title={`Phòng học chưa mở (chỉ mở trước 60p): ${student.nextSessionFormatted}`}
                              disabled
                            >
                              <i className="bi bi-camera-video"></i>
                            </button>
                          )
                        )}
                        {student.studentUserId && (
                          <Link
                            to={`/tutor/chat?contactId=${student.studentUserId}${student.requestId ? `&requestId=${student.requestId}` : ''}`}
                            className="btn-tutor-outline-red d-inline-flex align-items-center justify-content-center"
                            style={{ width: 32, height: 32, padding: 0 }}
                            title="Nhắn tin với học viên"
                          >
                            <i className="bi bi-chat-dots"></i>
                          </Link>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* ==================== MOBILE CARD VIEW (< 768px) ==================== */}
          <div className="d-block d-md-none tutor-mobile-list">
            {students.map((student, idx) => (
              <div key={`${student.studentId || 's'}-${student.classroomId || student.requestId || idx}`} className="tutor-mobile-card mb-3 p-3 border rounded-3 bg-white shadow-sm">
                <div className="tutor-m-card-header d-flex justify-content-between align-items-center mb-2 pb-2 border-bottom">
                  <div className="tutor-m-student-info d-flex align-items-center gap-2">
                    <div
                      className="student-avatar-circle"
                      style={{
                        width: 34,
                        height: 34,
                        fontSize: 12,
                        borderRadius: '50%',
                        background: '#e0e7ff',
                        color: '#3730a3',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        fontWeight: 700,
                      }}
                    >
                      {!student.studentName ? 'H' : student.studentName.trim().charAt(0).toUpperCase()}
                    </div>
                    <div>
                      <div className="tutor-m-name fw-bold text-dark">{student.studentName}</div>
                      <div className="tutor-m-subject small text-muted">{student.subjectName}</div>
                    </div>
                  </div>
                  <span className={`badge ${student.isTrial ? 'bg-primary-subtle text-primary border border-primary-subtle' : 'bg-danger-subtle text-danger border border-danger-subtle'} py-1 px-2 fw-semibold`}>
                    {student.progress || `${student.totalDoneSessions || 0}/${student.totalSessions || 0} buổi`}
                  </span>
                </div>

                <div className="tutor-m-card-body small mb-3">
                  <div className="tutor-m-data-row d-flex justify-content-between mb-1">
                    <span className="text-muted"><i className="bi bi-mortarboard me-1"></i>Lớp học:</span>
                    <span className="fw-semibold text-dark text-end">
                      {student.classroomName}
                      {student.isTrial ? (
                        <span className="badge bg-danger-subtle text-danger ms-1" style={{ fontSize: 10 }}>Học thử</span>
                      ) : student.classroomType === 'Group' || student.classroomType === 1 ? (
                        <span className="badge bg-primary-subtle text-primary ms-1" style={{ fontSize: 10 }}>Nhóm</span>
                      ) : (
                        <span className="badge bg-warning-subtle text-warning-emphasis ms-1" style={{ fontSize: 10 }}>1-1</span>
                      )}
                    </span>
                  </div>
                  <div className="tutor-m-data-row d-flex justify-content-between mb-1">
                    <span className="text-muted"><i className="bi bi-calendar-event me-1"></i>Buổi tiếp theo:</span>
                    <span className="fw-semibold text-danger text-end">
                      {student.nextSessionFormatted || (student.nextSession?.scheduledAt ? new Date(student.nextSession.scheduledAt).toLocaleString('vi-VN') : 'Chưa có lịch')}
                    </span>
                  </div>
                </div>

                <div className="tutor-m-card-footer d-flex flex-wrap gap-2">
                  {student.isTrial && (
                    <Link
                      to="/tutor/schedule"
                      className="btn btn-sm btn-outline-success w-100 py-1.5 d-flex align-items-center justify-content-center gap-1"
                    >
                      <i className="bi bi-mortarboard"></i> Nhận vào lớp chính (Xếp lịch 6h/tuần)
                    </Link>
                  )}
                  {student.nextSessionId > 0 && (
                    student.canJoinNextSession ? (
                      <Link
                        to={`/room/${student.nextSessionId}`}
                        className="btn btn-sm btn-success flex-grow-1 py-1.5 d-flex align-items-center justify-content-center gap-1"
                      >
                        <i className="bi bi-camera-video-fill"></i> Vào phòng học
                      </Link>
                    ) : (
                      <button
                        type="button"
                        className="btn btn-sm btn-outline-secondary flex-grow-1 py-1.5 d-flex align-items-center justify-content-center gap-1"
                        style={{ opacity: 0.6, cursor: 'not-allowed' }}
                        disabled
                        title={`Phòng học chưa mở (chỉ mở trước 60p): ${student.nextSessionFormatted}`}
                      >
                        <i className="bi bi-clock"></i> Chưa mở
                      </button>
                    )
                  )}
                  {student.studentUserId && (
                    <Link
                      to={`/tutor/chat?contactId=${student.studentUserId}${student.requestId ? `&requestId=${student.requestId}` : ''}`}
                      className="btn btn-sm btn-outline-danger flex-grow-1 py-1.5 d-flex align-items-center justify-content-center gap-1"
                    >
                      <i className="bi bi-chat-dots"></i> Nhắn tin
                    </Link>
                  )}
                </div>
              </div>
            ))}
          </div>

          {/* Phân trang danh sách học viên */}
          {total > 0 && (
            <OtspPagination
              page={page}
              totalPages={totalPages}
              total={total}
              pageSize={10}
              onChange={setPage}
              className="mt-4 rounded-3 shadow-sm border"
            />
          )}
        </>
      )}
    </div>
  );
}
