import { Link, useLocation } from 'react-router-dom';

export default function AccessDeniedPage() {
  const location = useLocation();
  const from = location.state?.from;
  const roleNames = (location.state?.requiredRoles || []).map((role) => ({
    Admin: 'quản trị viên', Tutor: 'gia sư', Student: 'học viên', Student_Parent: 'học viên hoặc phụ huynh',
  }[role] || role));

  return (
    <section className="container py-5">
      <div className="mx-auto rounded-4 border bg-body p-4 p-md-5 text-center shadow-sm" style={{ maxWidth: 620 }}>
        <div className="text-danger mb-3"><i className="bi bi-shield-lock" style={{ fontSize: 48 }} aria-hidden="true" /></div>
        <h1 className="h3 fw-bold">Bạn không có quyền truy cập trang này</h1>
        <p className="text-body-secondary mb-4">
          {roleNames.length
            ? `Trang này chỉ dành cho ${roleNames.join(' hoặc ')}. Tài khoản hiện tại không có vai trò phù hợp.`
            : 'Tài khoản hiện tại không được phép mở nội dung này.'}
          {from ? <><br /><span className="small">Đường dẫn: {from}</span></> : null}
        </p>
        <div className="d-flex flex-wrap justify-content-center gap-2">
          <Link className="btn btn-danger" to="/">Về trang chủ</Link>
          <Link className="btn btn-outline-secondary" to="/login" state={{ loggedOut: true }}>Đăng nhập tài khoản khác</Link>
        </div>
      </div>
    </section>
  );
}
