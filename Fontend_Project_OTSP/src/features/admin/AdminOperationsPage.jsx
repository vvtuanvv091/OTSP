import { useEffect, useMemo, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import adminOperationsApi from '../../api/adminOperationsApi';
import { resolveAssetUrl } from '../../api/config';
import { AdminFeedback, AdminLoadingRows, AdminPageHeader, AdminPagination, AdminStats } from './AdminPageShared';

const PAGE_SIZE = 10;
const roleLabels = { Student_Parent: 'Học viên / Phụ huynh', Tutor: 'Gia sư', Admin: 'Admin' };
const complaintLabels = { New: 'Mới', Processing: 'Đang xử lý', Resolved: 'Đã giải quyết', Rejected: 'Từ chối' };
const alertLabels = { New: 'Chưa xử lý', Reviewed: 'Đã xem xét', Dismissed: 'Đã bỏ qua' };
const pageConfig = {
  reviews: ['Đánh giá & nhận xét', 'Theo dõi đánh giá sau buổi học và các nội dung đã được hệ thống cảnh báo.', 'reviews'],
  messages: ['Tin nhắn', 'Chỉ xem thông tin hội thoại có báo cáo; nội dung trò chuyện riêng được bảo vệ.', 'reported-conversations'],
  complaints: ['Khiếu nại', 'Tiếp nhận phản ánh và theo dõi quá trình xử lý trên nền tảng.', 'complaints'],
  news: ['Tin tức & thông báo', 'Soạn nội dung và quản lý các bài viết hiển thị trên OTSP.', 'news'],
  payments: ['Giao dịch học phí', 'Theo dõi trạng thái thanh toán và hóa đơn học phí.', 'payments'],
  reconciliation: ['Doanh thu & đối soát', 'Tổng hợp giao dịch thành công theo kỳ và đối chiếu số liệu.', 'reconciliation'],
  accounts: ['Phân quyền tài khoản', 'Tạo tài khoản và quản lý vai trò, trạng thái truy cập.', 'accounts'],
  'audit-logs': ['Nhật ký thao tác', 'Tra cứu các thao tác quản trị được lưu trong hệ thống.', 'audit-logs'],
  alerts: ['Cảnh báo bất thường', 'Rà soát tín hiệu đánh giá bất thường trước khi đưa ra quyết định xử lý.', 'alerts'],
  ai: ['Tính năng AI', 'Theo dõi mức sử dụng trợ lý học tập AI; trang này không hiển thị nội dung câu hỏi riêng tư.', 'ai-usage'],
};

const money = (value) => `${Number(value || 0).toLocaleString('vi-VN')} đ`;
const date = (value) => value ? new Date(value).toLocaleDateString('vi-VN') : '—';
const dateTime = (value) => value ? new Date(value).toLocaleString('vi-VN') : '—';
const errorText = (error, fallback) => error.response?.data?.message || fallback;

function downloadCsv(filename, rows) {
  const csv = rows.map((row) => row.map((cell) => `"${String(cell ?? '').replaceAll('"', '""')}"`).join(',')).join('\r\n');
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url; anchor.download = filename; anchor.click();
  URL.revokeObjectURL(url);
}

export default function AdminOperationsPage() {
  const location = useLocation();
  const mode = location.pathname.split('/').filter(Boolean).at(-1) || 'reviews';
  const [prevMode, setPrevMode] = useState(mode);
  const [data, setData] = useState(null);
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('all');
  const [page, setPage] = useState(1);
  const [period, setPeriod] = useState(() => new Date().toISOString().slice(0, 7));
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState(null);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [newsForm, setNewsForm] = useState({ id: null, title: '', category: '', content: '' });
  const [accountForm, setAccountForm] = useState({ fullName: '', email: '', role: 'Student_Parent' });

  if (prevMode !== mode) {
    setPrevMode(mode);
    setSearch('');
    setQuery('');
    setStatus('all');
    setPage(1);
    setData(null);
    setError('');
    setMessage('');
  }

  useEffect(() => {
    const timer = window.setTimeout(() => { setQuery(search.trim()); setPage(1); }, 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let active = true;
    async function loadOperations() {
      const params = { search: query, page, pageSize: PAGE_SIZE };
      if (status !== 'all') params.status = status;
      setLoading(true);
      setError('');
      let request;
      if (mode === 'reconciliation') request = adminOperationsApi.getReconciliation(period);
      else if (mode === 'ai') request = adminOperationsApi.getAiUsage();
      else {
        const fetchers = {
          reviews: adminOperationsApi.getReviews,
          messages: adminOperationsApi.getReportedConversations,
          complaints: adminOperationsApi.getComplaints,
          news: adminOperationsApi.getNews,
          payments: adminOperationsApi.getPayments,
          accounts: adminOperationsApi.getAccounts,
          'audit-logs': adminOperationsApi.getAuditLogs,
          alerts: adminOperationsApi.getAlerts,
        };
        request = fetchers[mode]?.(params);
      }
      if (!request) {
        if (active) {
          setError('Không tìm thấy trang quản trị.');
          setLoading(false);
        }
        return;
      }
      try {
        const response = await request;
        if (active) setData(response.data);
      } catch (requestError) {
        if (active) setError(errorText(requestError, 'Không tải được dữ liệu quản trị.'));
      } finally {
        if (active) setLoading(false);
      }
    }
    loadOperations();
    return () => { active = false; };
  }, [mode, query, status, page, period]);

  const clearFeedback = () => { setError(''); setMessage(''); };
  const setStatusSafely = (value) => { setStatus(value); setPage(1); };
  const config = pageConfig[mode] || pageConfig.reviews;

  const saveNews = async (event) => {
    event.preventDefault(); clearFeedback(); setBusyId('news');
    try {
      const payload = { title: newsForm.title, category: newsForm.category, content: newsForm.content };
      if (newsForm.id) await adminOperationsApi.updateNews(newsForm.id, payload);
      else await adminOperationsApi.createNews(payload);
      setNewsForm({ id: null, title: '', category: '', content: '' });
      setMessage(newsForm.id ? 'Đã cập nhật bài viết.' : 'Đã đăng bài viết.');
      const response = await adminOperationsApi.getNews({ search: query, page, pageSize: PAGE_SIZE }); setData(response.data);
    } catch (requestError) { setError(errorText(requestError, 'Không thể lưu bài viết.')); }
    finally { setBusyId(null); }
  };

  const removeNews = async (article) => {
    if (!window.confirm(`Xóa bài viết “${article.title}”?`)) return;
    setBusyId(article.id); clearFeedback();
    try { await adminOperationsApi.deleteNews(article.id); setMessage('Đã xóa bài viết.'); const response = await adminOperationsApi.getNews({ search: query, page, pageSize: PAGE_SIZE }); setData(response.data); }
    catch (requestError) { setError(errorText(requestError, 'Không thể xóa bài viết.')); }
    finally { setBusyId(null); }
  };

  const updateComplaint = async (complaint, nextStatus) => {
    let resolution = complaint.resolution || '';
    if (nextStatus === 'Resolved' || nextStatus === 'Rejected') {
      resolution = window.prompt(nextStatus === 'Resolved' ? 'Nhập kết quả giải quyết:' : 'Nhập lý do từ chối:', resolution);
      if (resolution === null) return;
      if (resolution.trim().length < 5) { setError('Kết quả cần tối thiểu 5 ký tự.'); return; }
    }
    setBusyId(complaint.id); clearFeedback();
    try { await adminOperationsApi.reviewComplaint(complaint.id, { status: nextStatus, resolution: resolution.trim() || null }); setMessage('Đã cập nhật trạng thái khiếu nại.'); const response = await adminOperationsApi.getComplaints({ search: query, page, pageSize: PAGE_SIZE, ...(status !== 'all' ? { status } : {}) }); setData(response.data); }
    catch (requestError) { setError(errorText(requestError, 'Không thể cập nhật khiếu nại.')); }
    finally { setBusyId(null); }
  };

  const createAccount = async (event) => {
    event.preventDefault(); clearFeedback(); setBusyId('account');
    try {
      const response = await adminOperationsApi.createAccount(accountForm);
      window.alert(`Tài khoản đã được tạo. Mật khẩu tạm thời (hãy sao chép và gửi an toàn):\n\n${response.data.temporaryPassword}`);
      setAccountForm({ fullName: '', email: '', role: 'Student_Parent' }); setMessage('Đã tạo tài khoản.');
      const result = await adminOperationsApi.getAccounts({ search: query, page, pageSize: PAGE_SIZE }); setData(result.data);
    } catch (requestError) { setError(errorText(requestError, 'Không thể tạo tài khoản.')); }
    finally { setBusyId(null); }
  };

  const changeRole = async (account, role) => {
    setBusyId(account.id); clearFeedback();
    try { await adminOperationsApi.changeAccountRole(account.id, role); setMessage('Đã cập nhật vai trò.'); const result = await adminOperationsApi.getAccounts({ search: query, page, pageSize: PAGE_SIZE }); setData(result.data); }
    catch (requestError) { setError(errorText(requestError, 'Không thể đổi vai trò.')); }
    finally { setBusyId(null); }
  };

  const toggleAccount = async (account) => {
    setBusyId(account.id); clearFeedback();
    try { await adminOperationsApi.setAccountStatus(account.id, account.status !== 'Locked'); setMessage('Đã cập nhật trạng thái tài khoản.'); const result = await adminOperationsApi.getAccounts({ search: query, page, pageSize: PAGE_SIZE }); setData(result.data); }
    catch (requestError) { setError(errorText(requestError, 'Không thể cập nhật tài khoản.')); }
    finally { setBusyId(null); }
  };

  const updateAlert = async (alert, nextStatus) => {
    setBusyId(alert.id); clearFeedback();
    try { await adminOperationsApi.updateAlert(alert.id, nextStatus); setMessage('Đã cập nhật cảnh báo.'); const result = await adminOperationsApi.getAlerts({ search: query, page, pageSize: PAGE_SIZE, ...(status !== 'all' ? { status } : {}) }); setData(result.data); }
    catch (requestError) { setError(errorText(requestError, 'Không thể cập nhật cảnh báo.')); }
    finally { setBusyId(null); }
  };

  const pageData = useMemo(() => {
    const keys = { reviews: 'reviews', complaints: 'complaints', news: 'articles', payments: 'payments', accounts: 'accounts', 'audit-logs': 'logs', alerts: 'alerts' };
    return data?.[keys[mode]] || null;
  }, [data, mode]);
  const items = pageData?.items || [];
  const pagination = <AdminPagination page={pageData?.page || page} totalPages={pageData?.totalPages} total={pageData?.total || 0} pageSize={PAGE_SIZE} onChange={setPage} />;

  const renderSearch = (placeholder, filters = null) => (
    <div className="admin-toolbar admin-operations-toolbar">
      <label className="admin-search"><i className="bi bi-search" aria-hidden="true" /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={placeholder} /></label>
      {filters}
    </div>
  );
  const renderRows = (columns, hasItems, emptyText) => <AdminLoadingRows columns={columns} loading={loading} hasItems={hasItems} emptyText={emptyText} />;

  return (
    <div className="admin-page admin-operations-page">
      <AdminPageHeader title={config[0]} description={config[1]} />
      <AdminFeedback error={error} message={message} onDismiss={clearFeedback} />

      {mode === 'reviews' && <>
        <AdminStats items={[{ label: 'Đánh giá', value: data?.stats?.total }, { label: 'Điểm trung bình', value: Number(data?.stats?.average || 0).toFixed(1) }, { label: 'Điểm thấp (≤ 2)', value: data?.stats?.lowScore }, { label: 'Cảnh báo mới', value: data?.stats?.reported }]} columns={4} />
        {renderSearch('Tìm nội dung, học viên hoặc gia sư…')}
        <section className="admin-panel"><div className="admin-panel-heading">Danh sách đánh giá <span className="admin-count">{pageData?.total ?? 0}</span></div><div className="admin-table-scroll"><table className="admin-table"><thead><tr><th>Học viên / Gia sư</th><th>Lớp học</th><th>Đánh giá</th><th>Nhận xét</th><th>Ngày gửi</th><th>Cảnh báo</th></tr></thead><tbody>{renderRows(6, items.length, 'Chưa có đánh giá phù hợp.')}{!loading && items.map((item) => <tr key={item.id}><td><strong>{item.studentName}</strong><span className="admin-table-secondary">Gia sư: {item.tutorName}</span></td><td>{item.classroomName}<span className="admin-table-secondary">{item.subjectName}</span></td><td>★ {item.score}/5</td><td className="admin-message-cell">{item.comment || 'Không có nhận xét'}</td><td>{date(item.reviewedAt)}</td><td>{item.alertCount ? <span className="admin-status is-pending">{item.alertCount} cảnh báo</span> : 'Không có'}</td></tr>)}</tbody></table></div>{pagination}</section>
      </>}

      {mode === 'messages' && <>
        <aside className="admin-note is-warning"><i className="bi bi-shield-lock" aria-hidden="true" /><div><strong>Quyền riêng tư hội thoại</strong>Admin chỉ thấy các bên liên quan, số lượng tin nhắn và báo cáo. Nội dung chat riêng không được tải.</div></aside>
        {renderSearch('Tìm mã hội thoại hoặc người tham gia…')}
        <section className="admin-panel"><div className="admin-panel-heading">Hội thoại có báo cáo <span className="admin-count">{data?.stats?.conversations ?? 0}</span></div><div className="admin-table-scroll"><table className="admin-table"><thead><tr><th>Người tham gia</th><th>Hoạt động cuối</th><th>Số tin nhắn</th><th>Báo cáo</th><th>Thao tác</th></tr></thead><tbody>{renderRows(5, data?.conversations?.length > 0, 'Không có hội thoại được báo cáo.')}{!loading && (data?.conversations || []).map((item) => <tr key={`${item.userOneId}-${item.userTwoId}`}><td><strong>{item.userOneName} ↔ {item.userTwoName}</strong><span className="admin-table-secondary">Mã: {(item.userOneId || '').slice(0, 8)} / {(item.userTwoId || '').slice(0, 8)}</span></td><td>{dateTime(item.lastActivity)}</td><td>{item.messageCount}</td><td><span className="admin-status is-pending">{item.reportCount} báo cáo</span></td><td><Link className="admin-action" to="/admin/complaints">Xem khiếu nại</Link></td></tr>)}</tbody></table></div></section>
      </>}

      {mode === 'complaints' && <>
        <AdminStats items={['New', 'Processing', 'Resolved'].map((key) => ({ label: complaintLabels[key], value: data?.stats?.find((item) => item.status === key)?.count || 0 }))} />
        {renderSearch('Tìm mã khiếu nại hoặc người gửi…', <label className="admin-select"><select value={status} onChange={(event) => setStatusSafely(event.target.value)}><option value="all">Tất cả trạng thái</option>{Object.entries(complaintLabels).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>)}
        <section className="admin-panel"><div className="admin-panel-heading">Danh sách khiếu nại <span className="admin-count">{pageData?.total ?? 0}</span></div><div className="admin-table-scroll"><table className="admin-table admin-wide-table"><thead><tr><th>Mã / Người liên quan</th><th>Nội dung phản ánh</th><th>Ngày gửi</th><th>Trạng thái</th><th>Kết quả</th><th>Thao tác</th></tr></thead><tbody>{renderRows(6, items.length, 'Không có khiếu nại phù hợp.')}{!loading && items.map((item) => <tr key={item.id}><td><strong>KN{String(item.id).padStart(3, '0')}</strong><span className="admin-table-secondary">{item.senderName}{item.targetName ? ` → ${item.targetName}` : ''}</span></td><td>{item.content}{item.evidenceUrl && <a className="admin-table-secondary" href={resolveAssetUrl(item.evidenceUrl)} target="_blank" rel="noreferrer">Mở minh chứng</a>}</td><td>{date(item.createdAt)}</td><td><span className={`admin-status ${item.status === 'New' ? 'is-pending' : item.status === 'Resolved' ? 'is-active' : item.status === 'Rejected' ? 'is-locked' : ''}`}>{complaintLabels[item.status] || item.status}</span></td><td>{item.resolution || '—'}</td><td><div className="admin-actions-inline">{item.status === 'New' && <button type="button" className="admin-action is-approve" disabled={busyId === item.id} onClick={() => updateComplaint(item, 'Processing')}>Tiếp nhận</button>}{item.status !== 'Resolved' && item.status !== 'Rejected' && <button type="button" className="admin-action" disabled={busyId === item.id} onClick={() => updateComplaint(item, 'Resolved')}>Giải quyết</button>}{item.status === 'Processing' && <button type="button" className="admin-action is-danger" disabled={busyId === item.id} onClick={() => updateComplaint(item, 'Rejected')}>Từ chối</button>}</div></td></tr>)}</tbody></table></div>{pagination}</section>
      </>}

      {mode === 'news' && <>
        <AdminStats items={[
          { label: 'Tổng bài viết đã đăng', value: pageData?.total ?? items.length ?? 0 },
          { label: 'Chuyên mục hoạt động', value: (data?.categories || []).length },
          { label: 'Vị trí hiển thị công khai', value: 'Trang /news & /news/:id' }
        ]} />
        <form className="admin-panel admin-operation-form" onSubmit={saveNews}>
          <div className="admin-panel-heading d-flex flex-wrap justify-content-between align-items-center gap-2">
            <span>{newsForm.id ? 'Chỉnh sửa bài viết tin tức' : 'Tạo bài viết mới (Hiển thị tại trang /news)'}</span>
            <a className="admin-action" href="/news" target="_blank" rel="noreferrer">
              <i className="bi bi-box-arrow-up-right me-1" /> Mở trang Tin tức công khai
            </a>
          </div>
          <div className="admin-operation-form-body">
            <label>
              Tiêu đề bài viết
              <input
                required
                maxLength={250}
                value={newsForm.title}
                onChange={(event) => setNewsForm({ ...newsForm, title: event.target.value })}
                placeholder="VD: Hà Nội công bố lịch thi tuyển sinh vào lớp 10…"
              />
            </label>
            <label>
              Chuyên mục hiển thị
              <input
                required
                minLength={2}
                maxLength={80}
                value={newsForm.category}
                onChange={(event) => setNewsForm({ ...newsForm, category: event.target.value })}
                placeholder="Chọn bên dưới hoặc nhập chuyên mục…"
              />
              <div className="d-flex flex-wrap gap-1 mt-2">
                {['Tuyển sinh', 'Phương pháp học', 'Kinh nghiệm chọn gia sư', 'Xu hướng giáo dục', 'Thông báo OTSP'].map((cat) => (
                  <button
                    key={cat}
                    type="button"
                    className={`btn btn-sm rounded-pill px-2 py-0 ${newsForm.category === cat ? 'btn-danger' : 'btn-outline-secondary'}`}
                    style={{ fontSize: 11.5 }}
                    onClick={() => setNewsForm({ ...newsForm, category: cat })}
                  >
                    {cat}
                  </button>
                ))}
              </div>
            </label>
            <label className="is-full">
              Nội dung chi tiết bài viết (Hỗ trợ xuống dòng chia đoạn &amp; đề mục 1. 2. 3.)
              <textarea
                required
                minLength={10}
                rows={6}
                value={newsForm.content}
                onChange={(event) => setNewsForm({ ...newsForm, content: event.target.value })}
                placeholder="Nhập nội dung đầy đủ của bài viết để người dùng đọc tại trang chi tiết /news/:id…"
              />
            </label>
            <div className="admin-form-actions">
              <button type="submit" className="admin-action is-approve" disabled={busyId === 'news'}>
                <i className="bi bi-send-check me-1" />
                {busyId === 'news' ? 'Đang lưu…' : newsForm.id ? 'Lưu cập nhật bài viết' : 'Đăng bài lên trang Tin tức'}
              </button>
              {newsForm.id && (
                <button type="button" className="admin-action" onClick={() => setNewsForm({ id: null, title: '', category: '', content: '' })}>
                  Hủy chỉnh sửa
                </button>
              )}
            </div>
          </div>
        </form>
        {renderSearch('Tìm tiêu đề, chuyên mục hoặc nội dung bài viết…')}
        <section className="admin-panel">
          <div className="admin-panel-heading">
            Danh sách bài viết &amp; Vị trí hiển thị <span className="admin-count">{pageData?.total ?? 0}</span>
          </div>
          <div className="admin-table-scroll">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Tiêu đề &amp; Tóm tắt</th>
                  <th>Chuyên mục</th>
                  <th>Vị trí hiển thị (/news)</th>
                  <th>Người đăng / Ngày</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {renderRows(5, items.length, 'Chưa có bài viết.')}
                {!loading && items.map((item, idx) => (
                  <tr key={item.id}>
                    <td>
                      <strong>{item.title}</strong>
                      <span className="admin-table-secondary">{(item.content || '').slice(0, 120)}{(item.content || '').length > 120 ? '…' : ''}</span>
                    </td>
                    <td><span className="admin-subject-tag">{item.category}</span></td>
                    <td>
                      {page === 1 && idx === 0 ? (
                        <span className="admin-status is-active">★ Tin tiêu điểm (Top 1)</span>
                      ) : (
                        <span className="admin-status is-pending">Danh sách tin &amp; Đọc nhiều</span>
                      )}
                    </td>
                    <td>
                      {item.adminName}
                      <span className="admin-table-secondary">{date(item.publishedAt)}</span>
                    </td>
                    <td>
                      <div className="admin-actions-inline">
                        <a className="admin-action is-approve" href={`/news/${item.id}`} target="_blank" rel="noreferrer">
                          <i className="bi bi-book me-1" /> Đọc chi tiết
                        </a>
                        <button
                          type="button"
                          className="admin-action"
                          onClick={() => {
                            setNewsForm({ id: item.id, title: item.title, category: item.category, content: item.content });
                            window.scrollTo({ top: 0, behavior: 'smooth' });
                          }}
                        >
                          <i className="bi bi-pencil me-1" /> Sửa
                        </button>
                        <button type="button" className="admin-action is-danger" disabled={busyId === item.id} onClick={() => removeNews(item)}>
                          <i className="bi bi-trash me-1" /> Xóa
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {pagination}
        </section>
      </>}

      {mode === 'payments' && <>
        <AdminStats items={[
          { label: 'Giao dịch', value: data?.stats?.reduce((sum, row) => sum + row.count, 0) || 0 },
          { label: 'Đã thanh toán', value: data?.stats?.find((x) => x.status === 'Success')?.count || 0 },
          { label: 'Tổng học phí', value: money(data?.summary?.totalAmount ?? data?.stats?.find((x) => x.status === 'Success')?.amount) },
          { label: 'Phí sàn (20%)', value: money(data?.summary?.totalPlatformFee ?? (data?.stats?.find((x) => x.status === 'Success')?.amount ? Math.round(data?.stats?.find((x) => x.status === 'Success')?.amount * 0.2) : 0)) },
          { label: 'Cần thanh toán lại cho gia sư', value: money(data?.summary?.totalTutorPayout ?? (data?.stats?.find((x) => x.status === 'Success')?.amount ? Math.round(data?.stats?.find((x) => x.status === 'Success')?.amount * 0.8) : 0)) }
        ]} />
        {renderSearch('Tìm học viên, gia sư, lớp hoặc mã giao dịch…', <label className="admin-select"><select value={status} onChange={(event) => setStatusSafely(event.target.value)}><option value="all">Tất cả trạng thái</option><option value="Pending">Đang chờ</option><option value="Success">Thành công</option><option value="Failed">Thất bại</option></select></label>)}
        <section className="admin-panel"><div className="admin-panel-heading">Giao dịch học phí <span className="admin-count">{pageData?.total ?? 0}</span></div><div className="admin-table-scroll"><table className="admin-table"><thead><tr><th>Mã / Ngày</th><th>Học viên</th><th>Gia sư / Lớp</th><th>Tổng học phí</th><th>Phí sàn (20%)</th><th>Cần thanh toán lại cho gia sư</th><th>Phương thức</th><th>Trạng thái</th></tr></thead><tbody>{renderRows(8, items.length, 'Không tìm thấy giao dịch.')}{!loading && items.map((item) => <tr key={item.id}><td>GD{item.id}<span className="admin-table-secondary">{dateTime(item.paidAt)}</span></td><td>{item.studentName}</td><td>{item.tutorName}<span className="admin-table-secondary">{item.classroomName}</span></td><td>{money(item.amount)}</td><td><span className="text-muted">{money(item.platformFee ?? Math.round(item.amount * 0.2))}</span></td><td><strong style={{ color: '#16834c' }}>{money(item.tutorPayout ?? (item.amount - Math.round(item.amount * 0.2)))}</strong></td><td>{item.method}<span className="admin-table-secondary">{item.gatewayTransactionCode || ''}</span></td><td><span className={`admin-status ${item.status === 'Success' ? 'is-active' : item.status === 'Failed' ? 'is-locked' : 'is-pending'}`}>{item.status === 'Success' ? 'Thành công' : item.status === 'Failed' ? 'Thất bại' : 'Đang chờ'}</span></td></tr>)}</tbody></table></div>{pagination}</section>
      </>}

      {mode === 'reconciliation' && <>
        <div className="admin-reconciliation-toolbar"><label>Kỳ đối soát<input type="month" value={period} onChange={(event) => setPeriod(event.target.value)} /></label><button type="button" className="admin-action" onClick={() => downloadCsv(`doi-soat-${period}.csv`, [['Gia sư', 'Số giao dịch', 'Tổng học phí', 'Phí nền tảng', 'Thực nhận'], ...(data?.items || []).map((x) => [x.tutorName, x.transactionCount, x.totalRevenue, x.platformFee, x.tutorNet])])}><i className="bi bi-download" /> Xuất đối soát</button></div>
        <AdminStats items={[{ label: 'Tổng học phí thành công', value: money(data?.totals?.totalRevenue) }, { label: `Phí nền tảng (${Number(data?.platformFeeRate || 0) * 100}%)`, value: money(data?.totals?.platformFee) }, { label: 'Gia sư thực nhận', value: money(data?.totals?.tutorNet) }]} />
        <section className="admin-panel"><div className="admin-panel-heading">Đối soát theo gia sư <span className="admin-count">{data?.items?.length || 0}</span></div><div className="admin-table-scroll"><table className="admin-table"><thead><tr><th>Gia sư</th><th>Số giao dịch</th><th>Tổng học phí</th><th>Phí nền tảng</th><th>Thực nhận</th></tr></thead><tbody>{loading ? renderRows(5, false, '') : !data?.items?.length ? <tr><td className="admin-table-state" colSpan="5">Kỳ này chưa có giao dịch thành công.</td></tr> : data.items.map((item) => <tr key={item.tutorId}><td>{item.tutorName}</td><td>{item.transactionCount}</td><td>{money(item.totalRevenue)}</td><td>{money(item.platformFee)}</td><td><strong>{money(item.tutorNet)}</strong></td></tr>)}</tbody></table></div></section>
        <aside className="admin-note"><i className="bi bi-info-circle" /><div><strong>Phạm vi dữ liệu</strong>Số liệu tính từ giao dịch Success trong tháng được chọn; phí nền tảng theo tỷ lệ 20%. Đây là báo cáo đối chiếu, không tự chuyển tiền.</div></aside>
      </>}

      {mode === 'accounts' && <>
        <form className="admin-panel admin-operation-form" onSubmit={createAccount}><div className="admin-panel-heading">Tạo tài khoản mới</div><div className="admin-operation-form-body admin-account-form"><label>Họ tên<input required value={accountForm.fullName} onChange={(event) => setAccountForm({ ...accountForm, fullName: event.target.value })} placeholder="Nhập họ tên…" /></label><label>Email<input required type="email" value={accountForm.email} onChange={(event) => setAccountForm({ ...accountForm, email: event.target.value })} placeholder="Nhập địa chỉ email…" /></label><label>Vai trò<select value={accountForm.role} onChange={(event) => setAccountForm({ ...accountForm, role: event.target.value })}>{(data?.availableRoles || ['Student_Parent', 'Tutor', 'Admin']).map((role) => <option key={role} value={role}>{roleLabels[role] || role}</option>)}</select></label><button type="submit" className="admin-action is-approve" disabled={busyId === 'account'}>＋ Tạo tài khoản</button><small>Mật khẩu tạm thời được tạo tự động và chỉ hiển thị một lần.</small></div></form>
        {renderSearch('Tìm họ tên hoặc email…', <label className="admin-select"><select value={status} onChange={(event) => setStatusSafely(event.target.value)}><option value="all">Tất cả trạng thái</option><option value="Active">Đang hoạt động</option><option value="Locked">Đã khóa</option></select></label>)}
        <section className="admin-panel"><div className="admin-panel-heading">Tài khoản trên hệ thống <span className="admin-count">{pageData?.total ?? 0}</span></div><div className="admin-table-scroll"><table className="admin-table"><thead><tr><th>Họ tên</th><th>Email</th><th>Vai trò</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>{renderRows(5, items.length, 'Không tìm thấy tài khoản.')}{!loading && items.map((item) => <tr key={item.id}><td>{item.fullName}</td><td>{item.email}</td><td><select className="admin-inline-select" value={item.role} disabled={busyId === item.id} onChange={(event) => changeRole(item, event.target.value)}>{(data?.availableRoles || []).map((role) => <option key={role} value={role}>{roleLabels[role] || role}</option>)}</select></td><td><span className={`admin-status ${item.status === 'Active' ? 'is-active' : 'is-locked'}`}>{item.status === 'Active' ? 'Đang hoạt động' : 'Đã khóa'}</span></td><td><button type="button" className="admin-action" disabled={busyId === item.id || item.role === 'Admin'} onClick={() => toggleAccount(item)}>{item.status === 'Locked' ? 'Mở khóa' : 'Khóa'}</button>{item.role === 'Admin' && <span className="admin-table-secondary">Bảo vệ quyền quản trị</span>}</td></tr>)}</tbody></table></div>{pagination}</section>
      </>}

      {mode === 'audit-logs' && <>{renderSearch('Tìm thao tác, quản trị viên hoặc email…')}<section className="admin-panel"><div className="admin-panel-heading">Nhật ký thao tác <span className="admin-count">{pageData?.total ?? 0}</span></div><div className="admin-table-scroll"><table className="admin-table"><thead><tr><th>Thời gian</th><th>Người thực hiện</th><th>Thao tác</th><th>Địa chỉ IP</th></tr></thead><tbody>{renderRows(4, items.length, 'Chưa có nhật ký thao tác.')}{!loading && items.map((item) => <tr key={item.id}><td>{dateTime(item.timestamp)}</td><td>{item.adminName}<span className="admin-table-secondary">{item.adminEmail}</span></td><td>{item.action}</td><td>{item.ipAddress || '—'}</td></tr>)}</tbody></table></div>{pagination}</section></>}

      {mode === 'alerts' && <>
        <AdminStats items={['New', 'Reviewed', 'Dismissed'].map((key) => ({ label: alertLabels[key], value: data?.stats?.find((item) => item.status === key)?.count || 0 }))} />
        {renderSearch('Tìm tài khoản hoặc nội dung cảnh báo…', <label className="admin-select"><select value={status} onChange={(event) => setStatusSafely(event.target.value)}><option value="all">Tất cả mức độ</option>{Object.entries(alertLabels).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>)}
        <section className="admin-panel"><div className="admin-panel-heading">Cảnh báo cần rà soát <span className="admin-count">{pageData?.total ?? 0}</span></div><div className="admin-table-scroll"><table className="admin-table"><thead><tr><th>Mức độ</th><th>Tài khoản</th><th>Nội dung cảnh báo</th><th>Đánh giá</th><th>Ngày</th><th>Thao tác</th></tr></thead><tbody>{renderRows(6, items.length, 'Không có cảnh báo phù hợp.')}{!loading && items.map((item) => <tr key={item.id}><td><span className={`admin-status ${item.status === 'New' ? 'is-pending' : item.status === 'Dismissed' ? 'is-locked' : 'is-active'}`}>{item.status === 'New' ? 'Cần xem' : alertLabels[item.status]}</span></td><td>{item.studentName}<span className="admin-table-secondary">Gia sư: {item.tutorName}</span></td><td>{item.reason}</td><td>{item.reviewComment || '★'} <span className="admin-table-secondary">{item.reviewScore}/5</span></td><td>{date(item.createdAt)}</td><td>{item.status === 'New' ? <div className="admin-actions-inline"><button type="button" className="admin-action is-approve" disabled={busyId === item.id} onClick={() => updateAlert(item, 'Reviewed')}>Đã xem</button><button type="button" className="admin-action" disabled={busyId === item.id} onClick={() => updateAlert(item, 'Dismissed')}>Bỏ qua</button></div> : '—'}</td></tr>)}</tbody></table></div>{pagination}</section>
        <aside className="admin-note is-warning"><i className="bi bi-exclamation-triangle" /><div><strong>AI chỉ đưa ra tín hiệu</strong>Quản trị viên cần xem xét nội dung và ngữ cảnh trước khi kết luận; cảnh báo không tự động xóa đánh giá hay khóa tài khoản.</div></aside>
      </>}

      {mode === 'ai' && <>
        <AdminStats items={[{ label: 'Lượt hỏi AI', value: data?.totalSessions }, { label: 'Lượt hỏi hôm nay', value: data?.sessionsToday }, { label: 'Học viên đã sử dụng', value: data?.activeStudents }]} />
        <section className="admin-panel"><div className="admin-panel-heading">Hoạt động 14 ngày gần đây</div><div className="admin-ai-chart">{(data?.recentActivity || []).map((item) => <div className="admin-ai-bar" key={item.date}><span title={`${item.count} lượt` } style={{ height: `${Math.max(6, Math.min(100, item.count * 8))}%` }} /><small>{date(item.date)}</small><b>{item.count}</b></div>)}{!loading && !data?.recentActivity?.length && <p>Chưa có lượt sử dụng trong 14 ngày gần đây.</p>}</div></section>
        <aside className="admin-note"><i className="bi bi-shield-lock" /><div><strong>Bảo vệ dữ liệu học viên</strong>Trang chỉ tổng hợp số lượt và không hiển thị nội dung câu hỏi hoặc câu trả lời.</div></aside>
      </>}
    </div>
  );
}
