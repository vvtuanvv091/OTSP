import { useEffect, useState } from 'react';
import adminApi from '../../api/adminApi';
import { AdminFeedback, AdminLoadingRows, AdminPageHeader, AdminPagination, AdminStats, AdminStatus } from './AdminPageShared';

const PAGE_SIZE = 10;

export default function AdminTutorsPage() {
  const [data, setData] = useState(null);
  const [search, setSearch] = useState('');
  const [querySearch, setQuerySearch] = useState('');
  const [subjectId, setSubjectId] = useState('all');
  const [teachingFormat, setTeachingFormat] = useState('all');
  const [status, setStatus] = useState('all');
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busyUserId, setBusyUserId] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [editingTutor, setEditingTutor] = useState(null);
  const [tutorForm, setTutorForm] = useState({ fullName: '', hourlyFee: 0, teachingFormat: 'Online', introduction: '' });

  useEffect(() => {
    const timer = window.setTimeout(() => setQuerySearch(search.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let active = true;
    async function loadTutors() {
      setLoading(true);
      setError('');
      const params = { search: querySearch, page, pageSize: PAGE_SIZE };
      if (subjectId !== 'all') params.subjectId = subjectId;
      if (teachingFormat !== 'all') params.teachingFormat = teachingFormat;
      if (status !== 'all') params.status = status;

      try {
        const response = await adminApi.getTutors(params);
        if (active) setData(response.data);
      } catch (requestError) {
        if (active) setError(requestError.response?.data?.message || 'Không tải được danh sách gia sư.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadTutors();
    return () => { active = false; };
  }, [querySearch, subjectId, teachingFormat, status, page, reload]);

  const updateFilter = (setter, value) => { setter(value); setPage(1); };

  const toggleAccount = async (tutor) => {
    setBusyUserId(tutor.userId);
    setError('');
    setMessage('');
    try {
      if (tutor.status === 'Locked') await adminApi.unlockTutor(tutor.userId);
      else await adminApi.lockTutor(tutor.userId);
      setMessage(tutor.status === 'Locked' ? 'Đã mở khóa tài khoản gia sư.' : 'Đã khóa tài khoản gia sư.');
      setReload((value) => value + 1);
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Không thể cập nhật trạng thái tài khoản.');
    } finally {
      setBusyUserId('');
    }
  };

  const openEditModal = (tutor) => {
    setEditingTutor(tutor);
    setTutorForm({
      fullName: tutor.fullName || '',
      hourlyFee: tutor.hourlyFee || 0,
      teachingFormat: tutor.teachingFormat || 'Online',
      introduction: tutor.introduction || '',
    });
  };

  const saveEditTutor = async (e) => {
    e.preventDefault();
    if (!editingTutor) return;
    setBusyUserId(editingTutor.userId);
    setError('');
    setMessage('');
    try {
      await adminApi.updateTutor(editingTutor.userId, tutorForm);
      setMessage('Cập nhật thông tin gia sư thành công.');
      setEditingTutor(null);
      setReload((v) => v + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể cập nhật thông tin gia sư.');
    } finally {
      setBusyUserId('');
    }
  };

  const handleDeleteTutor = async (tutor) => {
    if (!window.confirm(`Bạn có chắc chắn muốn xóa gia sư "${tutor.fullName}"?`)) return;
    setBusyUserId(tutor.userId);
    setError('');
    setMessage('');
    try {
      await adminApi.deleteTutor(tutor.userId);
      setMessage('Đã xóa gia sư thành công.');
      setReload((v) => v + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể xóa gia sư.');
    } finally {
      setBusyUserId('');
    }
  };

  const tutors = data?.tutors?.items || [];
  const stats = data?.stats || {};

  return (
    <div className="admin-page">
      <AdminPageHeader title="Gia sư" description="Danh sách gia sư đã được duyệt và thông tin giảng dạy trên OTSP." />
      <AdminFeedback error={error} message={message} onDismiss={() => { setError(''); setMessage(''); }} />
      <AdminStats items={[
        { label: 'Gia sư đã duyệt', value: stats.approved, note: 'Danh sách chính thức' },
        { label: 'Dạy trực tuyến', value: stats.online, note: 'Hình thức online' },
        { label: 'Dạy trực tiếp', value: stats.offline, note: `Offline${stats.hybrid ? ` · Kết hợp ${stats.hybrid}` : ''}` },
      ]} />

      <div className="admin-toolbar admin-toolbar-tutors">
        <label className="admin-search"><i className="bi bi-search" aria-hidden="true" /><input value={search} onChange={(event) => updateFilter(setSearch, event.target.value)} placeholder="Tìm tên, email hoặc môn học…" /></label>
        <label className="admin-select"><select value={subjectId} onChange={(event) => updateFilter(setSubjectId, event.target.value)} aria-label="Lọc theo môn học"><option value="all">Tất cả môn học</option>{(data?.subjectOptions || []).map((subject) => <option key={subject.id} value={subject.id}>{subject.name}</option>)}</select></label>
        <label className="admin-select"><select value={teachingFormat} onChange={(event) => updateFilter(setTeachingFormat, event.target.value)} aria-label="Lọc theo hình thức"><option value="all">Tất cả hình thức</option><option value="Online">Online</option><option value="Offline">Offline</option><option value="Hybrid">Kết hợp</option></select></label>
        <label className="admin-select"><select value={status} onChange={(event) => updateFilter(setStatus, event.target.value)} aria-label="Lọc theo trạng thái"><option value="all">Mọi trạng thái</option><option value="Active">Đang hoạt động</option><option value="Locked">Đã khóa</option></select></label>
      </div>

      <section className="admin-panel">
        <div className="admin-panel-heading">Gia sư đã duyệt <span className="admin-count">{data?.tutors?.total ?? 0}</span></div>
        <div className="admin-table-scroll">
          <table className="admin-table admin-tutors-table">
            <thead><tr><th>Gia sư</th><th>Môn / Cấp học</th><th>Hình thức</th><th>Học phí</th><th>Đánh giá</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
            <tbody>
              <AdminLoadingRows columns={7} loading={loading} hasItems={tutors.length > 0} emptyText="Không tìm thấy gia sư phù hợp." />
              {!loading && tutors.map((tutor) => (
                <tr key={tutor.tutorId}>
                  <td><span className="admin-table-primary">{tutor.fullName}</span><span className="admin-table-secondary">{tutor.email || tutor.address || 'Chưa có thông tin liên hệ'}</span></td>
                  <td><div className="admin-subjects">{tutor.subjects?.length ? tutor.subjects.map((subject) => <span className="admin-subject-tag" key={`${subject.subjectId}-${subject.gradeName}`}>{subject.subjectName} · {subject.gradeName}</span>) : <span className="admin-table-secondary">Chưa cập nhật môn dạy</span>}</div></td>
                  <td className="admin-format">{tutor.teachingFormat === 'Hybrid' ? 'Kết hợp' : tutor.teachingFormat === 'Offline' ? 'Dạy trực tiếp' : 'Dạy trực tuyến'}</td>
                  <td>{Number(tutor.hourlyFee || 0).toLocaleString('vi-VN')} đ/giờ</td>
                  <td>★ {Number(tutor.averageRating || 0).toFixed(1)} <span className="admin-table-secondary">({tutor.reviewCount || 0})</span></td>
                  <td><AdminStatus value={tutor.status} isOnline={tutor.isOnline} /></td>
                  <td>
                    <div className="admin-actions-inline">
                      <button className="admin-action" type="button" onClick={() => openEditModal(tutor)}>
                        <i className="bi bi-pencil me-1"></i>Sửa
                      </button>
                      <button className="admin-action" type="button" disabled={busyUserId === tutor.userId} onClick={() => toggleAccount(tutor)}>
                        {tutor.status === 'Locked' ? 'Mở khóa' : 'Khóa'}
                      </button>
                      <button className="admin-action is-danger" type="button" disabled={busyUserId === tutor.userId} onClick={() => handleDeleteTutor(tutor)}>
                        <i className="bi bi-trash me-1"></i>Xóa
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <AdminPagination page={data?.tutors?.page || page} totalPages={data?.tutors?.totalPages} total={data?.tutors?.total || 0} pageSize={PAGE_SIZE} onChange={setPage} />
      </section>

      {/* Modal chỉnh sửa gia sư */}
      {editingTutor && (
        <div className="modal show d-block" style={{ backgroundColor: 'rgba(0,0,0,0.5)' }} tabIndex="-1">
          <div className="modal-dialog modal-dialog-centered">
            <div className="modal-content rounded-4 border-0 shadow">
              <div className="modal-header border-0 pb-0">
                <h5 className="modal-title fw-bold">Chỉnh sửa gia sư</h5>
                <button type="button" className="btn-close" onClick={() => setEditingTutor(null)}></button>
              </div>
              <form onSubmit={saveEditTutor}>
                <div className="modal-body">
                  <div className="mb-3">
                    <label className="form-label small fw-semibold">Họ và tên</label>
                    <input
                      type="text"
                      className="form-control"
                      value={tutorForm.fullName}
                      onChange={(e) => setTutorForm({ ...tutorForm, fullName: e.target.value })}
                      required
                    />
                  </div>
                  <div className="mb-3">
                    <label className="form-label small fw-semibold">Học phí (đ/giờ)</label>
                    <input
                      type="number"
                      className="form-control"
                      value={tutorForm.hourlyFee}
                      onChange={(e) => setTutorForm({ ...tutorForm, hourlyFee: Number(e.target.value) })}
                      min="0"
                      step="10000"
                    />
                  </div>
                  <div className="mb-3">
                    <label className="form-label small fw-semibold">Hình thức dạy</label>
                    <select
                      className="form-select"
                      value={tutorForm.teachingFormat}
                      onChange={(e) => setTutorForm({ ...tutorForm, teachingFormat: e.target.value })}
                    >
                      <option value="Online">Online</option>
                      <option value="Offline">Offline</option>
                      <option value="Hybrid">Kết hợp</option>
                    </select>
                  </div>
                </div>
                <div className="modal-footer border-0 pt-0">
                  <button type="button" className="btn btn-light rounded-pill px-4" onClick={() => setEditingTutor(null)}>
                    Hủy
                  </button>
                  <button type="submit" className="btn btn-danger rounded-pill px-4" style={{ backgroundColor: '#DB303A' }} disabled={busyUserId === editingTutor.userId}>
                    {busyUserId === editingTutor.userId ? 'Đang lưu…' : 'Lưu thay đổi'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      <aside className="admin-note"><i className="bi bi-info-circle" aria-hidden="true" /><div><strong>Thông tin hồ sơ</strong>Danh sách này hiển thị gia sư trên hệ thống. Quản trị viên có thể chỉnh sửa hoặc xóa khi cần thiết.</div></aside>
    </div>
  );
}
