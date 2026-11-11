import { useState, useEffect, useRef } from 'react';
import { hubUrl, resolveAssetUrl } from '../../api/config';
import { useSearchParams } from 'react-router-dom';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useAuth } from '../../context/AuthContext';
import { useToast } from '../../context/ToastContext';
import { useConfirm } from '../../context/ConfirmDialogContext';
import chatApi from '../../api/chatApi';

// Hàm helper hợp nhất và chuẩn hóa tin nhắn chống trùng lặp / race condition khi 2 người gửi cùng lúc
function upsertChatMessage(prevList, incoming, currentUserId) {
  if (!incoming || !incoming.id) return prevList;
  const currentUidStr = String(currentUserId || '').trim().toLowerCase();
  const incomingSenderStr = String(incoming.senderId || '').trim().toLowerCase();
  // CHỈ tính isMine dựa trên ID người gửi thực tế khớp với user hiện tại (TUYỆT ĐỐI không dùng incoming.isOwn vì SignalR broadcast chung)
  const isMine = Boolean(currentUidStr) && incomingSenderStr === currentUidStr;

  // 1. Kiểm tra nếu tin nhắn đã tồn tại theo ID thực
  const existingByIdIndex = prevList.findIndex((m) => m.id === incoming.id);
  if (existingByIdIndex !== -1) {
    const updated = [...prevList];
    updated[existingByIdIndex] = {
      ...updated[existingByIdIndex],
      ...incoming,
      isMine
    };
    return updated;
  }

  // 2. Nếu là tin nhắn từ chính user gửi, thay thế optimistic temp message
  if (isMine) {
    const tempIndex = prevList.findIndex((m) =>
      typeof m.id === 'number' &&
      m.id > 1000000000000 &&
      m.content === incoming.content
    );
    if (tempIndex !== -1) {
      const updated = [...prevList];
      updated[tempIndex] = {
        ...incoming,
        isMine: true
      };
      return updated;
    }
  }

  // 3. Nếu chưa có, thêm mới và sắp xếp theo sentAtUtc đảm bảo thứ tự thời gian không bị đảo lộn
  const nextList = [...prevList, { ...incoming, isMine }];
  return nextList.sort((a, b) => {
    const timeA = Number(new Date(a.sentAtUtc || a.sentAt || 0).getTime()) || 0;
    const timeB = Number(new Date(b.sentAtUtc || b.sentAt || 0).getTime()) || 0;
    if (timeA !== timeB) return timeA - timeB;
    return (Number(a.id) || 0) - (Number(b.id) || 0);
  });
}

export default function ChatPage() {
  const { user } = useAuth();
  const toast = useToast();
  const confirm = useConfirm();
  const [searchParams] = useSearchParams();
  const contactIdParam = searchParams.get('contactId') || searchParams.get('recipientId');
  const contactNameParam = searchParams.get('contactName');
  const contactAvatarParam = searchParams.get('contactAvatar');
  const inquiryParam = searchParams.get('inquiry');
  const classTitleParam = searchParams.get('classTitle');

  const [conversations, setConversations] = useState([]);
  const [selectedContact, setSelectedContact] = useState(null);
  const [messages, setMessages] = useState([]);
  const [loadingConversations, setLoadingConversations] = useState(true);
  const [loadingMessages, setLoadingMessages] = useState(false);
  const [inputText, setInputText] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [showEmojiPicker, setShowEmojiPicker] = useState(false);
  const [isMobileChatActive, setIsMobileChatActive] = useState(false);
  const [hubConnected, setHubConnected] = useState(false);
  const [editingMessageId, setEditingMessageId] = useState(null);
  const [editText, setEditText] = useState('');

  const messagesBoxRef = useRef(null);
  const fileInputRef = useRef(null);
  const selectedContactRef = useRef(null);
  const hubConnectionRef = useRef(null);

  useEffect(() => {
    selectedContactRef.current = selectedContact;
  }, [selectedContact]);

  // Tự động điền lời nhắn tư vấn khi mở từ modal "Chi tiết lớp học"
  useEffect(() => {
    if (inquiryParam && classTitleParam && !inputText) {
      const decodedTitle = decodeURIComponent(classTitleParam);
      setInputText(`[Tư vấn lớp: ${decodedTitle}] Em chào thầy/cô, em muốn hỏi thêm thông tin về lịch học và giáo trình của lớp này trước khi đăng ký ạ.`);
    }
  }, [inquiryParam, classTitleParam]);

  const scrollToBottom = () => {
    if (messagesBoxRef.current) {
      messagesBoxRef.current.scrollTop = messagesBoxRef.current.scrollHeight;
    }
  };

  // 1. Lấy danh sách các cuộc trò chuyện
  useEffect(() => {
    let isMounted = true;
    async function loadConversations() {
      try {
        setLoadingConversations(true);
        const res = await chatApi.getConversations();
        if (isMounted && res.data) {
          let list = Array.isArray(res.data) ? res.data : [];

          // Học viên CHỈ được phép chat với Gia sư và Hỗ trợ OTSP (xóa bạn cùng lớp / học viên khác)
          const isStudentUser = user?.role === 'Student_Parent' || user?.role === 'Student' ||
            (Array.isArray(user?.roles) && user.roles.some((r) => String(r).toLowerCase().includes('student')));

          if (isStudentUser) {
            list = list.filter((c) => c.roleLabel !== 'Bạn cùng lớp' && c.roleLabel !== 'Học viên');
          }

          // Khử trùng lặp liên hệ theo contactUserId
          const seenIds = new Set();
          list = list.filter((c) => {
            if (!c.contactUserId) return true;
            const norm = String(c.contactUserId).trim().toLowerCase();
            if (seenIds.has(norm)) return false;
            seenIds.add(norm);
            return true;
          });

          setConversations(list);

          // Nếu có tham số người liên hệ trên URL, tìm và chọn hoặc tạo liên hệ tạm
          if (contactIdParam) {
            const found = list.find(
              (c) => String(c.contactUserId).toLowerCase() === String(contactIdParam).toLowerCase() ||
                String(c.requestId) === String(contactIdParam)
            );
            if (found) {
              if (contactNameParam && (!found.contactName || found.contactName === 'Người dùng OTSP' || found.contactName === 'Thành viên OTSP')) {
                found.contactName = decodeURIComponent(contactNameParam);
              }
              if (contactAvatarParam && !found.contactAvatarUrl) {
                found.contactAvatarUrl = decodeURIComponent(contactAvatarParam);
              }
              setSelectedContact(found);
            } else {
              // Tạo liên hệ tạm thời để người dùng có thể gửi tin nhắn ngay
              const tempContact = {
                contactUserId: contactIdParam,
                contactName: contactNameParam ? decodeURIComponent(contactNameParam) : (classTitleParam ? `Gia sư phụ trách` : 'Thành viên OTSP'),
                roleLabel: 'Gia sư',
                contactAvatarUrl: contactAvatarParam ? decodeURIComponent(contactAvatarParam) : null,
                lastMessageContent: 'Bắt đầu cuộc trò chuyện...',
                unreadCount: 0,
                isOnline: false,
                requestId: null
              };
              setConversations((prev) => [tempContact, ...prev]);
              setSelectedContact(tempContact);
            }
          } else if (list.length > 0 && !selectedContactRef.current) {
            setSelectedContact(list[0]);
          }
        }
      } catch (err) {
        console.warn('Could not load conversations from API:', err);
      } finally {
        if (isMounted) setLoadingConversations(false);
      }
    }

    loadConversations();
    return () => { isMounted = false; };
  }, [contactIdParam, classTitleParam]);

  // 2. Tải lịch sử tin nhắn khi chọn một cuộc trò chuyện
  useEffect(() => {
    let isMounted = true;

    async function loadMessages() {
      if (!selectedContact?.contactUserId) {
        if (isMounted) setMessages([]);
        return;
      }

      try {
        setLoadingMessages(true);
        const res = await chatApi.getMessages(selectedContact.contactUserId, 80);
        if (isMounted && res.data) {
          const list = Array.isArray(res.data) ? res.data : [];
          const myId = String(user?.id || '').trim().toLowerCase();
          const mapped = list.map((m) => {
            const senderStr = String(m.senderId || '').trim().toLowerCase();
            return {
              ...m,
              isMine: Boolean(myId) && senderStr === myId
            };
          });
          setMessages(mapped);
          setTimeout(scrollToBottom, 60);
        }
      } catch (err) {
        console.warn('Could not load messages for contact:', err);
      } finally {
        if (isMounted) setLoadingMessages(false);
      }
    }

    loadMessages();
    return () => { isMounted = false; };
  }, [selectedContact?.contactUserId, user?.id]);

  // Khi chọn một người liên hệ, gia nhập phòng SignalR direct chat
  useEffect(() => {
    if (selectedContact?.contactUserId && hubConnectionRef.current?.state === 'Connected') {
      hubConnectionRef.current.invoke('JoinUserRoom', selectedContact.contactUserId).catch(() => { });
    }
  }, [selectedContact?.contactUserId, hubConnected]);

  // 3. Kết nối Hub chat thời gian thực qua SignalR
  useEffect(() => {
    let isMounted = true;
    const token = localStorage.getItem('token') || sessionStorage.getItem('token') || localStorage.getItem('otsp_token');
    if (!token) return undefined;

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl('/hubs/chat'), {
        accessTokenFactory: () => localStorage.getItem('token') || sessionStorage.getItem('token') || localStorage.getItem('otsp_token') || '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    hubConnectionRef.current = connection;

    const handleIncomingMessage = (newMsg) => {
      if (!newMsg) return;
      const activeContact = selectedContactRef.current;
      const myId = String(user?.id || '').trim().toLowerCase();
      const activeContactId = String(activeContact?.contactUserId || '').trim().toLowerCase();
      const msgSender = String(newMsg.senderId || '').trim().toLowerCase();
      const msgReceiver = String(newMsg.receiverId || '').trim().toLowerCase();

      // Kiểm tra xem tin nhắn có thuộc về hội thoại 1-1 hiện tại đang mở hay không
      const isCurrentConversation =
        Boolean(activeContactId) &&
        ((msgSender === activeContactId && (!msgReceiver || msgReceiver === myId)) ||
          (msgSender === myId && msgReceiver === activeContactId));

      if (isCurrentConversation) {
        setMessages((prev) => upsertChatMessage(prev, newMsg, user?.id));
        setTimeout(scrollToBottom, 60);
      }

      // Cập nhật nội dung xem trước và thời gian trong danh sách cuộc trò chuyện
      setConversations((prev) =>
        prev.map((c) => {
          const cId = String(c.contactUserId || '').trim().toLowerCase();
          const isMatch = cId === msgSender || cId === msgReceiver;

          if (isMatch) {
            return {
              ...c,
              lastMessageContent: newMsg.content,
              lastMessageTimeUtc: newMsg.sentAtUtc || new Date().toISOString(),
              unreadCount:
                isCurrentConversation
                  ? 0
                  : (c.unreadCount || 0) + (msgSender === cId ? 1 : 0)
            };
          }
          return c;
        })
      );
    };

    connection.on('ReceiveMessage', handleIncomingMessage);

    connection.on('MessageEdited', (event) => {
      setMessages((prev) => prev.map((message) => message.id === event.messageId
        ? {
          ...message,
          content: event.content ?? event.newContent,
          isEdited: true,
          editedAtUtc: event.editedAtUtc,
          canEdit: event.canEdit ?? message.canEdit
        }
        : message));
      setConversations((prev) => prev.map((conversation) =>
        conversation.contactUserId === event.senderId || conversation.contactUserId === event.receiverId
          ? { ...conversation, lastMessageContent: event.lastMessage ?? event.content ?? event.newContent }
          : conversation
      ));
    });

    connection.on('MessageDeleted', (event) => {
      setMessages((prev) => prev.map((message) => message.id === event.messageId
        ? { ...message, content: 'Tin nhắn đã được thu hồi', isDeleted: true, canEdit: false }
        : message));
      setConversations((prev) => prev.map((conversation) =>
        conversation.contactUserId === event.senderId || conversation.contactUserId === event.receiverId
          ? { ...conversation, lastMessageContent: event.lastMessage || 'Tin nhắn đã được thu hồi' }
          : conversation
      ));
    });

    // Lắng nghe cập nhật trạng thái Online / Offline tức thì qua SignalR (0 delay)
    connection.on('UserPresenceChanged', (changedUserId, isOnline) => {
      if (!changedUserId) return;
      const targetId = String(changedUserId).trim().toLowerCase();
      const statusBool = Boolean(isOnline);

      setConversations((prev) =>
        prev.map((c) => {
          if (String(c.contactUserId || '').trim().toLowerCase() === targetId) {
            return { ...c, isOnline: statusBool };
          }
          return c;
        })
      );

      setSelectedContact((curr) => {
        if (!curr) return curr;
        if (String(curr.contactUserId || '').trim().toLowerCase() === targetId) {
          return { ...curr, isOnline: statusBool };
        }
        return curr;
      });
    });

    connection
      .start()
      .then(() => {
        if (isMounted) {
          setHubConnected(true);
          if (selectedContactRef.current?.contactUserId) {
            connection.invoke('JoinUserRoom', selectedContactRef.current.contactUserId).catch(() => { });
          }
        }
      })
      .catch((err) => {
        console.warn('SignalR chat connection warning:', err);
      });

    return () => {
      isMounted = false;
      hubConnectionRef.current = null;
      connection.stop().catch(() => { });
    };
  }, [user?.id]);

  // 3.1 Gửi heartbeat hoạt động (TouchActivity) khi người dùng thao tác chuột/phím (giãn cách 45s)
  useEffect(() => {
    let lastActivityTime = 0;
    const sendActivityHeartbeat = () => {
      const now = Date.now();
      if (now - lastActivityTime >= 45000 && hubConnectionRef.current?.state === 'Connected') {
        lastActivityTime = now;
        hubConnectionRef.current.invoke('TouchActivity').catch(() => { });
      }
    };

    const activityEvents = ['mousemove', 'keydown', 'click', 'scroll', 'touchstart'];
    activityEvents.forEach((evt) => window.addEventListener(evt, sendActivityHeartbeat, { passive: true }));

    const handleUnload = () => {
      if (hubConnectionRef.current && hubConnectionRef.current.state === 'Connected') {
        hubConnectionRef.current.stop().catch(() => {});
      }
    };
    window.addEventListener('beforeunload', handleUnload);
    window.addEventListener('pagehide', handleUnload);

    return () => {
      activityEvents.forEach((evt) => window.removeEventListener(evt, sendActivityHeartbeat));
      window.removeEventListener('beforeunload', handleUnload);
      window.removeEventListener('pagehide', handleUnload);
    };
  }, []);

  // 3.2 Tự động đồng bộ trạng thái Online / Offline (sau 3 phút không hoạt động sẽ chuyển sang Offline)
  useEffect(() => {
    const presenceTimer = setInterval(async () => {
      try {
        const res = await chatApi.getConversations();
        if (res.data && Array.isArray(res.data)) {
          const isStudentUser = user?.role === 'Student_Parent' || user?.role === 'Student' ||
            (Array.isArray(user?.roles) && user.roles.some((r) => String(r).toLowerCase().includes('student')));

          let list = res.data;
          if (isStudentUser) {
            list = list.filter((c) => c.roleLabel !== 'Bạn cùng lớp' && c.roleLabel !== 'Học viên');
          }

          setConversations((prev) =>
            prev.map((c) => {
              const fresh = list.find((item) => String(item.contactUserId).toLowerCase() === String(c.contactUserId).toLowerCase());
              return fresh ? { ...c, isOnline: fresh.isOnline } : c;
            })
          );

          setSelectedContact((curr) => {
            if (!curr) return curr;
            const fresh = list.find((item) => String(item.contactUserId).toLowerCase() === String(curr.contactUserId).toLowerCase());
            return fresh ? { ...curr, isOnline: fresh.isOnline } : curr;
          });
        }
      } catch { }
    }, 30000);

    return () => clearInterval(presenceTimer);
  }, [user]);

  // 4. Xử lý gửi tin nhắn văn bản
  const handleSendMessage = async (e) => {
    e?.preventDefault();
    if (!inputText.trim() || !selectedContact?.contactUserId) return;

    const content = inputText.trim();
    setInputText('');

    const tempId = Date.now();
    const optimisticMsg = {
      id: tempId,
      senderId: user?.id || 'me',
      senderName: user?.fullName || 'Tôi',
      receiverId: selectedContact.contactUserId,
      content,
      sentAtUtc: new Date().toISOString(),
      isMine: true
    };

    setMessages((prev) => [...prev, optimisticMsg]);
    setTimeout(scrollToBottom, 50);

    try {
      const res = await chatApi.sendMessage({
        contactUserId: selectedContact.contactUserId,
        content
      });

      if (res.data) {
        setMessages((prev) => {
          const alreadyHasReal = prev.some((m) => m.id === res.data.id);
          if (alreadyHasReal) {
            return prev.filter((m) => m.id !== tempId);
          }
          return prev
            .map((m) => (m.id === tempId ? { ...res.data, isMine: true } : m))
            .sort((a, b) => {
              const timeA = Number(new Date(a.sentAtUtc || a.sentAt || 0).getTime()) || 0;
              const timeB = Number(new Date(b.sentAtUtc || b.sentAt || 0).getTime()) || 0;
              if (timeA !== timeB) return timeA - timeB;
              return (Number(a.id) || 0) - (Number(b.id) || 0);
            });
        });
      }

      // Cập nhật xem trước ngay trên giao diện danh sách chat
      setConversations((prev) =>
        prev.map((c) => {
          const isMatch = String(c.contactUserId).toLowerCase() === String(selectedContact.contactUserId).toLowerCase();
          return isMatch
            ? { ...c, lastMessageContent: content, lastMessageTimeUtc: new Date().toISOString() }
            : c;
        })
      );
    } catch (err) {
      console.error('Lỗi gửi tin nhắn:', err);
      setMessages((prev) => prev.filter((m) => m.id !== tempId));
      const errMsg = err?.response?.data?.message || 'Không thể gửi tin nhắn. Vui lòng thử lại.';
      toast.error(errMsg);
    }
  };

  // 5. Xử lý gửi tệp đính kèm hình ảnh
  const handleSendImage = async (e) => {
    const file = e.target.files?.[0];
    if (!file || !selectedContact?.contactUserId) return;

    let imageMsg = '';
    const tempId = Date.now();

    try {
      // 1. Upload ảnh trực tiếp lên Cloudinary thông qua API
      const uploadRes = await chatApi.uploadAttachment(file);
      const uploadedUrl = uploadRes?.data?.url;
      if (uploadedUrl) {
        imageMsg = `[img]${uploadedUrl}[/img]`;
      }
    } catch (uploadErr) {
      console.warn('Lỗi tải ảnh lên Cloudinary, dùng preview base64:', uploadErr);
    }

    if (!imageMsg) {
      // Fallback base64 nếu upload lỗi
      const dataUrl = await new Promise((resolve) => {
        const reader = new FileReader();
        reader.onload = (ev) => resolve(ev.target.result);
        reader.readAsDataURL(file);
      });
      imageMsg = `[img]${dataUrl}[/img]`;
    }

    const optimisticMsg = {
      id: tempId,
      senderId: user?.id || 'me',
      senderName: user?.fullName || 'Tôi',
      receiverId: selectedContact.contactUserId,
      classroomId: selectedContact.classroomId || null,
      content: imageMsg,
      sentAtUtc: new Date().toISOString(),
      isMine: true
    };

    setMessages((prev) => [...prev, optimisticMsg]);
    setTimeout(scrollToBottom, 50);

    try {
      const res = await chatApi.sendMessage({
        contactUserId: selectedContact.contactUserId,
        content: imageMsg
      });

      if (res.data) {
        setMessages((prev) => {
          const alreadyHasReal = prev.some((m) => m.id === res.data.id);
          if (alreadyHasReal) {
            return prev.filter((m) => m.id !== tempId);
          }
          return prev
            .map((m) => (m.id === tempId ? { ...res.data, isMine: true } : m))
            .sort((a, b) => new Date(a.sentAtUtc || 0) - new Date(b.sentAtUtc || 0));
        });
      }

      setConversations((prev) =>
        prev.map((c) => {
          const isMatch =
            c.contactUserId === selectedContact.contactUserId ||
            (selectedContact.classroomId && c.classroomId === selectedContact.classroomId);
          return isMatch
            ? { ...c, lastMessageContent: '📷 [Hình ảnh]', lastMessageTimeUtc: new Date().toISOString() }
            : c;
        })
      );
    } catch (err) {
      console.error('Lỗi gửi ảnh:', err);
      setMessages((prev) => prev.filter((m) => m.id !== tempId));
    }
    e.target.value = '';
  };

  const handleInsertEmoji = (emoji) => {
    setInputText((prev) => prev + emoji);
    setShowEmojiPicker(false);
  };

  const handleSaveEdit = async (message) => {
    const content = editText.trim();
    if (!content || !message?.id) return;
    try {
      const connection = hubConnectionRef.current;
      if (connection?.state === 'Connected') {
        await connection.invoke('EditMessage', message.id, content);
      } else {
        const response = await chatApi.editMessage(message.id, content);
        const updated = response.data;
        setMessages((prev) => prev.map((item) => item.id === message.id
          ? { ...updated, isMine: true }
          : item));
      }
      setEditingMessageId(null);
      setEditText('');
    } catch (err) {
      toast.error(err?.response?.data?.message || err?.message || 'Không thể chỉnh sửa tin nhắn.');
    }
  };

  const handleDeleteMessage = async (message) => {
    const ok = await confirm({
      title: 'Thu hồi tin nhắn',
      message: 'Bạn có chắc chắn muốn thu hồi tin nhắn này không?',
      confirmText: 'Thu hồi',
      variant: 'danger',
    });
    if (!ok) return;
    try {
      const connection = hubConnectionRef.current;
      if (connection?.state === 'Connected') {
        await connection.invoke('DeleteMessage', message.id);
      } else {
        const response = await chatApi.deleteMessage(message.id);
        const event = response.data;
        setMessages((prev) => prev.map((item) => item.id === message.id
          ? { ...item, content: event.content, isDeleted: true, canEdit: false }
          : item));
        setConversations((prev) => prev.map((conversation) =>
          conversation.contactUserId === event.senderId || conversation.contactUserId === event.receiverId
            ? { ...conversation, lastMessageContent: event.lastMessage }
            : conversation
        ));
      }
    } catch (err) {
      toast.error(err?.response?.data?.message || err?.message || 'Không thể thu hồi tin nhắn.');
    }
  };

  const handleEditKeyDown = (event, message) => {
    if (event.key === 'Escape') {
      setEditingMessageId(null);
      setEditText('');
    } else if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      handleSaveEdit(message);
    }
  };

  const filteredConversations = conversations.filter((c) => {
    return (c.contactName || c.fullName || '').toLowerCase().includes(searchQuery.toLowerCase());
  });

  return (
    <div className={`chat-workspace-page ${isMobileChatActive ? 'is-chatting-mobile' : ''}`} id="chatWorkspace">
      {/* Phong cách CSS tông đỏ thương hiệu OTSP */}
      <style>{`
        .chat-workspace-page {
          width: 100%;
          max-width: 100%;
          margin: 0;
          padding: 0;
        }
        .chat-workspace-card {
          background-color: var(--bs-card-bg, #ffffff);
          border: 1px solid var(--bs-border-color, #eaedf1);
          border-radius: 16px;
          overflow: hidden;
          height: calc(100vh - 180px);
          min-height: 540px;
          display: flex;
          flex-direction: column;
        }
        .chat-sidebar-column {
          display: flex;
          flex-direction: column;
          height: 100%;
          background-color: var(--bs-body-bg, #ffffff);
        }
        .chat-search-input-group {
          position: relative;
          display: flex;
          align-items: center;
        }
        .chat-search-input-group i {
          position: absolute;
          left: 12px;
          font-size: 13px;
          pointer-events: none;
        }
        .chat-search-input {
          border-radius: 20px;
          padding-left: 34px;
          padding-right: 14px;
          font-size: 13px;
          background-color: var(--bs-tertiary-bg, #f8fafc);
          border: 1px solid var(--bs-border-color, #e2e8f0);
        }
        .chat-conversations-list {
          flex-grow: 1;
          overflow-y: auto;
          padding: 8px 12px;
        }
        .chat-conv-item {
          display: flex;
          align-items: center;
          gap: 12px;
          padding: 10px 14px;
          border-radius: 12px;
          cursor: pointer;
          transition: all 0.18s ease;
          margin-bottom: 4px;
          user-select: none;
          border-left: 3px solid transparent;
        }
        .chat-conv-item:hover {
          background-color: var(--bs-tertiary-bg, #f8fafc);
        }
        /* Unified OTSP Red Theme for Active Chat (NO PURPLE) */
        .chat-conv-item.is-active {
          background-color: rgba(219, 48, 58, 0.08) !important;
          border-left: 3px solid #DB303A !important;
        }
        .chat-avatar-wrap {
          width: 44px;
          height: 44px;
          border-radius: 50%;
          overflow: hidden;
          flex-shrink: 0;
          background-color: #f1f5f9;
          display: flex;
          align-items: center;
          justify-content: center;
          border: 1px solid #e2e8f0;
        }
        .chat-avatar-img {
          width: 100%;
          height: 100%;
          object-fit: cover;
        }
        .chat-avatar-fallback {
          font-size: 16px;
          font-weight: 700;
          color: #DB303A;
        }
        .chat-conv-name {
          font-size: 14px;
          font-weight: 700;
          color: var(--bs-body-color, #0f172a);
        }
        .chat-conv-preview {
          font-size: 12px;
          color: var(--bs-secondary-color, #64748b);
        }
        /* Unified Red Badge for Unread Messages */
        .chat-unread-badge {
          background-color: #DB303A;
          color: #ffffff;
          font-size: 11px;
          font-weight: 700;
          padding: 2px 7px;
          border-radius: 12px;
          flex-shrink: 0;
        }
        .chat-main-column {
          height: 100%;
          background-color: var(--bs-body-bg, #ffffff);
        }
        .chat-room-header {
          background-color: var(--bs-body-bg, #ffffff);
        }
        .chat-online-dot {
          font-size: 8px;
          vertical-align: middle;
        }
        .chat-messages-body {
          background-color: var(--bs-tertiary-bg, #f8fafc);
          overflow-y: auto;
          display: flex;
          flex-direction: column;
          gap: 12px;
        }
        .chat-message-row {
          display: flex;
          align-items: flex-end;
          width: 100%;
          position: relative;
          margin-bottom: 6px;
          clear: both;
        }
        .chat-message-row.is-mine {
          justify-content: flex-end;
        }
        .chat-message-row.is-theirs {
          justify-content: flex-start;
        }
        .chat-message-row .chat-message-actions {
          display: inline-flex;
          align-items: center;
          gap: 6px;
          margin-right: 8px;
          opacity: 0;
          transition: opacity 0.15s ease;
        }
        .chat-message-row:hover .chat-message-actions,
        .chat-message-row:focus-within .chat-message-actions {
          opacity: 1;
        }
        .btn-msg-action {
          background: transparent;
          border: none;
          color: #64748b;
          padding: 5px 7px;
          border-radius: 6px;
          font-size: 13px;
          cursor: pointer;
          line-height: 1;
          transition: all 0.15s ease;
          display: inline-flex;
          align-items: center;
          justify-content: center;
        }
        .btn-edit-msg {
          background-color: #fde8e8 !important;
          color: #dc2626 !important;
        }
        .btn-edit-msg:hover {
          background-color: #fbd5d5 !important;
          color: #991b1b !important;
        }
        .btn-delete-msg {
          color: #64748b !important;
        }
        .btn-delete-msg:hover {
          color: #dc2626 !important;
          background-color: rgba(220, 38, 38, 0.12) !important;
        }
        .chat-bubble {
          display: inline-block;
          width: fit-content;
          max-width: 100%;
          min-width: 32px;
          padding: 10px 16px;
          border-radius: 18px;
          font-size: 14px;
          line-height: 1.5;
          white-space: pre-wrap;
          word-break: normal;
          overflow-wrap: break-word;
          box-shadow: 0 1px 2px rgba(0,0,0,0.04);
        }
        .chat-bubble-text {
          white-space: pre-wrap;
          word-break: normal;
          overflow-wrap: break-word;
        }
        .chat-message-row.is-mine .chat-bubble {
          background-color: #DB303A;
          color: #ffffff;
          border-bottom-right-radius: 4px;
        }
        .chat-message-row.is-theirs .chat-bubble {
          background-color: #ffffff;
          color: #1e293b;
          border-bottom-left-radius: 4px;
          border: 1px solid #e2e8f0;
        }
        .chat-bubble-image {
          padding: 4px;
          background: transparent !important;
          border: none !important;
          box-shadow: none !important;
        }
        .chat-message-img {
          max-width: 240px;
          max-height: 240px;
          border-radius: 12px;
          display: block;
          cursor: pointer;
          border: 1px solid #e2e8f0;
          background-color: #ffffff;
          object-fit: cover;
          box-shadow: 0 2px 8px rgba(0,0,0,0.08);
        }
        .chat-room-footer {
          background-color: var(--bs-body-bg, #ffffff);
        }
        .btn-chat-tool {
          border: none;
          background: transparent;
          color: var(--bs-secondary-color, #64748b);
          padding: 6px 10px;
          border-radius: 50%;
          cursor: pointer;
          transition: all 0.15s ease;
        }
        .btn-chat-tool:hover {
          color: #DB303A;
          background-color: rgba(219, 48, 58, 0.08);
        }
        .chat-input-rounded {
          border-radius: 20px;
          padding: 8px 16px;
          font-size: 14px;
          border: 1px solid #e2e8f0;
        }
        .chat-input-rounded:focus {
          border-color: #DB303A;
          box-shadow: 0 0 0 0.2rem rgba(219, 48, 58, 0.15);
        }
        .btn-chat-send {
          background-color: #DB303A;
          border-color: #DB303A;
          color: #ffffff;
          border-radius: 20px;
          padding: 8px 20px;
          font-size: 14px;
          font-weight: 600;
          border: none;
          transition: all 0.15s ease;
        }
        .btn-chat-send:hover {
          background-color: #b7212b;
          color: #ffffff;
        }
        .chat-emoji-popover {
          position: absolute;
          bottom: 60px;
          left: 16px;
          z-index: 1050;
          background: #ffffff;
          border: 1px solid #e2e8f0;
          border-radius: 12px;
          width: 260px;
        }
        .chat-emoji-grid {
          display: grid;
          grid-template-columns: repeat(5, 1fr);
          gap: 6px;
        }
        .btn-emoji {
          border: none;
          background: transparent;
          font-size: 20px;
          cursor: pointer;
          border-radius: 6px;
          padding: 4px;
        }
        .btn-emoji:hover {
          background-color: #f1f5f9;
        }
        .chat-message-content {
          position: relative;
          max-width: 82%;
          display: flex;
          flex-direction: column;
          align-items: flex-start;
        }
        .chat-message-row.is-mine .chat-message-content { align-items: flex-end; }
        .chat-message-actions { position: relative; z-index: 2; margin-bottom: 3px; }
        .chat-message-menu-trigger {
          width: 28px;
          height: 24px;
          padding: 0;
          border: 0;
          border-radius: 4px;
          background: transparent;
          color: var(--bs-secondary-color, #4b5563);
        }
        .chat-message-menu-trigger:hover,
        .chat-message-menu-trigger[aria-expanded="true"] { background: var(--bs-tertiary-bg, #f3f4f6); }
        .chat-message-menu {
          position: absolute;
          right: 0;
          top: 26px;
          min-width: 130px;
          padding: 4px;
          border: 1px solid var(--bs-border-color, #d1d5db);
          border-radius: 6px;
          background: var(--bs-body-bg, #fff);
          box-shadow: 0 4px 14px rgba(0, 0, 0, .14);
        }
        .chat-message-menu button {
          display: block;
          width: 100%;
          padding: 7px 9px;
          border: 0;
          border-radius: 4px;
          background: transparent;
          color: var(--bs-body-color, #111827);
          text-align: left;
          font-size: 13px;
        }
        .chat-message-menu button:hover { background: var(--bs-tertiary-bg, #f3f4f6); }
        .chat-bubble.is-deleted {
          background: #e5e7eb !important;
          border: 1px solid #9ca3af !important;
          color: #374151 !important;
          font-style: italic;
        }
        .chat-deleted-message { font-size: 13px; }
        .chat-edited-label { display: block; margin-top: 4px; opacity: .8; font-size: 11px; }
        .chat-edit-form { min-width: min(320px, 70vw); }
        .chat-edit-form textarea {
          width: 100%;
          min-height: 76px;
          padding: 7px;
          border: 1px solid #d1d5db;
          border-radius: 4px;
          resize: vertical;
          color: #111827;
        }
        .chat-edit-form textarea:focus { outline: 2px solid rgba(255,255,255,.65); }
        [data-bs-theme="dark"] .chat-bubble.is-deleted {
          background: #334155 !important;
          border-color: #64748b !important;
          color: #e2e8f0 !important;
        }
        @media (max-width: 575px) {
          .chat-message-content { max-width: 90%; }
          .chat-edit-form { min-width: min(240px, 72vw); }
        }
      `}</style>

      {/* Tiêu đề trang */}
      <div className="chat-page-header mb-3 d-flex align-items-center justify-content-between">
        <div>
          <h1 className="h4 fw-bold mb-1 text-body">Tin nhắn &amp; Trao đổi</h1>
          <p className="text-secondary small mb-0">Hộp thư trao đổi trực tuyến cùng gia sư và quản trị viên</p>
        </div>
        {hubConnected && (
          <span className="badge bg-success-subtle text-success border border-success-subtle rounded-pill px-3 py-1 small">
            <span className="chat-online-dot text-success me-1">●</span> Real-time SignalR
          </span>
        )}
      </div>

      {/* Khung giao diện nhắn tin chính */}
      <div className="chat-workspace-card shadow-sm">
        <div className="row g-0 h-100 flex-nowrap flex-md-wrap">
          {/* ==================== CỘT TRÁI: DANH SÁCH CUỘC TRÒ CHUYỆN ==================== */}
          <aside
            className={`col-12 col-md-5 col-lg-4 chat-sidebar-column border-end ${isMobileChatActive ? 'd-none d-md-flex' : 'd-flex'}`}
            aria-label="Danh sách cuộc trò chuyện"
          >
            {/* Ô tìm kiếm cuộc trò chuyện */}
            <div className="chat-search-wrap p-3 border-bottom">
              <div className="chat-search-input-group">
                <i className="bi bi-search text-secondary" aria-hidden="true"></i>
                <input
                  type="text"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="form-control form-control-sm chat-search-input"
                  placeholder="Tìm cuộc trò chuyện..."
                  aria-label="Tìm cuộc trò chuyện"
                />
              </div>
            </div>

            {/* Danh sách các cuộc trò chuyện */}
            <div className="chat-conversations-list">
              {loadingConversations ? (
                <div className="p-4 text-center text-secondary small">
                  <div className="spinner-border spinner-border-sm text-danger me-2" role="status"></div>
                  Đang tải danh sách cuộc trò chuyện...
                </div>
              ) : filteredConversations.length === 0 ? (
                <div className="p-4 text-center text-secondary small">
                  <i className="bi bi-chat-square-dots fs-3 d-block mb-2 text-muted"></i>
                  Không tìm thấy cuộc trò chuyện nào.
                </div>
              ) : (
                filteredConversations.map((conv) => {
                  const contactId = conv.contactUserId || conv.userId;
                  const contactName = conv.contactName || conv.fullName || 'Người dùng';
                  const avatar = conv.contactAvatarUrl || conv.avatarUrl;
                  const role = conv.roleLabel || 'Thành viên';
                  const lastMsg = conv.lastMessageContent || conv.lastMessage || 'Bắt đầu cuộc trò chuyện...';
                  const isActive = (selectedContact?.contactUserId || selectedContact?.userId) === contactId;

                  return (
                    <div
                      key={contactId}
                      className={`chat-conv-item ${isActive ? 'is-active' : ''}`}
                      onClick={() => {
                        setSelectedContact(conv);
                        setIsMobileChatActive(true);
                        setConversations((prev) => prev.map((c) =>
                          ((c.contactUserId || c.userId) === contactId) ? { ...c, unreadCount: 0 } : c
                        ));
                      }}
                      role="button"
                      tabIndex={0}
                    >
                      <div className="chat-avatar-wrap position-relative">
                        {avatar ? (
                          <img
                            src={resolveAssetUrl(avatar)}
                            alt={contactName}
                            className="chat-avatar-img"
                            onError={(e) => {
                              e.target.style.display = 'none';
                              e.target.nextSibling.style.display = 'flex';
                            }}
                          />
                        ) : null}
                        <div
                          className="chat-avatar-fallback"
                          style={{
                            display: avatar ? 'none' : 'flex',
                            backgroundColor: conv.isGroupClass || conv.contactUserId?.startsWith('class_') ? 'rgba(219, 48, 58, 0.12)' : undefined,
                            color: conv.isGroupClass || conv.contactUserId?.startsWith('class_') ? '#DB303A' : undefined
                          }}
                        >
                          {conv.isGroupClass || conv.contactUserId?.startsWith('class_') ? (
                            <i className="bi bi-people-fill" style={{ fontSize: '15px' }}></i>
                          ) : (
                            contactName.charAt(0).toUpperCase()
                          )}
                        </div>
                        {/* Chấm tròn trạng thái Online / Offline cho liên hệ cá nhân */}
                        {!(conv.isGroupClass || conv.contactUserId?.startsWith('class_')) && (
                          <span
                            className={`position-absolute bottom-0 end-0 rounded-circle border border-white ${conv.isOnline ? 'bg-success' : 'bg-secondary'}`}
                            style={{ width: 10, height: 10 }}
                            title={conv.isOnline ? 'Đang hoạt động' : 'Không hoạt động'}
                          />
                        )}
                      </div>

                      <div className="chat-conv-info flex-grow-1 min-w-0">
                        <div className="d-flex align-items-center justify-content-between mb-1">
                          <span className="chat-conv-name text-truncate">
                            {(conv.isGroupClass || conv.contactUserId?.startsWith('class_')) && (
                              <i className="bi bi-people-fill text-danger me-1"></i>
                            )}
                            {contactName}
                          </span>
                          {role && (
                            <span
                              className="badge bg-secondary-subtle text-secondary border small px-2 py-0.5 ms-1"
                              style={{ fontSize: '0.68rem', fontWeight: 500 }}
                            >
                              {role}
                            </span>
                          )}
                        </div>
                        <p className="chat-conv-preview text-truncate mb-0">
                          {lastMsg.startsWith('[img]') || lastMsg.includes('res.cloudinary.com') || lastMsg.startsWith('/uploads/') ? '📷 [Hình ảnh]' : lastMsg}
                        </p>
                      </div>

                      {conv.unreadCount > 0 && (
                        <span className="chat-unread-badge ms-2">{conv.unreadCount}</span>
                      )}
                    </div>
                  );
                })
              )}
            </div>
          </aside>

          {/* ==================== CỘT PHẢI: KHUNG CHAT ĐANG MỞ ==================== */}
          <section
            className={`col-12 col-md-7 col-lg-8 chat-main-column d-flex flex-column ${!isMobileChatActive ? 'd-none d-md-flex' : 'd-flex'}`}
            aria-label="Nội dung cuộc trò chuyện"
          >
            {selectedContact ? (
              <div className="chat-active-panel d-flex flex-column flex-grow-1 h-100 overflow-hidden">
                {/* Thanh tiêu đề khung chat */}
                <div className="chat-room-header d-flex align-items-center justify-content-between p-2 p-md-3 border-bottom">
                  <div className="d-flex align-items-center gap-2 min-w-0">
                    <button
                      type="button"
                      className="btn btn-sm btn-light border d-md-none me-1 flex-shrink-0"
                      onClick={() => setIsMobileChatActive(false)}
                      title="Quay lại"
                    >
                      <i className="bi bi-chevron-left"></i>
                    </button>

                    <div className="chat-avatar-wrap flex-shrink-0">
                      {selectedContact.contactAvatarUrl || selectedContact.avatarUrl ? (
                        <img
                          src={resolveAssetUrl(selectedContact.contactAvatarUrl || selectedContact.avatarUrl)}
                          alt={selectedContact.contactName || selectedContact.fullName}
                          className="chat-avatar-img"
                          onError={(e) => {
                            e.target.style.display = 'none';
                            e.target.nextSibling.style.display = 'flex';
                          }}
                        />
                      ) : null}
                      <div
                        className="chat-avatar-fallback"
                        style={{
                          display: (selectedContact.contactAvatarUrl || selectedContact.avatarUrl) ? 'none' : 'flex'
                        }}
                      >
                        {(selectedContact.contactName || selectedContact.fullName || 'U').charAt(0).toUpperCase()}
                      </div>
                    </div>

                    <div className="min-w-0">
                      <div className="d-flex align-items-center gap-1 gap-md-2 flex-wrap">
                        <h2 className="h6 fw-bold mb-0 text-body text-truncate">
                          {selectedContact.contactName || selectedContact.fullName}
                        </h2>
                        {selectedContact.roleLabel && (
                          <span
                            className="badge bg-danger-subtle text-danger border border-danger-subtle small px-2 py-0.5"
                            style={{ fontSize: '0.68rem', fontWeight: 600 }}
                          >
                            {selectedContact.roleLabel}
                          </span>
                        )}
                      </div>
                      {selectedContact.isGroupClass || selectedContact.contactUserId?.startsWith('class_') ? (
                        <span className="chat-status-indicator small text-muted">
                          <i className="bi bi-people-fill me-1"></i> {selectedContact.memberCount ? `${selectedContact.memberCount} thành viên` : 'Kênh trao đổi lớp học'}
                        </span>
                      ) : selectedContact.isOnline ? (
                        <span className="chat-status-indicator small text-success">
                          <span className="chat-online-dot text-success" aria-hidden="true">●</span> Đang hoạt động
                        </span>
                      ) : (
                        <span className="chat-status-indicator small text-secondary">
                          <span className="chat-online-dot text-secondary" aria-hidden="true">○</span> Không hoạt động
                        </span>
                      )}
                    </div>
                  </div>

                  <div className="d-flex align-items-center gap-2 flex-shrink-0">
                    {selectedContact.requestId && (
                      <span className="badge text-bg-light border text-secondary small d-none d-sm-inline">
                        Yêu cầu #{selectedContact.requestId}
                      </span>
                    )}
                    {selectedContact.isGroupClass || selectedContact.contactUserId?.startsWith('class_') ? (
                      <span className="badge rounded-pill bg-danger-subtle text-danger border border-danger-subtle px-2 py-1" style={{ fontSize: 11 }}>
                        Nhóm lớp học
                      </span>
                    ) : selectedContact.isOnline ? (
                      <span className="badge rounded-pill bg-success-subtle text-success border border-success-subtle px-2 py-1" style={{ fontSize: 11 }}>
                        Trực tuyến
                      </span>
                    ) : (
                      <span className="badge rounded-pill bg-secondary-subtle text-secondary border px-2 py-1" style={{ fontSize: 11 }}>
                        Ngoại tuyến
                      </span>
                    )}
                  </div>
                </div>

                {/* Banner cảnh báo an toàn OTSP & chống giao dịch ngoài để bảo vệ 20% hoa hồng và quyền lợi học viên */}


                {/* Vùng hiển thị nội dung tin nhắn */}
                <div
                  ref={messagesBoxRef}
                  className="chat-messages-body flex-grow-1 p-2 p-md-3"
                  role="log"
                >
                  {loadingMessages ? (
                    <div className="text-center text-secondary small my-auto py-5">
                      <div className="spinner-border spinner-border-sm text-danger me-2" role="status"></div>
                      Đang tải tin nhắn...
                    </div>
                  ) : messages.length === 0 ? (
                    <div className="text-center text-secondary small my-auto py-5">
                      <i className="bi bi-chat-text fs-2 d-block mb-2 text-muted"></i>
                      Chưa có tin nhắn nào. Hãy gửi lời chào để bắt đầu cuộc trò chuyện!
                    </div>
                  ) : (
                    messages.map((msg) => {
                      const isImg = !msg.isDeleted && (
                        (typeof msg.content === 'string' && msg.content.startsWith('[img]') && msg.content.endsWith('[/img]')) ||
                        (typeof msg.content === 'string' && (
                          msg.content.startsWith('/uploads/chat/') ||
                          msg.content.startsWith('data:image/') ||
                          msg.content.includes('res.cloudinary.com') ||
                          /\.(jpeg|jpg|gif|png|webp)($|\?)/i.test(msg.content)
                        ))
                      );
                      const imgSrc = isImg
                        ? msg.content.startsWith('[img]')
                          ? msg.content.substring(5, msg.content.length - 6)
                          : msg.content
                        : null;

                      return (
                        <div
                          key={msg.id}
                          className={`chat-message-row ${msg.isMine ? 'is-mine' : 'is-theirs'}`}
                        >
                          <div
                            className="d-flex flex-column"
                            style={{
                              maxWidth: '75%',
                              alignItems: msg.isMine ? 'flex-end' : 'flex-start'
                            }}
                          >
                            <div className="d-flex align-items-end gap-1" style={{ maxWidth: '100%', justifyContent: msg.isMine ? 'flex-end' : 'flex-start' }}>
                              {msg.isMine && !msg.isDeleted && editingMessageId !== msg.id && (
                                <div className="chat-message-actions">
                                  {!isImg && (
                                    <button
                                      type="button"
                                      className="btn-msg-action btn-edit-msg"
                                      title="Chỉnh sửa tin nhắn"
                                      aria-label="Sửa"
                                      onClick={() => {
                                        setEditingMessageId(msg.id);
                                        setEditText(msg.content);
                                      }}
                                    >
                                      <i className="bi bi-pencil"></i>
                                    </button>
                                  )}
                                  <button
                                    type="button"
                                    className="btn-msg-action btn-delete-msg"
                                    title="Xóa / Thu hồi tin nhắn"
                                    aria-label="Xóa"
                                    onClick={() => handleDeleteMessage(msg)}
                                  >
                                    <i className="bi bi-trash"></i>
                                  </button>
                                </div>
                              )}

                              <div className={`chat-bubble ${isImg ? 'chat-bubble-image' : ''} ${msg.isDeleted ? 'is-deleted' : ''}`}>
                                {editingMessageId === msg.id ? (
                                  <div className="chat-edit-form">
                                    <textarea
                                      value={editText}
                                      maxLength={2000}
                                      autoFocus
                                      onChange={(event) => setEditText(event.target.value)}
                                      onKeyDown={(event) => handleEditKeyDown(event, msg)}
                                      aria-label="Chỉnh sửa tin nhắn"
                                    />
                                    <div className="d-flex justify-content-end gap-2 mt-2">
                                      <button type="button" className="btn btn-sm btn-light" onClick={() => {
                                        setEditingMessageId(null);
                                        setEditText('');
                                      }}>Hủy</button>
                                      <button type="button" className="btn btn-sm btn-light fw-bold" onClick={() => handleSaveEdit(msg)}>Lưu</button>
                                    </div>
                                  </div>
                                ) : msg.isDeleted ? (
                                  <div className="chat-deleted-message">Tin nhắn đã được thu hồi</div>
                                ) : isImg && imgSrc ? (
                                  <a href={resolveAssetUrl(imgSrc)} target="_blank" rel="noopener noreferrer">
                                    <img
                                      src={resolveAssetUrl(imgSrc)}
                                      alt="Hình ảnh"
                                      className="chat-message-img"
                                      onError={(e) => {
                                        e.target.src = '/assets/img/default-avatar.svg';
                                      }}
                                    />
                                  </a>
                                ) : (
                                  <>
                                    <div className="chat-bubble-text">{msg.content}</div>
                                    {msg.isEdited && <small className="chat-edited-label">(đã chỉnh sửa)</small>}
                                  </>
                                )}
                              </div>
                            </div>
                          </div>
                        </div>
                      );
                    })
                  )}
                </div>

                {/* Thanh nhập liệu tin nhắn */}
                <div className="chat-room-footer p-2 p-md-3 border-top position-relative">
                  {/* Bảng chọn biểu tượng cảm xúc */}
                  {showEmojiPicker && (
                    <div className="chat-emoji-popover shadow-sm p-2">
                      <div className="d-flex align-items-center justify-content-between pb-1 mb-1 border-bottom">
                        <span className="small fw-semibold text-secondary">Biểu tượng cảm xúc</span>
                        <button
                          type="button"
                          className="btn-close btn-close-sm"
                          onClick={() => setShowEmojiPicker(false)}
                          aria-label="Đóng"
                        ></button>
                      </div>
                      <div className="chat-emoji-grid">
                        {['😊', '😂', '👍', '❤️', '📚', '🎓', '💡', '🔥', '⭐', '👏', '✍️', '🎯', '💯', '🤝', '🎉'].map(
                          (emoji) => (
                            <button
                              key={emoji}
                              type="button"
                              className="btn-emoji"
                              onClick={() => handleInsertEmoji(emoji)}
                            >
                              {emoji}
                            </button>
                          )
                        )}
                      </div>
                    </div>
                  )}

                  <form onSubmit={handleSendMessage} className="d-flex align-items-center gap-1 gap-md-2" autoComplete="off">
                    {/* Nút chọn biểu tượng cảm xúc */}
                    <button
                      type="button"
                      className="btn btn-chat-tool"
                      title="Chèn icon / emoji"
                      onClick={() => setShowEmojiPicker(!showEmojiPicker)}
                    >
                      <i className="bi bi-emoji-smile fs-5"></i>
                    </button>

                    {/* Nút tải ảnh đính kèm */}
                    <label className="btn btn-chat-tool mb-0" title="Gửi ảnh" style={{ cursor: 'pointer' }}>
                      <i className="bi bi-image fs-5"></i>
                      <input
                        ref={fileInputRef}
                        type="file"
                        accept="image/png,image/jpeg,image/webp,image/gif"
                        className="d-none"
                        onChange={handleSendImage}
                      />
                    </label>

                    {/* Ô nhập nội dung tin nhắn */}
                    <input
                      type="text"
                      className="form-control chat-input-rounded"
                      placeholder="Nhập tin nhắn..."
                      maxLength={2000}
                      value={inputText}
                      onChange={(e) => setInputText(e.target.value)}
                    />

                    {/* Nút gửi tin nhắn */}
                    <button type="submit" className="btn btn-chat-send" title="Gửi tin nhắn">
                      <i className="bi bi-send-fill me-1 d-none d-sm-inline"></i>
                      <span className="d-none d-sm-inline">Gửi</span>
                      <i className="bi bi-send-fill d-sm-none"></i>
                    </button>
                  </form>
                </div>
              </div>
            ) : (
              <div className="d-flex flex-column align-items-center justify-content-center flex-grow-1 text-center p-4">
                <i className="bi bi-chat-heart text-danger display-4 mb-3" style={{ opacity: 0.8 }}></i>
                <h5 className="fw-semibold text-body">Chào mừng đến với Hộp thư OTSP</h5>
                <p className="text-secondary small mb-0">
                  Chọn một người liên hệ từ danh sách bên trái hoặc nhắn tin trực tiếp từ hồ sơ gia sư / lớp học.
                </p>
              </div>
            )}
          </section>
        </div>
      </div>
    </div>
  );
}
