import { useState, useEffect } from 'react';
import tutorApi from '../../api/tutorApi';
import { useToast } from '../../context/toastHooks';
import { useConfirm } from '../../context/confirmHooks';
import OtspPagination from '../../components/common/OtspPagination';

export default function TutorSubjectsPage() {
  const toast = useToast();
  const confirm = useConfirm();
  const [subjectsList, setSubjectsList] = useState([]);
  const [groupBy, setGroupBy] = useState('subject');
  const [allSubjects, setAllSubjects] = useState([]);
  const [allGradeLevels, setAllGradeLevels] = useState([]);
  const [totalItems, setTotalItems] = useState(0);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [reloadKey, setReloadKey] = useState(0);


  const [selectedSubjectId, setSelectedSubjectId] = useState('');
  const [selectedGradeLevelId, setSelectedGradeLevelId] = useState('');
  const [hourlyFee, setHourlyFee] = useState(150000);
  const [adding, setAdding] = useState(false);


  const [editingId, setEditingId] = useState(null);
  const [editFeeValue, setEditFeeValue] = useState(0);
  const [updating, setUpdating] = useState(false);

  useEffect(() => {
    let active = true;
    async function loadSubjects() {
      try {
        setLoading(true);
        const res = await tutorApi.getSubjects({ groupBy, page, pageSize: 15 });
        if (!active) return;
        if (res.data) {
          setSubjectsList(res.data.items || []);
          setTotalItems(res.data.totalItems || 0);
          setTotalPages(res.data.totalPages || 1);
          setPage(res.data.page || 1);
          if (res.data.allSubjects) setAllSubjects(res.data.allSubjects);
          if (res.data.allGradeLevels) setAllGradeLevels(res.data.allGradeLevels);
          if (res.data.allSubjects?.length > 0) {
            setSelectedSubjectId((prev) => prev || res.data.allSubjects[0].id);
          }
          if (res.data.allGradeLevels?.length > 0) {
            setSelectedGradeLevelId((prev) => prev || res.data.allGradeLevels[0].id);
          }
        }
      } catch {
        if (active) setError('Không tải được danh sách môn học.');
      } finally {
        if (active) setLoading(false);
      }
    }
    loadSubjects();
    return () => {
      active = false;
    };
  }, [groupBy, page, reloadKey]);

  const handleAdd = async (e) => {
    e.preventDefault();
    if (!selectedSubjectId || !selectedGradeLevelId || hourlyFee <= 0) {
      setError('Vui lòng chọn môn, cấp học và nhập học phí hợp lệ.');
      return;
    }

    setAdding(true);
    setMessage('');
    setError('');

    try {
      const res = await tutorApi.addSubject({
        subjectId: Number(selectedSubjectId),
        gradeLevelId: Number(selectedGradeLevelId),
        hourlyFee: Number(hourlyFee),
      });
      setMessage(res.data?.message || 'Đã thêm môn dạy thành công.');
      setReloadKey((k) => k + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể thêm môn dạy.');
    } finally {
      setAdding(false);
    }
  };

  const handleStartEdit = (item) => {
    setEditingId(item.id);
    setEditFeeValue(item.hourlyFee);
  };

  const handleSaveEdit = async (id) => {
    if (editFeeValue <= 0) {
      setError('Học phí phải lớn hơn 0 VNĐ.');
      return;
    }

    setUpdating(true);
    setMessage('');
    setError('');

    try {
      const res = await tutorApi.updateSubjectRate(id, Number(editFeeValue));
      setMessage(res.data?.message || 'Đã cập nhật học phí thành công.');
      setEditingId(null);
      setReloadKey((k) => k + 1);
    } catch (err) {
      setError(err.response?.data?.message || 'Không thể cập nhật học phí.');
    } finally {
      setUpdating(false);
    }
  };

  const handleRemove = async (id) => {
    const ok = await confirm({
      title: 'Xóa môn dạy',
      message: 'Bạn có chắc chắn muốn xóa môn và cấp học này khỏi danh sách dạy?',
      confirmText: 'Xóa',
      variant: 'danger',
    });
    if (!ok) return;

    try {
      const res = await tutorApi.removeSubject(id);
      toast.success(res.data?.message || 'Đã xóa môn dạy thành công.');
      setReloadKey((k) => k + 1);
    } catch (err) {
      toast.error(err.response?.data?.message || 'Không thể xóa môn dạy.');
    }
  };

  const formatPrice = (val) => new Intl.NumberFormat('vi-VN').format(val || 0);

  return (
    <div className="container-fluid py-4">
      {/* Tiêu đề trang */}
      <div className="mb-4">
        <h1 className="h3 mb-1">
          {groupBy === 'grade' ? 'Cấp học giảng dạy' : 'Môn học giảng dạy'}
        </h1>
        <p className="text-body-secondary mb-0">
          Mỗi dòng là một môn học và cấp học bạn nhận dạy cùng mức học phí mỗi giờ tương ứng.
        </p>
      </div>

      {/* Thông báo hệ thống */}
      {message && (
        <div className="alert alert-success alert-dismissible fade show" role="alert">
          {message}
          <button type="button" className="btn-close" onClick={() => setMessage('')} aria-label="Đóng"></button>
        </div>
      )}

      {error && (
        <div className="alert alert-warning alert-dismissible fade show" role="alert">
          {error}
          <button type="button" className="btn-close" onClick={() => setError('')} aria-label="Đóng"></button>
        </div>
      )}

      {/* Tab nhóm môn học */}
      <ul className="nav nav-pills gap-2 mb-3" aria-label="Cách xem môn và cấp học">
        <li className="nav-item">
          <button
            type="button"
            className={`nav-link ${groupBy === 'subject' ? 'active' : ''}`}
            onClick={() => { setGroupBy('subject'); setPage(1); }}
          >
            Theo môn học
          </button>
        </li>
        <li className="nav-item">
          <button
            type="button"
            className={`nav-link ${groupBy === 'grade' ? 'active' : ''}`}
            onClick={() => { setGroupBy('grade'); setPage(1); }}
          >
            Theo cấp học
          </button>
        </li>
      </ul>

      {/* Khối biểu mẫu thêm môn học mới */}
      <section className="card border-0 shadow-sm mb-4">
        <div className="card-body">
          <h2 className="h5 mb-3">Thêm môn và học phí</h2>
          <form onSubmit={handleAdd} className="row g-3 align-items-end" id="formAddSubject">
            <div className="col-12 col-md-4">
              <label htmlFor="subjectId" className="form-label">
                Môn học <span className="text-danger">*</span>
              </label>
              <select
                id="subjectId"
                className="form-select"
                value={selectedSubjectId}
                onChange={(e) => setSelectedSubjectId(e.target.value)}
                required
              >
                {allSubjects.map((s) => (
                  <option key={s.id} value={s.id}>{s.name}</option>
                ))}
              </select>
            </div>

            <div className="col-12 col-md-3">
              <label htmlFor="gradeLevelId" className="form-label">
                Cấp học <span className="text-danger">*</span>
              </label>
              <select
                id="gradeLevelId"
                className="form-select"
                value={selectedGradeLevelId}
                onChange={(e) => setSelectedGradeLevelId(e.target.value)}
                required
              >
                {allGradeLevels.map((g) => (
                  <option key={g.id} value={g.id}>{g.name}</option>
                ))}
              </select>
            </div>

            <div className="col-12 col-md-3">
              <label htmlFor="hourlyFee" className="form-label">
                Học phí (VNĐ/giờ) <span className="text-danger">*</span>
              </label>
              <input
                type="number"
                id="hourlyFee"
                className="form-control"
                min={10000}
                step={5000}
                placeholder="VD: 150000"
                value={hourlyFee}
                onChange={(e) => setHourlyFee(e.target.value)}
                required
              />
            </div>

            <div className="col-12 col-md-2 d-grid">
              <button type="submit" className="btn btn-success" disabled={adding}>
                {adding ? 'Đang thêm...' : 'Thêm môn'}
              </button>
            </div>
          </form>
        </div>
      </section>

      {/* Bảng danh sách môn học đã đăng ký */}
      <section className="card border-0 shadow-sm">
        <div className="card-body">
          <div className="d-flex align-items-center justify-content-between gap-2 mb-3">
            <h2 className="h5 mb-0">
              {groupBy === 'grade' ? 'Các môn theo cấp học' : 'Các cấp theo môn học'}
            </h2>
            <span id="pairCount" className="badge text-bg-success">{totalItems}</span>
          </div>

          {loading ? (
            <div className="text-center py-5">
              <div className="spinner-border text-success" role="status"></div>
            </div>
          ) : subjectsList.length === 0 ? (
            <div id="emptyState" className="text-center text-body-secondary py-4">
              <i className="bi bi-journal-plus fs-2 d-block mb-2" aria-hidden="true"></i>
              Bạn chưa đăng ký môn và cấp học giảng dạy.
            </div>
          ) : (
            <>
              <div className="table-responsive">
                <table className="table align-middle mb-0">
                  <thead>
                    <tr>
                      <th scope="col" style={{ width: '8%' }}>#</th>
                      <th scope="col">{groupBy === 'grade' ? 'Cấp học' : 'Môn học'}</th>
                      <th scope="col">{groupBy === 'grade' ? 'Môn học' : 'Cấp học'}</th>
                      <th scope="col" style={{ width: '25%' }}>Học phí (VNĐ/giờ)</th>
                      <th scope="col" style={{ width: '20%', textAlign: 'right' }}>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {subjectsList.map((item, idx) => (
                      <tr key={item.id}>
                        <td className="text-muted small">{(page - 1) * 15 + idx + 1}</td>
                        <td className="fw-semibold">
                          {groupBy === 'grade' ? item.gradeLevelName : item.subjectName}
                        </td>
                        <td>
                          {groupBy === 'grade' ? item.subjectName : item.gradeLevelName}
                        </td>
                        <td>
                          {editingId === item.id ? (
                            <div className="d-flex align-items-center gap-1">
                              <input
                                type="number"
                                className="form-control form-control-sm"
                                value={editFeeValue}
                                min={10000}
                                step={5000}
                                onChange={(e) => setEditFeeValue(e.target.value)}
                                style={{ width: 120 }}
                              />
                              <button
                                type="button"
                                className="btn btn-sm btn-success py-0 px-2"
                                disabled={updating}
                                onClick={() => handleSaveEdit(item.id)}
                              >
                                Lưu
                              </button>
                              <button
                                type="button"
                                className="btn btn-sm btn-outline-secondary py-0 px-2"
                                onClick={() => setEditingId(null)}
                              >
                                Hủy
                              </button>
                            </div>
                          ) : (
                            <span className="fw-bold text-success">
                              {formatPrice(item.hourlyFee)} đ/giờ
                            </span>
                          )}
                        </td>
                        <td style={{ textAlign: 'right' }}>
                          <div className="d-flex justify-content-end align-items-center gap-1">
                            {editingId !== item.id && (
                              <button
                                type="button"
                                className="btn btn-sm btn-outline-primary"
                                onClick={() => handleStartEdit(item)}
                              >
                                Sửa phí
                              </button>
                            )}
                            <button
                              type="button"
                              className="btn btn-sm btn-outline-danger"
                              onClick={() => handleRemove(item.id)}
                            >
                              Xóa
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {totalItems > 0 && (
                <OtspPagination
                  page={page}
                  totalPages={totalPages}
                  total={totalItems}
                  pageSize={15}
                  onChange={setPage}
                  className="mt-4 rounded-3 shadow-sm border"
                />
              )}
            </>
          )}
        </div>
      </section>
    </div>
  );
}
