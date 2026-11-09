import json
import logging
from typing import Any
from google import genai
from google.genai import types
from core.config import settings

logger = logging.getLogger("wein-agent.llm")

# Biến singleton giữ kết nối Client tới Google Gemini
_client: genai.Client | None = None

def get_gemini_client() -> genai.Client:
    """
    Khởi tạo hoặc tái sử dụng một kết nối duy nhất tới Google GenAI API.
    Timeout 15 giây để tránh treo request nếu mạng chậm.
    """
    global _client
    if _client is None:
        if not settings.GEMINI_API_KEY:
            raise RuntimeError("GEMINI_API_KEY chưa được cấu hình trong file .env!")
        _client = genai.Client(
            api_key=settings.GEMINI_API_KEY,
            http_options=types.HttpOptions(timeout=15_000)
        )
    return _client

def ask_gemini(system_prompt: str, user_message: str, max_tokens: int = 1500) -> str:
    """
    Gọi Gemini sinh câu trả lời cho 1 lượt chat đơn lẻ (không có lịch sử trước đó).
    - system_prompt: Chỉ thị ngữ cảnh và vai trò của bot.
    - user_message: Câu hỏi hiện tại của người dùng.
    """
    client = get_gemini_client()
    try:
        response = client.models.generate_content(
            model=settings.GEMINI_MODEL,
            contents=user_message,
            config=types.GenerateContentConfig(
                system_instruction=system_prompt,
                max_output_tokens=max_tokens,
            ),
        )
        if response.text and response.text.strip():
            return response.text.strip()
    except Exception as exc:
        logger.error("Lỗi khi gọi Gemini đơn lẻ: %s", exc)
    return "Xin lỗi, hiện tại Wein đang bận xử lý dữ liệu. Bạn vui lòng thử lại sau giây lát nhé!"

def ask_gemini_chat(
    system_prompt: str,
    conversation_history: list[dict],
    user_message: str,
    max_tokens: int = 1500
) -> str:
    """
    Gọi Gemini có ĐẦY ĐỦ TRÍ NHỚ ĐA LƯỢT (Multi-turn Conversation):
    - Chuyển đổi lịch sử chat (từ Frontend gửi lên) thành mảng `types.Content`.
    - Vai trò 'assistant' từ FE được map sang 'model' của Gemini.
    - Đảm bảo AI nhớ được các câu hỏi và câu trả lời trước đó trong phiên hội thoại.
    """
    client = get_gemini_client()

    # Chuẩn bị danh sách tin nhắn đa lượt
    contents: list[types.Content] = []

    # 1. Nạp tối đa 10 tin nhắn gần nhất từ lịch sử hội thoại để tiết kiệm token và giữ mạch lạc
    recent_history = conversation_history[-10:] if conversation_history else []
    for msg in recent_history:
        role = msg.get("role", "user")
        content_text = msg.get("content", "").strip()
        if not content_text:
            continue
        # Gemini quy định role chỉ có thể là 'user' hoặc 'model'
        gemini_role = "model" if role == "assistant" else "user"
        contents.append(
            types.Content(
                role=gemini_role,
                parts=[types.Part.from_text(text=content_text)]
            )
        )

    # 2. Đưa câu chat mới nhất của người dùng vào cuối chuỗi hội thoại
    contents.append(
        types.Content(
            role="user",
            parts=[types.Part.from_text(text=user_message.strip())]
        )
    )

    try:
        response = client.models.generate_content(
            model=settings.GEMINI_MODEL,
            contents=contents,
            config=types.GenerateContentConfig(
                system_instruction=system_prompt,
                max_output_tokens=max_tokens,
                temperature=0.7,
            ),
        )
        if response.text and response.text.strip():
            return response.text.strip()
    except Exception as exc:
        logger.error("Lỗi khi gọi Gemini đa lượt: %s", exc)

    return "Xin lỗi bạn, Wein đang gặp gián đoạn tạm thời khi kết nối. Bạn hãy hỏi lại nhé!"

def safe_parse_json(text: str) -> dict[str, Any]:
    """
    Parse JSON an toàn, tự động dọn dẹp markdown và khắc phục lỗi LaTeX escape:
    Khi Gemini sinh công thức toán như \frac, \cdot... json.loads thường lỗi Invalid \escape.
    Hàm này tự động chuẩn hóa để nạp JSON mượt mà 100%.
    """
    import re
    clean_text = text.strip()
    if clean_text.startswith("```json"):
        clean_text = clean_text[7:]
    elif clean_text.startswith("```"):
        clean_text = clean_text[3:]
    if clean_text.endswith("```"):
        clean_text = clean_text[:-3]
    clean_text = clean_text.strip()

    try:
        return json.loads(clean_text, strict=False)
    except Exception:
        pass

    try:
        # Thay thế các dấu \ của LaTeX không phải escape chuẩn JSON thành \\
        fixed_text = re.sub(r'\\(?!["\\/bfnrtu])', r'\\\\', clean_text)
        return json.loads(fixed_text, strict=False)
    except Exception as exc:
        logger.warning("Không thể parse JSON từ phản hồi của Gemini: %s", exc)
        return {}

def ask_gemini_json(system_prompt: str, user_message: str, max_tokens: int = 1500) -> dict[str, Any]:
    """
    Gọi Gemini trả về đúng đối tượng JSON chuẩn (phục vụ trích xuất ý định / tham số).
    Ép kiểu response_mime_type="application/json".
    """
    client = get_gemini_client()
    try:
        response = client.models.generate_content(
            model=settings.GEMINI_MODEL,
            contents=user_message,
            config=types.GenerateContentConfig(
                system_instruction=system_prompt + "\nLƯU Ý: Với công thức toán LaTeX trong chuỗi JSON, luôn dùng 2 dấu gạch chéo \\\\ (ví dụ: \\\\frac, \\\\sqrt, \\\\cdot) để JSON hợp lệ.",
                max_output_tokens=max_tokens,
                response_mime_type="application/json",
            ),
        )
        return safe_parse_json(response.text)
    except Exception as exc:
        logger.warning("Lỗi gọi Gemini JSON: %s", exc)
        return {}