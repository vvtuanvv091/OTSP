from database.memory import (
    get_tutors_from_ram,
    get_classrooms_from_ram,
    get_news_from_ram,
    get_documents_from_ram
)

def search_tutors_tool(keyword: str | None = None, subject: str | None = None, max_price: int | None = None) -> list[dict]:
    """Tìm gia sư trong RAM cache (< 1ms)"""
    return get_tutors_from_ram(keyword=keyword, subject=subject, max_price=max_price, limit=3)

def search_classrooms_tool(subject: str | None = None) -> list[dict]:
    """Tìm lớp học nhóm trong RAM cache (< 1ms)"""
    return get_classrooms_from_ram(subject=subject, limit=2)

def search_news_tool(keyword: str | None = None) -> list[dict]:
    """Tìm tin tức, thông báo mới nhất trong RAM cache (< 1ms)"""
    return get_news_from_ram(keyword=keyword, limit=3)

def search_documents_tool(keyword: str | None = None, subject: str | None = None) -> list[dict]:
    """Tìm tài liệu ôn tập trong RAM cache (< 1ms)"""
    return get_documents_from_ram(keyword=keyword, subject=subject, limit=3)
