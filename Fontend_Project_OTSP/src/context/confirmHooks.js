import { createContext, useContext } from 'react';

export const ConfirmDialogContext = createContext(null);

export function useConfirm() {
  const context = useContext(ConfirmDialogContext);
  if (!context) {
    return (options) => {
      const hasInput = typeof options === 'object' && options?.input;
      if (hasInput) return Promise.resolve(null);
      const message = typeof options === 'string' ? options : options?.message || 'Bạn có chắc chắn?';
      return Promise.resolve(window.confirm(message));
    };
  }
  return context;
}
