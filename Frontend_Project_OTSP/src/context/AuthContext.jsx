import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import authApi from '../api/authApi';
import {
  clearAuthStorage,
  getStoredToken,
  getStoredUserRaw,
  hasSessionStorageToken,
  isAuthStorageEventKey,
  persistAuthSession,
  updateStoredUser,
} from '../utils/authStorage';

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
      const saved = getStoredUserRaw();
      return saved ? normalizeUser(JSON.parse(saved)) : null;
    } catch {
      return null;
    }
  });
  const [token, setToken] = useState(() => getStoredToken());
  const [loading, setLoading] = useState(() => {
    const hasToken = Boolean(getStoredToken());
    const hasSavedUser = Boolean(getStoredUserRaw());
    // Tải tức thì không trắng trang: nếu không có token hoặc đã có sẵn cache user thì không block giao diện
    return hasToken && !hasSavedUser;
  });

  const clearSession = useCallback(() => {
    setToken('');
    setUser(null);
    clearAuthStorage();
  }, []);

  const logout = useCallback(() => {
    const activeToken = getStoredToken();
    if (activeToken) {
      authApi.logout().catch(() => {});
    }
    clearSession();
  }, [clearSession]);

  useEffect(() => {
    const handleUnauthorized = () => clearSession();
    window.addEventListener('otsp:unauthorized', handleUnauthorized);

    // Đồng bộ phiên giữa các tab cùng trình duyệt để tránh tab Admin giữ UI Admin nhưng lại gửi JWT của học viên/gia sư vừa đăng nhập ở tab khác
    const handleStorageChange = (event) => {
      if (!isAuthStorageEventKey(event.key) || hasSessionStorageToken()) return;
      const nextToken = getStoredToken();
      setToken(nextToken);
      if (!nextToken) {
        setUser(null);
        return;
      }
      try {
        const nextRawUser = getStoredUserRaw();
        setUser(nextRawUser ? normalizeUser(JSON.parse(nextRawUser)) : null);
      } catch {
        setUser(null);
      }
    };
    window.addEventListener('storage', handleStorageChange);

    const fetchMe = async () => {
      const storedToken = getStoredToken();
      if (storedToken) {
        setToken(storedToken);
        try {
          const res = await authApi.getMe();
          const normalized = normalizeUser(res.data);
          setUser(normalized);
          updateStoredUser(normalized);
        } catch (err) {
          console.error('Lỗi khi lấy thông tin user:', err);
          clearSession();
        }
      }
      setLoading(false);
    };
    fetchMe();
    return () => {
      window.removeEventListener('otsp:unauthorized', handleUnauthorized);
      window.removeEventListener('storage', handleStorageChange);
    };
  }, [clearSession]);

  const login = useCallback(async (email, password, rememberMe = false) => {
    const res = await authApi.login({ email: email?.trim(), password, rememberMe });
    const { token: jwtToken, user: rawUserInfo } = res.data;
    const userInfo = normalizeUser(rawUserInfo);
    persistAuthSession(jwtToken, userInfo, rememberMe);
    setToken(jwtToken);
    setUser(userInfo);
    return userInfo;
  }, []);

  const register = useCallback(async (registerData) => {
    const res = await authApi.register(registerData);
    const { token: jwtToken, user: rawUserInfo } = res.data;
    const userInfo = normalizeUser(rawUserInfo);
    persistAuthSession(jwtToken, userInfo, true);
    setToken(jwtToken);
    setUser(userInfo);
    return userInfo;
  }, []);

  const loginWithGoogle = useCallback(async (googleData) => {
    const res = await authApi.googleLogin(googleData);
    const { token: jwtToken, user: rawUserInfo } = res.data;
    const userInfo = normalizeUser(rawUserInfo);
    persistAuthSession(jwtToken, userInfo, true);
    setToken(jwtToken);
    setUser(userInfo);
    return userInfo;
  }, []);

  const updateUser = useCallback((updates) => {
    setUser((current) => {
      if (!current) return current;
      const nextUser = normalizeUser({ ...current, ...updates });
      updateStoredUser(nextUser);
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
