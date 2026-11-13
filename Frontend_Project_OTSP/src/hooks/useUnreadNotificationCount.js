import { useEffect, useState, useCallback } from 'react';
import notificationApi from '../api/notificationApi';
import { useSignalREvent } from '../context/signalRHooks';

export default function useUnreadNotificationCount(user) {
  const [unreadCount, setUnreadCount] = useState(0);

  const refresh = useCallback(async () => {
    if (!user) return;
    try {
      const response = await notificationApi.getNotifications({ page: 1, pageSize: 1 });
      setUnreadCount(response.data?.unreadCount || 0);
    } catch {
      // Giữ lại số lượng thông báo đã biết gần nhất khi API tạm thời không phản hồi.
    }
  }, [user]);

  // Lắng nghe các sự kiện SignalR dùng chung từ SignalRContext (0 kết nối WebSocket phụ)
  useSignalREvent('NotificationReceived', refresh);
  useSignalREvent('ChatMessageReceived', refresh);
  useSignalREvent('ConnectionRequestCreated', refresh);
  useSignalREvent('ConnectionRequestStatusChanged', refresh);
  useSignalREvent('ClassSessionStatusChanged', refresh);
  useSignalREvent('PaymentStatusChanged', refresh);
  useSignalREvent('TutorApprovalStatusChanged', refresh);
  useSignalREvent('ComplaintStatusChanged', refresh);

  useEffect(() => {
    if (!user) return undefined;
    let active = true;

    const handleCountChange = (event) => {
      if (Number.isFinite(event.detail?.unreadCount)) setUnreadCount(event.detail.unreadCount);
      else refresh();
    };

    Promise.resolve().then(() => {
      if (active) refresh();
    });
    const timer = window.setInterval(refresh, 60000); // 1 phút dự phòng nếu mất mạng
    window.addEventListener('otsp:notification-count', handleCountChange);

    return () => {
      active = false;
      window.clearInterval(timer);
      window.removeEventListener('otsp:notification-count', handleCountChange);
    };
  }, [user, refresh]);

  return [user ? unreadCount : 0, setUnreadCount];
}
