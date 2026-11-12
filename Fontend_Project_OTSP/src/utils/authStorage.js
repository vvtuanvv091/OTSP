const AUTH_STORAGE_KEYS = ['token', 'user', 'otsp_token', 'otsp_user'];

export function hasSessionStorageToken() {
  try {
    return Boolean(sessionStorage.getItem('token'));
  } catch {
    return false;
  }
}

export function getStoredToken() {
  try {
    return (
      sessionStorage.getItem('token')
      || localStorage.getItem('token')
      || localStorage.getItem('otsp_token')
      || ''
    );
  } catch {
    return '';
  }
}

export function getStoredUserRaw() {
  try {
    if (sessionStorage.getItem('token')) {
      return sessionStorage.getItem('user');
    }
    return localStorage.getItem('user') || sessionStorage.getItem('user');
  } catch {
    return null;
  }
}

export function persistAuthSession(jwtToken, userInfo, rememberMe = true) {
  const serializedUser = JSON.stringify(userInfo);
  if (rememberMe) {
    localStorage.setItem('token', jwtToken);
    localStorage.setItem('user', serializedUser);
    sessionStorage.removeItem('token');
    sessionStorage.removeItem('user');
  } else {
    sessionStorage.setItem('token', jwtToken);
    sessionStorage.setItem('user', serializedUser);
    localStorage.removeItem('token');
    localStorage.removeItem('user');
  }
}

export function updateStoredUser(userInfo) {
  const serializedUser = JSON.stringify(userInfo);
  if (hasSessionStorageToken()) {
    sessionStorage.setItem('user', serializedUser);
    return;
  }
  if (localStorage.getItem('token')) {
    localStorage.setItem('user', serializedUser);
  } else if (sessionStorage.getItem('user')) {
    sessionStorage.setItem('user', serializedUser);
  }
}

export function clearAuthStorage() {
  for (const key of AUTH_STORAGE_KEYS) {
    localStorage.removeItem(key);
    sessionStorage.removeItem(key);
  }
}

export function isAuthStorageEventKey(key) {
  return key === null || AUTH_STORAGE_KEYS.includes(key);
}

