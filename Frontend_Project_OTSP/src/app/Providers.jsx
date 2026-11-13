import { AuthProvider } from '../context/AuthContext';
import { SignalRProvider } from '../context/SignalRContext';
import { ToastProvider } from '../context/ToastContext';
import { ConfirmDialogProvider } from '../context/ConfirmDialogContext';

export default function Providers({ children }) {
  return (
    <AuthProvider>
      <SignalRProvider>
        <ToastProvider>
          <ConfirmDialogProvider>
            {children}
          </ConfirmDialogProvider>
        </ToastProvider>
      </SignalRProvider>
    </AuthProvider>
  );
}

