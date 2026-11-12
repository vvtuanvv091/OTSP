import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import adminOperationsApi from '../../api/adminOperationsApi';
import { resolveAssetUrl } from '../../api/config';
import { useToast } from '../../context/ToastContext';
import { useConfirm } from '../../context/ConfirmDialogContext';
import { AdminFeedback, AdminLoadingRows, AdminPageHeader, AdminPagination, AdminStats } from './AdminPageShared';

const PAGE_SIZE = 10;
const AUTO_REFRESH_MODES = new Set(['accounts', 'audit-logs']);
const AUTO_REFRESH_INTERVAL_MS = 15000;
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

function requestOperationsData(mode, params, period, config) {
  if (mode === 'reconciliation') return adminOperationsApi.getReconciliation(period);
  if (mode === 'ai') return adminOperationsApi.getAiUsage();
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
  return fetchers[mode]?.(params, config);
}

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
  const toast = useToast();
  const confirm = useConfirm();
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
  const [refreshing, setRefreshing] = useState(false);
  const [refreshTick, setRefreshTick] = useState(0);
  const silentRefreshRef = useRef(false);
  const [busyId, setBusyId] = useState(null);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [newsForm, setNewsForm] = useState({ id: null, title: '', category: '', content: '' });
  const [accountForm, setAccountForm] = useState({ fullName: '', email: '', role: 'Student_Parent' });
  const [selectedPayout, setSelectedPayout] = useState(null);
  const [selectedPayoutReceipt, setSelectedPayoutReceipt] = useState(null);
  const [selectedLogDetail, setSelectedLogDetail] = useState(null);
  const [selectedAlertDetail, setSelectedAlertDetail] = useState(null);
  const [payoutLoading, setPayoutLoading] = useState(false);
  const [payoutNote, setPayoutNote] = useState('');

  if (prevMode !== mode) {
    setPrevMode(mode);
    setSearch('');
    setQuery('');
    setStatus('all');
    setPage(1);
    setData(null);
    setRefreshing(false);
    setError('');
    setMessage('');
    setSelectedPayout(null);
    setSelectedPayoutReceipt(null);
    setSelectedLogDetail(null);
    setSelectedAlertDetail(null);
  }

  useEffect(() => {
    const timer = window.setTimeout(() => { setQuery(search.trim()); setPage(1); }, 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let active = true;
    const isSilent = silentRefreshRef.current;
    silentRefreshRef.current = false;

    async function loadOperations() {
      const params = { search: query, page, pageSize: PAGE_SIZE };
      if (status !== 'all') params.status = status;
      if (!isSilent) {
        setLoading(true);
        setError('');
      }
      const config = refreshTick > 0 ? { skipDedup: true } : undefined;
      const request = requestOperationsData(mode, params, period, config);
      if (!request) {
        if (active) {
          setError('Không tìm thấy trang quản trị.');
          setLoading(false);
          setRefreshing(false);
        }
        return;
      }
      try {
        const response = await request;
        if (active) {
          setData(response.data);
          setError('');
        }
      } catch (requestError) {
        if (active && !isSilent) setError(errorText(requestError, 'Không tải được dữ liệu quản trị.'));
      } finally {
        if (active) {
          setLoading(false);
          setRefreshing(false);
        }
      }
    }
    loadOperations();
    return () => { active = false; };
  }, [mode, query, status, page, period, refreshTick]);

  // Tự động cập nhật danh sách tài khoản và nhật ký thao tác khi quay lại tab hoặc định kỳ mỗi 15 giây
  useEffect(() => {
    if (!AUTO_REFRESH_MODES.has(mode)) return undefined;
    const triggerSilentRefresh = () => {
      if (document.visibilityState === 'hidden') return;
      silentRefreshRef.current = true;
      setRefreshTick((tick) => tick + 1);
    };
    window.addEventListener('focus', triggerSilentRefresh);
    document.addEventListener('visibilitychange', triggerSilentRefresh);
    const intervalId = window.setInterval(triggerSilentRefresh, AUTO_REFRESH_INTERVAL_MS);
    return () => {
      window.removeEventListener('focus', triggerSilentRefresh);
      document.removeEventListener('visibilitychange', triggerSilentRefresh);
      window.clearInterval(intervalId);
    };
  }, [mode]);

  const handleManualRefresh = useCallback(() => {
    silentRefreshRef.current = false;
    setRefreshing(true);
    setRefreshTick((tick) => tick + 1);
  }, []);

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
    const ok = await confirm({
      title: 'Xóa bài viết',
      message: `Bạn có chắc chắn muốn xóa bài viết “${article.title}”?`,
      confirmText: 'Xóa',
      variant: 'danger',
    });
    if (!ok) return;
    setBusyId(article.id); clearFeedback();
    try { await adminOperationsApi.deleteNews(article.id); toast.success('Đã xóa bài viết.'); const response = await adminOperationsApi.getNews({ search: query, page, pageSize: PAGE_SIZE }); setData(response.data); }
    catch (requestError) { toast.error(errorText(requestError, 'Không thể xóa bài viết.')); }
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

  const reloadAccounts = useCallback(async () => {
    const result = await adminOperationsApi.getAccounts(
      { search: query, page, pageSize: PAGE_SIZE, ...(status !== 'all' ? { status } : {}) },
      { skipDedup: true }
    );
    setData(result.data);
  }, [query, page, status]);

  const createAccount = async (event) => {
    event.preventDefault(); clearFeedback(); setBusyId('account');
    try {
      const response = await adminOperationsApi.createAccount(accountForm);
      toast.info(`Tài khoản đã được tạo. Mật khẩu tạm thời: ${response.data.temporaryPassword}`, 'Tạo tài khoản');
      setAccountForm({ fullName: '', email: '', role: 'Student_Parent' }); setMessage('Đã tạo tài khoản.');
      await reloadAccounts();
    } catch (requestError) { setError(errorText(requestError, 'Không thể tạo tài khoản.')); }
    finally { setBusyId(null); }
  };

  const changeRole = async (account, role) => {
    if (!role || role === account.role) return;
    const fromLabel = roleLabels[account.role] || account.role;
    const toLabel = roleLabels[role] || role;
    const ok = await confirm({
      title: 'Xác nhận chuyển vai trò tài khoản',
      message: `Chuyển tài khoản “${account.fullName}” (${account.email}) từ ${fromLabel} sang ${toLabel}? Chỉ tài khoản sạch (chưa có lớp học, yêu cầu kết nối, thanh toán, đánh giá, tài liệu hoặc khiếu nại) mới được chuyển; hồ sơ vai trò cũ sẽ bị xóa sạch và phiên đăng nhập cũ sẽ bị đăng xuất.`,
      confirmText: 'Chuyển vai trò',
      cancelText: 'Hủy',
      variant: role === 'Admin' ? 'danger' : 'warning',
    });
    if (!ok) return;

    setBusyId(account.id); clearFeedback();
    try {
      await adminOperationsApi.changeAccountRole(account.id, role);
      setMessage(`Đã chuyển vai trò thành ${toLabel} và dọn sạch hồ sơ vai trò cũ.`);
      toast.success(`Đã chuyển vai trò sang ${toLabel}.`);
      await reloadAccounts();
    } catch (requestError) {
      const msg = errorText(requestError, 'Không thể đổi vai trò.');
      setError(msg);
      toast.error(msg);
    } finally {
      setBusyId(null);
    }
  };

  const toggleAccount = async (account) => {
    setBusyId(account.id); clearFeedback();
    try {
      await adminOperationsApi.setAccountStatus(account.id, account.status !== 'Locked');
      setMessage('Đã cập nhật trạng thái tài khoản.');
      await reloadAccounts();
    }
    catch (requestError) { setError(errorText(requestError, 'Không thể cập nhật tài khoản.')); }
    finally { setBusyId(null); }
  };

  const updateAlert = async (alert, nextStatus) => {
    setBusyId(alert.id); clearFeedback();
    try {
      await adminOperationsApi.updateAlert(alert.id, nextStatus);
      setMessage('Đã cập nhật trạng thái cảnh báo.');
      const result = await adminOperationsApi.getAlerts({ search: query, page, pageSize: PAGE_SIZE, ...(status !== 'all' ? { status } : {}) });
      setData(result.data);
      if (selectedAlertDetail?.id === alert.id) {
        setSelectedAlertDetail(null);
      }
    }
    catch (requestError) { setError(errorText(requestError, 'Không thể cập nhật cảnh báo.')); }
    finally { setBusyId(null); }
  };

  const handleToggleReviewVisibility = async (review, isHidden) => {
    setBusyId(review.id);
    clearFeedback();
    try {
      await adminOperationsApi.toggleReviewVisibility(review.id, isHidden);
      setMessage(isHidden ? 'Đã ẩn đánh giá khỏi giao diện người dùng.' : 'Đã mở lại hiển thị đánh giá.');
      const response = await adminOperationsApi.getReviews({ search: query, page, pageSize: PAGE_SIZE, ...(status !== 'all' ? { status } : {}) });
      setData(response.data);
    } catch (err) {
      setError(errorText(err, 'Không thể cập nhật trạng thái hiển thị đánh giá.'));
    } finally {
      setBusyId(null);
    }
  };

  const handleDeleteReview = async (review) => {
    const ok = await confirm({
      title: 'Xóa đánh giá',
      message: `Xóa vĩnh viễn nhận xét của "${review.studentName}" cho gia sư "${review.tutorName}"?`,
      confirmText: 'Xóa vĩnh viễn',
      variant: 'danger',
    });
    if (!ok) return;
    setBusyId(review.id);
    clearFeedback();
    try {
      await adminOperationsApi.deleteReview(review.id);
      toast.success('Đã xóa đánh giá vi phạm thành công.');
      const response = await adminOperationsApi.getReviews({ search: query, page, pageSize: PAGE_SIZE, ...(status !== 'all' ? { status } : {}) });
      setData(response.data);
    } catch (err) {
      toast.error(errorText(err, 'Không thể xóa đánh giá.'));
    } finally {
      setBusyId(null);
    }
  };

  const handleFlagReview = async (review) => {
    const reason = window.prompt(`Nhập lý do gắn cờ cảnh báo cho đánh giá này:`, 'Nghi vấn đánh giá ảo hoặc ngôn từ chưa chuẩn mực');
    if (!reason || !reason.trim()) return;
    setBusyId(review.id);
    clearFeedback();
    try {
      await adminOperationsApi.flagReview(review.id, { reason: reason.trim(), severity: 'Medium' });
      setMessage('Đã gắn cờ cảnh báo thành công. Quản trị viên có thể theo dõi tại mục Cảnh báo.');
      const response = await adminOperationsApi.getReviews({ search: query, page, pageSize: PAGE_SIZE, ...(status !== 'all' ? { status } : {}) });
      setData(response.data);
    } catch (err) {
      setError(errorText(err, 'Không thể gắn cờ đánh giá.'));
    } finally {
      setBusyId(null);
    }
  };

  const handleConfirmPayout = async () => {
    if (!selectedPayout) return;
    setPayoutLoading(true);
    clearFeedback();
    try {
      const res = await adminOperationsApi.settlePayout({
        tutorId: selectedPayout.tutorId,
        period,
        note: payoutNote.trim() || 'Admin đã thanh toán thu nhập qua mã VietQR'
      });
      // Hiển thị Biên lai quyết toán thành công trong Modal chứ không tự động tắt đột ngột
      setSelectedPayoutReceipt({
        tutorName: selectedPayout.tutorName,
        tutorNet: selectedPayout.tutorNet,
        bankName: selectedPayout.bankAccount?.bankName || 'Ngân hàng thụ hưởng',
        accountNumber: selectedPayout.bankAccount?.accountNumber || '—',
        accountHolder: selectedPayout.bankAccount?.accountHolder || selectedPayout.tutorName,
        settledAt: new Date(),
        note: payoutNote.trim() || 'Admin đã thanh toán thu nhập qua mã VietQR',
        transactionCode: `OTSP-PAY-${period.replace('-', '')}-${selectedPayout.tutorId}-${Date.now().toString().slice(-4)}`
      });
      setMessage(res.data?.message || `Đã ghi nhận thanh toán thành công và gửi thông báo cho gia sư ${selectedPayout.tutorName}!`);
      const result = await adminOperationsApi.getReconciliation(period);
      setData(result.data);
    } catch (requestError) {
      setError(errorText(requestError, 'Không thể xử lý quyết toán cho gia sư.'));
    } finally {
      setPayoutLoading(false);
    }
  };

  const pageData = useMemo(() => {
    const keys = { reviews: 'reviews', complaints: 'complaints', news: 'articles', payments: 'payments', accounts: 'accounts', 'audit-logs': 'logs', alerts: 'alerts' };
    return data?.[keys[mode]] || null;
  }, [data, mode]);
  const items = pageData?.items || [];
  const statsArray = useMemo(() => (Array.isArray(data?.stats) ? data.stats : []), [data]);
  const findStatCount = (statusKey) => statsArray.find((item) => item.status === statusKey)?.count || 0;
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
        <AdminStats items={[
          { label: 'Tổng đánh giá', value: data?.stats?.total },
          { label: 'Điểm trung bình', value: Number(data?.stats?.average || 0).toFixed(1) },
          { label: 'Điểm thấp (≤ 2★)', value: data?.stats?.lowScore },
          { label: 'Cảnh báo rà soát', value: data?.stats?.reported }
        ]} columns={4} />
        {renderSearch('Tìm nội dung, học viên hoặc gia sư…', (
          <label className="admin-select">
            <select value={status} onChange={(event) => setStatusSafely(event.target.value)}>
              <option value="all">Tất cả đánh giá</option>
              <option value="visible">Đang hiển thị</option>
              <option value="hidden">Đã bị ẩn</option>
              <option value="star1">1 sao ★</option>
              <option value="star2">2 sao ★</option>
              <option value="star3">3 sao ★</option>
              <option value="star4">4 sao ★</option>
              <option value="star5">5 sao ★</option>
            </select>
          </label>
        ))}
        <section className="admin-panel">
          <div className="admin-panel-heading">
            Danh sách đánh giá &amp; Quản trị nội dung <span className="admin-count">{pageData?.total ?? 0}</span>
          </div>
          <div className="admin-table-scroll">
            <table className="admin-table admin-wide-table">
              <thead>
                <tr>
                  <th>Học viên / Gia sư</th>
                  <th>Lớp học</th>
                  <th>Đánh giá</th>
                  <th>Nhận xét</th>
                  <th>Trạng thái</th>
                  <th>Cảnh báo</th>
                  <th>Thao tác quản trị</th>
                </tr>
              </thead>
              <tbody>
                {renderRows(7, items.length, 'Chưa có đánh giá phù hợp.')}
                {!loading && items.map((item) => (
                  <tr key={item.id} style={{ opacity: item.isHidden ? 0.75 : 1 }}>
                    <td>
                      <strong>{item.studentName}</strong>
                      <span className="admin-table-secondary">Gia sư: {item.tutorName}</span>
                    </td>
                    <td>
                      {item.classroomName}
                      <span className="admin-table-secondary">{item.subjectName}</span>
                    </td>
                    <td>
                      <span className="badge" style={{ background: item.score <= 2 ? '#b45309' : '#15803d', color: '#fff', fontSize: 11.5 }}>
                        ★ {item.score}/5
                      </span>
                    </td>
                    <td className="admin-message-cell" title={item.comment || ''}>
                      {item.comment || 'Không có nhận xét'}
                    </td>
                    <td>
                      {item.isHidden ? (
                        <span className="admin-status is-locked">
                          <i className="bi bi-eye-slash-fill me-1"></i>Đã ẩn
                        </span>
                      ) : (
                        <span className="admin-status is-active">
                          <i className="bi bi-eye-fill me-1"></i>Hiển thị
                        </span>
                      )}
                    </td>
                    <td>
                      {item.alertCount ? (
                        <span className="admin-status is-pending">{item.alertCount} cảnh báo</span>
                      ) : (
                        <span className="text-secondary small">Bình thường</span>
                      )}
                    </td>
                    <td>
                      <div className="admin-actions-inline">
                        <button
                          type="button"
                          className={`admin-action ${item.isHidden ? 'is-approve' : ''}`}
                          disabled={busyId === item.id}
                          title={item.isHidden ? 'Hiện lại đánh giá này lên website' : 'Ẩn đánh giá khỏi giao diện website'}
                          onClick={() => handleToggleReviewVisibility(item, !item.isHidden)}
                        >
                          <i className={`bi ${item.isHidden ? 'bi-eye' : 'bi-eye-slash'} me-1`}></i>
                          {item.isHidden ? 'Hiện lại' : 'Ẩn'}
                        </button>
                        <button
                          type="button"
                          className="admin-action"
                          disabled={busyId === item.id}
                          title="Gắn cờ nghi vấn vi phạm để theo dõi rà soát"
                          onClick={() => handleFlagReview(item)}
                        >
                          <i className="bi bi-flag me-1 text-warning"></i>
                          Gắn cờ
                        </button>
                        <button
                          type="button"
                          className="admin-action is-danger"
                          disabled={busyId === item.id}
                          title="Xóa vĩnh viễn đánh giá vi phạm này"
                          onClick={() => handleDeleteReview(item)}
                        >
                          <i className="bi bi-trash me-1"></i>
                          Xóa
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

      {mode === 'messages' && <>
        <aside className="admin-note is-warning"><i className="bi bi-shield-lock" aria-hidden="true" /><div><strong>Quyền riêng tư hội thoại</strong>Admin chỉ thấy các bên liên quan, số lượng tin nhắn và báo cáo. Nội dung chat riêng không được tải.</div></aside>
        {renderSearch('Tìm mã hội thoại hoặc người tham gia…')}
        <section className="admin-panel"><div className="admin-panel-heading">Hội thoại có báo cáo <span className="admin-count">{data?.stats?.conversations ?? 0}</span></div><div className="admin-table-scroll"><table className="admin-table"><thead><tr><th>Người tham gia</th><th>Hoạt động cuối</th><th>Số tin nhắn</th><th>Báo cáo</th><th>Thao tác</th></tr></thead><tbody>{renderRows(5, data?.conversations?.length > 0, 'Không có hội thoại được báo cáo.')}{!loading && (data?.conversations || []).map((item) => <tr key={`${item.userOneId}-${item.userTwoId}`}><td><strong>{item.userOneName} ↔ {item.userTwoName}</strong><span className="admin-table-secondary">Mã: {(item.userOneId || '').slice(0, 8)} / {(item.userTwoId || '').slice(0, 8)}</span></td><td>{dateTime(item.lastActivity)}</td><td>{item.messageCount}</td><td><span className="admin-status is-pending">{item.reportCount} báo cáo</span></td><td><Link className="admin-action" to="/admin/complaints">Xem khiếu nại</Link></td></tr>)}</tbody></table></div></section>
      </>}

      {mode === 'complaints' && <>
        <AdminStats items={['New', 'Processing', 'Resolved'].map((key) => ({ label: complaintLabels[key], value: findStatCount(key) }))} />
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
          { label: 'Giao dịch', value: statsArray.reduce((sum, row) => sum + (Number(row.count) || 0), 0) },
          { label: 'Đã thanh toán', value: findStatCount('Success') },
          { label: 'Tổng học phí', value: money(data?.summary?.totalAmount ?? statsArray.find((x) => x.status === 'Success')?.amount) },
          { label: 'Phí sàn (20%)', value: money(data?.summary?.totalPlatformFee ?? (statsArray.find((x) => x.status === 'Success')?.amount ? Math.round(statsArray.find((x) => x.status === 'Success').amount * 0.2) : 0)) },
          { label: 'Cần thanh toán lại cho gia sư', value: money(data?.summary?.totalTutorPayout ?? (statsArray.find((x) => x.status === 'Success')?.amount ? Math.round(statsArray.find((x) => x.status === 'Success').amount * 0.8) : 0)) }
        ]} />
        {renderSearch('Tìm học viên, gia sư, lớp hoặc mã giao dịch…', <label className="admin-select"><select value={status} onChange={(event) => setStatusSafely(event.target.value)}><option value="all">Tất cả trạng thái</option><option value="Pending">Đang chờ</option><option value="Success">Thành công</option><option value="Failed">Thất bại</option></select></label>)}
        <section className="admin-panel"><div className="admin-panel-heading">Giao dịch học phí <span className="admin-count">{pageData?.total ?? 0}</span></div><div className="admin-table-scroll"><table className="admin-table"><thead><tr><th>Mã / Ngày</th><th>Học viên</th><th>Gia sư / Lớp</th><th>Tổng học phí</th><th>Phí sàn (20%)</th><th>Cần thanh toán lại cho gia sư</th><th>Phương thức</th><th>Trạng thái</th></tr></thead><tbody>{renderRows(8, items.length, 'Không tìm thấy giao dịch.')}{!loading && items.map((item) => <tr key={item.id}><td>GD{item.id}<span className="admin-table-secondary">{dateTime(item.paidAt)}</span></td><td>{item.studentName}</td><td>{item.tutorName}<span className="admin-table-secondary">{item.classroomName}</span></td><td>{money(item.amount)}</td><td><span className="text-muted">{money(item.platformFee ?? Math.round(item.amount * 0.2))}</span></td><td><strong style={{ color: '#16834c' }}>{money(item.tutorPayout ?? (item.amount - Math.round(item.amount * 0.2)))}</strong></td><td>{item.method}<span className="admin-table-secondary">{item.gatewayTransactionCode || ''}</span></td><td><span className={`admin-status ${item.status === 'Success' ? 'is-active' : item.status === 'Failed' ? 'is-locked' : 'is-pending'}`}>{item.status === 'Success' ? 'Thành công' : item.status === 'Failed' ? 'Thất bại' : 'Đang chờ'}</span></td></tr>)}</tbody></table></div>{pagination}</section>
      </>}

      {mode === 'reconciliation' && <>
        <div className="admin-reconciliation-toolbar">
          <label>Kỳ đối soát<input type="month" value={period} onChange={(event) => setPeriod(event.target.value)} /></label>
          <button type="button" className="admin-action" onClick={() => downloadCsv(`doi-soat-${period}.csv`, [['Gia sư', 'Số giao dịch', 'Tổng học phí', 'Phí nền tảng', 'Thực nhận', 'Trạng thái'], ...(data?.items || []).map((x) => [x.tutorName, x.transactionCount, x.totalRevenue, x.platformFee, x.tutorNet, x.isSettled ? 'Đã quyết toán' : 'Chờ giải ngân'])])}>
            <i className="bi bi-download" /> Xuất đối soát
          </button>
        </div>
        <AdminStats items={[
          { label: 'Tổng học phí thành công', value: money(data?.totals?.totalRevenue) },
          { label: `Phí nền tảng (${Number(data?.platformFeeRate || 0) * 100}%)`, value: money(data?.totals?.platformFee) },
          { label: 'Gia sư thực nhận (80%)', value: money(data?.totals?.tutorNet) }
        ]} />
        <section className="admin-panel">
          <div className="admin-panel-heading">
            Đối soát &amp; Chi trả thu nhập gia sư <span className="admin-count">{data?.items?.length || 0}</span>
          </div>
          <div className="admin-table-scroll">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Gia sư</th>
                  <th>Số giao dịch</th>
                  <th>Tổng học phí</th>
                  <th>Phí sàn (20%)</th>
                  <th>Thực nhận</th>
                  <th>Trạng thái</th>
                  <th>Thao tác chi trả</th>
                </tr>
              </thead>
              <tbody>
                {loading ? renderRows(7, false, '') : !data?.items?.length ? (
                  <tr><td className="admin-table-state" colSpan="7">Kỳ này chưa có giao dịch thành công.</td></tr>
                ) : data.items.map((item) => (
                  <tr key={item.tutorId}>
                    <td>
                      <strong>{item.tutorName}</strong>
                      {item.bankAccount?.bankName && (
                        <span className="admin-table-secondary">
                          {item.bankAccount.bankName} - {item.bankAccount.accountNumber} ({item.bankAccount.accountHolder})
                        </span>
                      )}
                    </td>
                    <td>{item.transactionCount}</td>
                    <td>{money(item.totalRevenue)}</td>
                    <td>{money(item.platformFee)}</td>
                    <td><strong style={{ color: '#16834c' }}>{money(item.tutorNet)}</strong></td>
                    <td>
                      <span className={`admin-status ${item.isSettled ? 'is-active' : 'is-pending'}`}>
                        <i className={`bi ${item.isSettled ? 'bi-check-circle-fill' : 'bi-hourglass-split'} me-1`} />
                        {item.isSettled ? 'Đã quyết toán' : 'Chờ giải ngân'}
                      </span>
                    </td>
                    <td>
                      {item.isSettled ? (
                        <button
                          type="button"
                          className="admin-action"
                          onClick={() => { setSelectedPayout(item); setPayoutNote(''); }}
                        >
                          <i className="bi bi-eye me-1" />Xem QR &amp; Chi tiết
                        </button>
                      ) : (
                        <button
                          type="button"
                          className="admin-action is-approve"
                          style={{ background: '#db303a', borderColor: '#db303a', color: '#fff', fontWeight: 600 }}
                          onClick={() => { setSelectedPayout(item); setPayoutNote(''); }}
                        >
                          <i className="bi bi-qr-code-scan me-1" />Chi trả VietQR
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
        <aside className="admin-note">
          <i className="bi bi-info-circle" />
          <div>
            <strong>Quy trình chuyển tiền &amp; quyết toán:</strong>
            Admin bấm <em>"Chi trả VietQR"</em> để quét mã QR ngân hàng của gia sư (đã tích hợp chính xác số tiền thực nhận). Sau khi chuyển khoản qua Internet Banking, bấm <em>"Xác nhận đã chuyển tiền"</em> để hệ thống tự động ghi nhận quyết toán thành công và gửi thông báo trực tiếp đến tài khoản gia sư.
          </div>
        </aside>
      </>}

      {mode === 'accounts' && <>
        <form className="admin-panel admin-operation-form" onSubmit={createAccount}><div className="admin-panel-heading">Tạo tài khoản mới</div><div className="admin-operation-form-body admin-account-form"><label>Họ tên<input required value={accountForm.fullName} onChange={(event) => setAccountForm({ ...accountForm, fullName: event.target.value })} placeholder="Nhập họ tên…" /></label><label>Email<input required type="email" value={accountForm.email} onChange={(event) => setAccountForm({ ...accountForm, email: event.target.value })} placeholder="Nhập địa chỉ email…" /></label><label>Vai trò<select value={accountForm.role} onChange={(event) => setAccountForm({ ...accountForm, role: event.target.value })}>{(data?.availableRoles || ['Student_Parent', 'Tutor', 'Admin']).map((role) => <option key={role} value={role}>{roleLabels[role] || role}</option>)}</select></label><button type="submit" className="admin-action is-approve" disabled={busyId === 'account'}>＋ Tạo tài khoản</button><small>Mật khẩu tạm thời được tạo tự động và chỉ hiển thị một lần.</small></div></form>
        {renderSearch('Tìm họ tên hoặc email…', <label className="admin-select"><select value={status} onChange={(event) => setStatusSafely(event.target.value)}><option value="all">Tất cả trạng thái</option><option value="Active">Đang hoạt động</option><option value="Locked">Đã khóa</option></select></label>)}
        <section className="admin-panel"><div className="admin-panel-heading"><span>Tài khoản trên hệ thống <span className="admin-count">{pageData?.total ?? 0}</span></span><button type="button" className="admin-action ms-auto" onClick={handleManualRefresh} disabled={loading || refreshing}><i className="bi bi-arrow-clockwise" aria-hidden="true" />{refreshing ? 'Đang làm mới…' : 'Làm mới'}</button></div><div className="admin-table-scroll"><table className="admin-table"><thead><tr><th>Họ tên</th><th>Email</th><th>Vai trò</th><th>Trạng thái</th><th>Ngày tạo</th><th>Thao tác</th></tr></thead><tbody>{renderRows(6, items.length, 'Không tìm thấy tài khoản.')}{!loading && items.map((item) => <tr key={item.id}><td>{item.fullName}</td><td>{item.email}</td><td><select className="admin-inline-select" value={item.role} disabled={busyId === item.id} onChange={(event) => changeRole(item, event.target.value)}>{(data?.availableRoles || []).map((role) => <option key={role} value={role}>{roleLabels[role] || role}</option>)}</select></td><td><span className={`admin-status ${item.status === 'Active' ? 'is-active' : 'is-locked'}`}>{item.status === 'Active' ? 'Đang hoạt động' : 'Đã khóa'}</span></td><td>{dateTime(item.createdAt)}</td><td><button type="button" className="admin-action" disabled={busyId === item.id || item.role === 'Admin'} onClick={() => toggleAccount(item)}>{item.status === 'Locked' ? 'Mở khóa' : 'Khóa'}</button>{item.role === 'Admin' && <span className="admin-table-secondary">Bảo vệ quyền quản trị</span>}</td></tr>)}</tbody></table></div>{pagination}</section>
        <aside className="admin-note">
          <i className="bi bi-info-circle" />
          <div>
            <strong>Quy tắc chuyển vai trò:</strong> Hệ thống chỉ cho phép đổi vai trò đối với tài khoản sạch (chưa phát sinh lớp học, yêu cầu kết nối, thanh toán, đánh giá, tài liệu hoặc khiếu nại), đồng thời tự động xóa sạch hồ sơ vai trò cũ và vô hiệu hóa phiên đăng nhập trước đó. Tài khoản đã có lịch sử học/dạy vui lòng dùng mục <em>Tạo tài khoản mới</em>.
          </div>
        </aside>
      </>}

      {mode === 'audit-logs' && <>
        {renderSearch('Tìm thao tác, quản trị viên, email hoặc IP…', (
          <label className="admin-select">
            <select value={status} onChange={(event) => setStatusSafely(event.target.value)}>
              <option value="all">Tất cả danh mục thao tác</option>
              <option value="AiUsage">Sử dụng AI Wein</option>
              <option value="Auth">Đăng nhập &amp; Xác thực</option>
              <option value="Payment">Thanh toán &amp; Quyết toán</option>
              <option value="Classroom">Lớp học &amp; Buổi học</option>
              <option value="Profile">Hồ sơ &amp; Duyệt gia sư</option>
              <option value="System">Hệ thống &amp; Vận hành</option>
            </select>
          </label>
        ))}
        <section className="admin-panel">
          <div className="admin-panel-heading">
            <span>Nhật ký kiểm toán &amp; Hoạt động người dùng <span className="admin-count">{pageData?.total ?? 0}</span></span>
            <button type="button" className="admin-action ms-auto" onClick={handleManualRefresh} disabled={loading || refreshing}>
              <i className="bi bi-arrow-clockwise" aria-hidden="true" />
              {refreshing ? 'Đang làm mới…' : 'Làm mới'}
            </button>
          </div>
          <div className="admin-table-scroll">
            <table className="admin-table admin-wide-table">
              <thead>
                <tr>
                  <th>Thời gian</th>
                  <th>Người thực hiện</th>
                  <th>Vai trò</th>
                  <th>Phân loại</th>
                  <th>Thao tác</th>
                  <th>Địa chỉ IP</th>
                  <th>Chi tiết</th>
                </tr>
              </thead>
              <tbody>
                {renderRows(7, items.length, 'Chưa có nhật ký thao tác.')}
                {!loading && items.map((item) => (
                  <tr key={item.id}>
                    <td>{dateTime(item.timestamp)}</td>
                    <td>
                      <strong>{item.adminName}</strong>
                      <span className="admin-table-secondary">{item.adminEmail}</span>
                    </td>
                    <td>
                      <span className="badge rounded-pill" style={{
                        background: (item.userRole === 'Admin' || item.adminEmail?.includes('admin')) ? '#db303a' : item.userRole === 'Tutor' ? '#0d6efd' : item.userRole === 'Khách' ? '#64748b' : '#15803d',
                        color: '#fff',
                        fontSize: 11
                      }}>
                        {roleLabels[item.userRole] || item.userRole || (item.adminEmail?.includes('admin') ? 'Admin' : 'Thành viên')}
                      </span>
                    </td>
                    <td>
                      <span className="admin-subject-tag">
                        {item.category || 'Hệ thống'}
                      </span>
                    </td>
                    <td>
                      <strong>{item.action}</strong>
                      {item.detail && (
                        <span className="admin-table-secondary text-truncate" style={{ maxWidth: 240 }} title={item.detail}>
                          {item.detail}
                        </span>
                      )}
                    </td>
                    <td>
                      <code>{item.ipAddress || '127.0.0.1'}</code>
                    </td>
                    <td>
                      <button
                        type="button"
                        className="admin-action"
                        onClick={() => setSelectedLogDetail(item)}
                      >
                        <i className="bi bi-eye me-1"></i>Chi tiết
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {pagination}
        </section>
        <aside className="admin-note">
          <i className="bi bi-shield-check text-success" />
          <div>
            <strong>Nhật ký kiểm toán hệ thống (Audit Logs):</strong>
            Ghi nhận tự động mọi hoạt động quan trọng trên hệ thống (đăng nhập, duyệt hồ sơ, thanh toán học phí, thay đổi lớp học...). Quản trị viên có thể bấm <em>"Chi tiết"</em> tại từng dòng để xem thông số kỹ thuật, thiết bị người dùng và lịch sử thay đổi.
          </div>
        </aside>
      </>}

      {mode === 'alerts' && <>
        <AdminStats items={[
          { label: 'Cảnh báo mới', value: findStatCount('New') },
          { label: 'Đã xem xét', value: findStatCount('Reviewed') },
          { label: 'Đã bỏ qua', value: findStatCount('Dismissed') }
        ]} columns={3} />
        {renderSearch('Tìm tài khoản hoặc nội dung cảnh báo…', (
          <label className="admin-select">
            <select value={status} onChange={(event) => setStatusSafely(event.target.value)}>
              <option value="all">Tất cả trạng thái</option>
              {Object.entries(alertLabels).map(([key, label]) => <option key={key} value={key}>{label}</option>)}
            </select>
          </label>
        ))}
        <section className="admin-panel">
          <div className="admin-panel-heading">
            Cảnh báo bất thường cần rà soát <span className="admin-count">{pageData?.total ?? 0}</span>
          </div>
          <div className="admin-table-scroll">
            <table className="admin-table admin-wide-table">
              <thead>
                <tr>
                  <th>Mức độ</th>
                  <th>Phân loại</th>
                  <th>Tài khoản</th>
                  <th>Nội dung cảnh báo</th>
                  <th>Đánh giá gốc</th>
                  <th>Ngày phát hiện</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {renderRows(7, items.length, 'Không có cảnh báo phù hợp.')}
                {!loading && items.map((item) => (
                  <tr key={item.id}>
                    <td>
                      <span className="badge" style={{
                        background: item.severity === 'High' ? '#dc2626' : item.severity === 'Low' ? '#1d4ed8' : '#b45309',
                        color: '#fff',
                        fontSize: 11
                      }}>
                        {item.severity === 'High' ? '🔴 Khẩn cấp' : item.severity === 'Low' ? '🔵 Theo dõi' : '🟡 Cảnh báo'}
                      </span>
                    </td>
                    <td>
                      <span className="admin-subject-tag">{item.category || 'Đánh giá'}</span>
                    </td>
                    <td>
                      <strong>{item.studentName}</strong>
                      <span className="admin-table-secondary">Gia sư: {item.tutorName}</span>
                    </td>
                    <td>
                      <strong className="text-dark">{item.reason}</strong>
                    </td>
                    <td>
                      <span className="badge" style={{ background: item.reviewScore <= 2 ? '#b45309' : '#15803d', color: '#fff', fontSize: 10 }}>
                        ★ {item.reviewScore}/5
                      </span>
                      <span className="admin-table-secondary text-truncate" style={{ maxWidth: 180 }} title={item.reviewComment || ''}>
                        {item.reviewComment || 'Không có nhận xét'}
                      </span>
                    </td>
                    <td>{date(item.createdAt)}</td>
                    <td>
                      <div className="admin-actions-inline">
                        <button
                          type="button"
                          className="admin-action is-approve"
                          onClick={() => setSelectedAlertDetail(item)}
                        >
                          <i className="bi bi-shield-exclamation me-1"></i>Xem &amp; Xử lý
                        </button>
                        {item.status === 'New' && (
                          <>
                            <button
                              type="button"
                              className="admin-action"
                              disabled={busyId === item.id}
                              onClick={() => updateAlert(item, 'Reviewed')}
                            >
                              Đã xem
                            </button>
                            <button
                              type="button"
                              className="admin-action"
                              disabled={busyId === item.id}
                              onClick={() => updateAlert(item, 'Dismissed')}
                            >
                              Bỏ qua
                            </button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {pagination}
        </section>
        <aside className="admin-note is-warning">
          <i className="bi bi-exclamation-triangle-fill" />
          <div>
            <strong>Cơ chế phát hiện cảnh báo bất thường:</strong>
            Hệ thống và AI tự động phân tích ngôn từ đánh giá tiêu cực, nghi vấn đánh giá ảo, xung đột điểm số hoặc tần suất gửi đánh giá bất thường. Quản trị viên bấm <em>"Xem &amp; Xử lý"</em> để đọc ngữ cảnh đầy đủ, ẩn đánh giá độc hại hoặc duyệt bỏ qua.
          </div>
        </aside>
      </>}

      {mode === 'ai' && <>
        <AdminStats items={[{ label: 'Lượt hỏi AI', value: data?.totalSessions }, { label: 'Lượt hỏi hôm nay', value: data?.sessionsToday }, { label: 'Học viên đã sử dụng', value: data?.activeStudents }]} />
        <section className="admin-panel"><div className="admin-panel-heading">Hoạt động 14 ngày gần đây</div><div className="admin-ai-chart">{(data?.recentActivity || []).map((item) => <div className="admin-ai-bar" key={item.date}><span title={`${item.count} lượt` } style={{ height: `${Math.max(6, Math.min(100, item.count * 8))}%` }} /><small>{date(item.date)}</small><b>{item.count}</b></div>)}{!loading && !data?.recentActivity?.length && <p>Chưa có lượt sử dụng trong 14 ngày gần đây.</p>}</div></section>
        <aside className="admin-note"><i className="bi bi-shield-lock" /><div><strong>Bảo vệ dữ liệu học viên</strong>Trang chỉ tổng hợp số lượt và không hiển thị nội dung câu hỏi hoặc câu trả lời.</div></aside>
      </>}

      {selectedPayout && (
        <div className="modal show d-block" tabIndex="-1" style={{ backgroundColor: 'rgba(0,0,0,0.55)', zIndex: 1050 }}>
          <div className="modal-dialog modal-dialog-centered" style={{ maxWidth: 540 }}>
            <div className="modal-content border-0 shadow-lg rounded-4 overflow-hidden">
              {selectedPayoutReceipt ? (
                /* MÀN HÌNH BIÊN LAI QUYẾT TOÁN THÀNH CÔNG (Không bị tự tắt đột ngột) */
                <div>
                  <div className="p-4 text-center text-white" style={{ background: '#15803d' }}>
                    <div className="rounded-circle bg-white d-inline-flex align-items-center justify-content-center mb-2 shadow" style={{ width: 60, height: 60, color: '#15803d' }}>
                      <i className="bi bi-check-lg fs-1 fw-bold"></i>
                    </div>
                    <h5 className="fw-bold mb-1">CHI TRẢ THU NHẬP THÀNH CÔNG</h5>
                    <p className="mb-0 small opacity-75">Hệ thống đã lưu trạng thái Đã quyết toán và thông báo đến gia sư</p>
                  </div>

                  <div className="modal-body p-4">
                    <div className="p-3 rounded-3 mb-3" style={{ background: '#f8fafc', border: '1px solid #e2e8f0' }}>
                      <div className="d-flex justify-content-between mb-2">
                        <span className="text-secondary small">Mã biên lai giao dịch:</span>
                        <code className="text-dark fw-bold">{selectedPayoutReceipt.transactionCode}</code>
                      </div>
                      <div className="d-flex justify-content-between mb-2">
                        <span className="text-secondary small">Gia sư thụ hưởng:</span>
                        <strong className="text-dark">{selectedPayoutReceipt.tutorName}</strong>
                      </div>
                      <div className="d-flex justify-content-between mb-2">
                        <span className="text-secondary small">Số tiền đã chi trả (80%):</span>
                        <strong className="fs-5" style={{ color: '#15803d' }}>{money(selectedPayoutReceipt.tutorNet)}</strong>
                      </div>
                      <div className="d-flex justify-content-between mb-2">
                        <span className="text-secondary small">Ngân hàng:</span>
                        <span className="text-dark">{selectedPayoutReceipt.bankName}</span>
                      </div>
                      <div className="d-flex justify-content-between mb-2">
                        <span className="text-secondary small">Số tài khoản:</span>
                        <span className="text-primary font-monospace fw-bold">{selectedPayoutReceipt.accountNumber}</span>
                      </div>
                      <div className="d-flex justify-content-between mb-2">
                        <span className="text-secondary small">Chủ tài khoản:</span>
                        <span className="text-dark text-uppercase fw-bold">{selectedPayoutReceipt.accountHolder}</span>
                      </div>
                      <div className="d-flex justify-content-between mb-2">
                        <span className="text-secondary small">Thời gian quyết toán:</span>
                        <span className="text-dark">{dateTime(selectedPayoutReceipt.settledAt)}</span>
                      </div>
                      <div className="d-flex justify-content-between">
                        <span className="text-secondary small">Ghi chú giao dịch:</span>
                        <span className="text-dark small text-end">{selectedPayoutReceipt.note}</span>
                      </div>
                    </div>

                    <div className="alert alert-success py-2 px-3 small mb-0 rounded-3 d-flex align-items-center gap-2">
                      <i className="bi bi-bell-fill fs-5" style={{ color: '#15803d' }}></i>
                      <span>Gia sư <strong>{selectedPayoutReceipt.tutorName}</strong> đã nhận được thông báo biến động số dư và xác nhận giải ngân thành công trong tài khoản!</span>
                    </div>
                  </div>

                  <div className="modal-footer border-0 pt-0 pb-3 px-4">
                    <button
                      type="button"
                      className="btn btn-success fw-bold w-100 rounded-3 py-2"
                      style={{ background: '#15803d', borderColor: '#166534' }}
                      onClick={() => {
                        setSelectedPayout(null);
                        setSelectedPayoutReceipt(null);
                      }}
                    >
                      <i className="bi bi-check2-circle me-1"></i>
                      Hoàn tất &amp; Đóng biên lai
                    </button>
                  </div>
                </div>
              ) : (
                /* FORM CHI TRẢ & QUÉT MÃ VIETQR */
                <>
                  <div className="modal-header border-0 pb-0">
                    <div>
                      <h5 className="modal-title fw-bold text-dark">
                        <i className="bi bi-cash-stack text-danger me-2"></i>
                        {selectedPayout.isSettled ? 'Chi tiết quyết toán thu nhập' : 'Chi trả thu nhập qua VietQR'}
                      </h5>
                      <p className="text-muted small mb-0">Kỳ đối soát: <strong>{period}</strong></p>
                    </div>
                    <button type="button" className="btn-close" onClick={() => setSelectedPayout(null)} aria-label="Đóng"></button>
                  </div>

                  <div className="modal-body py-3">
                    <div className="p-3 rounded-3 mb-3" style={{ background: '#f8fafc', border: '1px solid #e2e8f0' }}>
                      <div className="d-flex justify-content-between align-items-center mb-2">
                        <span className="text-secondary small">Gia sư thụ hưởng:</span>
                        <strong className="text-dark">{selectedPayout.tutorName}</strong>
                      </div>
                      <div className="d-flex justify-content-between align-items-center mb-2">
                        <span className="text-secondary small">Số tiền thực nhận (80%):</span>
                        <span className="fs-5 fw-bold" style={{ color: '#15803d' }}>{money(selectedPayout.tutorNet)}</span>
                      </div>
                      <div className="d-flex justify-content-between align-items-center mb-2">
                        <span className="text-secondary small">Ngân hàng:</span>
                        <strong className="text-dark">{selectedPayout.bankAccount?.bankName || 'Chưa cung cấp'}</strong>
                      </div>
                      <div className="d-flex justify-content-between align-items-center mb-2">
                        <span className="text-secondary small">Số tài khoản:</span>
                        <strong className="text-primary font-monospace">{selectedPayout.bankAccount?.accountNumber || 'Chưa cung cấp'}</strong>
                      </div>
                      <div className="d-flex justify-content-between align-items-center">
                        <span className="text-secondary small">Chủ tài khoản:</span>
                        <strong className="text-dark text-uppercase">{selectedPayout.bankAccount?.accountHolder || selectedPayout.tutorName}</strong>
                      </div>
                    </div>

                    {/* QR Code container */}
                    <div className="text-center p-3 rounded-3 mb-3" style={{ background: '#fff', border: '1px dashed #cbd5e1' }}>
                      {selectedPayout.bankAccount?.qrImageUrl ? (
                        <div>
                          <div className="small fw-semibold text-danger mb-2">
                            <i className="bi bi-qr-code-scan me-1"></i>Mã VietQR đã tích hợp số tiền {money(selectedPayout.tutorNet)}
                          </div>
                          <img
                            src={selectedPayout.bankAccount.qrImageUrl}
                            alt="Mã VietQR"
                            className="img-fluid rounded-3 shadow-sm"
                            style={{ maxHeight: 260 }}
                          />
                          <div className="small text-muted mt-2">
                            Mở ứng dụng Ngân hàng trên điện thoại để quét mã chuyển tiền nhanh.
                          </div>
                        </div>
                      ) : (
                        <div className="py-4 text-warning">
                          <i className="bi bi-exclamation-triangle fs-2 d-block mb-1"></i>
                          Gia sư chưa thiết lập thông tin ngân hàng trong mục Thu nhập. Vui lòng liên hệ gia sư cập nhật trước khi chuyển khoản.
                        </div>
                      )}
                    </div>

                    {!selectedPayout.isSettled ? (
                      <div>
                        <label className="form-label small fw-semibold text-dark">Ghi chú giao dịch / Mã tham chiếu ngân hàng:</label>
                        <input
                          type="text"
                          className="form-control form-control-sm"
                          placeholder="Ví dụ: Đã chuyển khoản qua MBBank lúc 10:30..."
                          value={payoutNote}
                          onChange={(e) => setPayoutNote(e.target.value)}
                        />
                      </div>
                    ) : (
                      <div className="alert alert-success py-2 px-3 small mb-0 rounded-3">
                        <i className="bi bi-check-circle-fill me-1"></i>
                        Khoản thu nhập này đã được quyết toán và thông báo xác nhận thành công đã gửi đến gia sư.
                      </div>
                    )}
                  </div>

                  <div className="modal-footer border-0 pt-0">
                    <button type="button" className="btn btn-sm btn-secondary" onClick={() => setSelectedPayout(null)}>
                      Đóng
                    </button>
                    {!selectedPayout.isSettled && (
                      <button
                        type="button"
                        className="btn btn-sm btn-danger fw-semibold px-3"
                        disabled={payoutLoading}
                        onClick={handleConfirmPayout}
                      >
                        {payoutLoading ? (
                          <>
                            <span className="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
                            Đang xử lý...
                          </>
                        ) : (
                          <>
                            <i className="bi bi-check-lg me-1"></i>
                            Xác nhận đã chuyển tiền cho gia sư
                          </>
                        )}
                      </button>
                    )}
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}

      {/* MODAL CHI TIẾT NHẬT KÝ THAO TÁC (t5) */}
      {selectedLogDetail && (
        <div className="modal show d-block" tabIndex="-1" style={{ backgroundColor: 'rgba(0,0,0,0.55)', zIndex: 1060 }}>
          <div className="modal-dialog modal-dialog-centered" style={{ maxWidth: 560 }}>
            <div className="modal-content border-0 shadow-lg rounded-4">
              <div className="modal-header border-0 pb-0">
                <div>
                  <h5 className="modal-title fw-bold text-dark">
                    <i className="bi bi-journal-text text-danger me-2"></i>
                    Chi tiết Nhật ký Kiểm toán #{selectedLogDetail.id}
                  </h5>
                  <p className="text-muted small mb-0">Thời gian ghi nhận: {dateTime(selectedLogDetail.timestamp)}</p>
                </div>
                <button type="button" className="btn-close" onClick={() => setSelectedLogDetail(null)} aria-label="Đóng"></button>
              </div>
              <div className="modal-body py-3">
                <div className="p-3 rounded-3 mb-3" style={{ background: '#f8fafc', border: '1px solid #e2e8f0' }}>
                  <div className="d-flex justify-content-between mb-2">
                    <span className="text-secondary small">Người thực hiện:</span>
                    <strong className="text-dark">{selectedLogDetail.adminName} ({selectedLogDetail.adminEmail})</strong>
                  </div>
                  <div className="d-flex justify-content-between mb-2">
                    <span className="text-secondary small">Vai trò:</span>
                    <span className="badge rounded-pill bg-danger text-white">{selectedLogDetail.userRole || 'Admin'}</span>
                  </div>
                  <div className="d-flex justify-content-between mb-2">
                    <span className="text-secondary small">Phân loại danh mục:</span>
                    <span className="badge bg-secondary text-white">{selectedLogDetail.category || 'Hệ thống'}</span>
                  </div>
                  <div className="d-flex justify-content-between mb-2">
                    <span className="text-secondary small">Thao tác thực hiện:</span>
                    <strong className="text-primary">{selectedLogDetail.action}</strong>
                  </div>
                  <div className="d-flex justify-content-between mb-2">
                    <span className="text-secondary small">Địa chỉ IP:</span>
                    <code>{selectedLogDetail.ipAddress || '127.0.0.1'}</code>
                  </div>
                  <div className="d-flex justify-content-between">
                    <span className="text-secondary small">Thiết bị / User-Agent:</span>
                    <span className="small text-muted text-truncate" style={{ maxWidth: 280 }} title={selectedLogDetail.userAgent}>
                      {selectedLogDetail.userAgent || 'Trình duyệt Web'}
                    </span>
                  </div>
                </div>

                <div className="mb-0">
                  <label className="form-label small fw-semibold text-dark">Nội dung chi tiết (Payload / Data changes):</label>
                  <pre className="p-3 rounded-3 small bg-dark text-light font-monospace mb-0" style={{ maxHeight: 180, overflowY: 'auto', whiteSpace: 'pre-wrap' }}>
                    {selectedLogDetail.detail || JSON.stringify({ action: selectedLogDetail.action, timestamp: selectedLogDetail.timestamp, actor: selectedLogDetail.adminEmail }, null, 2)}
                  </pre>
                </div>
              </div>
              <div className="modal-footer border-0 pt-0">
                <button type="button" className="btn btn-sm btn-secondary" onClick={() => setSelectedLogDetail(null)}>
                  Đóng
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* MODAL CHI TIẾT & XỬ LÝ CẢNH BÁO BẤT THƯỜNG (t6) */}
      {selectedAlertDetail && (
        <div className="modal show d-block" tabIndex="-1" style={{ backgroundColor: 'rgba(0,0,0,0.55)', zIndex: 1060 }}>
          <div className="modal-dialog modal-dialog-centered" style={{ maxWidth: 580 }}>
            <div className="modal-content border-0 shadow-lg rounded-4">
              <div className="modal-header border-0 pb-0">
                <div>
                  <h5 className="modal-title fw-bold text-dark">
                    <i className="bi bi-shield-exclamation text-danger me-2"></i>
                    Chi tiết Cảnh báo Bất thường #{selectedAlertDetail.id}
                  </h5>
                  <p className="text-muted small mb-0">Phát hiện lúc: {dateTime(selectedAlertDetail.createdAt)}</p>
                </div>
                <button type="button" className="btn-close" onClick={() => setSelectedAlertDetail(null)} aria-label="Đóng"></button>
              </div>
              <div className="modal-body py-3">
                <div className="p-3 rounded-3 mb-3" style={{ background: '#f8fafc', border: '1px solid #e2e8f0' }}>
                  <div className="d-flex justify-content-between mb-2">
                    <span className="text-secondary small">Mức độ cảnh báo:</span>
                    <span className="badge" style={{
                      background: selectedAlertDetail.severity === 'High' ? '#dc2626' : selectedAlertDetail.severity === 'Low' ? '#1d4ed8' : '#b45309',
                      color: '#fff'
                    }}>
                      {selectedAlertDetail.severity === 'High' ? '🔴 Khẩn cấp' : selectedAlertDetail.severity === 'Low' ? '🔵 Theo dõi' : '🟡 Cảnh báo'}
                    </span>
                  </div>
                  <div className="d-flex justify-content-between mb-2">
                    <span className="text-secondary small">Học viên gửi:</span>
                    <strong className="text-dark">{selectedAlertDetail.studentName}</strong>
                  </div>
                  <div className="d-flex justify-content-between mb-2">
                    <span className="text-secondary small">Gia sư nhận đánh giá:</span>
                    <strong className="text-dark">{selectedAlertDetail.tutorName}</strong>
                  </div>
                  <div className="d-flex justify-content-between mb-2">
                    <span className="text-secondary small">Lý do phát hiện:</span>
                    <strong className="text-danger">{selectedAlertDetail.reason}</strong>
                  </div>
                  <div className="d-flex justify-content-between">
                    <span className="text-secondary small">Trạng thái rà soát:</span>
                    <span className={`admin-status ${selectedAlertDetail.status === 'New' ? 'is-pending' : 'is-active'}`}>
                      {alertLabels[selectedAlertDetail.status] || selectedAlertDetail.status}
                    </span>
                  </div>
                </div>

                <div className="p-3 rounded-3 mb-3" style={{ background: '#fff', border: '1px solid #cbd5e1' }}>
                  <div className="d-flex justify-content-between align-items-center mb-1">
                    <span className="fw-semibold text-dark small">Nội dung đánh giá gốc:</span>
                    <span className="badge" style={{ background: selectedAlertDetail.reviewScore <= 2 ? '#b45309' : '#15803d', color: '#fff' }}>
                      ★ {selectedAlertDetail.reviewScore}/5
                    </span>
                  </div>
                  <p className="mb-0 text-secondary small fst-italic">
                    "{selectedAlertDetail.reviewComment || 'Không có bình luận chữ'}"
                  </p>
                </div>

                <div className="alert alert-info py-2 px-3 small mb-0 rounded-3">
                  <i className="bi bi-info-circle me-1"></i>
                  <strong>Hành động khuyến nghị:</strong> Nếu nhận xét chứa từ ngữ xúc phạm hoặc đánh giá ác ý, hãy bấm <em>"Ẩn đánh giá khỏi Web"</em>. Nếu bình thường, bấm <em>"Đã xem / Duyệt"</em> hoặc <em>"Bỏ qua"</em>.
                </div>
              </div>

              <div className="modal-footer border-0 pt-0 d-flex justify-content-between">
                <button type="button" className="btn btn-sm btn-secondary" onClick={() => setSelectedAlertDetail(null)}>
                  Đóng
                </button>
                <div className="d-flex gap-2">
                  {selectedAlertDetail.reviewId && (
                    <button
                      type="button"
                      className="btn btn-sm btn-warning text-dark fw-semibold"
                      disabled={busyId === selectedAlertDetail.reviewId}
                      onClick={async () => {
                        await handleToggleReviewVisibility({ id: selectedAlertDetail.reviewId, isHidden: false }, true);
                        await updateAlert(selectedAlertDetail, 'Reviewed');
                      }}
                    >
                      <i className="bi bi-eye-slash me-1"></i>
                      Ẩn đánh giá khỏi Web
                    </button>
                  )}
                  <button
                    type="button"
                    className="btn btn-sm btn-success fw-semibold"
                    style={{ background: '#15803d', borderColor: '#166534' }}
                    disabled={busyId === selectedAlertDetail.id}
                    onClick={() => updateAlert(selectedAlertDetail, 'Reviewed')}
                  >
                    <i className="bi bi-check-lg me-1"></i>
                    Đã xem xét
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
