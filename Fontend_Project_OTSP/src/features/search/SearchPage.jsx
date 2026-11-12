import { useState, useEffect } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { resolveAssetUrl } from '../../api/config';
import commonApi from '../../api/commonApi';
import studentApi from '../../api/studentApi';
import ClassDetailModal from '../../components/classroom/ClassDetailModal';
import OtspPagination from '../../components/common/OtspPagination';

export default function SearchPage() {
  const { user, isTutor, isStudent } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();

  const [tab, setTab] = useState(() => (searchParams.get('tab') === 'classes' ? 'classes' : 'tutors'));
  const [keyword, setKeyword] = useState(() => searchParams.get('keyword') || '');
  const [subjectId, setSubjectId] = useState(() => searchParams.get('subjectId') || '');
  const [gradeLevelId, setGradeLevelId] = useState(() => searchParams.get('gradeLevelId') || '');
  const [format, setFormat] = useState(() => searchParams.get('format') || '');
  const [classType, setClassType] = useState(() => searchParams.get('classType') || '');
  const [maxFee, setMaxFee] = useState(() => searchParams.get('maxFee') || '');
  const [page, setPage] = useState(() => Number(searchParams.get('page')) || 1);
  const [pageSize] = useState(12);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [searchTrigger, setSearchTrigger] = useState(0);

  const currentUrlSearch = searchParams.toString();
  const [prevUrlSearch, setPrevUrlSearch] = useState(currentUrlSearch);
  if (prevUrlSearch !== currentUrlSearch) {
    setPrevUrlSearch(currentUrlSearch);
    const urlTab = searchParams.get('tab') === 'classes' ? 'classes' : 'tutors';
    setTab(urlTab);
    setKeyword(searchParams.get('keyword') || '');
    setSubjectId(searchParams.get('subjectId') || '');
    setGradeLevelId(searchParams.get('gradeLevelId') || '');
    setFormat(searchParams.get('format') || '');
    setClassType(searchParams.get('classType') || '');
    setMaxFee(searchParams.get('maxFee') || '');
    setPage(Number(searchParams.get('page')) || 1);
    setSearchTrigger((t) => t + 1);
  }

  const [subjects, setSubjects] = useState([]);
  const [gradeLevels, setGradeLevels] = useState([]);
  const [results, setResults] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  // Lớp học được chọn để xem chi tiết & mở modal đăng ký học
  const [selectedClassForModal, setSelectedClassForModal] = useState(null);

  // Danh sách ID lớp học viên đang học hoặc đã gửi yêu cầu chờ duyệt (tránh đăng ký trùng)
  const [enrolledClassroomIds, setEnrolledClassroomIds] = useState([]);
  const [pendingClassroomIds, setPendingClassroomIds] = useState([]);

  // Tải danh mục bộ lọc (môn học, khối lớp)
  useEffect(() => {
    commonApi.getSearchFilters()
      .then((res) => {
        if (res.data) {
          setSubjects(res.data.subjects || []);
          setGradeLevels(res.data.gradeLevels || []);
        }
      })
      .catch((err) => console.warn('Không thể tải bộ lọc tìm kiếm', err));
  }, []);

  // Lấy danh sách lớp đã tham gia/chờ duyệt nếu học viên đã đăng nhập
  useEffect(() => {
    if (user && (user.role === 'Student_Parent' || user.role === 'Student')) {
      let active = true;
      studentApi.getClassrooms('Active', 1, 100)
        .then((res) => {
          if (!active) return;
          const ids = (res.data?.items || []).map((c) => c.classroomId);
          setEnrolledClassroomIds(ids);
        })
        .catch(() => { });

      studentApi.getMyConnectionRequests('Pending', 1, 100)
        .then((res) => {
          if (!active) return;
          const ids = (res.data?.items || []).filter((r) => r.classroomId).map((r) => r.classroomId);
          setPendingClassroomIds(ids);
        })
        .catch(() => { });
      return () => { active = false; };
    }
    return undefined;
  }, [user]);

  // Tự động gọi API tìm kiếm mỗi khi thay đổi bộ lọc, tab, từ khóa hoặc trang
  useEffect(() => {
    let active = true;
    async function runSearch() {
      setLoading(true);
      setError('');

      const params = {
        keyword: keyword || undefined,
        subjectId: subjectId || undefined,
        gradeLevelId: gradeLevelId || undefined,
        format: tab === 'tutors' && format ? format : undefined,
        classType: tab === 'classes' && classType ? classType : undefined,
        maxFee: maxFee || undefined,
        page: page || 1,
        pageSize: pageSize || 12
      };

      try {
        const res = await (tab === 'tutors' ? commonApi.searchTutors(params) : commonApi.searchClasses(params));
        if (active) {
          const data = res.data;
          const list = Array.isArray(data) ? data : (data?.items || []);
          setResults(list);
          const totalCount = data?.total ?? list.length;
          setTotal(totalCount);
          const computedTotalPages = data?.totalPages ?? Math.max(1, Math.ceil(totalCount / (pageSize || 12)));
          setTotalPages(computedTotalPages);
        }
      } catch (err) {
        if (active) {
          console.error('Search error', err);
          setError('Không tải được kết quả tìm kiếm. Vui lòng thử lại.');
        }
      } finally {
        if (active) setLoading(false);
      }
    }

    runSearch();
    return () => { active = false; };
  }, [tab, searchTrigger, classType, format, gradeLevelId, keyword, maxFee, subjectId, page, pageSize]);

  const handleFilterSubmit = (e) => {
    e.preventDefault();
    setPage(1);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set('page', '1');
      return next;
    });
    setSearchTrigger((t) => t + 1);
  };

  const handleClear = () => {
    setKeyword('');
    setSubjectId('');
    setGradeLevelId('');
    setFormat('');
    setClassType('');
    setMaxFee('');
    setPage(1);
    setSearchParams({});
    setSearchTrigger((t) => t + 1);
  };

  const changeTab = (newTab) => {
    setTab(newTab);
    setPage(1);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      if (newTab === 'classes') next.set('tab', 'classes');
      else next.delete('tab');
      next.delete('page');
      return next;
    });
  };

  const handlePageChange = (newPage) => {
    if (newPage < 1 || newPage > totalPages || newPage === page) return;
    setPage(newPage);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set('page', String(newPage));
      return next;
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };


  const formatPrice = (val) => new Intl.NumberFormat('vi-VN').format(val || 0);

  return (
    <section className="container py-4">
      <h1 className="otsp-title mb-3">Tìm gia sư &amp; lớp học</h1>

      {/* Bộ lọc tìm kiếm */}
      <form onSubmit={handleFilterSubmit} className="otsp-filter mb-4 p-3 rounded-4 border bg-light shadow-sm">
        <div className="row g-2 g-md-3 align-items-end">
          <div className="col-12 col-md-6 col-xl-3">
            <label className="form-label small mb-1 fw-semibold" htmlFor="keyword">Tên hoặc môn</label>
            <input
              id="keyword"
              type="search"
              className="form-control"
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              placeholder="Ví dụ: Minh Anh, Toán..."
            />
          </div>

          <div className="col-6 col-md-3 col-xl-2">
            <label className="form-label small mb-1 fw-semibold" htmlFor="subjectId">Môn học</label>
            <select
              id="subjectId"
              className="form-select"
              value={subjectId}
              onChange={(e) => setSubjectId(e.target.value)}
            >
              <option value="">Tất cả</option>
              {subjects.map((s, idx) => (
                <option key={`subj-${s.id ?? s.value ?? idx}`} value={s.value ?? s.id ?? ''}>
                  {s.text || s.name}
                </option>
              ))}
            </select>
          </div>

          <div className="col-6 col-md-3 col-xl-2">
            <label className="form-label small mb-1 fw-semibold" htmlFor="gradeLevelId">Cấp học</label>
            <select
              id="gradeLevelId"
              className="form-select"
              value={gradeLevelId}
              onChange={(e) => setGradeLevelId(e.target.value)}
            >
              <option value="">Tất cả</option>
              {gradeLevels.map((g, idx) => (
                <option key={`grade-${g.id ?? g.value ?? idx}`} value={g.value ?? g.id ?? ''}>
                  {g.text || g.name}
                </option>
              ))}
            </select>
          </div>

          {tab === 'tutors' ? (
            <div className="col-6 col-md-3 col-xl-2">
              <label className="form-label small mb-1 fw-semibold" htmlFor="format">Hình thức</label>
              <select
                id="format"
                className="form-select"
                value={format}
                onChange={(e) => setFormat(e.target.value)}
              >
                <option value="">Tất cả</option>
                <option value="Online">Trực tuyến</option>
                <option value="Offline">Trực tiếp</option>
              </select>
            </div>
          ) : (
            <div className="col-6 col-md-3 col-xl-2">
              <label className="form-label small mb-1 fw-semibold" htmlFor="classType">Loại lớp</label>
              <select
                id="classType"
                className="form-select"
                value={classType}
                onChange={(e) => setClassType(e.target.value)}
              >
                <option value="">Tất cả</option>
                <option value="OneToOne">1 kèm 1</option>
                <option value="Group">Nhóm</option>
              </select>
            </div>
          )}

          <div className="col-6 col-md-3 col-xl-2">
            <label className="form-label small mb-1 fw-semibold" htmlFor="maxFee">Học phí tối đa</label>
            <select
              id="maxFee"
              className="form-select"
              value={maxFee}
              onChange={(e) => setMaxFee(e.target.value)}
            >
              <option value="">Không giới hạn</option>
              <option value="150000">≤ 150.000đ</option>
              <option value="200000">≤ 200.000đ</option>
              <option value="300000">≤ 300.000đ</option>
              <option value="500000">≤ 500.000đ</option>
            </select>
          </div>

          <div className="col-12 col-xl-1 d-grid">
            <button type="submit" className="btn btn-danger fw-semibold" style={{ backgroundColor: '#DB303A' }}>
              <i className="bi bi-filter me-1"></i>Lọc
            </button>
          </div>
        </div>

        <div className="text-end mt-2">
          <button type="button" onClick={handleClear} className="btn btn-sm btn-outline-secondary">
            <i className="bi bi-arrow-counterclockwise me-1"></i>Xóa lọc
          </button>
        </div>
      </form>

      {/* Tab chuyển đổi Gia sư / Lớp học phong cách nút đỏ OTSP */}
      <div className="d-flex align-items-center gap-2 mb-4 p-1.5 bg-light rounded-pill border d-inline-flex shadow-xs">
        <button
          type="button"
          className={`btn btn-sm rounded-pill px-4 py-2 fw-semibold transition-all ${tab === 'tutors'
            ? 'btn-danger text-white shadow-sm'
            : 'btn-light text-secondary border-0 bg-transparent'
            }`}
          style={tab === 'tutors' ? { backgroundColor: '#DB303A', borderColor: '#DB303A' } : {}}
          onClick={() => changeTab('tutors')}
        >
          <i className="bi bi-person-workspace me-2"></i>Gia sư
        </button>
        <button
          type="button"
          className={`btn btn-sm rounded-pill px-4 py-2 fw-semibold transition-all ${tab === 'classes'
            ? 'btn-danger text-white shadow-sm'
            : 'btn-light text-secondary border-0 bg-transparent'
            }`}
          style={tab === 'classes' ? { backgroundColor: '#DB303A', borderColor: '#DB303A' } : {}}
          onClick={() => changeTab('classes')}
        >
          <i className="bi bi-easel me-2"></i>Lớp học
        </button>
      </div>

      {/* Số lượng kết quả tìm thấy */}
      <div className="mb-3 d-flex justify-content-between align-items-center">
        <span className="small text-secondary">
          {loading ? 'Đang tìm kiếm...' : `Tìm thấy ${total} kết quả phù hợp (Trang ${page}/${totalPages})`}
        </span>
      </div>

      {/* Thông báo lỗi nếu có */}
      {error && <div className="alert alert-danger py-2 small">{error}</div>}

      {/* Khung tải dữ liệu giả lập (Loading Skeletons) */}
      {loading && (
        <div className="row g-4">
          {[1, 2, 3, 4, 5, 6].map((n) => (
            <div key={n} className="col-md-6 col-lg-4">
              <div className="card p-3 border rounded-3 placeholder-glow">
                <div className="d-flex gap-3 mb-3">
                  <div className="placeholder rounded-circle" style={{ width: 50, height: 50 }}></div>
                  <div className="flex-grow-1">
                    <div className="placeholder col-8 mb-2"></div>
                    <div className="placeholder col-5"></div>
                  </div>
                </div>
                <div className="placeholder col-12 mb-2" style={{ height: 40 }}></div>
                <div className="placeholder col-6"></div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Lưới hiển thị kết quả */}
      {!loading && results.length > 0 && (
        <div className="row g-4">
          {tab === 'tutors' ? (
            results.map((t, idx) => (
              <div key={t.id || idx} className="col-md-6 col-lg-4">
                <div className="card h-100 border rounded-4 shadow-sm p-3 d-flex flex-column" style={{ backgroundColor: 'var(--otsp-card-bg, #fff)' }}>
                  <div className="d-flex align-items-center gap-3 mb-3">
                    <img
                      src={resolveAssetUrl(t.avatarUrl) || '/assets/img/default-avatar.svg'}
                      alt={t.fullName}
                      className="rounded-circle border"
                      style={{ width: 60, height: 60, objectFit: 'cover' }}
                      onError={(e) => { e.target.src = '/assets/img/default-avatar.svg'; }}
                    />
                    <div>
                      <h5 className="fw-bold mb-1 text-truncate" style={{ maxWidth: 200 }}>{t.fullName}</h5>
                      <div className="d-flex align-items-center gap-2 small">
                        {t.averageRating > 0 ? (
                          <span className="text-warning fw-semibold">
                            <i className="bi bi-star-fill me-1"></i>{t.averageRating.toFixed(1)}
                          </span>
                        ) : (
                          <span className="badge bg-secondary-subtle text-secondary">Mới</span>
                        )}
                        <span className="text-secondary">•</span>
                        <span className="text-secondary">{t.area || 'Toàn quốc'}</span>
                      </div>
                    </div>
                  </div>

                  {t.introduction ? (
                    <p className="text-secondary small mb-3 flex-grow-1 line-clamp-3">
                      {t.introduction}
                    </p>
                  ) : (
                    <div className="flex-grow-1 mb-3" />
                  )}

                  {/* Huy hiệu các môn giảng dạy */}
                  <div className="d-flex flex-wrap gap-1 mb-3">
                    {t.teaching && t.teaching.slice(0, 3).map((item, i) => (
                      <span key={i} className="badge bg-danger-subtle text-danger border border-danger-subtle">
                        {item.subjectName} · {item.gradeName}
                      </span>
                    ))}
                    {t.teaching && t.teaching.length > 3 && (
                      <span className="badge bg-light text-secondary border">+{t.teaching.length - 3} môn khác</span>
                    )}
                  </div>

                  <div className="d-flex justify-content-between align-items-center pt-3 border-top mt-auto">
                    <div>
                      <small className="text-muted d-block">Học phí</small>
                      <strong className="text-danger fs-5">{formatPrice(t.hourlyFee)} đ<small className="text-muted fw-normal fs-7">/giờ</small></strong>
                    </div>
                    <div className="d-flex gap-2">
                      <Link
                        to={isTutor ? `/tutor/chat?contactId=${t.userId || t.id}` : isStudent ? `/student/chat?contactId=${t.userId || t.id}` : `/chat?contactId=${t.userId || t.id}`}
                        className="btn btn-sm btn-outline-secondary rounded-pill px-2.5 py-1"
                        title="Nhắn tin cho gia sư"
                      >
                        <i className="bi bi-chat-dots"></i>
                      </Link>
                      <Link
                        to={isStudent ? `/student/booking/${t.id}` : `/tutors/${t.id}`}
                        className="btn btn-sm btn-danger rounded-pill px-3 py-1 fw-semibold text-white"
                        style={{ backgroundColor: '#DB303A' }}
                      >
                        Xem hồ sơ
                      </Link>
                    </div>
                  </div>
                </div>
              </div>
            ))
          ) : (
            results.map((c, idx) => {
              const isEnrolled = enrolledClassroomIds.includes(c.id);
              const isPending = pendingClassroomIds.includes(c.id);
              const isFull = Boolean(c.maxStudents && (c.studentCount || 0) >= c.maxStudents);

              return (
                <div key={c.id || idx} className="col-md-6 col-lg-4">
                  <div className="card h-100 border rounded-4 shadow-sm p-3 d-flex flex-column" style={{ backgroundColor: 'var(--otsp-card-bg, #fff)' }}>
                    <div className="d-flex justify-content-between align-items-start mb-2 flex-wrap gap-1">
                      <span className="badge bg-danger-subtle text-danger border border-danger-subtle">
                        {c.subjectName} · {c.gradeName}
                      </span>
                      <div className="d-flex gap-1">
                        {isEnrolled && (
                          <span className="badge bg-success text-white">
                            <i className="bi bi-check-circle me-1"></i>Đang học
                          </span>
                        )}
                        {isPending && !isEnrolled && (
                          <span className="badge bg-warning text-dark">
                            <i className="bi bi-hourglass-split me-1"></i>Chờ duyệt
                          </span>
                        )}
                        <span className="badge bg-secondary-subtle text-secondary">
                          {c.type === 'Group' || c.type === 1 || !c.type ? 'Lớp nhóm' : '1 kèm 1'}
                        </span>
                      </div>
                    </div>

                    <h5 className="fw-bold mb-2">{c.name}</h5>
                    {c.description ? (
                      <p className="text-secondary small mb-2 flex-grow-1 line-clamp-2">
                        {c.description}
                      </p>
                    ) : (
                      <div className="flex-grow-1 mb-2" />
                    )}

                    {/* Xem trước lịch học dự kiến nếu có */}
                    {c.weeklySchedules && c.weeklySchedules.length > 0 && (
                      <div className="small text-muted mb-2 d-flex align-items-center gap-1.5 flex-wrap">
                        <i className="bi bi-calendar-event text-danger"></i>
                        <span>{c.weeklySchedules.map((ws) => `${ws.dayOfWeekName} (${ws.timeRange})`).join(', ')}</span>
                      </div>
                    )}

                    <div className="d-flex align-items-center gap-2 mb-3 text-secondary small">
                      <i className="bi bi-person-circle"></i>
                      <span>Gia sư: <strong>{c.tutorName}</strong></span>
                    </div>

                    <div className="d-flex justify-content-between align-items-center pt-3 border-top mt-auto">
                      <div>
                        <small className="text-muted d-block">Học phí khóa học</small>
                        <strong className="text-danger fs-5">{formatPrice(c.fee)} đ</strong>
                      </div>
                      <div className="d-flex align-items-center gap-1.5">
                        <div className="text-muted small me-1">
                          <i className="bi bi-people me-1"></i>
                          <span>{c.studentCount || 0}{c.maxStudents ? `/${c.maxStudents}` : ''}</span>
                        </div>
                        <button
                          type="button"
                          className="btn btn-sm btn-outline-secondary rounded-pill px-2.5 py-1 fw-semibold"
                          onClick={() => setSelectedClassForModal(c)}
                        >
                          Chi tiết
                        </button>
                        {isEnrolled ? (
                          <button
                            type="button"
                            className="btn btn-sm btn-success rounded-pill px-2.5 py-1 fw-semibold"
                            disabled
                          >
                            <i className="bi bi-check-circle me-1"></i>Đang học
                          </button>
                        ) : isPending ? (
                          <button
                            type="button"
                            className="btn btn-sm btn-warning rounded-pill px-2.5 py-1 fw-semibold"
                            disabled
                          >
                            <i className="bi bi-hourglass-split me-1"></i>Chờ duyệt
                          </button>
                        ) : isFull ? (
                          <button
                            type="button"
                            className="btn btn-sm btn-secondary rounded-pill px-2.5 py-1 fw-semibold"
                            disabled
                          >
                            Đã đủ
                          </button>
                        ) : (
                          <button
                            type="button"
                            className="btn btn-sm btn-danger rounded-pill px-3 py-1 fw-semibold text-white shadow-xs"
                            style={{ backgroundColor: '#DB303A' }}
                            onClick={() => setSelectedClassForModal(c)}
                          >
                            Đăng ký
                          </button>
                        )}
                        <Link
                          to={`/student/booking/${c.tutorId}`}
                          className="btn btn-sm btn-light border rounded-pill px-2 py-1 text-secondary"
                          title="Xem thông tin gia sư"
                        >
                          <i className="bi bi-person"></i>
                        </Link>
                      </div>
                    </div>
                  </div>
                </div>
              );
            })
          )}
        </div>
      )}

      {/* Phân trang kết quả tìm kiếm */}
      {!loading && total > 0 && (
        <OtspPagination
          page={page}
          totalPages={totalPages}
          total={total}
          pageSize={pageSize || 12}
          onChange={handlePageChange}
          className="mt-5 pt-3 border-top"
        />
      )}

      {/* Trạng thái không tìm thấy kết quả */}
      {!loading && results.length === 0 && (
        <div className="text-center py-5">
          <i className="bi bi-search fs-1 text-secondary mb-3 d-block"></i>
          <h5 className="fw-bold text-secondary">Không tìm thấy kết quả phù hợp</h5>
          <p className="text-muted small">Thử xóa bớt bộ lọc hoặc tìm kiếm với từ khóa khác.</p>
          <button type="button" onClick={handleClear} className="btn btn-outline-danger btn-sm rounded-pill px-4">
            Xóa bộ lọc
          </button>
        </div>
      )}

      {/* Modal xem chi tiết và đăng ký lớp học */}
      {selectedClassForModal && (
        <ClassDetailModal
          classId={selectedClassForModal.id}
          initialData={selectedClassForModal}
          onClose={() => setSelectedClassForModal(null)}
          onRegistered={(cid) => {
            setPendingClassroomIds((prev) => [...prev, cid]);
          }}
        />
      )}
    </section>
  );
}
