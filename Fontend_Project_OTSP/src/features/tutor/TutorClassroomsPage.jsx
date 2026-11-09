import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import tutorApi from '../../api/tutorApi';
import { resolveAssetUrl } from '../../api/config';
import { useAuth } from '../../context/AuthContext';
import ComplaintModal from '../../components/common/ComplaintModal';
import OtspPagination from '../../components/common/OtspPagination';

export default function TutorClassroomsPage() {
  const { user } = useAuth();
  const [classrooms, setClassrooms] = useState([]);
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [reloadKey, setReloadKey] = useState(0);

  // Trạng thái modal khiếu nại
  const [complaintModalOpen, setComplaintModalOpen] = useState(false);
  const [complaintContext, setComplaintContext] = useState('');

  useEffect(() => {
    let active = true;
    async function loadClassrooms() {
      try {
        setLoading(true);
        setError('');
        const params = { page, pageSize: 9 };
        if (statusFilter) params.status = statusFilter;
        const res = await tutorApi.getClassrooms(params);
        if (!active) return;
        setClassrooms(res.data?.items || []);
        setPage(res.data?.page || 1);
        setTotalPages(res.data?.totalPages || 1);
        setTotal(res.data?.total ?? res.data?.totalItems ?? res.data?.items?.length ?? 0);
      } catch {
        if (active) setError('Không tải được danh sách lớp học.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadClassrooms();
    return () => {
      active = false;
    };
  }, [statusFilter, page, reloadKey]);

  const handleCancelClassroom = async (id, canHardDelete) => {
    const promptMsg = canHardDelete
      ? 'Xóa vĩnh viễn lớp này? Dữ liệu lớp và lịch học sẽ bị xóa hoàn toàn khỏi hệ thống.'
      : 'Hủy lớp này và thông báo cho học viên?';

    if (!window.confirm(promptMsg)) return;

    setMessage('');
    setError('');

    try {
      const res = await tutorApi.cancelClassroom(id);
      setMessage(res.data?.message || 'Đã thao tác lớp học thành công.');
      setReloadKey((k) => k + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể hủy/xóa lớp học.');
    }
  };

  const formatPrice = (val) => new Intl.NumberFormat('vi-VN').format(val || 0);

  return (
    <>
      <style>{`
        .tutor-classroom-card {
          transition: all 0.2s ease;
          border: 1px solid #E2E8F0;
          background: #FFFFFF;
        }
        .tutor-classroom-card:hover {
          border-color: #CBD5E1;
          box-shadow: 0 4px 14px rgba(0, 0, 0, 0.06);
          transform: translateY(-1px);
        }
        .tutor-fee-badge {
          background-color: #FAFAFA;
          border: 1px solid #F1F5F9;
          min-width: 76px;
          height: 60px;
        }
        .progress-bar-custom {
          background-color: #DB303A;
          border-radius: 999px;
        }
        .btn-danger {
          background-color: #DB303A !important;
          border-color: #DB303A !important;
        }
        .btn-outline-danger {
          color: #DB303A !important;
          border-color: #cbd5e1 !important;
          background: #ffffff !important;
          font-weight: 500;
        }
        .btn-outline-danger:hover {
          background-color: #DB303A !important;
          border-color: #DB303A !important;
          color: #ffffff !important;
        }
      `}</style>

      <div className="container-fluid py-4">
        {/* Header */}
        <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-4">
          <div>
            <h1 className="h3 fw-bold mb-1 text-dark">Quản lý lớp học</h1>
            <p className="text-secondary mb-0">Quản lý các lớp đang dạy (lớp 1-1 và lớp nhóm), lịch dạy và học viên.</p>
          </div>
          <Link to="/tutor/classrooms/create" className="btn btn-danger">
            <i className="bi bi-plus-lg me-1" aria-hidden="true"></i> Tạo lớp nhóm
          </Link>
        </div>

        {/* Alerts */}
        {user?.verificationStatus === 'Pending' && (
          <div className="alert alert-warning border border-warning-subtle rounded-3 p-3 mb-4 d-flex align-items-center gap-3">
            <i className="bi bi-hourglass-split fs-3 text-warning flex-shrink-0"></i>
            <div>
              <div className="fw-bold">Hồ sơ gia sư của bạn đang chờ Admin phê duyệt!</div>
              <div className="small text-muted">
                Bạn cần được Quản trị viên xét duyệt hồ sơ trước khi có thể tạo lớp học nhóm hoặc nhận học viên.
              </div>
            </div>
          </div>
        )}

        {message && (
          <div className="alert alert-success alert-dismissible fade show" role="alert">
            <i className="bi bi-check-circle me-1"></i>{message}
            <button type="button" className="btn-close" onClick={() => setMessage('')} aria-label="Đóng"></button>
          </div>
        )}

        {error && (
          <div className="alert alert-warning alert-dismissible fade show" role="alert">
            <i className="bi bi-exclamation-triangle me-1"></i>{error}
            <button type="button" className="btn-close" onClick={() => setError('')} aria-label="Đóng"></button>
          </div>
        )}

        {/* Filter Box */}
        <div className="card border-0 shadow-sm rounded-3 mb-4">
          <div className="card-body py-3">
            <div className="row g-2 align-items-center">
              <div className="col-12 col-sm-auto">
                <span className="small fw-semibold text-secondary me-2">
                  <i className="bi bi-funnel me-1"></i>Lọc trạng thái:
                </span>
              </div>
              <div className="col-12 col-sm-4 col-md-3">
                <select
                  id="statusFilter"
                  className="form-select form-select-sm"
                  value={statusFilter}
                  onChange={(e) => {
                    setStatusFilter(e.target.value);
                    setPage(1);
                  }}
                >
                  <option value="">Tất cả trạng thái</option>
                  <option value="Active">Đang tuyển sinh / Đang dạy</option>
                  <option value="Completed">Đã hoàn thành</option>
                  <option value="Cancelled">Đã hủy</option>
                </select>
              </div>
              {statusFilter && (
                <div className="col-auto">
                  <button
                    type="button"
                    className="btn btn-sm btn-link text-decoration-none text-danger p-0 ms-1"
                    onClick={() => { setStatusFilter(''); setPage(1); }}
                  >
                    <i className="bi bi-x-circle me-1"></i>Xóa lọc
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Grid 3x3 Card */}
        {loading ? (
          <div className="text-center py-5">
            <div className="spinner-border text-danger" role="status"></div>
          </div>
        ) : classrooms.length === 0 ? (
          <div className="card border-0 shadow-sm rounded-4 p-5 text-center text-secondary">
            <i className="bi bi-easel fs-1 d-block mb-2 text-muted" aria-hidden="true"></i>
            <h3 className="h5 fw-bold text-dark">Chưa có lớp học nào phù hợp</h3>
            <p className="small text-muted mb-3">Bạn chưa có lớp học nào trong danh mục hoặc trạng thái đã chọn.</p>
            <div>
              <Link to="/tutor/classrooms/create" className="btn btn-danger btn-sm">
                <i className="bi bi-plus-lg me-1"></i>Tạo lớp nhóm mới
              </Link>
            </div>
          </div>
        ) : (
          <>
            <div className="row row-cols-1 row-cols-md-2 row-cols-xl-3 g-3 mb-4">
              {classrooms.map((c) => {
                const isGroup = c.type === 'Group' || c.type === 1;
                const statusLabel = c.status === 'Active' ? 'Đang mở' : c.status === 'Completed' ? 'Đã hoàn thành' : 'Đã hủy';
                const statusBadge = c.status === 'Active' ? 'bg-success-subtle text-success border border-success-subtle' : c.status === 'Completed' ? 'bg-secondary-subtle text-secondary border border-secondary-subtle' : 'bg-danger-subtle text-danger border border-danger-subtle';
                const progressPct = c.totalSessions > 0 ? Math.round((c.completedSessions / c.totalSessions) * 100) : 0;

                return (
                  <div key={c.id} className="col">
                    <div className="card tutor-classroom-card h-100 rounded-3 p-3 p-sm-3.5 d-flex flex-column justify-content-between">
                      <div>
                        <div className="d-flex align-items-start gap-2.5">
                          {c.coverImageUrl ? (
                            <div className="position-relative flex-shrink-0" style={{ width: 60, height: 60 }}>
                              <img
                                src={resolveAssetUrl(c.coverImageUrl)}
                                alt={c.name}
                                className="w-100 h-100 rounded-3 object-fit-cover border shadow-sm"
                                onError={(e) => { e.currentTarget.style.display = 'none'; }}
                              />
                            </div>
                          ) : (
                            <div className="tutor-fee-badge p-2 rounded-3 text-center d-flex flex-column justify-content-center flex-shrink-0">
                              <span className="text-secondary small" style={{ fontSize: '0.72rem', lineHeight: 1.2 }}>Học phí</span>
                              <span className="fw-bold text-danger mt-0.5" style={{ fontSize: '0.9rem', lineHeight: 1.2 }}>
                                {formatPrice(c.fee)} đ
                              </span>
                            </div>
                          )}

                          <div className="flex-grow-1 min-w-0 ms-2">
                            {(() => {
                              const groupDisplayName = c.name?.trim() || (isGroup ? `Nhóm ${c.subjectName} (${c.formattedSchedule || c.scheduleSummary || 'Khác ca'})` : `Lớp 1-1 ${c.subjectName}`);
                              return (
                                <div className="d-flex align-items-start justify-content-between gap-1 mb-1">
                                  <div className="fw-bold text-dark text-truncate" style={{ fontSize: '0.95rem' }} title={`${groupDisplayName} (${c.subjectName} · ${c.gradeLevelName})`}>
                                    <i className="bi bi-collection-play-fill text-danger me-1"></i>
                                    <span className="text-dark fw-bold">{groupDisplayName}</span>
                                    <span className="text-secondary fw-normal ms-1" style={{ fontSize: '0.82rem' }}>
                                      ({c.subjectName} · {c.gradeLevelName})
                                    </span>
                                  </div>
                                  {c.coverImageUrl && (
                                    <span className="badge bg-danger-subtle text-danger fw-bold text-nowrap px-2 py-0.5" style={{ fontSize: '0.8rem' }}>
                                      {formatPrice(c.fee)} đ
                                    </span>
                                  )}
                                </div>
                              );
                            })()}

                            <div className="small text-secondary text-truncate mb-1" title={c.formattedSchedule || c.scheduleSummary}>
                              <i className="bi bi-calendar3 me-1 text-muted"></i>Lịch học: <b>{c.formattedSchedule || c.scheduleSummary || 'Chưa xếp'}</b>
                            </div>

                            <div className="d-flex align-items-center gap-1 flex-wrap">
                              <span className="small text-secondary text-nowrap me-1">
                                <i className="bi bi-people me-1 text-muted"></i>Sĩ số: <b>{c.studentCount}{isGroup ? `/${c.maxStudents}` : ''} hv</b>
                              </span>
                              <span className={`badge ${!isGroup ? 'bg-info-subtle text-info-emphasis border border-info-subtle' : 'bg-primary-subtle text-primary-emphasis border border-primary-subtle'}`} style={{ fontSize: '0.68rem', padding: '2px 6px' }}>
                                {!isGroup ? 'Lớp 1-1' : 'Lớp nhóm'}
                              </span>
                              <span className={`badge ${statusBadge}`} style={{ fontSize: '0.68rem', padding: '2px 6px' }}>
                                {statusLabel}
                              </span>
                            </div>
                          </div>
                        </div>

                        {/* Progress */}
                        <div className="mt-3 pt-2.5 border-top">
                          <div className="d-flex justify-content-between align-items-center small mb-1.5">
                            <span className="text-secondary" style={{ fontSize: '0.8rem' }}>Tiến độ giảng dạy</span>
                            <span className="fw-bold text-dark" style={{ fontSize: '0.825rem' }}>
                              {c.completedSessions || 0}/{c.totalSessions || 0} buổi
                            </span>
                          </div>
                          <div className="progress" style={{ height: 6, backgroundColor: '#E2E8F0', borderRadius: 999 }}>
                            <div
                              className="progress-bar progress-bar-custom"
                              role="progressbar"
                              style={{ width: `${progressPct}%` }}
                              aria-valuenow={progressPct}
                              aria-valuemin="0"
                              aria-valuemax="100"
                            ></div>
                          </div>
                        </div>
                      </div>

                      {/* Footer Actions */}
                      <div className="d-flex align-items-center justify-content-between gap-2 mt-3 pt-3 border-top">
                        <Link
                          to={`/tutor/documents?classroomId=${c.id}`}
                          className="btn btn-sm btn-outline-secondary px-2.5 px-sm-3 py-1.5 d-inline-flex align-items-center gap-1 rounded-2 text-nowrap flex-shrink-0"
                          style={{ fontSize: '0.825rem' }}
                          title="Xem tài liệu lớp học"
                        >
                          <i className="bi bi-folder2-open"></i><span>Tài liệu lớp</span>
                        </Link>

                        <div className="d-flex align-items-center gap-1">
                          {c.status === 'Active' && (
                            <Link
                              to={`/tutor/classrooms/edit/${c.id}`}
                              className="btn btn-sm btn-outline-danger px-2.5 px-sm-3 py-1.5 rounded-2 text-nowrap"
                              style={{ fontSize: '0.825rem' }}
                            >
                              Sửa lớp
                            </Link>
                          )}

                          <Link
                            to="/tutor/schedule"
                            className="btn btn-sm btn-danger px-2.5 px-sm-3 py-1.5 rounded-2 text-nowrap"
                            style={{ fontSize: '0.825rem' }}
                          >
                            Lịch dạy
                          </Link>

                          {/* Dropdown menu */}
                          <div className="dropdown">
                            <button
                              className="btn btn-sm btn-light border px-2 py-1.5 rounded-2 text-secondary d-flex align-items-center justify-content-center"
                              type="button"
                              data-bs-toggle="dropdown"
                              style={{ height: 32, width: 32 }}
                              title="Thao tác khác"
                            >
                              <i className="bi bi-three-dots-vertical"></i>
                            </button>
                            <ul className="dropdown-menu dropdown-menu-end shadow-sm border small">
                              <li>
                                <Link className="dropdown-item py-1.5" to={`/tutor/classrooms/edit/${c.id}`}>
                                  <i className="bi bi-pencil me-2 text-primary"></i>Chỉnh sửa thông tin
                                </Link>
                              </li>
                              <li>
                                <Link className="dropdown-item py-1.5" to={`/tutor/documents?classroomId=${c.id}`}>
                                  <i className="bi bi-folder2 me-2 text-secondary"></i>Quản lý tài liệu
                                </Link>
                              </li>
                              <li>
                                <button
                                  className="dropdown-item py-1.5 text-warning-emphasis"
                                  type="button"
                                  onClick={() => {
                                    setComplaintContext(`Lớp học: ${c.name || c.subjectName} (#${c.id})`);
                                    setComplaintModalOpen(true);
                                  }}
                                >
                                  <i className="bi bi-flag me-2 text-warning"></i>Báo cáo / Khiếu nại lớp
                                </button>
                              </li>
                              <li><hr className="dropdown-divider my-1" /></li>
                              <li>
                                <button
                                  className="dropdown-item py-1.5 text-danger"
                                  type="button"
                                  onClick={() => handleCancelClassroom(c.id, c.canHardDelete)}
                                >
                                  <i className="bi bi-x-circle me-2"></i>
                                  {c.canHardDelete ? 'Xóa lớp học' : 'Hủy lớp học'}
                                </button>
                              </li>
                            </ul>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Pagination - Luôn hiển thị thanh phân trang chuẩn khi có dữ liệu */}
            {total > 0 && (
              <OtspPagination
                page={page}
                totalPages={totalPages}
                total={total}
                pageSize={9}
                onChange={setPage}
                className="mt-4 rounded-3 shadow-sm border bg-white"
              />
            )}
          </>
        )}

        <ComplaintModal
          isOpen={complaintModalOpen}
          onClose={() => setComplaintModalOpen(false)}
          contextInfo={complaintContext}
          onSuccess={() => setMessage('Khiếu nại của bạn đã được gửi thành công tới Ban Quản Trị!')}
        />
      </div>
    </>
  );
}
