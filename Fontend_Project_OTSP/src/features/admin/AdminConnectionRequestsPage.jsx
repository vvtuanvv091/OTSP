import { useEffect, useState } from 'react';
import adminApi from '../../api/adminApi';
import { AdminFeedback, AdminLoadingRows, AdminPageHeader, AdminPagination, AdminStats } from './AdminPageShared';

const PAGE_SIZE = 10;
const statusLabel = { Pending: 'Chờ phản hồi', Accepted: 'Đã chấp nhận', Rejected: 'Đã từ chối', Cancelled: 'Đã đóng' };

export default function AdminConnectionRequestsPage() {
  const [data, setData] = useState(null);
  const [search, setSearch] = useState('');
  const [querySearch, setQuerySearch] = useState('');
  const [status, setStatus] = useState('all');
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState(null);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  useEffect(() => {
    const timer = window.setTimeout(() => setQuerySearch(search.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let active = true;
    async function loadRequests() {
      setLoading(true);
      setError('');
      const params = { search: querySearch, page, pageSize: PAGE_SIZE };
      if (status !== 'all') params.status = status;
      try {
        const response = await adminApi.getConnectionRequests(params);
        if (active) setData(response.data);
      } catch (requestError) {
        if (active) setError(requestError.response?.data?.message || 'Không tải được yêu cầu kết nối.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadRequests();
    return () => { active = false; };
  }, [querySearch, status, page, reload]);

  const cancelRequest = async (request) => {
    const reason = window.prompt('Nhập lý do đóng yêu cầu đang chờ phản hồi:');
    if (reason === null) return;
    if (reason.trim().length < 5) { setError('Lý do cần tối thiểu 5 ký tự.'); return; }
    setBusyId(request.id);
    setError('');
    setMessage('');
    try {
      await adminApi.cancelConnectionRequest(request.id, reason.trim());
      setMessage('Đã đóng yêu cầu và gửi thông báo cho học viên, gia sư.');
      setReload((value) => value + 1);
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Không thể đóng yêu cầu.');
    } finally { setBusyId(null); }
  };

  const requests = data?.requests?.items || [];
  const stats = data?.stats || {};
  const selectedPage = data?.requests?.page || page;

  return (
    <div className="admin-page">
      <AdminPageHeader title="Yêu cầu kết nối" description="Theo dõi các yêu cầu giữa học viên và gia sư; chỉ đóng yêu cầu đang chờ khi cần kiểm duyệt." />
      <AdminFeedback error={error} message={message} onDismiss={() => { setError(''); setMessage(''); }} />
      <AdminStats columns={4} items={[
        { label: 'Chờ phản hồi', value: stats.pending },
        { label: 'Đã chấp nhận', value: stats.accepted },
        { label: 'Đã từ chối', value: stats.rejected },
        { label: 'Đã đóng', value: stats.cancelled },
      ]} />

      <div className="admin-toolbar">
        <label className="admin-search"><i className="bi bi-search" aria-hidden="true" /><input value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} placeholder="Tìm học viên, gia sư hoặc môn học…" /></label>
        <label className="admin-select"><select value={status} onChange={(event) => { setStatus(event.target.value); setPage(1); }} aria-label="Lọc trạng thái yêu cầu"><option value="all">Tất cả trạng thái</option><option value="Pending">Chờ phản hồi</option><option value="Accepted">Đã chấp nhận</option><option value="Rejected">Đã từ chối</option><option value="Cancelled">Đã đóng</option></select></label>
      </div>

      <section className="admin-panel">
        <div className="admin-panel-heading">Danh sách yêu cầu <span className="admin-count">{data?.requests?.total ?? 0}</span></div>
        <div className="admin-table-scroll"><table className="admin-table admin-wide-table">
          <thead><tr><th>Học viên</th><th>Gia sư</th><th>Môn / Cấp học</th><th>Lời nhắn</th><th>Loại</th><th>Ngày gửi</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
          <tbody>
            <AdminLoadingRows columns={8} loading={loading} hasItems={requests.length > 0} emptyText="Không tìm thấy yêu cầu phù hợp." />
            {!loading && requests.map((request) => (
              <tr key={request.id}>
                <td><span className="admin-table-primary">{request.studentName}</span><span className="admin-table-secondary">{request.studentEmail}</span></td>
                <td><span className="admin-table-primary">{request.tutorName}</span><span className="admin-table-secondary">{request.tutorEmail}</span></td>
                <td>{request.subjectName} · {request.gradeLevelName}{request.classroomName && <span className="admin-table-secondary">Lớp: {request.classroomName}</span>}</td>
                <td className="admin-message-cell">{request.message || 'Không có lời nhắn'}</td>
                <td>{request.preferredType === 'Group' ? 'Lớp nhóm' : '1 kèm 1'}</td>
                <td>{new Date(request.createdAt).toLocaleDateString('vi-VN')}</td>
                <td><span className={`admin-status ${request.status === 'Pending' ? 'is-pending' : request.status === 'Accepted' ? 'is-active' : request.status === 'Rejected' ? 'is-locked' : ''}`}>{statusLabel[request.status] || request.status}</span></td>
                <td>{request.status === 'Pending' ? <button className="admin-action" type="button" disabled={busyId === request.id} onClick={() => cancelRequest(request)}>{busyId === request.id ? 'Đang lưu…' : 'Đóng'}</button> : <span className="admin-table-secondary">—</span>}</td>
              </tr>
            ))}
          </tbody>
        </table></div>
        <AdminPagination page={page} totalPages={data?.requests?.totalPages} total={data?.requests?.total || 0} pageSize={PAGE_SIZE} onChange={setPage} />
      </section>

      <aside className="admin-note"><i className="bi bi-info-circle" aria-hidden="true" /><div><strong>Quy tắc xử lý</strong>Gia sư và học viên tự quyết định chấp nhận/từ chối. Admin chỉ đóng yêu cầu đang chờ phản hồi khi phát hiện spam hoặc vi phạm; mọi thao tác đều được ghi nhật ký.</div></aside>
    </div>
  );
}
