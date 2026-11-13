import { createContext, useContext, useEffect, useRef } from 'react';

export const SignalRContext = createContext(null);

export function useSignalR() {
  const context = useContext(SignalRContext);
  if (!context) {
    throw new Error('useSignalR phải được sử dụng bên trong SignalRProvider');
  }
  return context;
}

// Hook tiện lợi giúp các component con lắng nghe sự kiện SignalR dùng chung (tự hủy đăng ký khi unmount)
export function useSignalREvent(eventName, callback) {
  const { on } = useSignalR();
  const callbackRef = useRef(callback);

  useEffect(() => {
    callbackRef.current = callback;
  }, [callback]);

  useEffect(() => {
    if (!eventName || !on) return undefined;
    const unsubscribe = on(eventName, (...args) => {
      if (callbackRef.current) {
        callbackRef.current(...args);
      }
    });
    return unsubscribe;
  }, [eventName, on]);
}
