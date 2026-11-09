import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export default function ProtectedRoute({ allowedRoles = [] }) {
  const { user, loading, hasRole } = useAuth();
  const location = useLocation();

  if (loading) {
    return (
      <div className="d-flex justify-content-center align-items-center py-5" role="status" aria-label="Đang kiểm tra quyền truy cập">
        <div className="text-center text-body-secondary">
          <div className="spinner-border text-danger mb-2" aria-hidden="true" />
          <div>Đang kiểm tra phiên đăng nhập và quyền truy cập…</div>
        </div>
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location, authRequired: true }} />;
  }

  if (allowedRoles.length > 0) {
    const allowed = allowedRoles.some((role) => hasRole?.(role) || user.roles?.some((value) => String(value).toLowerCase() === role.toLowerCase()));
    if (!allowed) {
      return <Navigate to="/access-denied" replace state={{ from: location.pathname, requiredRoles: allowedRoles }} />;
    }
  }

  return <Outlet />;
}

