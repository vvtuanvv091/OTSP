import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import commonApi, { getCachedHomeData } from '../../api/commonApi';
import { resolveAssetUrl } from '../../api/config';
import AnimatedCounter from '../../components/common/AnimatedCounter';
import ClassDetailModal from '../../components/classroom/ClassDetailModal';

const FEATURE_COLORS = [
  '#ffbb2c', '#5578ff', '#e80368', '#e361ff',
  '#47aeff', '#ffa76e', '#11dbcf', '#4233ff',
  '#b2904f', '#b20969', '#ff5828', '#DB303A'
];

export default function HomePage() {
  const { isStudent } = useAuth();
  const cached = getCachedHomeData();
  const [data, setData] = useState(() => cached || {
    studentCount: 0,
    tutorCount: 0,
    subjectCount: 0,
    completedSessionCount: 0,
    subjects: [],
    classes: [],
    featuredTutors: []
  });
  const [loading, setLoading] = useState(!cached);
  const [selectedClassForModal, setSelectedClassForModal] = useState(null);

  useEffect(() => {
    let active = true;
    commonApi.getHomeData()
      .then((res) => {
        if (active && res.data) {
          setData(res.data);
        }
      })
      .catch((err) => {
        console.warn('Could not fetch home data:', err);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => { active = false; };
  }, []);

  const formatPrice = (price) => {
    return new Intl.NumberFormat('vi-VN').format(price || 0);
  };

  return (
    <div className="main otsp-home">
      {/* ==================== 1. KHỐI HERO GIỚI THIỆU CHÍNH ==================== */}
      <section id="hero" className="hero section dark-background otsp-hero">
        <img
          src="/assets/img/hero-bg.jpg"
          alt="Học tập cùng gia sư OTSP"
          width="1920"
          height="1080"
          fetchPriority="high"
          decoding="async"
        />
        <div className="container">
          <div className="otsp-hero-content">
            <h1 className="otsp-hero-title">
              Học đúng người,<br />
              <span className="otsp-text-gradient">giỏi đúng cách.</span>
            </h1>
            <p>
              Kết nối với đội ngũ gia sư tài năng được kiểm duyệt bằng cấp nghiêm ngặt. Lộ trình cá nhân hóa,
              lịch học linh hoạt và học thử trải nghiệm miễn phí.
            </p>
            <div className="otsp-hero-actions">
              <a href="#courses" className="btn-hero-primary">
                <span>Bắt đầu ngay</span>
                <i className="bi bi-arrow-right ms-2" aria-hidden="true"></i>
              </a>
              <Link to="/register" className="btn-hero-secondary">
                <i className="bi bi-person-plus-fill me-2" aria-hidden="true"></i>
                <span>Đăng ký làm gia sư</span>
              </Link>
            </div>
            <div className="otsp-hero-trust">
              <span><i className="bi bi-patch-check-fill text-danger me-1"></i> 100% Hồ sơ xác minh</span>
              <span><i className="bi bi-calendar2-check-fill text-danger me-1"></i> Lịch học linh hoạt 24/7</span>
              <span><i className="bi bi-shield-lock-fill text-danger me-1"></i> Học phí minh bạch</span>
            </div>
          </div>
        </div>
      </section>

      {/* ==================== 2. KHỐI GIỚI THIỆU VỀ OTSP ==================== */}
      <section id="about" className="section about">
        <div className="container">
          <div className="row gy-4 align-items-center">
            <div className="col-lg-6 order-1 order-lg-2">
              <div
                className="rounded-4 shadow overflow-hidden"
                style={{ aspectRatio: '4 / 3', width: '100%', backgroundColor: '#f1f5f9' }}
              >
                <img
                  src="/assets/img/about.jpg"
                  className="w-100 h-100"
                  style={{ objectFit: 'cover', display: 'block' }}
                  width="800"
                  height="600"
                  alt="Về OTSP"
                  loading="lazy"
                  decoding="async"
                />
              </div>
            </div>
            <div className="col-lg-6 order-2 order-lg-1 content">
              <h3>Nền tảng kết nối gia sư uy tín &amp; chuẩn hóa hàng đầu</h3>
              <p className="fst-italic text-muted">
                OTSP xây dựng quy trình kết nối chuẩn hóa giữa gia sư và học viên, đảm bảo chất lượng giảng dạy
                và sự an tâm tuyệt đối cho phụ huynh học sinh.
              </p>
              <ul className="list-unstyled">
                <li className="d-flex align-items-start gap-2 mb-2">
                  <i className="bi bi-check-circle-fill text-danger fs-5"></i>
                  <span>100% hồ sơ gia sư được thẩm định bằng cấp đại học và chứng chỉ nghiệp vụ sư phạm.</span>
                </li>
                <li className="d-flex align-items-start gap-2 mb-2">
                  <i className="bi bi-check-circle-fill text-danger fs-5"></i>
                  <span>Học phí niêm yết công khai, minh bạch, đối soát hóa đơn tự động và rõ ràng.</span>
                </li>
                <li className="d-flex align-items-start gap-2 mb-3">
                  <i className="bi bi-check-circle-fill text-danger fs-5"></i>
                  <span>Chủ động chọn lịch học linh hoạt theo ma trận thời gian thực của gia sư.</span>
                </li>
              </ul>
              <a href="#features" className="btn btn-otsp">
                <span>Khám phá môn học</span>
                <i className="bi bi-arrow-right ms-1"></i>
              </a>
            </div>
          </div>
        </div>
      </section>

      {/* ==================== 3. KHỐI CON SỐ THỐNG KÊ NỔI BẬT ==================== */}
      <section id="counts" className="section counts light-background">
        <div className="container">
          <div className="row gy-4">
            <div className="col-lg-3 col-md-6">
              <div className="stats-item text-center w-100 h-100">
                <AnimatedCounter end={data.studentCount ?? 0} />
                <p>Học viên đồng hành</p>
              </div>
            </div>
            <div className="col-lg-3 col-md-6">
              <div className="stats-item text-center w-100 h-100">
                <AnimatedCounter end={data.subjectCount ?? 0} />
                <p>Môn học đào tạo</p>
              </div>
            </div>
            <div className="col-lg-3 col-md-6">
              <div className="stats-item text-center w-100 h-100">
                <AnimatedCounter end={data.completedSessionCount ?? 0} />
                <p>Buổi học thành công</p>
              </div>
            </div>
            <div className="col-lg-3 col-md-6">
              <div className="stats-item text-center w-100 h-100">
                <AnimatedCounter end={data.tutorCount ?? 0} />
                <p>Gia sư đã kiểm duyệt</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ==================== 4. KHỐI LÝ DO NÊN CHỌN OTSP ==================== */}
      <section id="why-us" className="section why-us">
        <div className="container">
          <div className="row gy-4">
            <div className="col-lg-4">
              <div className="why-box">
                <h3>Vì sao hàng ngàn phụ huynh tin chọn OTSP?</h3>
                <p>
                  OTSP là nền tảng số hóa giáo dục gia sư hàng đầu, tiên phong trong việc chuẩn hóa chất lượng
                  gia sư tại Việt Nam. Chúng tôi cam kết đồng hành cùng học viên bứt phá thành tích trong mọi kỳ thi.
                </p>
                <div className="text-center">
                  <a href="#about" className="more-btn">
                    <span>Tìm hiểu thêm</span>
                    <i className="bi bi-chevron-right ms-1"></i>
                  </a>
                </div>
              </div>
            </div>
            <div className="col-lg-8 d-flex align-items-stretch">
              <div className="row gy-4 w-100">
                <div className="col-xl-4 col-md-4">
                  <div className="icon-box d-flex flex-column justify-content-center align-items-center">
                    <i className="bi bi-patch-check"></i>
                    <h4>Gia sư chọn lọc</h4>
                    <p>Bằng cấp đại học, chứng chỉ sư phạm được kiểm duyệt nghiêm ngặt bởi Admin.</p>
                  </div>
                </div>
                <div className="col-xl-4 col-md-4">
                  <div className="icon-box d-flex flex-column justify-content-center align-items-center">
                    <i className="bi bi-cash-stack"></i>
                    <h4>Học phí minh bạch</h4>
                    <p>Học phí niêm yết rõ ràng theo giờ hoặc theo tháng, không phụ phí ẩn, đối soát tự động.</p>
                  </div>
                </div>
                <div className="col-xl-4 col-md-4">
                  <div className="icon-box d-flex flex-column justify-content-center align-items-center">
                    <i className="bi bi-calendar-check"></i>
                    <h4>Lịch học linh hoạt</h4>
                    <p>Chủ động chọn khung giờ theo ma trận lịch rảnh của gia sư, dễ dàng đổi lịch khi bận.</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ==================== 5. KHỐI DANH SÁCH MÔN HỌC TIÊU BIỂU ==================== */}
      <section id="features" className="section features">
        <div className="container section-title">
          <h2>DANH MỤC ĐÀO TẠO</h2>
          <p>Môn học &amp; Cấp học phổ biến</p>
        </div>
        <div className="container" style={{ minHeight: '172px' }}>
          {loading && (!data.subjects || data.subjects.length === 0) ? (
            <div className="row gy-4" aria-hidden="true">
              {[1, 2, 3, 4, 5, 6, 7, 8].map((item) => (
                <div key={item} className="col-lg-3 col-md-4 col-sm-6 subject-item">
                  <div className="features-item placeholder-glow">
                    <span className="placeholder rounded-circle me-3" style={{ width: 28, height: 28 }} />
                    <span className="placeholder col-6 rounded" style={{ height: 20 }} />
                  </div>
                </div>
              ))}
            </div>
          ) : data.subjects && data.subjects.length > 0 ? (
            <div className="row gy-4" id="subjectCardsGrid">
              {data.subjects.slice(0, 8).map((s, index) => {
                const iconColor = FEATURE_COLORS[index % FEATURE_COLORS.length];
                return (
                  <div key={s.id || index} className="col-lg-3 col-md-4 col-sm-6 subject-item">
                    <div className="features-item">
                      <i className={`bi ${s.iconClass || 'bi-book'}`} style={{ color: iconColor }}></i>
                      <h3>
                        <Link to={`/search?keyword=${encodeURIComponent(s.name)}`} className="stretched-link">
                          {s.name}
                        </Link>
                      </h3>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <div className="text-center py-4">
              <p className="text-muted mb-0">Hiện chưa có danh mục môn học nào được công bố.</p>
            </div>
          )}
        </div>
      </section>

      {/* ==================== 6. KHỐI CÁC LỚP HỌC ĐANG MỞ ==================== */}
      <section id="courses" className="section courses">
        <div className="container section-title">
          <h2>LỚP HỌC ĐANG MỞ</h2>
          <p>Các lớp học chất lượng cao sẵn sàng nhận học viên</p>
        </div>
        <div className="container" style={{ minHeight: '448px' }}>
          {loading && (!data.classes || data.classes.length === 0) ? (
            <div className="row gy-4" aria-hidden="true">
              {[1, 2, 3].map((item) => (
                <div key={item} className="col-lg-4 col-md-6 d-flex align-items-stretch">
                  <div className="course-item w-100 d-flex flex-column h-100 shadow-sm rounded-4 overflow-hidden border bg-white" style={{ minHeight: '448px' }}>
                    <div style={{ height: '210px', width: '100%', backgroundColor: '#eef2f6' }} />
                    <div className="course-content d-flex flex-column flex-grow-1 p-3 placeholder-glow">
                      <div className="d-flex justify-content-between mb-2">
                        <span className="placeholder col-5 rounded" style={{ height: 24 }} />
                        <span className="placeholder col-3 rounded" style={{ height: 24 }} />
                      </div>
                      <span className="placeholder col-9 rounded mb-2" style={{ height: 24 }} />
                      <span className="placeholder col-12 rounded mb-1" style={{ height: 16 }} />
                      <span className="placeholder col-8 rounded mb-3" style={{ height: 16 }} />
                      <div className="mt-auto pt-2 border-top d-flex justify-content-between align-items-center">
                        <span className="placeholder col-5 rounded" style={{ height: 28 }} />
                        <span className="placeholder col-3 rounded" style={{ height: 20 }} />
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : data.classes && data.classes.length > 0 ? (
            <div className="row gy-4">
              {data.classes.map((c, i) => {
                const courseImgIndex = (i % 3) + 1;
                const isGroup = c.type === 1 || c.type === 'Group';
                return (
                  <div key={c.id || i} className="col-lg-4 col-md-6 d-flex align-items-stretch">
                    <div className="course-item w-100 d-flex flex-column h-100 shadow-sm rounded-4 overflow-hidden border bg-white" style={{ minHeight: '448px' }}>
                      <div className="position-relative" style={{ height: '210px', width: '100%', overflow: 'hidden', backgroundColor: '#f1f5f9' }}>
                        <img
                          src={resolveAssetUrl(c.coverImageUrl) || `/assets/img/course-${courseImgIndex}.jpg`}
                          className="w-100 h-100"
                          style={{ objectFit: 'cover', display: 'block' }}
                          width="600"
                          height="210"
                          loading="lazy"
                          decoding="async"
                          alt={c.name}
                          onError={(e) => { e.target.src = `/assets/img/course-${courseImgIndex}.jpg`; }}
                        />
                        <span
                          className="position-absolute top-0 start-0 m-3 badge rounded-pill px-2.5 py-1.5 shadow-sm fw-medium"
                          style={{
                            backgroundColor: isGroup ? '#dc2626' : '#2563eb',
                            color: '#fff',
                            fontSize: '0.78rem'
                          }}
                        >
                          <i className={`bi ${isGroup ? 'bi-people-fill' : 'bi-person-fill'} me-1`}></i>
                          {isGroup ? 'Lớp nhóm' : 'Lớp kèm 1-1'}
                        </span>
                      </div>
                      <div className="course-content d-flex flex-column flex-grow-1 p-3">
                        <div className="d-flex justify-content-between align-items-center mb-2">
                          <p className="category mb-0">{c.subjectName} · {c.gradeName}</p>
                          <p className="price text-danger fw-bold mb-0">{formatPrice(c.fee)} đ</p>
                        </div>
                        <h3 className="fs-5 mb-2 fw-bold">
                          <Link to={`/search?tab=classes&keyword=${encodeURIComponent(c.name)}`} className="text-dark text-decoration-none">
                            {c.name}
                          </Link>
                        </h3>
                        <p
                          className="description text-muted small mb-3 flex-grow-1"
                          style={{ display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}
                        >
                          {c.description || 'Chưa có mô tả chi tiết cho lớp học này.'}
                        </p>
                        <div className="trainer d-flex justify-content-between align-items-center pt-2 border-top mt-auto">
                          <div className="trainer-profile d-flex align-items-center">
                            <img
                              src={resolveAssetUrl(c.tutorAvatarUrl) || '/assets/img/default-avatar.svg'}
                              className="rounded-circle border"
                              style={{ width: 34, height: 34, objectFit: 'cover' }}
                              width="34"
                              height="34"
                              loading="lazy"
                              decoding="async"
                              alt={c.tutorName}
                              onError={(e) => { e.target.src = '/assets/img/default-avatar.svg'; }}
                            />
                            <span className="trainer-link ms-2 fw-semibold text-dark small">{c.tutorName}</span>
                          </div>
                          <div className="trainer-rank d-flex align-items-center small text-muted">
                            <i className="bi bi-people me-1 text-primary"></i>
                            <span>{c.studentCount ?? 0}/{c.maxStudents ?? 0} học viên</span>
                          </div>
                        </div>

                        <div className="mt-3 pt-2 border-top d-flex gap-2">
                          <button
                            type="button"
                            className="btn btn-sm btn-outline-danger rounded-pill px-3 py-1.5 fw-semibold w-100"
                            onClick={() => setSelectedClassForModal(c)}
                          >
                            <i className="bi bi-eye me-1"></i>Xem chi tiết &amp; Đăng ký
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <div className="text-center py-5 d-flex align-items-center justify-content-center" style={{ minHeight: '448px' }}>
              <div className="p-4 rounded-4 bg-light d-inline-block border">
                <i className="bi bi-door-closed text-muted" style={{ fontSize: '2.8rem' }}></i>
                <p className="mt-2 mb-3 text-muted">Hiện chưa có lớp học công khai nào đang mở.</p>
                <Link to="/search" className="btn btn-otsp">
                  <i className="bi bi-search me-1"></i> Kết nối trực tiếp với gia sư
                </Link>
              </div>
            </div>
          )}
        </div>
      </section>

      {/* ==================== 7. KHỐI GIA SƯ TIÊU BIỂU ==================== */}
      <section id="trainers-index" className="section trainers-index">
        <div className="container section-title">
          <h2>ĐỘI NGŨ GIA SƯ</h2>
          <p>Gia sư tiêu biểu được đánh giá cao</p>
        </div>
        <div className="container" style={{ minHeight: '468px' }}>
          {loading && (!data.featuredTutors || data.featuredTutors.length === 0) ? (
            <div className="row gy-4" aria-hidden="true">
              {[1, 2, 3].map((item) => (
                <div key={item} className="col-lg-4 col-md-6 d-flex align-items-stretch tutor-item">
                  <div className="member w-100 d-flex flex-column h-100 shadow-sm rounded-4 overflow-hidden border bg-white" style={{ minHeight: '468px' }}>
                    <div style={{ height: '260px', width: '100%', backgroundColor: '#eef2f6' }} />
                    <div className="member-content d-flex flex-column flex-grow-1 p-3 placeholder-glow">
                      <span className="placeholder col-7 rounded mb-2" style={{ height: 22 }} />
                      <span className="placeholder col-5 rounded mb-3" style={{ height: 16 }} />
                      <span className="placeholder col-12 rounded mb-1" style={{ height: 16 }} />
                      <span className="placeholder col-9 rounded mb-3" style={{ height: 16 }} />
                      <div className="mt-auto pt-2 border-top d-flex justify-content-between">
                        <span className="placeholder col-4 rounded" style={{ height: 22 }} />
                        <span className="placeholder col-3 rounded" style={{ height: 22 }} />
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : data.featuredTutors && data.featuredTutors.length > 0 ? (
            <div className="row gy-4" id="tutorsCardsGrid">
              {data.featuredTutors.map((t, i) => {
                const trainerImg = resolveAssetUrl(t.avatarUrl) || `/assets/img/trainers/trainer-${(i % 3) + 1}.jpg`;
                const avgRating = Number(t.averageRating) || 0;
                return (
                  <div key={t.id || i} className="col-lg-4 col-md-6 d-flex align-items-stretch tutor-item">
                    <div className="member w-100 d-flex flex-column h-100 shadow-sm rounded-4 overflow-hidden border bg-white" style={{ minHeight: '468px' }}>
                      <div className="member-img-wrap d-flex align-items-center justify-content-center" style={{ height: '260px', width: '100%', overflow: 'hidden', backgroundColor: '#f1f5f9' }}>
                        <img
                          src={trainerImg}
                          alt={t.fullName}
                          width="600"
                          height="260"
                          loading="lazy"
                          decoding="async"
                          style={{ width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'center center', display: 'block' }}
                          onError={(e) => { e.target.src = '/assets/img/default-avatar.svg'; }}
                        />
                      </div>
                      <div className="member-content d-flex flex-column flex-grow-1 p-3">
                        <h4 className="fw-bold mb-1 fs-5 text-dark">{t.fullName}</h4>
                        <span className="text-danger fst-italic small mb-2 d-block">
                          {t.teaching && t.teaching.length > 0
                            ? `${t.teaching[0].subjectName} · ${t.teaching[0].gradeName}`
                            : 'Chưa cập nhật môn dạy'}
                        </span>
                        <p
                          className="text-muted small mb-3 flex-grow-1"
                          style={{ display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}
                        >
                          {t.introduction || 'Gia sư chưa cập nhật phần giới thiệu.'}
                        </p>
                        <div className="d-flex justify-content-between align-items-center pt-2.5 border-top mt-auto">
                          <span className="otsp-price text-danger fw-bold">
                            {formatPrice(t.hourlyFee)} <small className="text-muted fw-normal">đ/giờ</small>
                          </span>
                          {avgRating > 0 ? (
                            <span className="badge bg-warning-subtle text-warning-emphasis border border-warning-subtle px-2 py-1">
                              <i className="bi bi-star-fill text-warning me-1"></i>{avgRating.toFixed(1)}
                            </span>
                          ) : (
                            <span className="badge bg-light text-muted border">Mới</span>
                          )}
                        </div>
                        <div className="social mt-3 pt-2">
                          <Link to={isStudent ? `/student/booking/${t.id}` : `/tutors/${t.id}`} className="btn btn-sm btn-otsp w-100 py-2">
                            <i className="bi bi-chat-dots-fill me-1"></i> Kết nối học tập
                          </Link>
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <div className="text-center py-5 d-flex flex-column align-items-center justify-content-center" style={{ minHeight: '468px' }}>
              <i className="bi bi-person-x text-muted" style={{ fontSize: '3rem' }}></i>
              <p className="mt-3 text-muted">Chưa có gia sư nào được phê duyệt hiển thị.</p>
              <Link to="/register" className="btn btn-outline-otsp mt-2">
                <i className="bi bi-person-plus me-1"></i> Đăng ký làm gia sư ngay
              </Link>
            </div>
          )}
        </div>
      </section>

      {/* ==================== 8. KHỐI 4 BƯỚC BẮT ĐẦU DỄ DÀNG ==================== */}
      <section className="section steps-section">
        <div className="container section-title">
          <h2>QUY TRÌNH HỌC TẬP</h2>
          <p>Bắt đầu học cùng gia sư chỉ với 4 bước đơn giản</p>
        </div>
        <div className="container">
          <div className="row g-4">
            <div className="col-12 col-sm-6 col-lg-3">
              <div className="step-card">
                <div className="step-number">1</div>
                <h4>Tìm kiếm gia sư</h4>
                <p>Lọc theo môn học, cấp học, khu vực và mức học phí mong muốn.</p>
              </div>
            </div>
            <div className="col-12 col-sm-6 col-lg-3">
              <div className="step-card">
                <div className="step-number">2</div>
                <h4>Đăng ký học thử</h4>
                <p>Gửi yêu cầu học thử để đánh giá độ phù hợp của phương pháp giảng dạy.</p>
              </div>
            </div>
            <div className="col-12 col-sm-6 col-lg-3">
              <div className="step-card">
                <div className="step-number">3</div>
                <h4>Vào lớp chính thức</h4>
                <p>Học trực tuyến qua phòng video hoặc trực tiếp tại nhà với lịch linh hoạt.</p>
              </div>
            </div>
            <div className="col-12 col-sm-6 col-lg-3">
              <div className="step-card">
                <div className="step-number">4</div>
                <h4>Đánh giá &amp; Tiến bộ</h4>
                <p>Đánh giá chất lượng sau mỗi buổi học và theo dõi tiến độ vững chắc.</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ==================== 9. KHỐI KÊU GỌI HÀNH ĐỘNG (CTA) ==================== */}
      <section className="section otsp-cta-section">
        <div className="container">
          <div className="otsp-cta-box">
            <div className="row align-items-center">
              <div className="col-lg-8">
                <h2>Sẵn sàng bứt phá điểm số cùng gia sư xuất sắc?</h2>
                <p>
                  Đăng ký ngay hôm nay để nhận tư vấn lộ trình học tập miễn phí và kết nối với gia sư phù hợp nhất
                  cho kỳ thi sắp tới.
                </p>
              </div>
              <div className="col-lg-4 text-lg-end">
                <div className="d-flex flex-column flex-sm-row justify-content-lg-end gap-3">
                  <Link to="/search" className="btn btn-hero-primary">
                    <i className="bi bi-search me-1"></i> Tìm gia sư ngay
                  </Link>
                  <Link to="/register" className="btn btn-hero-secondary">
                    <i className="bi bi-briefcase me-1"></i> Làm gia sư
                  </Link>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {selectedClassForModal && (
        <ClassDetailModal
          classId={selectedClassForModal.id}
          initialData={selectedClassForModal}
          onClose={() => setSelectedClassForModal(null)}
        />
      )}
    </div>
  );
}

