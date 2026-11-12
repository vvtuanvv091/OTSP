import { useEffect, useRef, useState } from 'react';
import '../../styles/components/GoogleSignInButton.css';

let googleScriptPromise;

function loadGoogleIdentityScript() {
  if (window.google?.accounts?.id) return Promise.resolve(window.google);
  if (!googleScriptPromise) {
    googleScriptPromise = new Promise((resolve, reject) => {
      const existing = document.querySelector('script[data-google-identity]');
      const script = existing || document.createElement('script');
      const handleLoad = () => window.google?.accounts?.id ? resolve(window.google) : reject(new Error('Google Identity Services chưa sẵn sàng.'));
      script.addEventListener('load', handleLoad, { once: true });
      script.addEventListener('error', () => reject(new Error('Không tải được Google Identity Services.')), { once: true });
      if (!existing) {
        script.src = 'https://accounts.google.com/gsi/client';
        script.async = true;
        script.defer = true;
        script.dataset.googleIdentity = 'true';
        document.head.appendChild(script);
      }
    }).catch((error) => {
      googleScriptPromise = null;
      throw error;
    });
  }
  return googleScriptPromise;
}

const DEFAULT_GOOGLE_CLIENT_ID = '541864822323-bsdas0l4gcm6nfsgvkrjp2mtbtic4l6l.apps.googleusercontent.com';

export default function GoogleSignInButton({ onCredential, mode = 'signin', disabled = false }) {
  const buttonRef = useRef(null);
  const onCredentialRef = useRef(onCredential);
  const [loadError, setLoadError] = useState('');
  const clientId = (import.meta.env.VITE_GOOGLE_CLIENT_ID || DEFAULT_GOOGLE_CLIENT_ID).trim();

  useEffect(() => {
    onCredentialRef.current = onCredential;
  }, [onCredential]);

  useEffect(() => {
    if (!clientId) return undefined;
    let active = true;

    loadGoogleIdentityScript()
      .then((google) => {
        if (!active || !buttonRef.current) return;
        setLoadError('');
        buttonRef.current.replaceChildren();
        google.accounts.id.initialize({
          client_id: clientId,
          callback: (response) => {
            if (response?.credential) onCredentialRef.current?.(response.credential);
            else setLoadError('Google không trả về thông tin xác thực. Vui lòng thử lại.');
          },
        });
        google.accounts.id.renderButton(buttonRef.current, {
          type: 'standard',
          theme: 'outline',
          size: 'large',
          shape: 'pill',
          text: mode === 'signup' ? 'signup_with' : 'signin_with',
          locale: 'vi',
          width: Math.min(buttonRef.current.clientWidth || 400, 400),
        });
      })
      .catch((err) => { if (active) setLoadError(err.message || 'Không thể khởi tạo đăng nhập Google.'); });

    return () => { active = false; };
  }, [clientId, mode]);

  const error = !clientId
    ? 'Thiếu Google OAuth Web Client ID. Cần cấu hình VITE_GOOGLE_CLIENT_ID ở frontend và Google__ClientId ở backend.'
    : loadError;

  return (
    <div className={`google-signin-wrap ${disabled ? 'is-disabled' : ''}`} aria-busy={disabled}>
      <div ref={buttonRef} className="google-signin-button" />
      {disabled && <div className="google-signin-blocker" aria-hidden="true" />}
      {error && <div className="form-text text-danger text-center mt-2">{error}</div>}
    </div>
  );
}
