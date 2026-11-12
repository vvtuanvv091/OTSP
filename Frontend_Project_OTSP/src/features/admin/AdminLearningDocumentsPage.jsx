import { useEffect, useState } from 'react';
import adminApi from '../../api/adminApi';
import { repairMojibake } from '../../utils/repairMojibake';
import { useToast } from '../../context/toastHooks';
import { useConfirm } from '../../context/confirmHooks';
import { AdminFeedback, AdminLoadingRows, AdminPageHeader, AdminPagination, AdminStats } from './AdminPageShared';

const PAGE_SIZE = 10;
const statusLabel = { Pending: 'Chờ duyệt', Approved: 'Đã duyệt', Rejected: 'Đã gỡ / từ chối' };

export default function AdminLearningDocumentsPage() {
  const toast = useToast();
  const confirm = useConfirm();
  const [data, setData] = useState(null);
  const [search, setSearch] = useState('');
  const [querySearch, setQuerySearch] = useState('');
  const [scope, setScope] = useState('all');
  const [status, setStatus] = useState('all');
  const [subjectId, setSubjectId] = useState('all');
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState(null);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [editingDoc, setEditingDoc] = useState(null);
  const [docForm, setDocForm] = useState({ title: '', description: '', scope: 'Public' });

  useEffect(() => {
    const timer = window.setTimeout(() => setQuerySearch(search.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let active = true;
    async function loadDocuments() {
      setLoading(true);
      setError('');
      const params = { search: querySearch, page, pageSize: PAGE_SIZE };
      if (scope !== 'all') params.scope = scope;
      if (status !== 'all') params.status = status;
      if (subjectId !== 'all') params.subjectId = subjectId;
      try {
        const response = await adminApi.getLearningDocuments(params);
        if (active) setData(response.data);
      } catch (requestError) {
        if (active) setError(requestError.response?.data?.message || 'Không tải được danh sách tài liệu.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadDocuments();
    return () => { active = false; };
  }, [querySearch, scope, status, subjectId, page, reload]);

  const updateFilter = (setter, value) => { setter(value); setPage(1); };

  const review = async (document, approve) => {
    const isPublished = document.status === 'Approved';
    const reason = approve ? null : await confirm({
      title: isPublished ? 'Gỡ tài liệu khỏi mục công khai' : 'Từ chối tài liệu',
      message: isPublished ? `Nhập lý do gỡ tài liệu “${document.title}” khỏi mục công khai:` : `Nhập lý do từ chối tài liệu “${document.title}”:`,
      confirmText: isPublished ? 'Gỡ tài liệu' : 'Từ chối',
      variant: 'danger',
      input: {
        label: 'Lý do',
        placeholder: 'Nhập lý do (tối thiểu 5 ký tự)...',
        required: true,
        minLength: 5,
      },
    });
    if (!approve && reason === null) return;
    setBusyId(document.id);
    setError('');
    setMessage('');
    try {
      if (approve) await adminApi.approveLearningDocument(document.id);
      else await adminApi.rejectLearningDocument(document.id, reason.trim());
      setMessage(approve ? `Đã duyệt “${document.title}”.` : `Đã gỡ/từ chối “${document.title}” và báo cho gia sư.`);
      setReload((value) => value + 1);
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Không thể cập nhật tài liệu.');
    } finally { setBusyId(null); }
  };

  const openEditModal = (doc) => {
    setEditingDoc(doc);
    setDocForm({
      title: repairMojibake(doc.title || ''),
      description: doc.description || '',
      scope: doc.scope || 'Public',
    });
  };

  const saveEditDoc = async (e) => {
    e.preventDefault();
    if (!editingDoc) return;
    setBusyId(editingDoc.id);
    setError('');
    setMessage('');
    try {
      await adminApi.updateLearningDocument(editingDoc.id, docForm);
      setMessage('Cập nhật tài liệu thành công.');
      setEditingDoc(null);
      setReload((v) => v + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể cập nhật tài liệu.');
    } finally {
      setBusyId(null);
    }
  };

  const handleDeleteDocument = async (document) => {
    const ok = await confirm({
      title: 'Xóa tài liệu',
      message: `Bạn có chắc chắn muốn xóa tài liệu "${document.title}" khỏi hệ thống?`,
      confirmText: 'Xóa',
      variant: 'danger',
    });
    if (!ok) return;
    setBusyId(document.id);
    try {
      await adminApi.deleteLearningDocument(document.id);
      toast.success('Đã xóa tài liệu thành công.');
      setReload((v) => v + 1);
    } catch (err) {
      toast.error(err.response?.data?.message || 'Không thể xóa tài liệu.');
    } finally {
      setBusyId(null);
    }
  };

  const openFile = async (document) => {
    const isPdf = document.fileExtension?.toLowerCase() === '.pdf';
    let preview = null;
    if (isPdf) {
      preview = window.open('', '_blank');
      if (preview) {
        preview.document.title = document.title;
        preview.document.body.innerHTML = '<p style="font:14px sans-serif;padding:20px">Đang tải tài liệu…</p>';
      }
    }

    try {
      const response = await adminApi.getLearningDocumentFile(document.id);
      const fileUrl = URL.createObjectURL(response.data);
      if (isPdf && preview) {
        preview.location.href = fileUrl;
      } else {
        if (preview) preview.close();
        const a = window.document.createElement('a');
        a.href = fileUrl;
        a.download = document.title + (document.fileExtension || '');
        window.document.body.appendChild(a);
        a.click();
        window.document.body.removeChild(a);
        setMessage(`Đã tải xuống tệp “${document.title}”.`);
      }
      window.setTimeout(() => URL.revokeObjectURL(fileUrl), 60_000);
    } catch (requestError) {
      if (preview) preview.close();
      let errorMsg = 'Không mở được tệp tài liệu.';
      if (requestError.response?.data instanceof Blob) {
        try {
          const text = await requestError.response.data.text();
          const parsed = JSON.parse(text);
          if (parsed?.message) errorMsg = parsed.message;
        } catch {
          // fallback
        }
      } else if (requestError.response?.data?.message) {
        errorMsg = requestError.response.data.message;
      }
      setError(errorMsg);
    }
  };

  const setQuickFilter = (newScope, newStatus) => {
    setScope(newScope);
    setStatus(newStatus);
    setPage(1);
  };

  const documents = data?.documents?.items || [];
  const stats = data?.stats || {};
  const totalAll = (stats.publicPending || 0) + (stats.publicApproved || 0) + (stats.publicRejected || 0) + (stats.classroomDocuments || 0);

  return (
    <div className="admin-page">
      <AdminPageHeader title="Tài liệu ôn tập" description="Kiểm duyệt tài liệu công khai trước khi phát hành; tài liệu trong lớp chỉ hiển thị cho thành viên lớp." />
      <AdminFeedback error={error} message={message} onDismiss={() => { setError(''); setMessage(''); }} />
      <AdminStats items={[
        { label: 'Công khai chờ duyệt', value: stats.publicPending, note: 'Chưa hiển thị cho người học' },
        { label: 'Đang công khai', value: stats.publicApproved, note: 'Đã được Admin duyệt' },
        { label: 'Đã từ chối / gỡ', value: stats.publicRejected, note: 'Không hiển thị công khai' },
      ]} />
      <aside className="admin-note"><i className="bi bi-lock" aria-hidden="true" /><div><strong>Tài liệu lớp học: {stats.classroomDocuments ?? 0}</strong>Tài liệu Scope=Classroom không đi qua duyệt công khai; quyền xem vẫn giới hạn theo thành viên lớp.</div></aside>

      <div className="d-flex align-items-center gap-2 mb-3 flex-wrap">
        <button
          type="button"
          className={`btn btn-sm ${scope === 'all' && status === 'all' ? 'btn-danger' : 'btn-outline-secondary'}`}
          onClick={() => setQuickFilter('all', 'all')}
        >
          Tất cả ({totalAll})
        </button>
        <button
          type="button"
          className={`btn btn-sm ${scope === 'Public' && status === 'Pending' ? 'btn-warning text-dark fw-bold' : 'btn-outline-warning text-dark'}`}
          onClick={() => setQuickFilter('Public', 'Pending')}
        >
          <i className="bi bi-hourglass-split me-1"></i>Công khai chờ duyệt ({stats.publicPending || 0})
        </button>
        <button
          type="button"
          className={`btn btn-sm ${scope === 'Public' && status === 'Approved' ? 'btn-success' : 'btn-outline-success'}`}
          onClick={() => setQuickFilter('Public', 'Approved')}
        >
          <i className="bi bi-check-circle me-1"></i>Đang công khai ({stats.publicApproved || 0})
        </button>
        <button
          type="button"
          className={`btn btn-sm ${scope === 'Classroom' ? 'btn-primary' : 'btn-outline-primary'}`}
          onClick={() => setQuickFilter('Classroom', 'all')}
        >
          <i className="bi bi-easel me-1"></i>Tài liệu lớp học ({stats.classroomDocuments || 0})
        </button>
        <button
          type="button"
          className={`btn btn-sm ${scope === 'Public' && status === 'Rejected' ? 'btn-secondary' : 'btn-outline-secondary'}`}
          onClick={() => setQuickFilter('Public', 'Rejected')}
        >
          <i className="bi bi-x-circle me-1"></i>Đã từ chối ({stats.publicRejected || 0})
        </button>
      </div>

      <div className="admin-toolbar admin-document-toolbar">
        <label className="admin-search"><i className="bi bi-search" aria-hidden="true" /><input value={search} onChange={(event) => updateFilter(setSearch, event.target.value)} placeholder="Tìm tiêu đề, gia sư hoặc môn học…" /></label>
        <label className="admin-select"><select value={scope} onChange={(event) => updateFilter(setScope, event.target.value)} aria-label="Lọc phạm vi"><option value="all">Mọi phạm vi</option><option value="Public">Công khai</option><option value="Classroom">Trong lớp</option></select></label>
        <label className="admin-select"><select value={status} onChange={(event) => updateFilter(setStatus, event.target.value)} aria-label="Lọc trạng thái duyệt"><option value="all">Mọi trạng thái</option><option value="Pending">Chờ duyệt</option><option value="Approved">Đã duyệt</option><option value="Rejected">Đã gỡ / từ chối</option></select></label>
        <label className="admin-select"><select value={subjectId} onChange={(event) => updateFilter(setSubjectId, event.target.value)} aria-label="Lọc môn học"><option value="all">Tất cả môn học</option>{(data?.subjectOptions || []).map((subject) => <option key={subject.id} value={subject.id}>{subject.name}</option>)}</select></label>
      </div>

      <section className="admin-panel">
        <div className="admin-panel-heading">Danh sách tài liệu <span className="admin-count">{data?.documents?.total ?? 0}</span></div>
        <div className="admin-table-scroll"><table className="admin-table admin-wide-table">
          <thead><tr><th>Tài liệu</th><th>Gia sư</th><th>Môn / Cấp học</th><th>Phạm vi</th><th>Ngày tải</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
          <tbody>
            <AdminLoadingRows columns={7} loading={loading} hasItems={documents.length > 0} emptyText="Không tìm thấy tài liệu phù hợp." />
            {!loading && documents.map((document) => (
              <tr key={document.id}>
                <td><span className="admin-table-primary">{repairMojibake(document.title)}</span><span className="admin-table-secondary">{document.fileExtension?.toUpperCase() || 'Tệp'}{document.classroomName ? ` · ${document.classroomName}` : ''}</span></td>
                <td><span className="admin-table-primary">{document.tutorName}</span><span className="admin-table-secondary">{document.tutorEmail}</span></td>
                <td>{document.subjectName} · {document.gradeLevelName}</td>
                <td>{document.scope === 'Public' ? 'Công khai' : 'Trong lớp'}</td>
                <td>{new Date(document.uploadedAt).toLocaleDateString('vi-VN')}</td>
                <td><span className={`admin-status ${document.status === 'Approved' ? 'is-active' : document.status === 'Pending' ? 'is-pending' : 'is-locked'}`}>{statusLabel[document.status] || document.status}</span></td>
                <td><div className="admin-actions-inline">
                  <button className="admin-action" type="button" onClick={() => openFile(document)}>Xem</button>
                  <button className="admin-action" type="button" onClick={() => openEditModal(document)}>Sửa</button>
                  {document.scope === 'Public' && document.status !== 'Approved' && <button className="admin-action is-approve" type="button" disabled={busyId === document.id} onClick={() => review(document, true)}>{busyId === document.id ? 'Đang lưu…' : 'Duyệt'}</button>}
                  {document.scope === 'Public' && document.status !== 'Rejected' && <button className="admin-action" type="button" disabled={busyId === document.id} onClick={() => review(document, false)}>{busyId === document.id ? 'Đang lưu…' : document.status === 'Approved' ? 'Gỡ' : 'Từ chối'}</button>}
                  <button className="admin-action is-danger" type="button" disabled={busyId === document.id} onClick={() => handleDeleteDocument(document)}>Xóa</button>
                </div></td>
              </tr>
            ))}
          </tbody>
        </table></div>
        <AdminPagination page={data?.documents?.page || page} totalPages={data?.documents?.totalPages} total={data?.documents?.total || 0} pageSize={PAGE_SIZE} onChange={setPage} />
      </section>

      {/* Modal chỉnh sửa tài liệu */}
      {editingDoc && (
        <div className="modal show d-block" style={{ backgroundColor: 'rgba(0,0,0,0.5)' }} tabIndex="-1">
          <div className="modal-dialog modal-dialog-centered">
            <div className="modal-content rounded-4 border-0 shadow">
              <div className="modal-header border-0 pb-0">
                <h5 className="modal-title fw-bold">Chỉnh sửa tài liệu</h5>
                <button type="button" className="btn-close" onClick={() => setEditingDoc(null)}></button>
              </div>
              <form onSubmit={saveEditDoc}>
                <div className="modal-body">
                  <div className="mb-3">
                    <label className="form-label small fw-semibold">Tiêu đề tài liệu</label>
                    <input
                      type="text"
                      className="form-control"
                      value={docForm.title}
                      onChange={(e) => setDocForm({ ...docForm, title: e.target.value })}
                      required
                    />
                  </div>
                  <div className="mb-3">
                    <label className="form-label small fw-semibold">Mô tả tóm tắt</label>
                    <textarea
                      className="form-control"
                      rows={3}
                      value={docForm.description}
                      onChange={(e) => setDocForm({ ...docForm, description: e.target.value })}
                    />
                  </div>
                  <div className="mb-3">
                    <label className="form-label small fw-semibold">Phạm vi tài liệu</label>
                    <select
                      className="form-select"
                      value={docForm.scope}
                      onChange={(e) => setDocForm({ ...docForm, scope: e.target.value })}
                    >
                      <option value="Public">Công khai</option>
                      <option value="Classroom">Nội bộ lớp học</option>
                    </select>
                  </div>
                </div>
                <div className="modal-footer border-0 pt-0">
                  <button type="button" className="btn btn-light rounded-pill px-4" onClick={() => setEditingDoc(null)}>
                    Hủy
                  </button>
                  <button type="submit" className="btn btn-danger rounded-pill px-4" style={{ backgroundColor: '#DB303A' }} disabled={busyId === editingDoc.id}>
                    {busyId === editingDoc.id ? 'Đang lưu…' : 'Lưu thay đổi'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      <aside className="admin-note is-warning"><i className="bi bi-shield-check" aria-hidden="true" /><div><strong>Quy trình công khai</strong>Tài liệu công khai mới tải lên ở trạng thái chờ duyệt; quản trị viên có thể xem, duyệt, sửa hoặc xóa hoàn toàn tệp khi vi phạm.</div></aside>
    </div>
  );
}
