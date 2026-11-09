import { useState, useEffect } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import studentApi from '../../api/studentApi';
import { repairMojibake } from '../../utils/repairMojibake';
import OtspPagination from '../../components/common/OtspPagination';

export default function StudentDocumentsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const queryClassroomId = searchParams.get('classroomId') || '';

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [documents, setDocuments] = useState([]);
  const [classrooms, setClassrooms] = useState([]);
  const [selectedClassId, setSelectedClassId] = useState(queryClassroomId);
  const [prevQueryClassId, setPrevQueryClassId] = useState(queryClassroomId);
  const [search, setSearch] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalItems, setTotalItems] = useState(0);
  const [reloadKey, setReloadKey] = useState(0);
  const [downloadingId, setDownloadingId] = useState(null);

  if (prevQueryClassId !== queryClassroomId) {
    setPrevQueryClassId(queryClassroomId);
    setSelectedClassId(queryClassroomId);
    setCurrentPage(1);
  }

  useEffect(() => {
    let active = true;
    async function loadDocuments() {
      try {
        setLoading(true);
        setError('');
        const res = await studentApi.getDocuments({
          classroomId: selectedClassId || undefined,
          search: search || undefined,
          page: currentPage,
          pageSize: 10
        });
        if (!active) return;
        if (res.data) {
          const list = Array.isArray(res.data)
            ? res.data
            : (res.data.items || res.data.documents || []);
          setDocuments(list);
          if (res.data.myClassrooms) setClassrooms(res.data.myClassrooms);
          setTotalPages(res.data.totalPages || 1);
          if (res.data.totalItems !== undefined) setTotalItems(res.data.totalItems);
          else setTotalItems(list.length);
        }
      } catch (err) {
        if (!active) return;
        setError(err.response?.data?.message || 'Không thể tải danh sách tài liệu. Vui lòng thử lại.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadDocuments();
    return () => {
      active = false;
    };
  }, [selectedClassId, search, currentPage, reloadKey]);

  const handleSearchSubmit = (e) => {
    e.preventDefault();
    setCurrentPage(1);
    setSearch(searchInput.trim());
  };

  const handleClassChange = (value) => {
    setSelectedClassId(value);
    setCurrentPage(1);
    const next = new URLSearchParams(searchParams);
    if (value) next.set('classroomId', value);
    else next.delete('classroomId');
    setSearchParams(next, { replace: true });
  };

  const handleClearFilter = () => {
    setSelectedClassId('');
    setSearch('');
    setSearchInput('');
    setCurrentPage(1);
    if (searchParams.has('classroomId')) {
      const next = new URLSearchParams(searchParams);
      next.delete('classroomId');
      setSearchParams(next, { replace: true });
    }
  };

  const handleDownload = async (doc) => {
    try {
      setDownloadingId(doc.id);
      const res = await studentApi.downloadDocument(doc.id);
      const disposition = res.headers?.['content-disposition'] || '';
      const utf8Match = disposition.match(/filename\*=UTF-8''([^;]+)/i);
      const asciiMatch = disposition.match(/filename="?([^";]+)"?/i);
      const ext = doc.fileExtension || '';
      const baseTitle = repairMojibake(doc.title) || `tai-lieu-${doc.id}`;
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
      alert(err.response?.data?.message || 'Không thể tải xuống tài liệu này. Vui lòng thử lại.');
    } finally {
      setDownloadingId(null);
    }
  };

  const getDocTypeIcon = (ext) => {
    const e = (ext || '').toLowerCase();
    switch (e) {
      case '.pdf':
        return { icon: 'bi-file-earmark-pdf', bg: '#fee2e2', color: '#dc2626' };
      case '.doc':
      case '.docx':
        return { icon: 'bi-file-earmark-word', bg: '#e0e7ff', color: '#3730a3' };
      case '.xls':
      case '.xlsx':
        return { icon: 'bi-file-earmark-excel', bg: '#dcfce7', color: '#166534' };
      case '.ppt':
      case '.pptx':
        return { icon: 'bi-file-earmark-ppt', bg: '#ffedd5', color: '#c2410c' };
      default:
        return { icon: 'bi-file-earmark-text', bg: '#f1f5f9', color: '#475569' };
    }
  };

  return (
    <div className="container-fluid py-3 px-1 px-md-3" style={{ width: '100%' }}>
      {/* Tiêu đề trang */}
      <div className="d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3 mb-4">
        <div>
          <h1 className="h4 fw-bold mb-1 text-body">Tài liệu lớp học của tôi</h1>
          <p className="text-secondary small mb-0">
            Kho tài liệu ôn tập, đề cương và bài giảng do gia sư chia sẻ riêng cho các lớp học bạn đang theo học.
          </p>
        </div>
        <div>
          <Link to="/student/classrooms" className="btn btn-outline-danger btn-sm">
            <i className="bi bi-easel me-1"></i> Lớp học của tôi
          </Link>
        </div>
      </div>

      {/* Thanh công cụ tìm kiếm và lọc tài liệu */}
      <div className="card border shadow-sm rounded-3 mb-4 bg-body">
        <div className="card-body p-3">
          <form onSubmit={handleSearchSubmit} className="d-flex flex-wrap align-items-center justify-content-between gap-3 m-0">
            <div className="d-flex flex-wrap align-items-center gap-2 flex-grow-1">
              {classrooms.length > 0 && (
                <div className="d-flex align-items-center gap-2">
                  <label className="form-label mb-0 small text-secondary text-nowrap fw-semibold">
                    Lọc theo lớp:
                  </label>
                  <select
                    className="form-select form-select-sm"
                    style={{ minWidth: 220 }}
                    value={selectedClassId}
                    onChange={(e) => handleClassChange(e.target.value)}
                  >
                    <option value="">-- Tất cả lớp của tôi --</option>
                    {classrooms.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.type === 0 || c.type === 'OneToOne' ? '[1-1]' : '[Nhóm]'} {c.name} ({c.subjectName})
                      </option>
                    ))}
                  </select>
                </div>
              )}

              <div className="input-group input-group-sm" style={{ maxWidth: 320 }}>
                <input
                  type="text"
                  className="form-control"
                  placeholder="Tìm tên tài liệu, bài giảng..."
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                />
                <button type="submit" className="btn btn-outline-secondary">
                  <i className="bi bi-search"></i>
                </button>
              </div>

              {(search || selectedClassId) && (
                <button
                  type="button"
                  onClick={handleClearFilter}
                  className="btn btn-sm btn-link text-danger text-decoration-none p-0 ps-1"
                  title="Xóa lọc"
                >
                  <i className="bi bi-x-circle me-1"></i>Xóa bộ lọc
                </button>
              )}
            </div>

            <div className="text-secondary small">
              Tổng cộng: <strong className="text-body">{totalItems}</strong> tài liệu
            </div>
          </form>
        </div>
      </div>

      {error && (
        <div className="alert alert-danger d-flex align-items-center justify-content-between gap-3 mb-4" role="alert">
          <span>{error}</span>
          <button
            type="button"
            className="btn btn-outline-danger btn-sm"
            onClick={() => setReloadKey((k) => k + 1)}
          >
            Thử lại
          </button>
        </div>
      )}

      {/* Danh sách Tài liệu Lớp học */}
      <div className="card border shadow-sm rounded-3 bg-body">
        <div className="card-header bg-body border-bottom py-3 d-flex align-items-center justify-content-between">
          <h2 className="h6 fw-bold mb-0 text-body">
            <i className="bi bi-folder2-open me-2 text-danger"></i>
            Danh sách tài liệu lớp học
            <span className="badge border border-secondary text-secondary ms-2 fw-normal">
              {totalItems} tài liệu
            </span>
          </h2>
        </div>

        <div className="card-body p-0">
          {loading ? (
            <div className="text-center py-5">
              <div className="spinner-border text-danger" role="status"></div>
              <div className="mt-2 text-secondary small">Đang tải danh sách tài liệu...</div>
            </div>
          ) : documents.length === 0 ? (
            <div className="text-center py-5 text-muted">
              <i className="bi bi-folder-x fs-1 text-secondary d-block mb-2"></i>
              <h5 className="fw-semibold text-body">Chưa có tài liệu nào</h5>
              <p className="small text-secondary mb-0">
                {selectedClassId
                  ? 'Lớp học này hiện chưa có tài liệu nào được gia sư tải lên.'
                  : 'Gia sư các lớp của bạn chưa tải tài liệu lên cho lớp học.'}
              </p>
            </div>
          ) : (
            <div className="table-responsive">
              <table className="table table-hover align-middle mb-0">
                <thead className="table-light">
                  <tr>
                    <th style={{ width: 45 }} className="text-center">#</th>
                    <th style={{ minWidth: 250 }}>Tài liệu</th>
                    <th style={{ minWidth: 140 }}>Môn - Cấp học</th>
                    <th style={{ minWidth: 180 }}>Thuộc lớp học</th>
                    <th style={{ minWidth: 150 }}>Gia sư</th>
                    <th style={{ minWidth: 110 }}>Ngày tải</th>
                    <th style={{ minWidth: 130 }} className="text-end pe-3">Tải về</th>
                  </tr>
                </thead>
                <tbody>
                  {documents.map((doc, idx) => {
                    const iconMeta = getDocTypeIcon(doc.fileExtension);
                    const rowNumber = (currentPage - 1) * 10 + idx + 1;
                    return (
                      <tr key={doc.id}>
                        <td className="text-center text-secondary small">{rowNumber}</td>
                        <td>
                          <div className="d-flex align-items-center gap-2.5">
                            <div
                              style={{
                                width: 36,
                                height: 36,
                                borderRadius: 8,
                                display: 'inline-flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                fontSize: '1.25rem',
                                flexShrink: 0,
                                backgroundColor: iconMeta.bg,
                                color: iconMeta.color
                              }}
                            >
                              <i className={`bi ${iconMeta.icon}`}></i>
                            </div>
                            <div className="min-w-0">
                              <div className="fw-semibold text-body text-break">{repairMojibake(doc.title)}</div>
                              {doc.description && (
                                <div
                                  className="text-secondary small text-truncate"
                                  style={{ maxWidth: 280, fontSize: '0.82rem' }}
                                >
                                  {doc.description}
                                </div>
                              )}
                            </div>
                          </div>
                        </td>
                        <td>
                          <span className="text-body small">
                            {doc.subjectName} · {doc.gradeLevelName}
                          </span>
                        </td>
                        <td>
                          <div className="d-flex align-items-center gap-1 flex-wrap">
                            <span className="fw-medium text-body small">
                              {doc.classroomName || 'Lớp công khai'}
                            </span>
                            {doc.classroomType === 0 && (
                              <span className="badge border border-info text-info bg-transparent" style={{ fontSize: '0.7rem' }}>
                                1-1
                              </span>
                            )}
                            {doc.classroomType === 1 && (
                              <span className="badge border border-secondary text-secondary bg-transparent" style={{ fontSize: '0.7rem' }}>
                                Nhóm
                              </span>
                            )}
                          </div>
                        </td>
                        <td>
                          <div className="d-flex align-items-center gap-1.5">
                            <div
                              style={{
                                width: 26,
                                height: 26,
                                fontSize: 11,
                                borderRadius: '50%',
                                background: '#e2e8f0',
                                color: '#334155',
                                display: 'inline-flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                fontWeight: 600
                              }}
                            >
                              {doc.tutorName ? doc.tutorName.trim().charAt(0).toUpperCase() : 'G'}
                            </div>
                            <span className="text-body small">{doc.tutorName}</span>
                          </div>
                        </td>
                        <td className="text-secondary small text-nowrap">
                          {doc.uploadedAt ? new Date(doc.uploadedAt).toLocaleDateString('vi-VN') : ''}
                        </td>
                        <td className="text-end pe-3">
                          <button
                            type="button"
                            onClick={() => handleDownload(doc)}
                            disabled={downloadingId === doc.id}
                            className="btn btn-outline-success btn-sm py-1 px-2.5 text-nowrap d-inline-flex align-items-center gap-1"
                            title="Tải xuống tệp"
                          >
                            {downloadingId === doc.id ? (
                              <span className="spinner-border spinner-border-sm" role="status"></span>
                            ) : (
                              <i className="bi bi-download"></i>
                            )}
                            <span>{downloadingId === doc.id ? 'Đang tải...' : 'Tải về'}</span>
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Phân trang */}
        {totalItems > 0 && (
          <OtspPagination
            page={currentPage}
            totalPages={totalPages}
            total={totalItems}
            pageSize={10}
            onChange={setCurrentPage}
            className="border-top"
          />
        )}
      </div>
    </div>
  );
}
