import { useEffect, useState } from 'react';
import notificationApi from '../api/notificationApi';

export default function useUnreadNotificationCount(user) {
  const [unreadCount, setUnreadCount] = useState(0);

  useEffect(() => {
    if (!user) return undefined;

    let active = true;
    const refresh = async () => {
      try {
        const response = await notificationApi.getNotifications({ page: 1, pageSize: 1 });
        if (active) setUnreadCount(response.data?.unreadCount || 0);
      } catch {
        // Giữ lại số lượng thông báo đã biết gần nhất khi API tạm thời không phản hồi.
      }
    };
    const handleCountChange = (event) => {
      if (Number.isFinite(event.detail?.unreadCount)) setUnreadCount(event.detail.unreadCount);
      else refresh();
    };

    refresh();
    const timer = window.setInterval(refresh, 30000);
    window.addEventListener('otsp:notification-count', handleCountChange);
    return () => {
      active = false;
      window.clearInterval(timer);
      window.removeEventListener('otsp:notification-count', handleCountChange);
    };
  }, [user]);

  return [user ? unreadCount : 0, setUnreadCount];
}
