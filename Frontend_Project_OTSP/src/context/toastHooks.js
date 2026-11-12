import { createContext, useContext } from 'react';

export const ToastContext = createContext(null);

export function useToast() {
  const context = useContext(ToastContext);
  if (!context) {
    return {
      show: (msg) => console.log('Toast:', msg),
      success: (msg) => console.log('Toast Success:', msg),
      error: (msg) => console.error('Toast Error:', msg),
      warning: (msg) => console.warn('Toast Warning:', msg),
      info: (msg) => console.log('Toast Info:', msg),
      remove: () => {},
    };
  }
  return context;
}
