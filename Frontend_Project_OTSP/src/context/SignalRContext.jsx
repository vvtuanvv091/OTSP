import { useEffect, useRef, useState, useCallback, useMemo } from 'react';
import * as signalR from '@microsoft/signalr';
import { hubUrl } from '../api/config';
import { getStoredToken } from '../utils/authStorage';
import { useAuth } from './AuthContext';
import { SignalRContext } from './signalRHooks';

export function SignalRProvider({ children }) {
  const { user } = useAuth();
  const [connection, setConnection] = useState(null);
  const [connectionState, setConnectionState] = useState(signalR.HubConnectionState.Disconnected);
  const listenersMapRef = useRef(new Map());

  // Đăng ký lắng nghe một sự kiện SignalR
  const on = useCallback((eventName, callback) => {
    if (!eventName || typeof callback !== 'function') return () => {};

    if (!listenersMapRef.current.has(eventName)) {
      listenersMapRef.current.set(eventName, new Set());

      // Nếu connection đã tồn tại, gắn handler từ SignalR
      if (connection) {
        connection.on(eventName, (...args) => {
          const callbacks = listenersMapRef.current.get(eventName);
          if (callbacks) {
            callbacks.forEach((cb) => {
              try {
                cb(...args);
              } catch (err) {
                console.warn(`Lỗi khi thực thi handler cho SignalR event "${eventName}":`, err);
              }
            });
          }
        });
      }
    }

    listenersMapRef.current.get(eventName).add(callback);

    // Trả về hàm hủy đăng ký
    return () => {
      const callbacks = listenersMapRef.current.get(eventName);
      if (callbacks) {
        callbacks.delete(callback);
      }
    };
  }, [connection]);

  // Hủy đăng ký thủ công
  const off = useCallback((eventName, callback) => {
    const callbacks = listenersMapRef.current.get(eventName);
    if (callbacks) {
      if (callback) {
        callbacks.delete(callback);
      } else {
        callbacks.clear();
      }
    }
  }, []);

  // Các phương thức tiện ích invoke lên Hub
  const joinClassroom = useCallback(async (classroomId) => {
    if (connection && connection.state === signalR.HubConnectionState.Connected && Number(classroomId) > 0) {
      try {
        await connection.invoke('JoinClassroomRoom', Number(classroomId));
      } catch (err) {
        console.warn('Lỗi khi tham gia phòng lớp học SignalR:', err);
      }
    }
  }, [connection]);

  const leaveClassroom = useCallback(async (classroomId) => {
    if (connection && connection.state === signalR.HubConnectionState.Connected && Number(classroomId) > 0) {
      try {
        await connection.invoke('LeaveClassroomRoom', Number(classroomId));
      } catch (err) {
        console.warn('Lỗi khi rời phòng lớp học SignalR:', err);
      }
    }
  }, [connection]);

  const joinPayment = useCallback(async (paymentId) => {
    if (connection && connection.state === signalR.HubConnectionState.Connected && Number(paymentId) > 0) {
      try {
        await connection.invoke('JoinPaymentRoom', Number(paymentId));
      } catch (err) {
        console.warn('Lỗi khi tham gia phòng thanh toán SignalR:', err);
      }
    }
  }, [connection]);

  const leavePayment = useCallback(async (paymentId) => {
    if (connection && connection.state === signalR.HubConnectionState.Connected && Number(paymentId) > 0) {
      try {
        await connection.invoke('LeavePaymentRoom', Number(paymentId));
      } catch (err) {
        console.warn('Lỗi khi rời phòng thanh toán SignalR:', err);
      }
    }
  }, [connection]);

  useEffect(() => {
    const token = getStoredToken();
    if (!user || !token) return undefined;

    let isCancelled = false;

    // Khởi tạo DUY NHẤT 1 kết nối WebSocket cho toàn bộ ứng dụng qua Unified AppHub
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl('/hubs/app'), {
        accessTokenFactory: () => getStoredToken() || ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Error)
      .build();

    // Gắn lại toàn bộ các event listeners đã đăng ký
    listenersMapRef.current.forEach((callbacks, eventName) => {
      conn.on(eventName, (...args) => {
        callbacks.forEach((cb) => {
          try {
            cb(...args);
          } catch (err) {
            console.warn(`Lỗi khi thực thi handler cho SignalR event "${eventName}":`, err);
          }
        });
      });
    });

    conn.onreconnecting(() => {
      if (!isCancelled) setConnectionState(signalR.HubConnectionState.Reconnecting);
    });

    conn.onreconnected(() => {
      if (!isCancelled) setConnectionState(signalR.HubConnectionState.Connected);
    });

    conn.onclose(() => {
      if (!isCancelled) {
        setConnectionState(signalR.HubConnectionState.Disconnected);
        setConnection(null);
      }
    });

    conn
      .start()
      .then(() => {
        if (!isCancelled) {
          setConnection(conn);
          setConnectionState(signalR.HubConnectionState.Connected);
        }
      })
      .catch((err) => {
        if (!isCancelled) {
          console.warn('Không thể kết nối SignalR Hub:', err?.message || err);
          setConnection(null);
          setConnectionState(signalR.HubConnectionState.Disconnected);
        }
      });

    return () => {
      isCancelled = true;
      conn.stop().catch(() => {});
      setConnection(null);
      setConnectionState(signalR.HubConnectionState.Disconnected);
    };
  }, [user]);

  const value = useMemo(() => ({
    connection,
    connectionState,
    isConnected: connectionState === signalR.HubConnectionState.Connected,
    on,
    off,
    joinClassroom,
    leaveClassroom,
    joinPayment,
    leavePayment
  }), [connection, connectionState, on, off, joinClassroom, leaveClassroom, joinPayment, leavePayment]);

  return (
    <SignalRContext.Provider value={value}>
      {children}
    </SignalRContext.Provider>
  );
}
