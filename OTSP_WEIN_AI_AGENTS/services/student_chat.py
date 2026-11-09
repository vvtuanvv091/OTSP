import logging
from typing import Any
from pydantic import BaseModel, Field
from llm.client import ask_gemini_json

logger = logging.getLogger("wein-agent.student_chat")

class StudyAssistantRequest(BaseModel):
    """Dữ liệu yêu cầu giải bài tập từ Backend .NET (AiController / AiService)"""
    requestId: str = Field(default="req-study")
    studentId: int = Field(default=0)
    question: str = Field(min_length=1, description="Đề bài hoặc câu hỏi học tập")
    subject: str = Field(default="", description="Tên môn học (vd: Toán, Vật lý, Hóa học...)")
    gradeLevel: str = Field(default="", description="Khối lớp (vd: Lớp 12, Lớp 9...)")

class StudyAssistantResponse(BaseModel):
    """
    Kết quả trả về chuẩn hóa khớp 100% với StudyAssistantResponseDto trong C#
    - answer: Lời giải chi tiết từng bước, định dạng KaTeX LaTeX cho công thức
    - conceptSummary: Tóm tắt 1-2 điểm lý thuyết cốt lõi
    - practiceQuestion: 1 bài tập tương tự đổi số để học sinh tự luyện
    - practiceSolution: Lời giải và đáp số ngắn gọn của bài tập tự luyện
    """
    answer: str = Field(default="", description="Lời giải từng bước chi tiết")
    conceptSummary: str = Field(default="", description="Tóm tắt lý thuyết trọng tâm")
    practiceQuestion: str = Field(default="", description="Bài tập tương tự tự luyện")
    practiceSolution: str = Field(default="", description="Đáp án bài tập tự luyện")

def solve_study_problem(question: str, subject: str = "", grade_level: str = "") -> StudyAssistantResponse:
    """
    Hàm nghiệp vụ: Đóng vai giáo viên sư phạm giải bài tập từng bước cho học sinh.
    Đầu ra là JSON chuẩn {answer, conceptSummary, practiceQuestion, practiceSolution}.
    """
    subject_str = f"Môn: {subject}" if subject else "Môn học phổ thông"
    grade_str = f"Khối lớp: {grade_level}" if grade_level else ""

    system_prompt = (
        "Bạn là Chuyên gia Sư phạm và Giáo viên giỏi của Nền tảng Giáo dục OTSP.\n"
        f"Nhiệm vụ của bạn là giải bài tập cho học sinh ({subject_str} - {grade_str}) theo phong cách sư phạm chuẩn mực.\n\n"
        "YÊU CẦU ĐỊNH DẠNG ĐẦU RA (BẮT BUỘC TRẢ VỀ ĐÚNG JSON VỚI 4 KEYS SAU):\n"
        "1. 'answer': Lời giải chi tiết từng bước (Step-by-step), giải thích lý do biến đổi, "
        "dùng cú pháp LaTeX $công thức$ để công thức hiển thị đẹp mắt qua KaTeX.\n"
        "2. 'conceptSummary': Tóm tắt ngắn gọn 1-2 gạch đầu dòng lý thuyết, công thức hoặc định lý then chốt cần dùng.\n"
        "3. 'practiceQuestion': Đưa ra ĐÚNG 1 câu hỏi/bài tập tương tự (giữ dạng bài, đổi số liệu) để học sinh tự làm củng cố kiến thức.\n"
        "4. 'practiceSolution': Hướng dẫn giải vắn tắt và đáp án cuối cùng của bài tập tương tự đó.\n\n"
        "Chỉ trả về JSON thuần, không dùng markdown ```json bao quanh."
    )

    user_prompt = f"Đề bài cần giải đáp:\n{question.strip()}"

    try:
        data = ask_gemini_json(system_prompt=system_prompt, user_message=user_prompt)
        answer = data.get("answer") or "Lời giải đang được cập nhật."
        concept = data.get("conceptSummary") or "Nắm vững lý thuyết cơ bản của bài học."
        practice_q = data.get("practiceQuestion") or ""
        practice_s = data.get("practiceSolution") or ""

        return StudyAssistantResponse(
            answer=answer,
            conceptSummary=concept,
            practiceQuestion=practice_q,
            practiceSolution=practice_s
        )
    except Exception as exc:
        logger.error("Lỗi khi gọi trợ lý giải bài tập: %s", exc)
        return StudyAssistantResponse(
            answer="Hệ thống đã ghi nhận câu hỏi. Vui lòng kiểm tra lại đề bài hoặc thử lại trong giây lát.",
            conceptSummary="Kiến thức môn học",
            practiceQuestion="",
            practiceSolution=""
        )

