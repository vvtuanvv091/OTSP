import { useState, useEffect, useRef } from 'react';
import { useSearchParams } from 'react-router-dom';
import tutorApi from '../../api/tutorApi';
import { repairMojibake } from '../../utils/repairMojibake';
import { useToast } from '../../context/toastHooks';
import { useConfirm } from '../../context/confirmHooks';
import OtspPagination from '../../components/common/OtspPagination';
import '../../styles/tutor/TutorDocumentsPage.css';

export default function TutorDocumentsPage() {
  const toast = useToast();
  const confirm = useConfirm();
  const [searchParams] = useSearchParams();
  const classroomIdFilter = searchParams.get('classroomId') || '';

  const [documents, setDocuments] = useState([]);
  const [activeClassrooms, setActiveClassrooms] = useState([]);
  const [teachingPairs, setTeachingPairs] = useState([]);
  const [totalItems, setTotalItems] = useState(0);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [reloadKey, setReloadKey] = useState(0);
  const [downloadingId, setDownloadingId] = useState(null);
  const fileInputRef = useRef(null);

  // Trạng thái biểu mẫu tải lên tài liệu
  const [scope, setScope] = useState('classroom'); // 'classroom' hoặc 'public'
  const [title, setTitle] = useState('');
  const [classroomId, setClassroomId] = useState(classroomIdFilter);
  const [selectedPairKey, setSelectedPairKey] = useState('');
  const [prevClassroomIdFilter, setPrevClassroomIdFilter] = useState(classroomIdFilter);
  const [description, setDescription] = useState('');
  const [file, setFile] = useState(null);
  const [uploading, setUploading] = useState(false);

  if (prevClassroomIdFilter !== classroomIdFilter) {
    setPrevClassroomIdFilter(classroomIdFilter);
    setClassroomId(classroomIdFilter);
    setPage(1);
  }

  useEffect(() => {
    let active = true;
    async function loadDocuments() {
      try {
        setLoading(true);
        const params = { page, pageSize: 10 };
        if (classroomIdFilter) params.classroomId = classroomIdFilter;
        const res = await tutorApi.getDocuments(params);
        if (!active) return;
        if (res.data) {
          setDocuments(res.data.documents || res.data.items || []);
          setTotalItems(res.data.totalItems || 0);
          setTotalPages(res.data.totalPages || 1);
          setPage(res.data.page || 1);

          if (Array.isArray(res.data.activeClassrooms)) {
            const cls = res.data.activeClassrooms;
            setActiveClassrooms(cls);
            if (cls.length > 0) {
              setClassroomId((prev) => prev || classroomIdFilter || String(cls[0].id));
            }
          }

          if (Array.isArray(res.data.teachingPairs)) {
            const pairs = res.data.teachingPairs;
            setTeachingPairs(pairs);
            if (pairs.length > 0) {
              setSelectedPairKey((prev) => prev || `${pairs[0].subjectId}_${pairs[0].gradeLevelId}`);
            }
          }
        }
      } catch {
        if (active) setError('Không tải được danh sách tài liệu.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadDocuments();
    return () => {
      active = false;
    };
  }, [page, classroomIdFilter, reloadKey]);

  const handleUpload = async (e) => {
    e.preventDefault();
    if (!title.trim() || !file) {
      setError('Vui lòng nhập tên tài liệu và chọn tệp đính kèm.');
      return;
    }

    if (scope === 'classroom' && !classroomId) {
      setError('Vui lòng chọn lớp học áp dụng (hoặc chuyển sang phạm vi Công khai).');
      return;
    }

    if (scope === 'public' && teachingPairs.length === 0) {
      setError('Bạn cần đăng ký ít nhất một môn học giảng dạy trước khi tải lên tài liệu công khai.');
      return;
    }

    setUploading(true);
    setMessage('');
    setError('');

    const formData = new FormData();
    formData.append('title', title.trim());
    formData.append('scope', scope === 'classroom' ? 'Classroom' : 'Public');
    if (scope === 'classroom' && classroomId) {
      formData.append('classroomId', classroomId);
      const selectedCls = activeClassrooms.find((c) => String(c.id) === String(classroomId));
      if (selectedCls) {
        if (selectedCls.subjectId) formData.append('subjectId', selectedCls.subjectId);
        if (selectedCls.gradeLevelId) formData.append('gradeLevelId', selectedCls.gradeLevelId);
      }
    } else if (scope === 'public' && selectedPairKey) {
      const [subId, gradeId] = selectedPairKey.split('_');
      if (subId) formData.append('subjectId', subId);
      if (gradeId) formData.append('gradeLevelId', gradeId);
    }
    formData.append('description', description || '');
    formData.append('file', file);

    try {
      const res = await tutorApi.uploadDocument(formData);
      setMessage(res.data?.message || 'Tải lên tài liệu ôn tập thành công!');
      setTitle('');
      setDescription('');
      setFile(null);
      if (fileInputRef.current) fileInputRef.current.value = '';
      setReloadKey((k) => k + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Có lỗi xảy ra khi tải lên tài liệu.');
    } finally {
      setUploading(false);
    }
  };

  const handleDelete = async (id) => {
    const ok = await confirm({
      title: 'Xóa tài liệu',
      message: 'Bạn có chắc chắn muốn xóa tài liệu này khỏi hệ thống?',
      confirmText: 'Xóa',
      variant: 'danger',
    });
    if (!ok) return;

    try {
      const res = await tutorApi.deleteDocument(id);
      toast.success(res.data?.message || 'Đã xóa tài liệu thành công.');
      setReloadKey((k) => k + 1);
    } catch (err) {
      toast.error(err.response?.data?.message || 'Không thể xóa tài liệu.');
    }
  };

  const handleDownload = async (doc) => {
    try {
      setDownloadingId(doc.id);
      const res = await tutorApi.downloadDocument(doc.id);
      const disposition = res.headers?.['content-disposition'] || '';
      const utf8Match = disposition.match(/filename\*=UTF-8''([^;]+)/i);
      const asciiMatch = disposition.match(/filename="?([^";]+)"?/i);
      const ext = doc.fileExtension || '';
      const baseTitle = doc.fileName || repairMojibake(doc.title) || `tai-lieu-${doc.id}`;
      const fallbackName = ext && !baseTitle.toLowerCase().endsWith(ext.toLowerCase())
        ? `${baseTitle}${ext}`
        : baseTitle;
      const filename = utf8Match
        ? decodeURIComponent(utf8Match[1])
        : (asciiMatch ? asciiMatch[1] : fallbackName);

      const blobUrl = window.URL.createObjectURL(res.data);
      const link = document.createElement('a');
      link.href = blobUrl;
      link.download = filename;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(blobUrl);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể tải xuống tài liệu này.');
    } finally {
      setDownloadingId(null);
    }
  };

  return (
    <>
      <div className="container-fluid py-4">
        {/* Tiêu đề trang */}
        <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-4">
          <div>
            <h1 className="h3 mb-1">Tài liệu ôn tập</h1>
            <p className="text-body-secondary mb-0">Quản lý và phân phối tài liệu ôn tập theo lớp học hoặc công khai cho cộng đồng.</p>
          </div>
          <div>
            <span className="badge border border-success text-success bg-transparent rounded-pill px-3 py-2 fs-6">
              <i className="bi bi-folder2-open me-1"></i>{totalItems} tài liệu
            </span>
          </div>
        </div>

        {/* Thông báo hệ thống */}
        {message && (
          <div className="alert alert-success alert-dismissible fade show shadow-sm" role="alert">
            <i className="bi bi-check-circle me-2"></i>{message}
            <button type="button" className="btn-close" onClick={() => setMessage('')} aria-label="Đóng"></button>
          </div>
        )}

        {error && (
          <div className="alert alert-warning alert-dismissible fade show shadow-sm" role="alert">
            <i className="bi bi-exclamation-triangle me-2"></i>{error}
            <button type="button" className="btn-close" onClick={() => setError('')} aria-label="Đóng"></button>
          </div>
        )}

        {/* Khối biểu mẫu tải lên tài liệu */}
        <section className="card border-0 shadow-sm mb-4">
          <div className="card-body p-4">
            <h2 className="h5 fw-bold mb-3"><i className="bi bi-cloud-arrow-up me-2 text-success"></i>Tải lên tài liệu mới</h2>

            <form onSubmit={handleUpload}>
              <div className="row g-3 mb-3">
                <div className="col-md-6">
                  <div
                    className={`scope-card ${scope === 'classroom' ? 'is-active' : ''}`}
                    onClick={() => setScope('classroom')}
                  >
                    <div className="fw-bold text-dark"><i className="bi bi-easel me-2 text-success"></i>Theo lớp học cụ thể</div>
                    <small className="text-secondary">Chỉ học viên trong lớp mới có thể xem và tải tài liệu này.</small>
                  </div>
                </div>
                <div className="col-md-6">
                  <div
                    className={`scope-card ${scope === 'public' ? 'is-active' : ''}`}
                    onClick={() => setScope('public')}
                  >
                    <div className="fw-bold text-dark"><i className="bi bi-globe me-2 text-primary"></i>Công khai cho học viên</div>
                    <small className="text-secondary">Hiển thị tại Kho tài liệu công khai sau khi được Quản trị viên duyệt.</small>
                  </div>
                </div>
              </div>

              <div className="row g-3">
                <div className="col-md-6">
                  <label className="form-label fw-semibold">Tên tài liệu / Bài giảng <span className="text-danger">*</span></label>
                  <input
                    type="text"
                    className="form-control"
                    placeholder="Ví dụ: Đề cương ôn tập Toán giữa kỳ 1"
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                    required
                  />
                </div>

                {scope === 'classroom' ? (
                  <div className="col-md-6">
                    <label className="form-label fw-semibold">Chọn lớp học áp dụng <span className="text-danger">*</span></label>
                    <select
                      className="form-select"
                      value={classroomId}
                      onChange={(e) => setClassroomId(e.target.value)}
                      required
                    >
                      <option value="">-- Chọn lớp học áp dụng --</option>
                      {activeClassrooms.map((c) => {
                        const isOneToOne = c.type === 0 || c.type === 'OneToOne';
                        return (
                          <option key={c.id} value={c.id}>
                            {isOneToOne ? '[1-1]' : '[Nhóm]'} {c.nameClass || c.name} ({c.subjectName})
                          </option>
                        );
                      })}
                    </select>
                  </div>
                ) : (
                  <div className="col-md-6">
                    <label className="form-label fw-semibold">Môn học &amp; Cấp học <span className="text-danger">*</span></label>
                    <select
                      className="form-select"
                      value={selectedPairKey}
                      onChange={(e) => setSelectedPairKey(e.target.value)}
                      required
                    >
                      {teachingPairs.length === 0 ? (
                        <option value="">-- Chưa có môn học giảng dạy --</option>
                      ) : (
                        teachingPairs.map((pair) => {
                          const key = `${pair.subjectId}_${pair.gradeLevelId}`;
                          return (
                            <option key={key} value={key}>
                              {pair.subjectName} · {pair.gradeLevelName}
                            </option>
                          );
                        })
                      )}
                    </select>
                  </div>
                )}

                <div className="col-md-6">
                  <label className="form-label fw-semibold">Chọn tệp (PDF, Word, PPTX, Excel; tối đa 20 MB) <span className="text-danger">*</span></label>
                  <input
                    ref={fileInputRef}
                    type="file"
                    className="form-control"
                    accept=".pdf,.doc,.docx,.ppt,.pptx,.xls,.xlsx"
                    onChange={(e) => setFile(e.target.files?.[0] || null)}
                    required
                  />
                </div>

                <div className="col-md-6">
                  <label className="form-label fw-semibold">Ghi chú thêm</label>
                  <input
                    type="text"
                    className="form-control"
                    placeholder="Hướng dẫn làm bài, hạn nộp..."
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                  />
                </div>

                <div className="col-12 text-end">
                  <button type="submit" className="btn btn-success px-4" disabled={uploading}>
                    {uploading ? (
                      <>
                        <span className="spinner-border spinner-border-sm me-2" role="status"></span>Đang tải lên...
                      </>
                    ) : (
                      <>
                        <i className="bi bi-upload me-1"></i> Tải lên tài liệu
                      </>
                    )}
                  </button>
                </div>
              </div>
            </form>
          </div>
        </section>

        {/* Bảng danh sách tài liệu đã đăng */}
        <section className="card border-0 shadow-sm">
          <div className="card-body p-4">
            <h2 className="h5 fw-bold mb-3">Danh sách tài liệu đã đăng</h2>

            {loading ? (
              <div className="text-center py-5">
                <div className="spinner-border text-success" role="status"></div>
              </div>
            ) : documents.length === 0 ? (
              <div className="text-center py-5 text-secondary">
                <i className="bi bi-folder-x fs-1 text-muted d-block mb-2"></i>
                <p className="mb-0">Bạn chưa tải lên tài liệu nào.</p>
              </div>
            ) : (
              <>
                <div className="table-responsive">
                  <table className="table table-docs align-middle mb-0">
                    <thead>
                      <tr>
                        <th style={{ width: '6%' }}>#</th>
                        <th style={{ width: '40%' }}>Tài liệu</th>
                        <th style={{ width: '25%' }}>Phạm vi / Lớp học</th>
                        <th style={{ width: '15%' }}>Ngày tải</th>
                        <th style={{ width: '14%', textAlign: 'right' }}>Thao tác</th>
                      </tr>
                    </thead>
                    <tbody>
                      {documents.map((doc, idx) => {
                        const isPublicDoc = doc.scope === 'Public' || doc.scope === 1 || !doc.classroomName;
                        const statusVal = doc.status;
                        const isPending = statusVal === 'Pending' || statusVal === 0;
                        const isApproved = statusVal === 'Approved' || statusVal === 1;
                        return (
                        <tr key={doc.id}>
                          <td className="text-muted small">{(page - 1) * 10 + idx + 1}</td>
                          <td>
                            <div className="d-flex align-items-center gap-2">
                              <i className="bi bi-file-earmark-text text-danger fs-4"></i>
                              <div>
                                <div className="fw-semibold text-dark">{repairMojibake(doc.title)}</div>
                                <small className="text-muted">
                                  {doc.subjectName}
                                  {doc.gradeLevelName ? ` · ${doc.gradeLevelName}` : ''}
                                  {doc.fileExtension ? ` · ${doc.fileExtension.replace('.', '').toUpperCase()}` : ''}
                                </small>
                              </div>
                            </div>
                          </td>
                          <td>
                            <div className="d-flex flex-wrap align-items-center gap-1">
                              {doc.classroomName ? (
                                <span className="badge bg-light text-dark border">
                                  <i className="bi bi-easel me-1 text-danger"></i>
                                  {(doc.classroomType === 0 || doc.classroomType === 'OneToOne') ? '[1-1] ' : '[Nhóm] '}
                                  {doc.classroomName}
                                </span>
                              ) : (
                                <span className="badge bg-primary-subtle text-primary border border-primary-subtle">
                                  <i className="bi bi-globe me-1"></i>Công khai
                                </span>
                              )}

                              {isPublicDoc ? (
                                isPending ? (
                                  <span className="badge bg-warning-subtle text-warning-emphasis border border-warning-subtle">
                                    <i className="bi bi-hourglass-split me-1"></i>Chờ Admin duyệt
                                  </span>
                                ) : isApproved ? (
                                  <span className="badge bg-success-subtle text-success border border-success-subtle">
                                    <i className="bi bi-check-circle me-1"></i>Đã công khai
                                  </span>
                                ) : (
                                  <span className="badge bg-danger-subtle text-danger border border-danger-subtle">
                                    <i className="bi bi-x-circle me-1"></i>Bị từ chối
                                  </span>
                                )
                              ) : (
                                <span className="badge bg-success-subtle text-success border border-success-subtle">
                                  Hiển thị trong lớp
                                </span>
                              )}
                            </div>
                          </td>
                          <td className="text-muted small">
                            {doc.uploadedAt ? new Date(doc.uploadedAt).toLocaleDateString('vi-VN') : ''}
                          </td>
                          <td style={{ textAlign: 'right' }}>
                            <div className="d-flex justify-content-end align-items-center gap-1">
                              <button
                                type="button"
                                className="btn btn-sm btn-outline-success"
                                disabled={downloadingId === doc.id}
                                onClick={() => handleDownload(doc)}
                                title="Tải xuống tài liệu"
                              >
                                {downloadingId === doc.id ? (
                                  <span className="spinner-border spinner-border-sm" role="status"></span>
                                ) : (
                                  <i className="bi bi-download"></i>
                                )}
                              </button>
                              <button
                                type="button"
                                className="btn btn-sm btn-outline-danger"
                                onClick={() => handleDelete(doc.id)}
                              >
                                <i className="bi bi-trash"></i>
                              </button>
                            </div>
                          </td>
                        </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>

                {totalItems > 0 && (
                  <OtspPagination
                    page={page}
                    totalPages={totalPages}
                    total={totalItems}
                    pageSize={10}
                    onChange={setPage}
                    className="mt-4 rounded-3 shadow-sm border"
                  />
                )}
              </>
            )}
          </div>
        </section>
      </div>
    </>
  );
}
