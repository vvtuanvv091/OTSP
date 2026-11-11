import { useEffect, useState } from 'react';
import adminApi from '../../api/adminApi';
import { useToast } from '../../context/ToastContext';
import { useConfirm } from '../../context/ConfirmDialogContext';
import { AdminFeedback, AdminLoadingRows, AdminPageHeader, AdminPagination, AdminStats, AdminStatus } from './AdminPageShared';

const PAGE_SIZE = 10;

export default function AdminStudentsPage() {
  const toast = useToast();
  const confirm = useConfirm();
  const [data, setData] = useState(null);
  const [search, setSearch] = useState('');
  const [querySearch, setQuerySearch] = useState('');
  const [ageGroup, setAgeGroup] = useState('all');
  const [status, setStatus] = useState('all');
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busyUserId, setBusyUserId] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [editingStudent, setEditingStudent] = useState(null);
  const [studentForm, setStudentForm] = useState({ fullName: '', phoneNumber: '', guardianFullName: '', guardianContact: '' });

  useEffect(() => {
    const timer = window.setTimeout(() => setQuerySearch(search.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let active = true;
    async function loadStudents() {
      setLoading(true);
      setError('');
      const params = { search: querySearch, page, pageSize: PAGE_SIZE };
      if (ageGroup !== 'all') params.ageGroup = ageGroup;
      if (status !== 'all') params.status = status;

      try {
        const response = await adminApi.getStudents(params);
        if (active) setData(response.data);
      } catch (requestError) {
        if (active) setError(requestError.response?.data?.message || 'Không tải được danh sách học viên.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadStudents();
    return () => { active = false; };
  }, [querySearch, ageGroup, status, page, reload]);

  const updateFilter = (setter, value) => {
    setter(value);
    setPage(1);
  };

  const toggleAccount = async (student) => {
    setBusyUserId(student.userId);
    setError('');
    setMessage('');
    try {
      if (student.status === 'Locked') await adminApi.unlockStudent(student.userId);
      else await adminApi.lockStudent(student.userId);
      setMessage(student.status === 'Locked' ? 'Đã mở khóa tài khoản học viên.' : 'Đã khóa tài khoản học viên.');
      setReload((value) => value + 1);
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Không thể cập nhật trạng thái tài khoản.');
    } finally {
      setBusyUserId('');
    }
  };

  const openEditModal = (student) => {
    setEditingStudent(student);
    setStudentForm({
      fullName: student.fullName || '',
      phoneNumber: student.phoneNumber || '',
      guardianFullName: student.guardianName || '',
      guardianContact: student.guardianContact || '',
    });
  };

  const saveEditStudent = async (e) => {
    e.preventDefault();
    if (!editingStudent) return;
    setBusyUserId(editingStudent.userId);
    setError('');
    setMessage('');
    try {
      await adminApi.updateStudent(editingStudent.userId, studentForm);
      setMessage('Cập nhật thông tin học viên thành công.');
      setEditingStudent(null);
      setReload((v) => v + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể cập nhật học viên.');
    } finally {
      setBusyUserId('');
    }
  };

  const handleDeleteStudent = async (student) => {
    const ok = await confirm({
      title: 'Xóa học viên',
      message: `Bạn có chắc chắn muốn xóa học viên "${student.fullName}"?`,
      confirmText: 'Xóa',
      variant: 'danger',
    });
    if (!ok) return;
    setBusyUserId(student.userId);
    try {
      await adminApi.deleteStudent(student.userId);
      toast.success('Đã xóa học viên thành công.');
      setReload((v) => v + 1);
    } catch (err) {
      toast.error(err.response?.data?.message || 'Không thể xóa học viên.');
    } finally {
      setBusyUserId('');
    }
  };

  const students = data?.students?.items || [];
  const stats = data?.stats || {};

  return (
    <div className="admin-page">
      <AdminPageHeader
        title="Học viên / Phụ huynh"
        description="Quản lý học viên và thông tin người giám hộ được liên kết trong hồ sơ."
      />

      <AdminFeedback error={error} message={message} onDismiss={() => { setError(''); setMessage(''); }} />

      <AdminStats items={[
        { label: 'Học viên', value: stats.total, note: 'Tất cả tài khoản học viên' },
        { label: 'Có người giám hộ', value: stats.withGuardians, note: 'Học viên có liên hệ người giám hộ' },
        { label: 'Tự quản lý tài khoản', value: stats.selfManaged, note: 'Từ 18 tuổi trở lên' },
      ]} />

      <div className="admin-toolbar">
        <label className="admin-search">
          <i className="bi bi-search" aria-hidden="true" />
          <input value={search} onChange={(event) => updateFilter(setSearch, event.target.value)} placeholder="Tìm tên hoặc email học viên…" />
        </label>
        <label className="admin-select">
          <select value={ageGroup} onChange={(event) => updateFilter(setAgeGroup, event.target.value)} aria-label="Lọc theo độ tuổi">
            <option value="all">Tất cả độ tuổi</option>
            <option value="under18">Dưới 18 tuổi</option>
            <option value="adult">Từ 18 tuổi</option>
          </select>
        </label>
        <label className="admin-select">
          <select value={status} onChange={(event) => updateFilter(setStatus, event.target.value)} aria-label="Lọc theo trạng thái tài khoản">
            <option value="all">Tất cả trạng thái</option>
            <option value="Active">Đang hoạt động</option>
            <option value="Locked">Đã khóa</option>
          </select>
        </label>
      </div>

      <section className="admin-panel">
        <div className="admin-panel-heading">Danh sách học viên <span className="admin-count">{data?.students?.total ?? 0}</span></div>
        <div className="admin-table-scroll">
          <table className="admin-table">
            <thead><tr><th>Học viên</th><th>Độ tuổi</th><th>Người giám hộ</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
            <tbody>
              <AdminLoadingRows columns={5} loading={loading} hasItems={students.length > 0} emptyText="Không tìm thấy học viên phù hợp." />
              {!loading && students.map((student) => (
                <tr key={student.userId}>
                  <td><span className="admin-table-primary">{student.fullName}</span><span className="admin-table-secondary">{student.email || 'Chưa có email'}</span></td>
                  <td>{student.age == null ? 'Chưa cập nhật' : `${student.age} tuổi`}</td>
                  <td>
                    {student.guardianName ? <><span className="admin-table-primary">{student.guardianName}</span><span className="admin-table-secondary">{student.guardianContact}</span></> : <span className="admin-table-secondary">Không có người giám hộ</span>}
                  </td>
                  <td><AdminStatus value={student.status} isOnline={student.isOnline} /></td>
                  <td>
                    <div className="admin-actions-inline">
                      <button className="admin-action" type="button" onClick={() => openEditModal(student)}>
                        <i className="bi bi-pencil me-1"></i>Sửa
                      </button>
                      <button className="admin-action" type="button" disabled={busyUserId === student.userId} onClick={() => toggleAccount(student)}>
                        {student.status === 'Locked' ? 'Mở khóa' : 'Khóa'}
                      </button>
                      <button className="admin-action is-danger" type="button" disabled={busyUserId === student.userId} onClick={() => handleDeleteStudent(student)}>
                        <i className="bi bi-trash me-1"></i>Xóa
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <AdminPagination page={data?.students?.page || page} totalPages={data?.students?.totalPages} total={data?.students?.total || 0} pageSize={PAGE_SIZE} onChange={setPage} />
      </section>

      {/* Modal chỉnh sửa học viên */}
      {editingStudent && (
        <div className="modal show d-block" style={{ backgroundColor: 'rgba(0,0,0,0.5)' }} tabIndex="-1">
          <div className="modal-dialog modal-dialog-centered">
            <div className="modal-content rounded-4 border-0 shadow">
              <div className="modal-header border-0 pb-0">
                <h5 className="modal-title fw-bold">Chỉnh sửa học viên</h5>
                <button type="button" className="btn-close" onClick={() => setEditingStudent(null)}></button>
              </div>
              <form onSubmit={saveEditStudent}>
                <div className="modal-body">
                  <div className="mb-3">
                    <label className="form-label small fw-semibold">Họ và tên</label>
                    <input
                      type="text"
                      className="form-control"
                      value={studentForm.fullName}
                      onChange={(e) => setStudentForm({ ...studentForm, fullName: e.target.value })}
                      required
                    />
                  </div>
                  <div className="mb-3">
                    <label className="form-label small fw-semibold">Số điện thoại</label>
                    <input
                      type="text"
                      className="form-control"
                      value={studentForm.phoneNumber}
                      onChange={(e) => setStudentForm({ ...studentForm, phoneNumber: e.target.value })}
                    />
                  </div>
                  <div className="mb-3">
                    <label className="form-label small fw-semibold">Họ tên người giám hộ</label>
                    <input
                      type="text"
                      className="form-control"
                      value={studentForm.guardianFullName}
                      onChange={(e) => setStudentForm({ ...studentForm, guardianFullName: e.target.value })}
                    />
                  </div>
                  <div className="mb-3">
                    <label className="form-label small fw-semibold">Liên hệ người giám hộ</label>
                    <input
                      type="text"
                      className="form-control"
                      value={studentForm.guardianContact}
                      onChange={(e) => setStudentForm({ ...studentForm, guardianContact: e.target.value })}
                    />
                  </div>
                </div>
                <div className="modal-footer border-0 pt-0">
                  <button type="button" className="btn btn-light rounded-pill px-4" onClick={() => setEditingStudent(null)}>
                    Hủy
                  </button>
                  <button type="submit" className="btn btn-danger rounded-pill px-4" style={{ backgroundColor: '#DB303A' }} disabled={busyUserId === editingStudent.userId}>
                    {busyUserId === editingStudent.userId ? 'Đang lưu…' : 'Lưu thay đổi'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      <aside className="admin-note">
        <i className="bi bi-people" aria-hidden="true" />
        <div><strong>Thông tin giám hộ</strong>Người giám hộ là thông tin liên hệ trong hồ sơ học viên, không phải tài khoản đăng nhập riêng.</div>
      </aside>
    </div>
  );
}
