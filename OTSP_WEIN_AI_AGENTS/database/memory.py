import time
import logging
from database.postgresql import (
    fetch_all_tutors_from_db,
    fetch_all_classrooms_from_db,
    fetch_all_news_from_db,
    fetch_all_documents_from_db,
)

logger = logging.getLogger("wein-agent.memory")

# Bộ nhớ RAM lưu trữ dữ liệu siêu tốc (< 1ms)
_CACHE = {
    "tutors": [],
    "classrooms": [],
    "news": [],
    "documents": [],
    "last_updated": 0
}

CACHE_TTL_SECONDS = 300  # Tự làm mới sau 5 phút

def warmup_cache():
    """Nạp trước toàn bộ dữ liệu từ DB vào RAM lúc server vừa khởi động"""
    global _CACHE
    started = time.perf_counter()
    _CACHE["tutors"] = fetch_all_tutors_from_db()
    _CACHE["classrooms"] = fetch_all_classrooms_from_db()
    _CACHE["news"] = fetch_all_news_from_db()
    _CACHE["documents"] = fetch_all_documents_from_db()
    _CACHE["last_updated"] = time.time()
    elapsed = (time.perf_counter() - started) * 1000
    logger.info(
        "Đã nạp trước vào RAM: %d gia sư, %d lớp học, %d tin tức, %d tài liệu (mất %.1f ms)",
        len(_CACHE["tutors"]), len(_CACHE["classrooms"]), len(_CACHE["news"]), len(_CACHE["documents"]), elapsed
    )

def get_tutors_from_ram(
    keyword: str | None = None,
    subject: str | None = None,
    max_price: int | None = None,
    limit: int = 10
) -> list[dict]:
    """Tìm gia sư trong RAM trong < 2 mili-giây"""
    if time.time() - _CACHE["last_updated"] > CACHE_TTL_SECONDS:
        warmup_cache()

    results = _CACHE["tutors"]

    if keyword:
        kw = keyword.strip().lower()
        results = [
            t for t in results 
            if kw in t["fullName"].lower() or kw in t["subjects"].lower()
        ]

    if subject:
        sb = subject.strip().lower()
        results = [t for t in results if sb in t["subjects"].lower()]

    if max_price:
        results = [t for t in results if t["hourlyFee"] <= max_price]

    return results[:limit]

def get_classrooms_from_ram(
    subject: str | None = None, 
    limit: int = 5
) -> list[dict]:
    """Tìm lớp học nhóm trong RAM"""
    if time.time() - _CACHE["last_updated"] > CACHE_TTL_SECONDS:
        warmup_cache()

    results = _CACHE["classrooms"]
    if subject:
        sb = subject.strip().lower()
        results = [c for c in results if sb in c["subject"].lower()]

    return results[:limit]

def get_news_from_ram(
    keyword: str | None = None,
    limit: int = 5
) -> list[dict]:
    """Tìm tin tức, thông báo mới nhất trong RAM"""
    if time.time() - _CACHE["last_updated"] > CACHE_TTL_SECONDS:
        warmup_cache()

    results = _CACHE["news"]
    if keyword:
        kw = keyword.strip().lower()
        results = [n for n in results if kw in n["title"].lower() or kw in n["snippet"].lower() or kw in n["category"].lower()]

    return results[:limit]

def get_documents_from_ram(
    keyword: str | None = None,
    subject: str | None = None,
    limit: int = 5
) -> list[dict]:
    """Tìm tài liệu ôn thi, bài giảng trong RAM"""
    if time.time() - _CACHE["last_updated"] > CACHE_TTL_SECONDS:
        warmup_cache()

    results = _CACHE["documents"]
    if keyword:
        kw = keyword.strip().lower()
        results = [d for d in results if kw in d["title"].lower() or kw in d["description"].lower() or kw in d["subject"].lower()]

    if subject:
        sb = subject.strip().lower()
        results = [d for d in results if sb in d["subject"].lower()]

    return results[:limit]
