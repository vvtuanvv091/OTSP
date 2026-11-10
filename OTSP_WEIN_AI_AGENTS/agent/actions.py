from typing import Literal
from pydantic import BaseModel, Field

class UIAction(BaseModel):
    type: Literal["NAVIGATE", "OPEN_LINK", "NONE"] = "NONE"
    target: str = Field(default="", description="Đường dẫn URL mục tiêu trong React Router")
    actionType: str = Field(default="", description="Loại hành vi tác tử")
    label: str = Field(default="", description="Tên nhãn hiển thị")
    autoExecute: bool = Field(default=False, description="False: hiện nút để user tự bấm")


class AgentResponse(BaseModel):
    reply: str = Field(description="Nội dung phản hồi Markdown hiển thị trong bong bóng chat")
    intent: str = Field(default="GENERAL", description="Ý định phát hiện được")
    uiAction: UIAction = Field(default_factory=UIAction, description="Lệnh điều khiển UI")
    suggestedPrompts: list[str] = Field(default_factory=list, description="Các gợi ý câu hỏi tiếp theo (pills)")
    sources: list[str] = Field(default_factory=list, description="Nguồn trích dẫn (nếu có)")
    recommendedTutorIds: list[int] = Field(default_factory=list, description="Danh sách ID gia sư gợi ý")

    autoExecute: bool = Field(default=False, description="Tự động nhảy trang ở cấp root")
    actionType: str = Field(default="", description="Loại hành động ở cấp root")

