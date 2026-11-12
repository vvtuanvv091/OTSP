import { useEffect, useState } from 'react';
import adminApi from '../../api/adminApi';
import { resolveAssetUrl } from '../../api/config';
import { useConfirm } from '../../context/confirmHooks';
import { AdminFeedback, AdminLoadingRows, AdminPageHeader, AdminPagination, AdminStats } from './AdminPageShared';

const PAGE_SIZE = 10;

function AiVerificationResult({ result }) {
  if (!result) return null;
  const recommended = result.recommendation === 'APPROVED_RECOMMENDED';
  return (
    <div className="admin-ai-result-card">
      <div className="admin-ai-result-head">
        <strong><i className="bi bi-cpu me-1" aria-hidden="true" />Kết quả AI OCR &amp; Đối chiếu</strong>
        <span className={`admin-ai-badge ${recommended ? 'is-recommended' : 'is-warning'}`}>
          <i className={`bi ${recommended ? 'bi-check-circle-fill' : 'bi-exclamation-triangle-fill'}`} aria-hidden="true" />
          {recommended ? 'Đề xuất duyệt' : 'Cần kiểm tra kỹ'}
        </span>
      </div>
      <div>
        <strong>Tên trên bằng:</strong> {result.tutorName || 'N/A'}
        <span className={`admin-ai-inline-match ${result.isNameMatched ? 'is-ok' : 'is-warn'}`}>
          {result.isNameMatched ? '(Khớp hồ sơ)' : '(Chưa trùng khớp)'}
        </span>
      </div>
      <div>
        <strong>Trường / Tổ chức:</strong> {result.issuingOrganization || 'N/A'}
        {result.certificateType ? ` (${result.certificateType})` : ''}
      </div>
      <div>
        <strong>Xếp loại / Điểm:</strong> {result.scoreOrClassification || 'N/A'} ·{' '}
        <strong>Chuẩn OTSP:</strong> {result.qualificationStandardMet ? 'Đạt' : 'Chưa đạt'}
      </div>
      {result.summaryNotes && (
        <div className="admin-ai-result-notes">
          <i className="bi bi-chat-left-text me-1" aria-hidden="true" />
          {result.summaryNotes}
        </div>
      )}
    </div>
  );
}

export default function AdminApprovalsPage() {
  const confirm = useConfirm();
  const [data, setData] = useState(null);
  const [search, setSearch] = useState('');
  const [querySearch, setQuerySearch] = useState('');
  const [subjectId, setSubjectId] = useState('all');
  const [teachingFormat, setTeachingFormat] = useState('all');
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState('tutors'); // 'tutors' | 'qualifications'
  const [qualPage, setQualPage] = useState(1);
  const [qualData, setQualData] = useState(null);
  const [loadingQual, setLoadingQual] = useState(false);
  const [busyTutorId, setBusyTutorId] = useState(null);
  const [busyQualificationId, setBusyQualificationId] = useState(null);
  const [aiVerifyingId, setAiVerifyingId] = useState(null);
  const [aiResults, setAiResults] = useState({});
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  useEffect(() => {
    const timer = window.setTimeout(() => setQuerySearch(search.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let active = true;
    async function loadApprovals() {
      setLoading(true);
      setError('');
      const params = { search: querySearch, page, pageSize: PAGE_SIZE };
      if (subjectId !== 'all') params.subjectId = subjectId;
      if (teachingFormat !== 'all') params.teachingFormat = teachingFormat;

      try {
        const response = await adminApi.getTutorApprovalDashboard(params);
        if (active) setData(response.data);
      } catch (requestError) {
        if (active) setError(requestError.response?.data?.message || 'Không tải được danh sách hồ sơ chờ duyệt.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadApprovals();
    return () => { active = false; };
  }, [querySearch, subjectId, teachingFormat, page, reload]);

  useEffect(() => {
    if (activeTab !== 'qualifications') return;
    let active = true;
    async function loadPagedQualifications() {
      setLoadingQual(true);
      try {
        const res = await adminApi.getPendingQualifications({ page: qualPage, pageSize: PAGE_SIZE });
        if (active) {
          if (res.data?.items) {
            setQualData(res.data);
          } else {
            setQualData({ items: res.data || [], total: (res.data || []).length, totalPages: 1 });
          }
        }
      } catch (err) {
        if (active) setError(err.response?.data?.message || 'Không tải được danh sách chứng chỉ.');
      } finally {
        if (active) setLoadingQual(false);
      }
    }
    loadPagedQualifications();
    return () => { active = false; };
  }, [activeTab, qualPage, reload]);

  const updateFilter = (setter, value) => { setter(value); setPage(1); };

  const processTutor = async (tutor, approve) => {
    const reason = approve ? null : await confirm({
      title: 'Từ chối hồ sơ gia sư',
      message: `Xác nhận từ chối hồ sơ của gia sư “${tutor.fullName}”?`,
      confirmText: 'Từ chối hồ sơ',
      variant: 'danger',
      input: {
        label: 'Lý do từ chối',
        placeholder: 'Nhập lý do từ chối hồ sơ gia sư...',
        required: true,
        minLength: 1,
      },
    });
    if (!approve && reason === null) return;

    setBusyTutorId(tutor.tutorId);
    setError('');
    setMessage('');
    try {
      if (approve) await adminApi.approveTutor(tutor.tutorId);
      else await adminApi.rejectTutor(tutor.tutorId, reason.trim());
      setMessage(approve ? `Đã duyệt hồ sơ ${tutor.fullName}.` : `Đã từ chối hồ sơ ${tutor.fullName}.`);
      setReload((value) => value + 1);
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Không thể cập nhật hồ sơ gia sư.');
    } finally {
      setBusyTutorId(null);
    }
  };

  const reviewQualification = async (qualification, approve) => {
    const reason = approve ? null : await confirm({
      title: 'Từ chối chứng chỉ',
      message: `Nhập lý do từ chối “${qualification.title}”.`,
      confirmText: 'Từ chối chứng chỉ',
      variant: 'danger',
      input: {
        label: 'Lý do từ chối chứng chỉ',
        placeholder: 'Nhập lý do từ chối chứng chỉ...',
        required: true,
        minLength: 1,
      },
    });
    if (!approve && reason === null) return;

    setBusyQualificationId(qualification.id);
    setError('');
    setMessage('');
    try {
      if (approve) await adminApi.approveQualification(qualification.id);
      else await adminApi.rejectQualification(qualification.id, reason.trim());
      setMessage(approve ? 'Đã duyệt bằng cấp/chứng chỉ.' : 'Đã từ chối bằng cấp/chứng chỉ.');
      setReload((value) => value + 1);
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Không thể cập nhật trạng thái bằng cấp/chứng chỉ.');
    } finally {
      setBusyQualificationId(null);
    }
  };

  const openQualification = async (qualification) => {
    if (qualification?.fileUrl && /^https?:\/\//i.test(qualification.fileUrl)) {
      window.open(qualification.fileUrl, '_blank', 'noopener,noreferrer');
      return;
    }
    if (qualification?.fileUrl && qualification.fileUrl.startsWith('/uploads/')) {
      window.open(resolveAssetUrl(qualification.fileUrl), '_blank', 'noopener,noreferrer');
      return;
    }
    const preview = window.open('', '_blank');
    if (!preview) {
      setError('Trình duyệt đang chặn cửa sổ xem tài liệu. Hãy cho phép mở cửa sổ bật lên rồi thử lại.');
      return;
    }
    preview.document.title = qualification.title || 'Tài liệu gia sư';
    preview.document.body.innerHTML = '<p style="font:14px sans-serif;padding:20px">Đang tải tài liệu…</p>';
    try {
      const response = await adminApi.getQualificationFile(qualification.id);
      const fileUrl = URL.createObjectURL(response.data);
      preview.location.href = fileUrl;
      window.setTimeout(() => URL.revokeObjectURL(fileUrl), 60_000);
    } catch (requestError) {
      preview.close();
      setError(requestError.response?.data?.message || 'Không mở được tệp bằng cấp/chứng chỉ.');
    }
  };

  const handleAiVerify = async (qualification) => {
    setAiVerifyingId(qualification.id);
    setError('');
    setMessage('');
    try {
      const res = await adminApi.aiVerifyQualification(qualification.id);
      setAiResults((prev) => ({ ...prev, [qualification.id]: res.data }));
      setMessage(`AI đã quét xong thông tin bằng cấp: ${qualification.title}`);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể quét bằng cấp bằng AI. Vui lòng kiểm tra lại.');
    } finally {
      setAiVerifyingId(null);
    }
  };

  const tutors = data?.tutors?.items || [];
  const stats = data?.stats || {};
  const pendingQualifications = (data?.pendingQualifications && Array.isArray(data.pendingQualifications))
    ? data.pendingQualifications
    : tutors.flatMap((tutor) =>
        (tutor.qualifications || []).map((q) => ({
          ...q,
          tutorId: tutor.tutorId,
          tutorName: tutor.fullName,
          tutorEmail: tutor.email,
          tutorPhone: tutor.phoneNumber,
        }))
      ).filter((q) => q.status === 'Pending');

  const totalQualPages = Math.ceil(pendingQualifications.length / PAGE_SIZE) || 1;
  const pagedQualifications = pendingQualifications.slice((qualPage - 1) * PAGE_SIZE, qualPage * PAGE_SIZE);

  return (
    <div className="admin-page is-approval">
      <AdminPageHeader title="Duyệt hồ sơ & Bằng cấp gia sư" description="Xem xét hồ sơ đăng ký và chứng chỉ chuyên môn trước khi gia sư được xuất hiện trên nền tảng." />
      <AdminFeedback error={error} message={message} onDismiss={() => { setError(''); setMessage(''); }} />

      <AdminStats columns={4} items={[
        { label: 'Hồ sơ chờ duyệt', value: stats.pending, note: 'Gia sư cần xét duyệt' },
        { label: 'Chứng chỉ chờ duyệt', value: stats.pendingQualifications ?? pendingQualifications.length, note: 'Bằng cấp cần kiểm định' },
        { label: 'Dạy trực tuyến', value: stats.online, note: 'Hồ sơ chờ duyệt · Online' },
        { label: 'Dạy trực tiếp', value: stats.offline, note: `Hồ sơ chờ duyệt${stats.hybrid ? ` · Kết hợp ${stats.hybrid}` : ''}` },
      ]} />

      {/* Tab chuyển đổi: Hồ sơ gia sư vs Chứng chỉ cần xét duyệt */}
      <div className="admin-approval-tabs">
        <button
          type="button"
          className={`admin-approval-tab ${activeTab === 'tutors' ? 'is-active' : ''}`}
          onClick={() => {
            setActiveTab('tutors');
            setPage(1);
          }}
        >
          <i className="bi bi-person-check" aria-hidden="true"></i>
          <span>Hồ sơ gia sư cần duyệt ({data?.tutors?.total ?? tutors.length})</span>
        </button>

        <button
          type="button"
          className={`admin-approval-tab ${activeTab === 'qualifications' ? 'is-active' : ''}`}
          onClick={() => {
            setActiveTab('qualifications');
            setQualPage(1);
          }}
        >
          <i className="bi bi-award" aria-hidden="true"></i>
          <span>Chứng chỉ &amp; Bằng cấp cần xét duyệt ({pendingQualifications.length})</span>
        </button>
      </div>

      {activeTab === 'tutors' && (
        <>
          <div className="admin-toolbar">
            <label className="admin-search"><i className="bi bi-search" aria-hidden="true" /><input value={search} onChange={(event) => updateFilter(setSearch, event.target.value)} placeholder="Tìm tên, email hoặc môn học…" /></label>
            <label className="admin-select"><select value={subjectId} onChange={(event) => updateFilter(setSubjectId, event.target.value)} aria-label="Lọc theo môn học"><option value="all">Tất cả môn học</option>{(data?.subjectOptions || []).map((subject) => <option key={subject.id} value={subject.id}>{subject.name}</option>)}</select></label>
            <label className="admin-select"><select value={teachingFormat} onChange={(event) => updateFilter(setTeachingFormat, event.target.value)} aria-label="Lọc theo hình thức"><option value="all">Tất cả hình thức</option><option value="Online">Online</option><option value="Offline">Offline</option><option value="Hybrid">Kết hợp</option></select></label>
          </div>

          <section className="admin-panel">
        <div className="admin-panel-heading">Hồ sơ cần xét duyệt <span className="admin-count">{data?.tutors?.total ?? 0}</span></div>
        <div className="admin-table-scroll">
          <table className="admin-table">
            <thead><tr><th>Gia sư</th><th>Môn học / Cấp học</th><th>Bằng cấp / Chứng chỉ</th><th>Liên hệ</th><th>Hình thức</th><th>Thao tác</th></tr></thead>
            <tbody>
              <AdminLoadingRows columns={6} loading={loading} hasItems={tutors.length > 0} emptyText="Hiện không có hồ sơ chờ duyệt phù hợp." />
              {!loading && tutors.map((tutor) => (
                <tr key={tutor.tutorId}>
                  <td className="admin-approval-tutor-cell"><span className="admin-table-primary">{tutor.fullName}</span><span className="admin-table-secondary">{tutor.introduction || 'Chưa có giới thiệu'}</span></td>
                  <td><div className="admin-subjects">{tutor.subjects?.length ? tutor.subjects.map((subject, index) => <span className="admin-subject-tag" key={`${subject.subjectName}-${subject.gradeName}-${index}`}>{subject.subjectName} · {subject.gradeName}</span>) : <span className="admin-table-secondary">Chưa cập nhật</span>}</div></td>
                  <td className="admin-qualification-cell">
                    {tutor.qualifications?.length ? (
                      <details open className="admin-qualification-details">
                        <summary>Kiểm tra tài liệu ({tutor.qualifications.length})</summary>
                        <div className="admin-qualification-list">
                          {tutor.qualifications.map((qualification) => (
                            <div className="admin-qualification-item" key={qualification.id}>
                              <div className="admin-qualification-item-head">
                                <span className="admin-table-primary">{qualification.title}</span>
                                <span className={`admin-status ${qualification.status === 'Approved' ? 'is-active' : qualification.status === 'Rejected' ? 'is-locked' : 'is-pending'}`}>
                                  {qualification.status === 'Approved' ? 'Đã duyệt' : qualification.status === 'Rejected' ? 'Từ chối' : 'Chờ duyệt'}
                                </span>
                              </div>
                              <div className="admin-actions-inline">
                                <button className="admin-action" type="button" onClick={() => openQualification(qualification)}>Xem file</button>
                                <button
                                  className="admin-action is-ai"
                                  type="button"
                                  disabled={aiVerifyingId === qualification.id}
                                  onClick={() => handleAiVerify(qualification)}
                                >
                                  <i className="bi bi-cpu" aria-hidden="true" />
                                  {aiVerifyingId === qualification.id ? 'Đang quét…' : 'AI Quét'}
                                </button>
                                {qualification.status === 'Pending' && <>
                                  <button className="admin-action is-approve" type="button" disabled={busyQualificationId === qualification.id} onClick={() => reviewQualification(qualification, true)}>{busyQualificationId === qualification.id ? 'Đang lưu…' : 'Duyệt'}</button>
                                  <button className="admin-action" type="button" disabled={busyQualificationId === qualification.id} onClick={() => reviewQualification(qualification, false)}>Từ chối</button>
                                </>}
                              </div>
                              <AiVerificationResult result={aiResults[qualification.id]} />
                            </div>
                          ))}
                        </div>
                      </details>
                    ) : <span className="admin-table-secondary">Chưa có tài liệu</span>}
                  </td>
                  <td className="admin-approval-contact-cell"><span className="admin-table-primary">{tutor.email || 'Chưa có email'}</span><span className="admin-table-secondary">{tutor.phoneNumber || 'Chưa có số điện thoại'}</span></td>
                  <td className="admin-format">{tutor.teachingFormat === 'Hybrid' ? 'Kết hợp' : tutor.teachingFormat}</td>
                  <td><div className="admin-actions-inline">
                    <button className="admin-action is-approve" type="button" disabled={busyTutorId === tutor.tutorId} onClick={() => processTutor(tutor, true)}>{busyTutorId === tutor.tutorId ? 'Đang lưu…' : 'Duyệt'}</button>
                    <button className="admin-action" type="button" disabled={busyTutorId === tutor.tutorId} onClick={() => processTutor(tutor, false)}>Từ chối</button>
                  </div></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <AdminPagination page={data?.tutors?.page || page} totalPages={data?.tutors?.totalPages} total={data?.tutors?.total || 0} pageSize={PAGE_SIZE} onChange={setPage} />
      </section>
      </>
      )}

      {activeTab === 'qualifications' && (
        <section className="admin-panel">
          <div className="admin-panel-heading">
            Danh sách chứng chỉ &amp; bằng cấp cần xét duyệt <span className="admin-count">{qualData ? qualData.total : pendingQualifications.length}</span>
            <span className="admin-table-secondary" style={{ fontSize: '0.82rem', marginLeft: '12px', fontWeight: 'normal' }}>
              (Admin tự check thủ công qua file gốc hoặc bấm nút AI OCR quét tự động)
            </span>
          </div>
          <div className="admin-table-scroll">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Gia sư</th>
                  <th>Tên chứng chỉ / Bằng cấp</th>
                  <th>Trạng thái</th>
                  <th>Kiểm tra thủ công</th>
                  <th>AI OCR Quét tự động</th>
                  <th>Quyết định duyệt</th>
                </tr>
              </thead>
              <tbody>
                {loadingQual ? (
                  <tr>
                    <td colSpan={6} style={{ textAlign: 'center', padding: '36px', color: '#64748b' }}>
                      Đang tải danh sách bằng cấp/chứng chỉ…
                    </td>
                  </tr>
                ) : (qualData ? qualData.items : pagedQualifications).length === 0 ? (
                  <tr>
                    <td colSpan={6} style={{ textAlign: 'center', padding: '36px', color: '#64748b' }}>
                      Hiện không có chứng chỉ nào đang chờ xét duyệt.
                    </td>
                  </tr>
                ) : (
                  (qualData ? qualData.items : pagedQualifications).map((qualification) => (
                    <tr key={qualification.id}>
                      <td>
                        <span className="admin-table-primary">{qualification.tutorName}</span>
                        <span className="admin-table-secondary">{qualification.tutorEmail}</span>
                        {qualification.tutorPhone && <span className="admin-table-secondary">{qualification.tutorPhone}</span>}
                      </td>
                      <td>
                        <span className="admin-table-primary" style={{ fontWeight: '600' }}>{qualification.title}</span>
                        <span className="admin-table-secondary">Mã số: #{qualification.id}</span>
                      </td>
                      <td>
                        <span className="admin-status is-pending">Chờ duyệt</span>
                      </td>
                      <td>
                        <button
                          className="admin-action"
                          type="button"
                          style={{ whiteSpace: 'nowrap' }}
                          onClick={() => openQualification(qualification)}
                        >
                          <i className="bi bi-file-earmark-text me-1"></i> Xem file gốc
                        </button>
                      </td>
                      <td className="admin-qualification-cell">
                        <button
                          className="admin-action is-ai mb-2"
                          type="button"
                          disabled={aiVerifyingId === qualification.id}
                          onClick={() => handleAiVerify(qualification)}
                        >
                          <i className="bi bi-cpu" aria-hidden="true" />
                          {aiVerifyingId === qualification.id ? 'Đang quét OCR…' : 'AI OCR Quét'}
                        </button>

                        <AiVerificationResult result={aiResults[qualification.id]} />
                      </td>
                      <td>
                        <div className="admin-actions-inline">
                          <button
                            className="admin-action is-approve"
                            type="button"
                            disabled={busyQualificationId === qualification.id}
                            onClick={() => reviewQualification(qualification, true)}
                          >
                            {busyQualificationId === qualification.id ? 'Đang lưu…' : 'Duyệt'}
                          </button>
                          <button
                            className="admin-action"
                            type="button"
                            disabled={busyQualificationId === qualification.id}
                            onClick={() => reviewQualification(qualification, false)}
                          >
                            Từ chối
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
          <AdminPagination
            page={qualData?.page || qualPage}
            totalPages={qualData?.totalPages || totalQualPages}
            total={qualData ? qualData.total : pendingQualifications.length}
            pageSize={PAGE_SIZE}
            onChange={setQualPage}
          />
        </section>
      )}

      <aside className="admin-note is-warning"><i className="bi bi-shield-check" aria-hidden="true" /><div><strong>Cần kiểm tra trước khi duyệt</strong>Đối chiếu thông tin cá nhân, bằng cấp/CCCD và thông tin giảng dạy. Hồ sơ trong danh sách chưa được xem là đã xác minh.</div></aside>

      <section className="admin-guide">
        <h2>Hướng dẫn xét duyệt</h2>
        <div className="admin-guide-step"><b>Bước 1</b>Kiểm tra thông tin cá nhân</div>
        <div className="admin-guide-step"><b>Bước 2</b>Đối chiếu giấy tờ và bằng cấp</div>
        <div className="admin-guide-step"><b>Bước 3</b>Xem môn học, cấp học và hình thức dạy</div>
      </section>
    </div>
  );
}
