from typing import Literal
from pydantic import BaseModel, Field

class UIAction(BaseModel):
    """
    Lệnh điều khiển giao diện gửi về cho Frontend React tự thực thi.
    - type: "NAVIGATE" (chuyển hướng), "OPEN_LINK" (mở tab mới), "NONE" (không chuyển).
    - target: Đường dẫn tương đối trong React Router (vd: /search?tab=tutors hoặc /student/schedule).
    - actionType: Nhãn hành vi (PAGINATION, FILTER_SEARCH, SCHEDULE_VIEW, PAYMENT, SWITCH_TAB...).
    - label: Chữ hiển thị trên nút bấm (nếu người dùng muốn bấm lại).
    - autoExecute: True để Widget tự động chuyển trang sau 600ms mà không cần user bấm.
    """
    type: Literal["NAVIGATE", "OPEN_LINK", "NONE"] = "NONE"
    target: str = Field(default="", description="Đường dẫn URL mục tiêu trong React Router")
    actionType: str = Field(default="", description="Loại hành vi tác tử")
    label: str = Field(default="", description="Tên nhãn hiển thị")
    autoExecute: bool = Field(default=True, description="True để Frontend tự động nhảy trang")


class AgentResponse(BaseModel):
    """
    Cấu trúc phản hồi chuẩn gửi về Backend .NET và Frontend React.
    Đồng bộ 100% với AgentChatResponseDto.cs trong Backend_Project_OTSP.
    """
    reply: str = Field(description="Nội dung phản hồi Markdown hiển thị trong bong bóng chat")
    intent: str = Field(default="GENERAL", description="Ý định phát hiện được")
    uiAction: UIAction = Field(default_factory=UIAction, description="Lệnh điều khiển UI")
    suggestedPrompts: list[str] = Field(default_factory=list, description="Các gợi ý câu hỏi tiếp theo (pills)")
    sources: list[str] = Field(default_factory=list, description="Nguồn trích dẫn (nếu có)")
    recommendedTutorIds: list[int] = Field(default_factory=list, description="Danh sách ID gia sư gợi ý")
    
    # 2 trường này nằm ở cấp root để tương thích trực tiếp với Dto của Backend C#
    autoExecute: bool = Field(default=True, description="Tự động nhảy trang ở cấp root")
    actionType: str = Field(default="", description="Loại hành động ở cấp root")

