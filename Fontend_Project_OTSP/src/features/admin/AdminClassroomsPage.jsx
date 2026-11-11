import { useEffect, useState } from 'react';
import adminApi from '../../api/adminApi';
import { useToast } from '../../context/ToastContext';
import { useConfirm } from '../../context/ConfirmDialogContext';
import { AdminFeedback, AdminLoadingRows, AdminPageHeader, AdminPagination, AdminStats } from './AdminPageShared';

const PAGE_SIZE = 10;
const classroomStatus = { Active: 'Đang hoạt động', Completed: 'Hoàn thành', Cancelled: 'Đã đóng' };
const typeLabel = { OneToOne: '1 kèm 1', Group: 'Lớp nhóm' };

export default function AdminClassroomsPage() {
  const toast = useToast();
  const confirm = useConfirm();
  const [data, setData] = useState(null);
  const [search, setSearch] = useState('');
  const [querySearch, setQuerySearch] = useState('');
  const [status, setStatus] = useState('all');
  const [type, setType] = useState('all');
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busyKey, setBusyKey] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [editingClass, setEditingClass] = useState(null);
  const [classForm, setClassForm] = useState({ name: '', fee: 0, maxStudents: 1, status: 'Active' });

  useEffect(() => {
    const timer = window.setTimeout(() => setQuerySearch(search.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let active = true;
    async function loadClassrooms() {
      setLoading(true);
      setError('');
      const params = { search: querySearch, page, pageSize: PAGE_SIZE };
      if (status !== 'all') params.status = status;
      if (type !== 'all') params.type = type;
      try {
        const response = await adminApi.getClassrooms(params);
        if (active) setData(response.data);
      } catch (requestError) {
        if (active) setError(requestError.response?.data?.message || 'Không tải được danh sách lớp học.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadClassrooms();
    return () => { active = false; };
  }, [querySearch, status, type, page, reload]);

  const updateFilter = (setter, value) => { setter(value); setPage(1); };

  const cancel = async (item, kind) => {
    const target = kind === 'class' ? `lớp “${item.name}”` : `buổi học gần nhất của lớp “${item.name}”`;
    const reason = window.prompt(`Nhập lý do hủy ${target}:`);
    if (reason === null) return;
    if (reason.trim().length < 5) { setError('Lý do cần tối thiểu 5 ký tự.'); return; }
    const key = `${kind}-${item.id}`;
    setBusyKey(key);
    setError('');
    setMessage('');
    try {
      if (kind === 'class') await adminApi.cancelClassroom(item.id, reason.trim());
      else await adminApi.cancelClassSession(item.nextSessionId, reason.trim());
      setMessage(kind === 'class' ? 'Đã đóng lớp và thông báo các bên.' : 'Đã hủy buổi học và thông báo các bên.');
      setReload((value) => value + 1);
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Không thể cập nhật lớp học/lịch dạy.');
    } finally { setBusyKey(''); }
  };

  const openEditModal = (item) => {
    setEditingClass(item);
    setClassForm({
      name: item.name || '',
      fee: item.fee || 0,
      maxStudents: item.maxStudents || 1,
      status: item.status || 'Active',
    });
  };

  const saveEditClass = async (e) => {
    e.preventDefault();
    if (!editingClass) return;
    setBusyKey(`edit-${editingClass.id}`);
    setError('');
    setMessage('');
    try {
      await adminApi.updateClassroom(editingClass.id, classForm);
      setMessage('Cập nhật lớp học thành công.');
      setEditingClass(null);
      setReload((v) => v + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể cập nhật lớp học.');
    } finally {
      setBusyKey('');
    }
  };

  const handleDeleteClassroom = async (item) => {
    const ok = await confirm({
      title: 'Xóa lớp học',
      message: `Bạn có chắc chắn muốn xóa lớp học "${item.name}"?`,
      confirmText: 'Xóa',
      variant: 'danger',
    });
    if (!ok) return;
    setBusyKey(`delete-${item.id}`);
    try {
      await adminApi.deleteClassroom(item.id);
      toast.success('Đã xóa lớp học thành công.');
      setReload((v) => v + 1);
    } catch (err) {
      toast.error(err.response?.data?.message || 'Không thể xóa lớp học.');
    } finally {
      setBusyKey('');
    }
  };

  const classrooms = data?.classrooms?.items || [];
  const stats = data?.stats || {};

  return (
    <div className="admin-page">
      <AdminPageHeader title="Lớp học & lịch dạy" description="Theo dõi lớp, lịch sắp tới và tình trạng học tập; thao tác hủy được thông báo cho các bên." />
      <AdminFeedback error={error} message={message} onDismiss={() => { setError(''); setMessage(''); }} />
      <AdminStats items={[
        { label: 'Lớp đang hoạt động', value: stats.active },
        { label: 'Lớp đã hoàn thành', value: stats.completed },
        { label: 'Lớp đã đóng', value: stats.cancelled },
      ]} />
      <div className="admin-note"><i className="bi bi-calendar-check" aria-hidden="true" /><div><strong>Buổi học sắp tới</strong>{stats.upcomingSessions ?? 0} buổi đang được lên lịch trên toàn hệ thống.</div></div>

      <div className="admin-toolbar admin-toolbar-classrooms">
        <label className="admin-search"><i className="bi bi-search" aria-hidden="true" /><input value={search} onChange={(event) => updateFilter(setSearch, event.target.value)} placeholder="Tìm lớp, gia sư, học viên hoặc môn học…" /></label>
        <label className="admin-select"><select value={status} onChange={(event) => updateFilter(setStatus, event.target.value)} aria-label="Lọc trạng thái lớp"><option value="all">Tất cả trạng thái</option><option value="Active">Đang hoạt động</option><option value="Completed">Hoàn thành</option><option value="Cancelled">Đã đóng</option></select></label>
        <label className="admin-select"><select value={type} onChange={(event) => updateFilter(setType, event.target.value)} aria-label="Lọc loại lớp"><option value="all">Tất cả loại lớp</option><option value="OneToOne">1 kèm 1</option><option value="Group">Lớp nhóm</option></select></label>
      </div>

      <section className="admin-panel">
        <div className="admin-panel-heading">Lớp học <span className="admin-count">{data?.classrooms?.total ?? 0}</span></div>
        <div className="admin-table-scroll"><table className="admin-table admin-wide-table">
          <thead><tr><th>Lớp / Gia sư</th><th>Môn / Cấp học</th><th>Loại</th><th>Học viên</th><th>Học phí</th><th>Lịch tiếp theo</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
          <tbody>
            <AdminLoadingRows columns={8} loading={loading} hasItems={classrooms.length > 0} emptyText="Không tìm thấy lớp học phù hợp." />
            {!loading && classrooms.map((item) => (
              <tr key={item.id}>
                <td><span className="admin-table-primary">{item.name}</span><span className="admin-table-secondary">{item.tutorName} · {item.tutorEmail}</span></td>
                <td>{item.subjectName} · {item.gradeLevelName}</td>
                <td>{typeLabel[item.type] || item.type}</td>
                <td>{item.studentCount}/{item.maxStudents}</td>
                <td>{Number(item.fee || 0).toLocaleString('vi-VN')} đ</td>
                <td>{item.nextSessionAt ? <><span>{new Date(item.nextSessionAt).toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' })}</span><span className="admin-table-secondary">Còn {item.scheduledSessionCount} buổi đã lên lịch</span></> : <span className="admin-table-secondary">Chưa có buổi tới</span>}</td>
                <td><span className={`admin-status ${item.status === 'Active' ? 'is-active' : item.status === 'Cancelled' ? 'is-locked' : ''}`}>{classroomStatus[item.status] || item.status}</span></td>
                <td>
                  <div className="admin-actions-inline">
                    <button className="admin-action" type="button" onClick={() => openEditModal(item)} title="Chỉnh sửa thông tin lớp">
                      <i className="bi bi-pencil me-1"></i>Sửa
                    </button>
                    {item.status === 'Active' && item.nextSessionId && (
                      <button className="admin-action" type="button" disabled={busyKey === `session-${item.id}`} onClick={() => cancel(item, 'session')} title="Hủy buổi học sắp tới">
                        <i className="bi bi-calendar-x me-1"></i>Hủy buổi
                      </button>
                    )}
                    {item.status === 'Active' && (
                      <button className="admin-action is-danger" type="button" disabled={busyKey === `class-${item.id}`} onClick={() => cancel(item, 'class')} title="Đóng lớp học">
                        <i className="bi bi-slash-circle me-1"></i>Đóng lớp
                      </button>
                    )}
                    <button className="admin-action is-danger" type="button" disabled={busyKey === `delete-${item.id}`} onClick={() => handleDeleteClassroom(item)} title="Xóa lớp học vĩnh viễn">
                      <i className="bi bi-trash me-1"></i>Xóa
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table></div>
        <AdminPagination page={data?.classrooms?.page || page} totalPages={data?.classrooms?.totalPages} total={data?.classrooms?.total || 0} pageSize={PAGE_SIZE} onChange={setPage} />
      </section>

      {/* Modal chỉnh sửa lớp học */}
      {editingClass && (
        <div className="modal show d-block" style={{ backgroundColor: 'rgba(0,0,0,0.5)' }} tabIndex="-1">
          <div className="modal-dialog modal-dialog-centered">
            <div className="modal-content rounded-4 border-0 shadow">
              <div className="modal-header border-0 pb-0">
                <h5 className="modal-title fw-bold">Chỉnh sửa lớp học</h5>
                <button type="button" className="btn-close" onClick={() => setEditingClass(null)}></button>
              </div>
              <form onSubmit={saveEditClass}>
                <div className="modal-body">
                  <div className="mb-3">
                    <label className="form-label small fw-semibold">Tên lớp học</label>
                    <input
                      type="text"
                      className="form-control"
                      value={classForm.name}
                      onChange={(e) => setClassForm({ ...classForm, name: e.target.value })}
                      required
                    />
                  </div>
                  <div className="mb-3">
                    <label className="form-label small fw-semibold">Học phí (đ)</label>
                    <input
                      type="number"
                      className="form-control"
                      value={classForm.fee}
                      onChange={(e) => setClassForm({ ...classForm, fee: Number(e.target.value) })}
                      min="0"
                      step="10000"
                    />
                  </div>
                  <div className="mb-3">
                    <label className="form-label small fw-semibold">Số học viên tối đa</label>
                    <input
                      type="number"
                      className="form-control"
                      value={classForm.maxStudents}
                      onChange={(e) => setClassForm({ ...classForm, maxStudents: Number(e.target.value) })}
                      min="1"
                      max="100"
                    />
                  </div>
                  <div className="mb-3">
                    <label className="form-label small fw-semibold">Trạng thái lớp</label>
                    <select
                      className="form-select"
                      value={classForm.status}
                      onChange={(e) => setClassForm({ ...classForm, status: e.target.value })}
                    >
                      <option value="Active">Đang hoạt động</option>
                      <option value="Completed">Hoàn thành</option>
                      <option value="Cancelled">Đã đóng</option>
                    </select>
                  </div>
                </div>
                <div className="modal-footer border-0 pt-0">
                  <button type="button" className="btn btn-light rounded-pill px-4" onClick={() => setEditingClass(null)}>
                    Hủy
                  </button>
                  <button type="submit" className="btn btn-danger rounded-pill px-4" style={{ backgroundColor: '#DB303A' }} disabled={busyKey === `edit-${editingClass.id}`}>
                    {busyKey === `edit-${editingClass.id}` ? 'Đang lưu…' : 'Lưu thay đổi'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      <aside className="admin-note is-warning"><i className="bi bi-exclamation-triangle" aria-hidden="true" /><div><strong>Quản lý lớp học</strong>Quản trị viên có thể cập nhật thông tin lớp, đóng lớp hoặc xóa khi cần thiết.</div></aside>
    </div>
  );
}
