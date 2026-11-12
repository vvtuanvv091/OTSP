import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import notificationApi from '../api/notificationApi';
import { hubUrl } from '../api/config';
import { getStoredToken } from '../utils/authStorage';

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

    const connections = [];
    if (getStoredToken()) {
      const bookingHub = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl('/hubs/connection-requests'), { accessTokenFactory: getStoredToken })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Error)
        .build();
      bookingHub.on('ConnectionRequestCreated', refresh);
      bookingHub.on('ConnectionRequestStatusChanged', refresh);
      bookingHub.on('ClassSessionStatusChanged', refresh);
      bookingHub.start().catch(() => {});
      connections.push(bookingHub);

      const chatHub = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl('/hubs/chat'), { accessTokenFactory: getStoredToken })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Error)
        .build();
      chatHub.on('ChatMessageReceived', refresh);
      chatHub.start().catch(() => {});
      connections.push(chatHub);
    }

    return () => {
      active = false;
      window.clearInterval(timer);
      window.removeEventListener('otsp:notification-count', handleCountChange);
      connections.forEach((conn) => conn.stop().catch(() => {}));
    };
  }, [user]);

  return [user ? unreadCount : 0, setUnreadCount];
}
