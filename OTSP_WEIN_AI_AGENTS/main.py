import logging
from fastapi import FastAPI, Depends, Request
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field

from core.config import settings
from core.security import verify_internal_key
from database.memory import warmup_cache
from agent.luong import run_agent_workflow
from agent.actions import AgentResponse
from services.student_chat import (
    StudyAssistantRequest,
    StudyAssistantResponse,
    solve_study_problem
)
from services.certificate_ocr import (
    VerifyCertificateRequest,
    VerifyCertificateResponse,
    verify_tutor_certificate
)

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("wein-agent.main")

app = FastAPI(
    title="OTSP Wein AI Agent Service",
    version="1.0.0",
    description="Dịch vụ AI Agent tự hành: Tự động điều hướng, phân trang, lọc gia sư, giải bài tập sư phạm và kiểm định chứng chỉ gia sư."
)

@app.middleware("http")
async def add_utf8_charset(request: Request, call_next):
    """Bảo đảm mọi phản hồi JSON đều trả về charset=utf-8 hiển thị tiếng Việt mượt mà"""
    response = await call_next(request)
    content_type = response.headers.get("content-type", "")
    if content_type.startswith("application/json") and "charset=" not in content_type:
        response.headers["content-type"] = f"{content_type}; charset=utf-8"
    return response

# Cấu hình CORS chặt chẽ theo danh sách domain được phép (không dùng wildcard kèm credentials)
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.ALLOWED_ORIGINS,
    allow_credentials=False,
    allow_methods=["GET", "POST", "OPTIONS"],
    allow_headers=["Content-Type", "X-Internal-Key"],
)

class AgentChatRequest(BaseModel):
    """
    Mô hình dữ liệu nhận từ Backend_Project_OTSP (AiService.cs / AgentChatRequestDto)
    - requestId: Mã định danh duy nhất của lượt gọi
    - message: Nội dung tin nhắn người dùng nhập
    - userId: ID của người dùng trong hệ thống (nếu đã đăng nhập)
    - userRole: Vai trò ('Student', 'Tutor', 'Admin', 'Guest')
    - currentPath: Đường dẫn URL hiện tại trên trình duyệt React (vd: /search?tab=tutors)
    - currentPage: Số trang hiện tại
    - conversationHistory: Danh sách tin nhắn cũ trong phiên chat [{'role': 'user'|'assistant', 'content': '...'}]
    - systemContext: Thông tin ngữ cảnh backend làm giàu: {'userName': '...', 'upcomingSessions': [...]}
    """
    requestId: str = Field(default="req-1")
    message: str = Field(min_length=1, max_length=4000)
    userId: int | None = None
    userRole: str = Field(default="Student")
    currentPath: str = Field(default="/")
    currentPage: int = Field(default=1)
    conversationHistory: list[dict] = Field(default_factory=list)
    systemContext: dict = Field(default_factory=dict)

@app.on_event("startup")
def on_startup():
    """Khởi động server: Nạp trước dữ liệu DB vào RAM (Gia sư, Lớp học, Tin tức, Tài liệu) để tìm kiếm < 1ms"""
    logger.info("Server đang khởi động: Nạp trước dữ liệu vào RAM...")
    warmup_cache()

@app.get("/")
def root():
    return {
        "status": "online",
        "service": "OTSP Wein AI Agent Service",
        "version": "1.0.0"
    }

@app.get("/health")
def health():
    return {"status": "healthy"}

# =============================================================================
# ENDPOINT 1: Master Autonomous AI Agent Chat (Chatbot tự hành điều khiển Web)
# =============================================================================
@app.post("/v1/agent/chat", response_model=AgentResponse, dependencies=[Depends(verify_internal_key)])
def agent_chat_endpoint(req: AgentChatRequest):
    """
    Tiếp nhận request từ Backend .NET, chuyển vào luồng Agent workflow xử lý:
    - Nhận diện cá nhân hóa theo userRole và userName
    - Tra cứu lịch học sắp tới (upcomingSessions)
    - Tự động điều hướng trang web (uiAction)
    - Tra cứu Tin tức và Tài liệu học tập từ RAM
    - Nhớ lịch sử hội thoại (conversationHistory)
    """
    result = run_agent_workflow(
        user_message=req.message,
        current_path=req.currentPath,
        current_page=req.currentPage,
        user_role=req.userRole,
        conversation_history=req.conversationHistory,
        system_context=req.systemContext,
        user_id=req.userId
    )
    return result

# =============================================================================
# ENDPOINT 2: Study Assistant (Trợ lý giải bài tập sư phạm chuyên sâu)
# =============================================================================
@app.post("/v1/study-assistant", response_model=StudyAssistantResponse, dependencies=[Depends(verify_internal_key)])
def study_assistant_endpoint(req: StudyAssistantRequest):
    """
    Tiếp nhận bài tập từ học sinh (qua Backend AiService.AskStudyAssistantAsync):
    - Trả về lời giải chi tiết (Step-by-step) với KaTeX LaTeX
    - Tóm tắt công thức trọng tâm (conceptSummary)
    - Đưa ra bài tập tương tự kèm lời giải (practiceQuestion & practiceSolution)
    - Dữ liệu này được Backend tự động lưu vào bảng AiStudySessions để thống kê trên /admin/ai
    """
    logger.info("Đang giải bài tập: subject='%s', grade='%s'", req.subject, req.gradeLevel)
    return solve_study_problem(
        question=req.question,
        subject=req.subject,
        grade_level=req.gradeLevel
    )

# =============================================================================
# ENDPOINT 3: Tutor Certificate OCR & Verification (AI Soi và Thẩm định bằng cấp)
# =============================================================================
@app.post("/v1/tutor/verify-certificate", response_model=VerifyCertificateResponse, dependencies=[Depends(verify_internal_key)])
def verify_certificate_endpoint(req: VerifyCertificateRequest):
    """
    Tiếp nhận ảnh văn bằng/chứng chỉ từ AdminTutorApprovalService.cs:
    - Quét ảnh bằng Gemini Multimodal Vision
    - Đối chiếu tên gia sư và trường cấp bằng
    - Đánh giá tính toàn vẹn (dấu mộc, độ tin cậy)
    - Đưa ra khuyến nghị: APPROVED_RECOMMENDED hoặc MANUAL_REVIEW_REQUIRED
    """
    logger.info("Đang kiểm định chứng chỉ gia sư: expectedTutorName='%s'", req.expectedTutorName)
    return verify_tutor_certificate(req)

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=settings.PORT, reload=True)
