import { Fragment, useState, useEffect, useRef } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import aiApi from '../../api/aiApi';
import { useAuth } from '../../context/AuthContext';
import '../../styles/components/AiAssistantWidget.css';

export default function AiAssistantWidget() {
  const { user, isTutor, isStudent, isAdmin } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const idSeqRef = useRef(1);

  const [isOpen, setIsOpen] = useState(false);
  const [inputMessage, setInputMessage] = useState('');
  const [loading, setLoading] = useState(false);
  const [messages, setMessages] = useState([
    {
      id: 'init-1',
      role: 'assistant',
      content:
        'Xin chào bạn! Mình là **Trợ lý AI Wein** 🎓.\n\n' +
        'Mình có thể trò chuyện, giải bài tập và hỗ trợ thông tin giúp bạn:\n' +
        '- 🔍 **Tìm gia sư & Lớp học nhóm** phù hợp nhất.\n' +
        '- 🧭 **Gợi ý liên kết & điều hướng trang** theo yêu cầu.\n' +
        '- 📐 **Giải bài tập sư phạm** và tra cứu quy định học thử, thanh toán VietQR.\n\n' +
        'Bạn muốn Wein hỗ trợ gì hôm nay?',
      intent: 'GENERAL',
      uiAction: null,
      suggestedPrompts: [
        'Tìm gia sư dạy Toán lớp 9',
        'Có lớp học nhóm nào đang tuyển sinh?',
        'Xem lịch học của tôi',
        'Cách thanh toán học phí bằng VietQR',
      ],
      feedbackGiven: null,
    },
  ]);

  const messagesEndRef = useRef(null);
  const inputRef = useRef(null);

  useEffect(() => {
    if (isOpen) {
      messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
      inputRef.current?.focus();
    }
  }, [isOpen, messages]);

  const userRole = isAdmin ? 'Admin' : isTutor ? 'Tutor' : isStudent ? 'Student' : 'Guest';
  const searchParams = new URLSearchParams(location.search);
  const currentPage = Number(searchParams.get('page')) || 1;

  const handleSendMessage = async (textToSend) => {
    const text = (textToSend || inputMessage).trim();
    if (!text || loading) return;

    const seq = ++idSeqRef.current;
    const userMsg = {
      id: `user-${seq}`,
      role: 'user',
      content: text,
      timestamp: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }),
    };

    setMessages((prev) => [...prev, userMsg]);
    setInputMessage('');
    setLoading(true);

    try {
      const history = messages.slice(-10).map((m) => ({
        role: m.role,
        content: m.content,
      }));

      const payload = {
        requestId: `req-${seq}`,
        message: text,
        userRole: userRole,
        currentPath: location.pathname + location.search,
        currentPage: currentPage,
        conversationHistory: history,
      };

      const res = await aiApi.chatWithAgent(payload);
      const data = res.data;

      const botMsg = {
        id: `bot-${++idSeqRef.current}`,
        role: 'assistant',
        content: data.reply || 'Wein đã ghi nhận yêu cầu của bạn.',
        intent: data.intent || 'GENERAL',
        uiAction: data.uiAction?.type === 'NAVIGATE' ? data.uiAction : null,
        suggestedPrompts: data.suggestedPrompts || [],
        feedbackGiven: null,
        timestamp: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }),
      };

      setMessages((prev) => [...prev, botMsg]);

      // Tắt hoàn toàn tự động chuyển trang theo yêu cầu người dùng
      // Người dùng có thể nhấn nút "Mở trang" hiển thị trong tin nhắn nếu muốn điều hướng.
    } catch (err) {
      console.error('Lỗi khi gửi câu hỏi tới Wein:', err);
      setMessages((prev) => [
        ...prev,
        {
          id: `bot-err-${++idSeqRef.current}`,
          role: 'assistant',
          content: 'Xin lỗi bạn, Wein đang bận kết nối. Bạn có thể thử lại sau giây lát hoặc truy cập [Tìm gia sư](/search) nhé!',
          uiAction: { type: 'NAVIGATE', target: '/search', label: 'Mở trang Tìm gia sư' },
          suggestedPrompts: ['Tìm gia sư môn Toán', 'Xem lớp học đang mở'],
          feedbackGiven: null,
        },
      ]);
    } finally {
      setLoading(false);
    }
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSendMessage();
    }
  };

  const handleFeedback = async (msgId, questionText, answerText, rating) => {
    setMessages((prev) =>
      prev.map((m) => (m.id === msgId ? { ...m, feedbackGiven: rating } : m))
    );
    try {
      await aiApi.submitFeedback({
        question: questionText || 'Câu hỏi',
        answer: answerText,
        rating: rating,
        studentId: user?.id || null,
      });
    } catch (err) {
      console.error('Lỗi lưu feedback Wein:', err);
    }
  };

  const handleActionClick = (target) => {
    if (!target) return;
    if (target.startsWith('http')) {
      window.open(target, '_blank');
    } else {
      navigate(target);
      setIsOpen(false);
    }
  };

  const renderFormattedText = (rawText) => {
    if (!rawText) return null;
    const lines = rawText.split('\n');
    return lines.map((line, lineIdx) => {
      const parts = [];
      const linkRegex = /\[(.*?)\]\((.*?)\)/g;
      let lastIdx = 0;
      let match;

      while ((match = linkRegex.exec(line)) !== null) {
        if (match.index > lastIdx) {
          parts.push(line.substring(lastIdx, match.index));
        }
        const linkText = match[1];
        const linkUrl = match[2];
        parts.push(
          <a
            key={`link-${lineIdx}-${match.index}`}
            href={linkUrl}
            onClick={(e) => {
              if (linkUrl.startsWith('/')) {
                e.preventDefault();
                navigate(linkUrl);
                setIsOpen(false);
              }
            }}
            className="text-danger fw-semibold text-decoration-underline"
            style={{ wordBreak: 'break-word' }}
          >
            {linkText}
          </a>
        );
        lastIdx = match.index + match[0].length;
      }
      if (lastIdx < line.length) {
        parts.push(line.substring(lastIdx));
      }

      const renderedParts = parts.map((part, pIdx) => {
        if (typeof part !== 'string') return part;
        const boldSplit = part.split(/\*\*(.*?)\*\*/g);
        return boldSplit.map((frag, fIdx) => {
          if (fIdx % 2 === 1) {
            return <strong key={`b-${lineIdx}-${pIdx}-${fIdx}`}>{frag}</strong>;
          }
          const codeSplit = frag.split(/`(.*?)`/g);
          return codeSplit.map((cFrag, cIdx) => {
            if (cIdx % 2 === 1) {
              return (
                <code
                  key={`c-${lineIdx}-${pIdx}-${fIdx}-${cIdx}`}
                  className="bg-light text-danger px-1 py-0 rounded border"
                  style={{ fontSize: '0.88em' }}
                >
                  {cFrag}
                </code>
              );
            }
            return cFrag;
          });
        });
      });

      return (
        <Fragment key={`line-${lineIdx}`}>
          {renderedParts}
          {lineIdx < lines.length - 1 && <br />}
        </Fragment>
      );
    });
  };

  if (
    location.pathname.startsWith('/room/') ||
    ['/login', '/register', '/forgot-password'].includes(location.pathname)
  ) {
    return null;
  }

  return (
    <>
      {/* Nút bấm nổi hình chiếc mũ tốt nghiệp quen thuộc */}
      <div
        className="otsp-ai-fab"
        onClick={() => setIsOpen(!isOpen)}
        title="Trợ lý AI Wein (Nhấn để trò chuyện)"
      >
        <span className="otsp-ai-fab-pulse"></span>
        <i className={`bi ${isOpen ? 'bi-chevron-down' : 'bi-stars'}`}></i>
      </div>

      {isOpen && (
        <div className="otsp-ai-window">
          {/* 1. Header giao diện cũ với biểu tượng chiếc mũ */}
          <div className="otsp-ai-header">
            <div className="d-flex align-items-center gap-2">
              <div
                className="bg-white rounded-circle d-flex align-items-center justify-content-center text-danger"
                style={{ width: 34, height: 34, fontSize: 18 }}
              >
                <i className="bi bi-mortarboard-fill"></i>
              </div>
              <div>
                <div className="fw-bold fs-6 lh-1">Trợ lý AI WEIN</div>
                <small className="opacity-75" style={{ fontSize: 11 }}>
                  <i className="bi bi-circle-fill text-success me-1" style={{ fontSize: 8 }}></i>
                  Hỗ trợ học tập & Điều phối giao diện
                </small>
              </div>
            </div>
            <div className="d-flex align-items-center gap-2">
              <button
                type="button"
                className="btn btn-sm text-white p-0 opacity-75"
                style={{ fontSize: 16 }}
                onClick={() => {
                  setMessages([
                    {
                      id: `init-${++idSeqRef.current}`,
                      role: 'assistant',
                      content:
                        'Xin chào bạn! Mình là **Trợ lý AI Wein** 🎓.\n\n' +
                        'Cuộc trò chuyện đã được làm mới. Bạn muốn Wein hỗ trợ gì tiếp theo?',
                      intent: 'GENERAL',
                      uiAction: null,
                      suggestedPrompts: [
                        'Tìm gia sư dạy Toán lớp 9',
                        'Có lớp học nhóm nào đang tuyển sinh?',
                        'Xem lịch học của tôi',
                        'Cách thanh toán học phí bằng VietQR',
                      ],
                      feedbackGiven: null,
                    },
                  ]);
                }}
                title="Làm mới cuộc trò chuyện"
                aria-label="Làm mới"
              >
                <i className="bi bi-arrow-clockwise"></i>
              </button>
              <button
                type="button"
                className="btn btn-sm text-white p-0"
                style={{ fontSize: 20 }}
                onClick={() => setIsOpen(false)}
                aria-label="Đóng"
              >
                <i className="bi bi-x-lg"></i>
              </button>
            </div>
          </div>

          {/* 2. Body tin nhắn */}
          <div className="otsp-ai-body">
            {messages.map((m) => (
              <div
                key={m.id}
                className={m.role === 'user' ? 'otsp-ai-msg-user' : 'otsp-ai-msg-bot'}
              >
                {m.role === 'user' ? (
                  <div>{m.content}</div>
                ) : (
                  <div>
                    {renderFormattedText(m.content)}

                    {m.uiAction && (
                      <div className="otsp-ai-action-card">
                        <span className="text-danger fw-semibold" style={{ fontSize: 12.5 }}>
                          <i className="bi bi-compass me-1"></i> Điều phối trang:
                        </span>
                        <button
                          type="button"
                          className="btn btn-danger btn-sm rounded-pill px-3 py-1 fw-semibold"
                          style={{ fontSize: 12 }}
                          onClick={() => handleActionClick(m.uiAction.target)}
                        >
                          {m.uiAction.label || 'Mở trang'} <i className="bi bi-arrow-right ms-1"></i>
                        </button>
                      </div>
                    )}

                    <div className="d-flex align-items-center justify-content-between mt-2 pt-1 border-top border-light-subtle">
                      <small className="text-muted" style={{ fontSize: 10 }}>
                        {m.timestamp || ''}
                      </small>
                      <div className="d-flex align-items-center gap-1">
                        <button
                          type="button"
                          className={`btn btn-sm py-0 px-1 border-0 ${m.feedbackGiven === 'UP' ? 'text-success fw-bold' : 'text-secondary'
                            }`}
                          style={{ fontSize: 12 }}
                          title="Hữu ích"
                          onClick={() => {
                            const prevUserMsg = messages
                              .slice(0, messages.findIndex((x) => x.id === m.id))
                              .reverse()
                              .find((x) => x.role === 'user');
                            handleFeedback(m.id, prevUserMsg?.content || '', m.content, 'UP');
                          }}
                        >
                          <i className={`bi ${m.feedbackGiven === 'UP' ? 'bi-hand-thumbs-up-fill' : 'bi-hand-thumbs-up'}`}></i>
                        </button>
                        <button
                          type="button"
                          className={`btn btn-sm py-0 px-1 border-0 ${m.feedbackGiven === 'DOWN' ? 'text-danger fw-bold' : 'text-secondary'
                            }`}
                          style={{ fontSize: 12 }}
                          title="Chưa hài lòng"
                          onClick={() => {
                            const prevUserMsg = messages
                              .slice(0, messages.findIndex((x) => x.id === m.id))
                              .reverse()
                              .find((x) => x.role === 'user');
                            handleFeedback(m.id, prevUserMsg?.content || '', m.content, 'DOWN');
                          }}
                        >
                          <i className={`bi ${m.feedbackGiven === 'DOWN' ? 'bi-hand-thumbs-down-fill' : 'bi-hand-thumbs-down'}`}></i>
                        </button>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            ))}

            {loading && (
              <div className="otsp-ai-msg-bot d-flex align-items-center gap-1 py-3">
                <span className="me-2 text-muted" style={{ fontSize: 12 }}>
                  Wein đang trả lời...
                </span>
                <div className="otsp-ai-typing d-inline-flex gap-1">
                  <span></span>
                  <span></span>
                  <span></span>
                </div>
              </div>
            )}
            <div ref={messagesEndRef} />
          </div>

          {/* 3. Thẻ câu hỏi gợi ý */}
          {messages.length > 0 && messages[messages.length - 1].suggestedPrompts?.length > 0 && (
            <div className="px-3 pb-2 d-flex gap-1 overflow-x-auto" style={{ scrollbarWidth: 'none' }}>
              {messages[messages.length - 1].suggestedPrompts.map((p, idx) => (
                <button
                  key={`chip-${idx}`}
                  type="button"
                  className="otsp-ai-prompt-chip"
                  onClick={() => handleSendMessage(p)}
                >
                  {p}
                </button>
              ))}
            </div>
          )}

          {/* 4. Khung nhập tin nhắn */}
          <div className="p-2 border-top bg-body d-flex align-items-center gap-2">
            <textarea
              ref={inputRef}
              type="text"
              className="form-control form-control-sm rounded-pill px-3 py-2 border"
              placeholder="Hỏi Wein bất kỳ điều gì (giải bài, tìm gia sư, quy chế)..."
              value={inputMessage}
              onChange={(e) => setInputMessage(e.target.value)}
              onKeyDown={handleKeyDown}
              disabled={loading}
              style={{ fontSize: 13.5, resize: 'none', maxHeight: 120, overflowY: 'auto' }}
            />
            <button
              type="button"
              className="btn btn-danger btn-sm rounded-circle d-flex align-items-center justify-content-center p-0 flex-shrink-0"
              style={{ width: 36, height: 36, backgroundColor: '#DB303A' }}
              onClick={() => handleSendMessage()}
              disabled={loading || !inputMessage.trim()}
              title="Gửi"
            >
              <i className="bi bi-send-fill" style={{ fontSize: 13 }}></i>
            </button>
          </div>
        </div>
      )}
    </>
  );
}