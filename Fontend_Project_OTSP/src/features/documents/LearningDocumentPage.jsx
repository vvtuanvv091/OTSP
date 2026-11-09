import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import commonApi from '../../api/commonApi';
import { repairMojibake } from '../../utils/repairMojibake';

export default function LearningDocumentPage() {
  const [documents, setDocuments] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [gradeLevels, setGradeLevels] = useState([]);
  const [search, setSearch] = useState('');
  const [subjectId, setSubjectId] = useState('');
  const [gradeLevelId, setGradeLevelId] = useState('');
  const [totalItems, setTotalItems] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const fetchDocuments = useCallback(async (keywordVal, subjectVal, gradeVal) => {
    setLoading(true);
    setError('');
    const params = {};
    if (keywordVal) params.keyword = keywordVal;
    if (subjectVal) params.subjectId = subjectVal;
    if (gradeVal) params.gradeLevelId = gradeVal;

    try {
      const res = await commonApi.getPublicDocuments(params);
      setDocuments(res.data?.items || []);
      setTotalItems(res.data?.totalItems || res.data?.items?.length || 0);
      if (res.data?.subjects) setSubjects(res.data.subjects);
      if (res.data?.gradeLevels) setGradeLevels(res.data.gradeLevels);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể tải danh sách tài liệu ôn tập.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let active = true;
    async function loadInitial() {
      try {
        const res = await commonApi.getSearchFilters();
        if (active) {
          setSubjects(res.data?.subjects || []);
          setGradeLevels(res.data?.gradeLevels || []);
        }
      } catch {
        // Bỏ qua lỗi tải bộ lọc phụ
      }
      if (active) {
        await fetchDocuments('', '', '');
      }
    }
    loadInitial();
    return () => { active = false; };
  }, [fetchDocuments]);

  const handleSearchSubmit = (e) => {
    e.preventDefault();
    fetchDocuments(search.trim(), subjectId, gradeLevelId);
  };

  const getFileIconClass = (fileName = '') => {
    const ext = fileName.split('.').pop()?.toLowerCase();
    if (ext === 'pdf') return 'bi-filetype-pdf icon-pdf';
    if (ext === 'doc' || ext === 'docx') return 'bi-filetype-docx icon-word';
    if (ext === 'xls' || ext === 'xlsx') return 'bi-filetype-xlsx icon-excel';
    if (ext === 'ppt' || ext === 'pptx') return 'bi-filetype-pptx icon-ppt';
    return 'bi-file-earmark-text icon-file';
  };

  return (
    <div className="learning-doc-shell container py-4">
      {/* Đường dẫn điều hướng (Breadcrumb) */}
      <nav aria-label="breadcrumb" className="mb-3">
        <ol className="breadcrumb mb-0 small">
          <li className="breadcrumb-item"><Link to="/" className="text-decoration-none text-muted">Trang chủ</Link></li>
          <li className="breadcrumb-item text-muted">Môn học</li>
          <li className="breadcrumb-item active fw-semibold text-danger" aria-current="page">Tài liệu ôn tập</li>
        </ol>
      </nav>

      {/* Biểu ngữ giới thiệu kho tài liệu */}
      <div className="doc-hero-section p-4 rounded-4 mb-4 border">
        <div className="row align-items-center">
          <div className="col-lg-8">
            <span className="badge bg-danger-subtle text-danger border border-danger-subtle mb-2 px-2.5 py-1">
              <i className="bi bi-folder-check me-1"></i> Kho tài liệu công khai
            </span>
            <h1 className="h2 fw-bold mb-2 text-body">Tài Liệu Ôn Tập Tuyển Chọn</h1>
            <p className="text-secondary mb-0">
              Tổng hợp các tài liệu ôn thi, đề cương chi tiết và bài giảng tham khảo do đội ngũ gia sư OTSP chia sẻ miễn phí cho cộng đồng học viên.
            </p>
          </div>
          <div className="col-lg-4 text-lg-end mt-3 mt-lg-0">
            <span className="badge bg-white text-dark border p-2 px-3 shadow-sm">
              <i className="bi bi-file-earmark-text me-1 text-danger"></i>
              Tổng cộng: <strong>{totalItems}</strong> tài liệu
            </span>
          </div>
        </div>
      </div>

      {/* Thanh công cụ tìm kiếm và bộ lọc tài liệu */}
      <div className="card border shadow-sm rounded-3 mb-4">
        <div className="card-body p-3">
          <form onSubmit={handleSearchSubmit} className="row g-2 align-items-center">
            <div className="col-12 col-md-4">
              <div className="input-group input-group-sm">
                <span className="input-group-text bg-white border-end-0"><i className="bi bi-search text-muted"></i></span>
                <input
                  type="text"
                  className="form-control border-start-0"
                  placeholder="Tìm kiếm tên tài liệu, chủ đề..."
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </div>
            </div>

            <div className="col-6 col-md-3">
              <select
                className="form-select form-select-sm"
                value={subjectId}
                onChange={(e) => setSubjectId(e.target.value)}
              >
                <option value="">-- Tất cả môn học --</option>
                {subjects.map((s) => (
                  <option key={s.value || s.id} value={s.value || s.id}>
                    {s.text || s.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="col-6 col-md-3">
              <select
                className="form-select form-select-sm"
                value={gradeLevelId}
                onChange={(e) => setGradeLevelId(e.target.value)}
              >
                <option value="">-- Tất cả cấp học --</option>
                {gradeLevels.map((g) => (
                  <option key={g.value || g.id} value={g.value || g.id}>
                    {g.text || g.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="col-12 col-md-2 d-grid">
              <button type="submit" className="btn btn-sm btn-danger fw-semibold" style={{ backgroundColor: '#DB303A' }}>
                Lọc tài liệu
              </button>
            </div>
          </form>
        </div>
      </div>

      {error && <div className="alert alert-danger mb-4" role="alert">{error}</div>}

      {/* Danh sách tài liệu học tập */}
      <div className="card border-0 shadow-sm rounded-4 p-4" style={{ backgroundColor: 'var(--otsp-card-bg, #fff)' }}>
        {loading ? (
          <div className="text-center py-5">
            <div className="spinner-border text-danger" role="status"></div>
          </div>
        ) : documents.length > 0 ? (
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0">
              <thead className="table-light">
                <tr>
                  <th>Tài liệu</th>
                  <th>Gia sư chia sẻ</th>
                  <th>Lớp áp dụng</th>
                  <th>Ngày đăng</th>
                  <th className="text-end">Tải về</th>
                </tr>
              </thead>
              <tbody>
                {documents.map((d) => (
                  <tr key={d.id}>
                    <td>
                      <div className="d-flex align-items-center gap-3">
                        <i className={`bi ${getFileIconClass(d.fileName)} fs-3`}></i>
                        <div>
                          <strong className="d-block">{repairMojibake(d.title)}</strong>
                          <small className="text-muted">{repairMojibake(d.fileName)}</small>
                        </div>
                      </div>
                    </td>
                    <td>
                      <span className="text-secondary">{d.tutorName}</span>
                    </td>
                    <td>
                      <span className="badge bg-danger-subtle text-danger border border-danger-subtle">
                        {d.classroomName || 'Chung'}
                      </span>
                    </td>
                    <td>
                      <small className="text-muted">{new Date(d.uploadedAt).toLocaleDateString('vi-VN')}</small>
                    </td>
                    <td className="text-end">
                      <a
                        href={commonApi.downloadPublicDocumentUrl(d.id)}
                        className="btn btn-sm btn-outline-danger rounded-pill px-3"
                        download
                      >
                        <i className="bi bi-download me-1"></i>Tải về
                      </a>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="text-center py-5 text-secondary">
            <i className="bi bi-folder2-open fs-1 text-muted mb-2 d-block"></i>
            <p className="mb-0">Chưa có tài liệu nào phù hợp với bộ lọc.</p>
          </div>
        )}
      </div>
    </div>
  );
}

