import { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import tutorApi from '../../api/tutorApi';
import { resolveAssetUrl } from '../../api/config';

const DAY_OPTIONS = [
  { value: 1, label: 'Thứ 2 (Thứ Hai)' },
  { value: 2, label: 'Thứ 3 (Thứ Ba)' },
  { value: 3, label: 'Thứ 4 (Thứ Tư)' },
  { value: 4, label: 'Thứ 5 (Thứ Năm)' },
  { value: 5, label: 'Thứ 6 (Thứ Sáu)' },
  { value: 6, label: 'Thứ 7 (Thứ Bảy)' },
  { value: 0, label: 'Chủ nhật' },
];

const DURATION_OPTIONS = [
  { value: 1.0, label: '1.0 giờ (60 phút)' },
  { value: 1.5, label: '1.5 giờ (90 phút)' },
  { value: 2.0, label: '2.0 giờ (120 phút)' },
  { value: 2.5, label: '2.5 giờ (150 phút)' },
  { value: 3.0, label: '3.0 giờ (180 phút)' },
];

export default function TutorClassroomFormPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const isEdit = Boolean(id);

  const [loading, setLoading] = useState(isEdit);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  // Các trường dữ liệu biểu mẫu lớp học
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [subjectGradeKey, setSubjectGradeKey] = useState('');
  const [subjectGradeOptions, setSubjectGradeOptions] = useState([]);
  const [canChangeSubjectGrade, setCanChangeSubjectGrade] = useState(true);
  const [subjectGradeLabel, setSubjectGradeLabel] = useState('');

  const [fee, setFee] = useState(10000);
  const [canChangeFee, setCanChangeFee] = useState(true);
  const [maxStudents, setMaxStudents] = useState(5);
  const [isOneToOne, setIsOneToOne] = useState(false);

  // Ảnh bìa lớp học
  const [coverMode, setCoverMode] = useState('upload'); // 'upload' | 'url'
  const [coverFile, setCoverFile] = useState(null);
  const [coverUrl, setCoverUrl] = useState('');
  const [currentCoverUrl, setCurrentCoverUrl] = useState('');
  const [removeCover, setRemoveCover] = useState(false);
  const [previewUrl, setPreviewUrl] = useState('');
  const blobUrlRef = useRef(null);
  const redirectTimerRef = useRef(null);

  const revokeBlobPreview = () => {
    if (blobUrlRef.current) {
      URL.revokeObjectURL(blobUrlRef.current);
      blobUrlRef.current = null;
    }
  };

  useEffect(() => () => {
    revokeBlobPreview();
    if (redirectTimerRef.current) clearTimeout(redirectTimerRef.current);
  }, []);

  // Lịch học hàng tuần của lớp
  const [sessionsPerWeek, setSessionsPerWeek] = useState(2);
  const [hoursPerSession, setHoursPerSession] = useState(2.0);
  const [weeklySchedules, setWeeklySchedules] = useState([
    { sessionIndex: 1, dayOfWeek: 5, startTime: '19:00', endTime: '21:00' },
    { sessionIndex: 2, dayOfWeek: 4, startTime: '19:00', endTime: '21:00' },
  ]);

  // Tải danh mục môn học/khối lớp & dữ liệu lớp học hiện tại nếu ở chế độ sửa
  useEffect(() => {
    let isMounted = true;

    async function loadData() {
      try {
        const optRes = await tutorApi.getClassroomOptions();
        if (isMounted) {
          const opts = optRes.data?.subjectGradeOptions || [];
          setSubjectGradeOptions(opts);
          if (!isEdit && opts.length > 0) {
            setSubjectGradeKey(opts[0].key);
          }
        }

        if (isEdit) {
          const classRes = await tutorApi.getClassroom(id);
          if (isMounted && classRes.data) {
            const c = classRes.data;
            setName(c.name || '');
            setDescription(c.description || '');
            setFee(c.fee || 0);
            setMaxStudents(c.maxStudents || 5);
            setIsOneToOne(c.type === 'OneToOne' || c.maxStudents === 1);
            setCanChangeSubjectGrade(c.canChangeSubjectGrade !== false);
            setCanChangeFee(c.canChangeFee !== false);
            setSubjectGradeKey(`${c.subjectId}_${c.gradeLevelId}`);
            setSubjectGradeLabel(`${c.subjectName} · ${c.gradeLevelName}`);

            if (c.coverImageUrl) {
              setCurrentCoverUrl(c.coverImageUrl);
              setPreviewUrl(c.coverImageUrl);
            }

            const spw = c.sessionsPerWeek || (c.weeklySchedules?.length || 2);
            setSessionsPerWeek(spw);
            const hps = c.hoursPerSession || 2.0;
            setHoursPerSession(hps);

            if (c.weeklySchedules && c.weeklySchedules.length > 0) {
              setWeeklySchedules(
                c.weeklySchedules.map((w, idx) => ({
                  sessionIndex: w.sessionIndex || idx + 1,
                  dayOfWeek: typeof w.dayOfWeek === 'number' ? w.dayOfWeek : (DAY_OPTIONS.find(d => d.label.includes(w.dayOfWeekName))?.value ?? 1),
                  startTime: w.startTime?.substring(0, 5) || '19:00',
                  endTime: w.endTime?.substring(0, 5) || '21:00',
                }))
              );
            }
          }
        }
      } catch (err) {
        if (isMounted) {
          setError(err.response?.data?.message || 'Không thể tải thông tin lớp học.');
        }
      } finally {
        if (isMounted) setLoading(false);
      }
    }

    loadData();
    return () => { isMounted = false; };
  }, [id, isEdit]);

  // Điều chỉnh danh sách lịch học khi số buổi/tuần thay đổi
  const handleSessionsPerWeekChange = (newCount) => {
    const count = Number(newCount);
    setSessionsPerWeek(count);
    setWeeklySchedules((prev) => {
      const updated = [...prev];
      if (updated.length < count) {
        const defaultDays = [1, 3, 5, 2, 4];
        for (let i = updated.length; i < count; i++) {
          updated.push({
            sessionIndex: i + 1,
            dayOfWeek: defaultDays[i % defaultDays.length],
            startTime: '19:00',
            endTime: calculateEndTime('19:00', hoursPerSession),
          });
        }
      } else if (updated.length > count) {
        return updated.slice(0, count);
      }
      return updated;
    });
  };

  const calculateEndTime = (startStr, durationH) => {
    if (!startStr) return '21:00';
    const [h, m] = startStr.split(':').map(Number);
    const totalMinutes = (h || 0) * 60 + (m || 0) + Math.round(durationH * 60);
    const endH = Math.floor(totalMinutes / 60) % 24;
    const endM = totalMinutes % 60;
    return `${String(endH).padStart(2, '0')}:${String(endM).padStart(2, '0')}`;
  };

  const handleHoursPerSessionChange = (newHours) => {
    const hours = Number(newHours);
    setHoursPerSession(hours);
    setWeeklySchedules((prev) =>
      prev.map((s) => ({
        ...s,
        endTime: calculateEndTime(s.startTime, hours),
      }))
    );
  };

  const handleScheduleChange = (index, field, value) => {
    setWeeklySchedules((prev) => {
      const updated = [...prev];
      updated[index] = { ...updated[index], [field]: value };
      if (field === 'startTime') {
        updated[index].endTime = calculateEndTime(value, hoursPerSession);
      }
      return updated;
    });
  };

  const handleFileChange = (e) => {
    const file = e.target.files?.[0];
    if (file) {
      if (file.size > 5 * 1024 * 1024) {
        setError('Dung lượng ảnh tối đa 5 MB.');
        return;
      }
      revokeBlobPreview();
      const nextBlobUrl = URL.createObjectURL(file);
      blobUrlRef.current = nextBlobUrl;
      setCoverFile(file);
      setRemoveCover(false);
      setPreviewUrl(nextBlobUrl);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!name.trim()) {
      setError('Vui lòng nhập tên lớp học.');
      return;
    }
    if (!subjectGradeKey) {
      setError('Vui lòng chọn môn học và cấp học.');
      return;
    }

    const [subId, grId] = subjectGradeKey.split('_').map(Number);
    if (!subId || !grId) {
      setError('Môn học và cấp học không hợp lệ.');
      return;
    }

    setSubmitting(true);
    setError('');
    setSuccess('');

    const formData = new FormData();
    formData.append('Name', name.trim());
    formData.append('Description', description.trim());
    formData.append('SubjectId', subId);
    formData.append('GradeLevelId', grId);
    formData.append('Fee', fee);
    formData.append('MaxStudents', isOneToOne ? 1 : maxStudents);
    formData.append('SessionsPerWeek', sessionsPerWeek);
    formData.append('HoursPerSession', hoursPerSession);
    formData.append('Type', isOneToOne ? 'OneToOne' : 'Group');

    if (coverMode === 'upload' && coverFile) {
      formData.append('coverImageFile', coverFile);
    } else if (coverMode === 'url' && coverUrl.trim()) {
      formData.append('CoverImageUrl', coverUrl.trim());
    }

    if (removeCover) {
      formData.append('RemoveCoverImage', 'true');
    }

    // Gửi danh sách lịch học hàng tuần dưới dạng chuỗi JSON
    formData.append('WeeklySchedulesJson', JSON.stringify(weeklySchedules));

    try {
      if (isEdit) {
        const res = await tutorApi.updateClassroom(id, formData);
        setSuccess(res.data?.message || 'Cập nhật thông tin lớp học thành công!');
      } else {
        const res = await tutorApi.createClassroom(formData);
        setSuccess(res.data?.message || 'Đã tạo lớp học nhóm thành công!');
      }
      if (redirectTimerRef.current) clearTimeout(redirectTimerRef.current);
      redirectTimerRef.current = setTimeout(() => navigate('/tutor/classrooms'), 1200);
    } catch (err) {
      setError(err.response?.data?.message || 'Có lỗi xảy ra khi lưu thông tin lớp học.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="container py-5 text-center">
        <div className="spinner-border text-danger" role="status"></div>
        <div className="text-muted mt-2">Đang tải thông tin lớp học...</div>
      </div>
    );
  }

  return (
    <div className="container-fluid py-4" style={{ maxWidth: 960 }}>
      {/* Breadcrumb & Title */}
      <div className="d-flex align-items-center justify-content-between mb-4">
        <div>
          <Link to="/tutor/classrooms" className="text-secondary text-decoration-none small mb-1 d-inline-block">
            <i className="bi bi-arrow-left me-1"></i> Quay lại danh sách lớp học
          </Link>
          <h1 className="h3 fw-bold text-dark mb-1">
            {isEdit ? `Chỉnh sửa lớp học - ${name || `#${id}`}` : 'Tạo lớp nhóm mới'}
          </h1>
          <p className="text-muted small mb-0">
            {isEdit
              ? 'Cập nhật thông tin chi tiết, học phí, ảnh bìa và khung giờ giảng dạy của lớp học.'
              : 'Thiết lập thông tin lớp học nhóm, môn dạy, học phí và lịch học cố định theo tuần.'}
          </p>
        </div>
      </div>

      {/* Alerts */}
      {success && (
        <div className="alert alert-success alert-dismissible fade show" role="alert">
          <i className="bi bi-check-circle me-1"></i> {success}
        </div>
      )}
      {error && (
        <div className="alert alert-danger alert-dismissible fade show" role="alert">
          <i className="bi bi-exclamation-triangle me-1"></i> {error}
        </div>
      )}

      {/* Form Card */}
      <div className="card border-0 shadow-sm rounded-4 p-4 p-md-4.5 bg-white">
        <form onSubmit={handleSubmit}>
          {/* Tên lớp học */}
          <div className="mb-3">
            <label className="form-label fw-semibold text-dark">
              Tên lớp học <span className="text-danger">*</span>
            </label>
            <input
              type="text"
              className="form-control"
              placeholder="Ví dụ: Lớp Toán nâng cao / Lớp Tiếng Anh giao tiếp..."
              value={name}
              onChange={(e) => setName(e.target.value)}
              maxLength={200}
              required
            />
            <div className="form-text small text-muted">Đặt tên ngắn gọn, rõ ràng để học viên dễ nhận biết.</div>
          </div>

          {/* Môn và Cấp học */}
          <div className="mb-3">
            <label className="form-label fw-semibold text-dark">
              Môn học và Cấp học <span className="text-danger">*</span>
            </label>
            {isEdit && !canChangeSubjectGrade ? (
              <div className="form-control-plaintext fw-semibold text-danger">
                <i className="bi bi-book me-1"></i> {subjectGradeLabel}
                <div className="form-text small text-muted">
                  Không thể đổi môn/cấp vì lớp đã có học viên hoặc yêu cầu kết nối.
                </div>
              </div>
            ) : (
              <select
                className="form-select"
                value={subjectGradeKey}
                onChange={(e) => setSubjectGradeKey(e.target.value)}
                required
              >
                <option value="">-- Chọn môn và cấp học --</option>
                {subjectGradeOptions.map((opt) => (
                  <option key={opt.key} value={opt.key}>
                    {opt.label}
                  </option>
                ))}
              </select>
            )}
          </div>

          {/* Khối Ảnh bìa lớp học */}
          <div className="card mb-4 border border-light-subtle shadow-sm rounded-3">
            <div className="card-body p-3">
              <label className="form-label fw-bold text-dark d-flex align-items-center justify-content-between mb-2">
                <span>
                  <i className="bi bi-image text-danger me-1"></i> Ảnh bìa lớp học
                </span>
                <span className="badge bg-light text-secondary border fw-normal" style={{ fontSize: 11 }}>
                  Tùy chọn
                </span>
              </label>

              <div className="row g-3 align-items-center">
                {/* Preview Box */}
                <div className="col-12 col-md-4 text-center">
                  <div
                    className="p-2 bg-light border rounded-3 position-relative d-flex align-items-center justify-content-center"
                    style={{ minHeight: 140, maxHeight: 160, overflow: 'hidden' }}
                  >
                    {previewUrl && !removeCover ? (
                      <img
                        src={resolveAssetUrl(previewUrl)}
                        alt="Xem trước ảnh bìa"
                        className="img-fluid rounded"
                        style={{ maxHeight: 140, width: '100%', objectFit: 'cover' }}
                      />
                    ) : (
                      <div className="text-muted small">
                        <i className="bi bi-card-image fs-1 d-block text-secondary opacity-50 mb-1"></i>
                        <span>Chưa có ảnh bìa</span>
                      </div>
                    )}
                  </div>

                  {isEdit && currentCoverUrl && (
                    <div className="form-check mt-2 text-start">
                      <input
                        className="form-check-input"
                        type="checkbox"
                        id="removeCoverCheck"
                        checked={removeCover}
                        onChange={(e) => {
                          setRemoveCover(e.target.checked);
                          if (e.target.checked) {
                            revokeBlobPreview();
                            setCoverFile(null);
                            setPreviewUrl('');
                          } else {
                            setPreviewUrl(currentCoverUrl);
                          }
                        }}
                      />
                      <label className="form-check-label small text-danger" htmlFor="removeCoverCheck">
                        <i className="bi bi-trash3 me-1"></i> Xóa ảnh bìa hiện tại
                      </label>
                    </div>
                  )}
                </div>

                {/* Upload or URL Controls */}
                <div className="col-12 col-md-8">
                  <div className="btn-group btn-group-sm w-100 mb-2" role="group">
                    <button
                      type="button"
                      className={`btn ${coverMode === 'upload' ? 'btn-danger' : 'btn-outline-danger'}`}
                      onClick={() => setCoverMode('upload')}
                    >
                      <i className="bi bi-upload me-1"></i> Tải ảnh từ máy tính
                    </button>
                    <button
                      type="button"
                      className={`btn ${coverMode === 'url' ? 'btn-danger' : 'btn-outline-danger'}`}
                      onClick={() => {
                        revokeBlobPreview();
                        setCoverFile(null);
                        setCoverMode('url');
                      }}
                    >
                      <i className="bi bi-link-45deg me-1"></i> Dán liên kết ảnh (URL)
                    </button>
                  </div>

                  {coverMode === 'upload' ? (
                    <div>
                      <input
                        className="form-control form-control-sm"
                        type="file"
                        accept=".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp"
                        onChange={handleFileChange}
                      />
                      <div className="form-text small text-muted mt-1" style={{ fontSize: 11 }}>
                        <i className="bi bi-info-circle me-1"></i> Định dạng JPG, PNG hoặc WebP; dung lượng tối đa 5 MB.
                      </div>
                    </div>
                  ) : (
                    <div>
                      <div className="input-group input-group-sm">
                        <span className="input-group-text"><i className="bi bi-link"></i></span>
                        <input
                          type="url"
                          className="form-control"
                          placeholder="https://example.com/anh-lop.jpg"
                          value={coverUrl}
                          onChange={(e) => {
                            setCoverUrl(e.target.value);
                            setPreviewUrl(e.target.value);
                          }}
                          maxLength={500}
                        />
                      </div>
                      <div className="form-text small text-muted mt-1" style={{ fontSize: 11 }}>
                        <i className="bi bi-info-circle me-1"></i> Nhập đường dẫn trực tiếp tới ảnh (bắt đầu bằng http:// hoặc https://).
                      </div>
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>

          {/* Học phí & Sĩ số */}
          <div className="row g-3 mb-4">
            <div className="col-12 col-md-6">
              <label className="form-label fw-semibold text-dark">
                Học phí (VNĐ) <span className="text-danger">*</span>
              </label>
              <input
                type="number"
                className="form-control"
                min="1000"
                step="1000"
                value={fee}
                onChange={(e) => setFee(Number(e.target.value))}
                disabled={isEdit && !canChangeFee}
                required
              />
              {isEdit && !canChangeFee && (
                <div className="form-text small text-muted">
                  Không thể sửa học phí vì lớp đã có thanh toán thành công.
                </div>
              )}
            </div>

            <div className="col-12 col-md-6">
              <label className="form-label fw-semibold text-dark">
                Sĩ số tối đa <span className="text-danger">*</span>
              </label>
              {isOneToOne ? (
                <div className="form-control bg-light d-flex align-items-center justify-content-between border">
                  <span className="text-dark fw-bold"><i className="bi bi-person me-1 text-danger"></i>1 học viên</span>
                  <span className="badge bg-warning-subtle text-dark border border-warning-subtle">
                    <i className="bi bi-lock-fill me-1"></i>Lớp 1 kèm 1
                  </span>
                </div>
              ) : (
                <input
                  type="number"
                  className="form-control"
                  min="2"
                  max="50"
                  value={maxStudents}
                  onChange={(e) => setMaxStudents(Number(e.target.value))}
                  required
                />
              )}
              <div className="form-text small text-muted">Sĩ số lớp nhóm từ 2 đến 50 học viên.</div>
            </div>
          </div>

          {/* Cấu hình lịch học các buổi trong tuần */}
          <div className="card bg-light border-0 mb-4 p-3 rounded-3">
            <div className="d-flex align-items-center justify-content-between mb-3 border-bottom pb-2">
              <div>
                <h5 className="h6 mb-0 fw-bold text-danger">
                  <i className="bi bi-calendar-week me-1"></i> Thiết lập lịch học cố định theo buổi / tuần
                </h5>
                <small className="text-muted">Quy định số buổi học trong tuần, thứ và thời gian từng buổi</small>
              </div>
              <span className={`badge ${isOneToOne ? 'bg-warning-subtle text-dark border' : 'bg-danger-subtle text-danger border'}`}>
                {isOneToOne ? 'Lớp 1 kèm 1' : 'Lớp nhóm cố định'}
              </span>
            </div>

            <div className="row g-3 mb-3">
              <div className="col-12 col-md-6">
                <label className="form-label fw-semibold">Số buổi học / tuần <span className="text-danger">*</span></label>
                <select
                  className="form-select"
                  value={sessionsPerWeek}
                  onChange={(e) => handleSessionsPerWeekChange(e.target.value)}
                >
                  <option value="1">1 buổi / tuần</option>
                  <option value="2">2 buổi / tuần</option>
                  <option value="3">3 buổi / tuần</option>
                  {!isOneToOne && <option value="4">4 buổi / tuần</option>}
                  {!isOneToOne && <option value="5">5 buổi / tuần</option>}
                </select>
              </div>

              <div className="col-12 col-md-6">
                <label className="form-label fw-semibold">Thời lượng mỗi buổi (tiếng)</label>
                <select
                  className="form-select"
                  value={hoursPerSession}
                  onChange={(e) => handleHoursPerSessionChange(e.target.value)}
                >
                  {DURATION_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
                <div className="form-text small text-danger">Chuẩn 2.0 giờ / buổi.</div>
              </div>
            </div>

            {/* Chi tiết từng buổi */}
            <label className="form-label fw-semibold mb-2">Chi tiết khung giờ từng buổi trong tuần:</label>
            <div className="d-flex flex-column gap-2">
              {weeklySchedules.map((schedule, idx) => (
                <div key={idx} className="p-3 border rounded-3 bg-white shadow-sm">
                  <div className="d-flex align-items-center justify-content-between mb-2">
                    <span className="badge bg-danger-subtle text-danger fw-bold">Buổi {idx + 1}</span>
                    <span className="small text-muted">{hoursPerSession} giờ</span>
                  </div>

                  <div className="row g-2">
                    <div className="col-12 col-sm-4">
                      <label className="form-label small text-muted mb-1">Thứ trong tuần</label>
                      <select
                        className="form-select form-select-sm"
                        value={schedule.dayOfWeek}
                        onChange={(e) => handleScheduleChange(idx, 'dayOfWeek', Number(e.target.value))}
                      >
                        {DAY_OPTIONS.map((d) => (
                          <option key={d.value} value={d.value}>
                            {d.label}
                          </option>
                        ))}
                      </select>
                    </div>

                    <div className="col-6 col-sm-4">
                      <label className="form-label small text-muted mb-1">Giờ bắt đầu</label>
                      <input
                        type="time"
                        className="form-control form-control-sm"
                        value={schedule.startTime}
                        onChange={(e) => handleScheduleChange(idx, 'startTime', e.target.value)}
                        required
                      />
                    </div>

                    <div className="col-6 col-sm-4">
                      <label className="form-label small text-muted mb-1">Giờ kết thúc</label>
                      <input
                        type="time"
                        className="form-control form-control-sm bg-light"
                        value={schedule.endTime}
                        onChange={(e) => handleScheduleChange(idx, 'endTime', e.target.value)}
                        required
                      />
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Mô tả lớp học */}
          <div className="mb-4">
            <label className="form-label fw-semibold text-dark">Mô tả / Ghi chú lớp học</label>
            <textarea
              className="form-control"
              rows={3}
              placeholder="Giới thiệu nội dung khóa học, mục tiêu, lộ trình học tập..."
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            ></textarea>
          </div>

          {/* Action Buttons */}
          <div className="d-flex align-items-center justify-content-end gap-2 pt-3 border-top">
            <Link to="/tutor/classrooms" className="btn btn-outline-secondary px-4 py-2">
              Hủy bỏ
            </Link>
            <button
              type="submit"
              className="btn btn-danger px-4 py-2 fw-semibold"
              disabled={submitting}
            >
              {submitting ? (
                <>
                  <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                  Đang lưu...
                </>
              ) : isEdit ? (
                <>
                  <i className="bi bi-check2 me-1"></i> Lưu thay đổi
                </>
              ) : (
                <>
                  <i className="bi bi-plus-lg me-1"></i> Tạo lớp học
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

