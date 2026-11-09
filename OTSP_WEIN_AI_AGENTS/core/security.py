import hmac
from fastapi import Header, HTTPException
from core.config import settings

def verify_internal_key(x_internal_key: str = Header(default="")) -> None:
    """Bảo vệ API nội bộ, chỉ cho phép Backend .NET gọi sang"""
    if not settings.INTERNAL_API_KEY or not hmac.compare_digest(x_internal_key, settings.INTERNAL_API_KEY):
        raise HTTPException(
            status_code=401, 
            detail="Unauthorized: Khóa xác thực X-Internal-Key không hợp lệ."
        )