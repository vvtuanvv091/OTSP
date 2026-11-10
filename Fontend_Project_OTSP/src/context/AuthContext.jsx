import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import authApi from '../api/authApi';

const AuthContext = createContext();

function normalizeUser(raw) {
  if (!raw || typeof raw !== 'object') return null;
  const avatarUrl = raw.avatarUrl || raw.avatarURL || raw.AvatarURL || raw.AvatarUrl || null;
  return {
    ...raw,
    avatarUrl,
    avatarURL: avatarUrl,
  };
}

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(() => {
    try {
      const saved = localStorage.getItem('user') || sessionStorage.getItem('user');
      return saved ? normalizeUser(JSON.parse(saved)) : null;
    } catch {
      return null;
    }
  });
  const [token, setToken] = useState(() => (
    localStorage.getItem('token')
    || sessionStorage.getItem('token')
    || localStorage.getItem('otsp_token')
    || ''
  ));
  const [loading, setLoading] = useState(() => {
    const hasToken = Boolean(
      localStorage.getItem('token')
      || sessionStorage.getItem('token')
      || localStorage.getItem('otsp_token')
    );
    const hasSavedUser = Boolean(
      localStorage.getItem('user')
      || sessionStorage.getItem('user')
    );
    // Tải tức thì không trắng trang: nếu không có token hoặc đã có sẵn cache user thì không block giao diện
    return hasToken && !hasSavedUser;
  });

  const clearSession = useCallback(() => {
    setToken('');
    setUser(null);
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    localStorage.removeItem('otsp_token');
    localStorage.removeItem('otsp_user');
    sessionStorage.removeItem('token');
    sessionStorage.removeItem('user');
  }, []);

  const logout = useCallback(() => {
    const activeToken = localStorage.getItem('token')
      || sessionStorage.getItem('token')
      || localStorage.getItem('otsp_token');
    if (activeToken) {
      authApi.logout().catch(() => {});
    }
    clearSession();
  }, [clearSession]);

  useEffect(() => {
    const handleUnauthorized = () => clearSession();
    window.addEventListener('otsp:unauthorized', handleUnauthorized);

    const fetchMe = async () => {
      const storedToken = localStorage.getItem('token')
        || sessionStorage.getItem('token')
        || localStorage.getItem('otsp_token');
      if (storedToken) {
        setToken(storedToken);
        try {
          const res = await authApi.getMe();
          const normalized = normalizeUser(res.data);
          setUser(normalized);
          if (localStorage.getItem('token')) {
            localStorage.setItem('user', JSON.stringify(normalized));
          } else {
            sessionStorage.setItem('user', JSON.stringify(normalized));
          }
        } catch (err) {
          console.error('Lỗi khi lấy thông tin user:', err);
          clearSession();
        }
      }
      setLoading(false);
    };
    fetchMe();
    return () => window.removeEventListener('otsp:unauthorized', handleUnauthorized);
  }, [clearSession]);

  const login = useCallback(async (email, password, rememberMe = false) => {
    const res = await authApi.login({ email: email?.trim(), password, rememberMe });
    const { token: jwtToken, user: rawUserInfo } = res.data;
    const userInfo = normalizeUser(rawUserInfo);
    if (rememberMe) {
      localStorage.setItem('token', jwtToken);
      localStorage.setItem('user', JSON.stringify(userInfo));
      sessionStorage.removeItem('token');
      sessionStorage.removeItem('user');
    } else {
      sessionStorage.setItem('token', jwtToken);
      sessionStorage.setItem('user', JSON.stringify(userInfo));
      localStorage.removeItem('token');
      localStorage.removeItem('user');
    }
    setToken(jwtToken);
    setUser(userInfo);
    return userInfo;
  }, []);

  const register = useCallback(async (registerData) => {
    const res = await authApi.register(registerData);
    const { token: jwtToken, user: rawUserInfo } = res.data;
    const userInfo = normalizeUser(rawUserInfo);
    localStorage.setItem('token', jwtToken);
    localStorage.setItem('user', JSON.stringify(userInfo));
    setToken(jwtToken);
    setUser(userInfo);
    return userInfo;
  }, []);

  const loginWithGoogle = useCallback(async (googleData) => {
    const res = await authApi.googleLogin(googleData);
    const { token: jwtToken, user: rawUserInfo } = res.data;
    const userInfo = normalizeUser(rawUserInfo);
    localStorage.setItem('token', jwtToken);
    localStorage.setItem('user', JSON.stringify(userInfo));
    setToken(jwtToken);
    setUser(userInfo);
    return userInfo;
  }, []);

  const updateUser = useCallback((updates) => {
    setUser((current) => {
      if (!current) return current;
      const nextUser = normalizeUser({ ...current, ...updates });
      if (localStorage.getItem('token')) {
        localStorage.setItem('user', JSON.stringify(nextUser));
      }
      if (sessionStorage.getItem('user')) {
        sessionStorage.setItem('user', JSON.stringify(nextUser));
      }
      try {
        window.dispatchEvent(new CustomEvent('otsp:user-updated', { detail: nextUser }));
      } catch {
        // Safe fallback
      }
      return nextUser;
    });
  }, []);

  const roles = useMemo(() => [
    ...(Array.isArray(user?.roles) ? user.roles : []),
    ...(Array.isArray(user?.Roles) ? user.Roles : []),
    user?.role,
    user?.Role,
    user?.roleName,
  ].filter(Boolean).map((role) => String(role).trim().toLowerCase()), [user]);

  const hasRole = useCallback((role) => {
    const normalizedRole = String(role).trim().toLowerCase();
    if (normalizedRole === 'admin' && user?.isAdmin === true) return true;
    return roles.includes(normalizedRole);
  }, [roles, user?.isAdmin]);

  const isTutor = hasRole('Tutor');
  const isStudent = hasRole('Student_Parent') || hasRole('Student');
  const isAdmin = hasRole('Admin') || user?.isAdmin === true;

  const contextValue = useMemo(() => ({
    user,
    token,
    loading,
    login,
    loginWithGoogle,
    register,
    logout,
    updateUser,
    isTutor,
    isStudent,
    isAdmin,
    hasRole,
  }), [
    user,
    token,
    loading,
    login,
    loginWithGoogle,
    register,
    logout,
    updateUser,
    isTutor,
    isStudent,
    isAdmin,
    hasRole,
  ]);

  return (
    <AuthContext.Provider value={contextValue}>
      {children}
    </AuthContext.Provider>
  );
};

// eslint-disable-next-line react-refresh/only-export-components
export const useAuth = () => useContext(AuthContext);
export default AuthContext;
