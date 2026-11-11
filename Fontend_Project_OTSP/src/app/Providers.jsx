import { AuthProvider } from '../context/AuthContext';
import { ToastProvider } from '../context/ToastContext';
import { ConfirmDialogProvider } from '../context/ConfirmDialogContext';

export default function Providers({ children }) {
  return (
    <AuthProvider>
      <ToastProvider>
        <ConfirmDialogProvider>
          {children}
        </ConfirmDialogProvider>
      </ToastProvider>
    </AuthProvider>
  );
}

