import json
import base64
import logging
import urllib.request
from typing import Any
from pydantic import BaseModel, Field
from google.genai import types
from llm.client import get_gemini_client
from core.config import settings

logger = logging.getLogger("wein-agent.certificate_ocr")

class VerifyCertificateRequest(BaseModel):
    """Yêu cầu kiểm định ảnh chứng chỉ gửi từ AdminTutorApprovalService.cs"""
    imageUrl: str | None = Field(default=None, description="URL ảnh chứng chỉ trên Cloudinary/S3")
    imageBase64: str | None = Field(default=None, description="Chuỗi Base64 của ảnh")
    expectedTutorName: str | None = Field(default=None, description="Họ tên gia sư đăng ký trên hệ thống")
    qualificationType: str | None = Field(default=None, description="Loại văn bằng khai báo (Cử nhân, IELTS...)")

class VerifyCertificateResponse(BaseModel):
    """Kết quả kiểm định trả về khớp 100% với VerifyCertificateResponseDto trong C#"""
    tutorName: str = Field(default="", description="Họ tên đọc được trên mặt bằng")
    certificateType: str = Field(default="", description="Loại văn bằng / chứng chỉ")
    issuingOrganization: str = Field(default="", description="Cơ quan / Trường đại học cấp bằng")
    majorOrSubject: str = Field(default="", description="Ngành học / Chuyên môn")
    scoreOrClassification: str = Field(default="", description="Điểm số hoặc Xếp loại tốt nghiệp")
    issueDate: str = Field(default="", description="Ngày cấp văn bằng")
    expiryDate: str | None = Field(default=None, description="Ngày hết hạn (nếu là chứng chỉ như IELTS, TOEIC)")
    isNameMatched: bool = Field(default=True, description="True nếu tên trên bằng khớp với tên gia sư khai báo")
    authenticityStatus: str = Field(default="GENUINE", description="GENUINE | SUSPICIOUS | UNCLEAR")
    confidenceScore: float = Field(default=0.9, description="Độ tin cậy từ 0.0 đến 1.0")
    anomalies: list[str] = Field(default_factory=list, description="Các điểm bất thường phát hiện được")
    qualificationStandardMet: bool = Field(default=True, description="Đạt chuẩn chuyên môn giảng dạy")
    recommendation: str = Field(default="APPROVED_RECOMMENDED", description="APPROVED_RECOMMENDED | MANUAL_REVIEW_REQUIRED | REJECTED")
    summaryNotes: str = Field(default="", description="Nhận xét tóm tắt bằng tiếng Việt cho Admin")

def _load_image_bytes(image_url: str | None, image_base64: str | None) -> tuple[bytes | None, str]:
    """Tải và chuyển đổi ảnh thành mảng bytes cùng định dạng mime_type"""
    if image_base64:
        try:
            b64_clean = image_base64.strip()
            mime_type = "image/jpeg"
            if b64_clean.startswith("data:image/"):
                header, b64_clean = b64_clean.split(",", 1)
                if "png" in header:
                    mime_type = "image/png"
                elif "webp" in header:
                    mime_type = "image/webp"
            img_bytes = base64.b64decode(b64_clean)
            return img_bytes, mime_type
        except Exception as exc:
            logger.warning("Không thể giải mã imageBase64: %s", exc)

    if image_url:
        try:
            req = urllib.request.Request(
                image_url.strip(),
                headers={"User-Agent": "OTSP-Agent-OCR/1.0"}
            )
            with urllib.request.urlopen(req, timeout=12) as resp:
                img_bytes = resp.read()
                content_type = resp.headers.get("content-type", "image/jpeg").split(";")[0]
                return img_bytes, content_type
        except Exception as exc:
            logger.error("Không thể tải ảnh từ URL %s: %s", image_url, exc)

    return None, "image/jpeg"

def verify_tutor_certificate(req: VerifyCertificateRequest) -> VerifyCertificateResponse:
    """
    Thực hiện OCR đa phương thức bằng Gemini Vision:
    1. Đọc văn bản trên ảnh văn bằng/chứng chỉ
    2. Đối chiếu họ tên người được cấp với expectedTutorName
    3. Đánh giá tính toàn vẹn, con dấu, cơ quan cấp
    """
    img_bytes, mime_type = _load_image_bytes(req.imageUrl, req.imageBase64)
    if not img_bytes:
        return VerifyCertificateResponse(
            tutorName=req.expectedTutorName or "",
            certificateType=req.qualificationType or "Văn bằng chuyên môn",
            issuingOrganization="Không tải được ảnh",
            authenticityStatus="UNCLEAR",
            confidenceScore=0.0,
            anomalies=["Không thể nạp dữ liệu ảnh chứng chỉ từ URL hoặc Base64."],
            qualificationStandardMet=False,
            recommendation="MANUAL_REVIEW_REQUIRED",
            summaryNotes="Lỗi tải ảnh chứng chỉ. Quản trị viên vui lòng kiểm tra thủ công."
        )

    expected_name = req.expectedTutorName or "Không rõ"
    expected_type = req.qualificationType or "Văn bằng/Chứng chỉ"

    system_prompt = (
        "Bạn là Chuyên gia Thẩm định Văn bằng và Bằng cấp của Nền tảng Giáo dục OTSP.\n"
        "Nhiệm vụ của bạn là soi kỹ bức ảnh văn bằng/chứng chỉ được cung cấp và trích xuất các thông tin sau:\n"
        f"- Tên gia sư đăng ký trên hệ thống cần đối chiếu: '{expected_name}'\n"
        f"- Loại bằng/chứng chỉ khai báo: '{expected_type}'\n\n"
        "YÊU CẦU TRẢ VỀ ĐÚNG ĐỊNH DẠNG JSON (không dùng markdown ```json) CÓ CÁC KEYS:\n"
        "{\n"
        "  'tutorName': 'Họ tên đầy đủ ghi trên văn bằng',\n"
        "  'certificateType': 'Tên loại văn bằng (Cử nhân, Thạc sĩ, Chứng chỉ Sư phạm, IELTS, TOEIC...)',\n"
        "  'issuingOrganization': 'Trường đại học, viện hoặc tổ chức cấp bằng',\n"
        "  'majorOrSubject': 'Ngành học, chuyên ngành hoặc môn học',\n"
        "  'scoreOrClassification': 'Xếp loại tốt nghiệp (Xuất sắc, Giỏi, Khá) hoặc điểm số',\n"
        "  'issueDate': 'Ngày tháng năm cấp bằng',\n"
        "  'expiryDate': 'Ngày hết hạn nếu có (vd IELTS 2 năm), null nếu vô thời hạn',\n"
        "  'isNameMatched': true/false (So sánh tên trên bằng với tên gia sư khai báo, cho phép sai khác dấu hoặc thứ tự họ tên nhẹ nếu cùng người),\n"
        "  'authenticityStatus': 'GENUINE' (Rõ nét, có dấu mộc/chữ ký) hoặc 'SUSPICIOUS' (Có dấu vết chỉnh sửa/photoshop) hoặc 'UNCLEAR' (Mờ, mất góc, không đọc được),\n"
        "  'confidenceScore': số thực từ 0.5 đến 1.0 đánh giá mức độ tin cậy,\n"
        "  'anomalies': ['Danh sách các điểm nghi vấn nếu có, để mảng rỗng nếu không có'],\n"
        "  'qualificationStandardMet': true/false (Đạt chuẩn giảng dạy môn học),\n"
        "  'recommendation': 'APPROVED_RECOMMENDED' (Khuyên Admin nên duyệt) hoặc 'MANUAL_REVIEW_REQUIRED' (Cần Admin kiểm tra thêm) hoặc 'REJECTED' (Từ chối vì sai tên/bằng giả),\n"
        "  'summaryNotes': 'Nhận xét tóm tắt ngắn gọn 1-2 câu bằng tiếng Việt cho Admin'\n"
        "}"
    )

    client = get_gemini_client()
    image_part = types.Part.from_bytes(data=img_bytes, mime_type=mime_type)

    try:
        response = client.models.generate_content(
            model=settings.GEMINI_MODEL,
            contents=[image_part, "Hãy thẩm định bức ảnh văn bằng chứng chỉ này theo đúng định dạng JSON yêu cầu."],
            config=types.GenerateContentConfig(
                system_instruction=system_prompt,
                response_mime_type="application/json",
                max_output_tokens=1500,
            )
        )
        data = json.loads(response.text.strip())
        return VerifyCertificateResponse(**data)
    except Exception as exc:
        logger.error("Lỗi khi thẩm định chứng chỉ bằng Gemini Vision: %s", exc)
        return VerifyCertificateResponse(
            tutorName=expected_name,
            certificateType=expected_type,
            issuingOrganization="Chưa xác định",
            authenticityStatus="UNCLEAR",
            confidenceScore=0.5,
            anomalies=["Lỗi xử lý hình ảnh tự động."],
            qualificationStandardMet=False,
            recommendation="MANUAL_REVIEW_REQUIRED",
            summaryNotes="Dịch vụ AI kiểm định tạm thời không phân tích được ảnh. Vui lòng đối chiếu bằng mắt thường."
        )

