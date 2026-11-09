import logging
import psycopg2
from psycopg2.extras import RealDictCursor
from core.config import settings

logger = logging.getLogger("wein-agent.db")

def get_db_connection():
    """Tạo kết nối Read-Only tới Neon PostgreSQL"""
    if not settings.DATABASE_URL:
        logger.warning("DATABASE_URL chưa được cấu hình. Chạy ở chế độ không có Database.")
        return None
    try:
        conn = psycopg2.connect(settings.DATABASE_URL, connect_timeout=8)
        return conn
    except Exception as exc:
        logger.error("Lỗi kết nối Neon PostgreSQL: %s", exc)
        return None

def fetch_all_tutors_from_db() -> list[dict]:
    """Truy vấn danh sách gia sư đã duyệt và tài khoản đang hoạt động"""
    conn = get_db_connection()
    if not conn:
        return []
    try:
        with conn.cursor(cursor_factory=RealDictCursor) as cur:
            cur.execute("""
                SELECT 
                    tp."Id" AS tutor_id,
                    u."FullName" AS full_name,
                    tp."HourlyFee" AS hourly_fee,
                    tp."AverageRating" AS rating,
                    tp."TeachingFormat" AS teaching_format,
                    u."AvatarUrl" AS avatar_url,
                    string_agg(DISTINCT s."Name", ', ') AS subjects,
                    string_agg(DISTINCT gl."Name", ', ') AS grades
                FROM "TutorProfiles" tp
                JOIN "Users" u ON tp."UserId" = u."Id"
                LEFT JOIN "TutorSubjects" ts ON tp."Id" = ts."TutorId"
                LEFT JOIN "Subjects" s ON ts."SubjectId" = s."Id"
                LEFT JOIN "GradeLevels" gl ON ts."GradeLevelId" = gl."Id"
                WHERE u."Status" = 0
                GROUP BY tp."Id", u."FullName", tp."HourlyFee", tp."AverageRating", tp."TeachingFormat", u."AvatarUrl"
                ORDER BY tp."AverageRating" DESC, tp."HourlyFee" ASC;
            """)
            rows = cur.fetchall()
            return [
                {
                    "tutorId": r["tutor_id"],
                    "fullName": r["full_name"],
                    "hourlyFee": int(r["hourly_fee"] or 150000),
                    "rating": float(r["rating"] or 0.0),
                    "subjects": r["subjects"] or "Tổng hợp",
                    "grades": r["grades"] or "Phổ thông",
                    "teachingFormat": "Online/Trực tiếp" if r["teaching_format"] == 2 else ("Online" if r["teaching_format"] == 0 else "Trực tiếp"),
                    "profileUrl": f"/search?tab=tutors&tutorId={r['tutor_id']}",
                }
                for r in rows
            ]
    except Exception as exc:
        logger.error("Lỗi khi tải danh sách gia sư: %s", exc)
        return []
    finally:
        conn.close()

def fetch_all_classrooms_from_db() -> list[dict]:
    """Truy vấn các lớp học nhóm đang mở đăng ký"""
    conn = get_db_connection()
    if not conn:
        return []
    try:
        with conn.cursor(cursor_factory=RealDictCursor) as cur:
            cur.execute("""
                SELECT 
                    c."Id" AS classroom_id,
                    c."NameClass" AS class_name,
                    c."Fee" AS fee,
                    s."Name" AS subject_name,
                    gl."Name" AS grade_name,
                    u."FullName" AS tutor_name
                FROM "Classrooms" c
                LEFT JOIN "Subjects" s ON c."SubjectId" = s."Id"
                LEFT JOIN "GradeLevels" gl ON c."GradeLevelId" = gl."Id"
                LEFT JOIN "TutorProfiles" tp ON c."TutorId" = tp."Id"
                LEFT JOIN "Users" u ON tp."UserId" = u."Id"
                WHERE c."Status" = 0
                ORDER BY c."Id" DESC;
            """)
            rows = cur.fetchall()
            return [
                {
                    "classroomId": r["classroom_id"],
                    "NameClass": r["class_name"],
                    "fee": int(r["fee"] or 0),
                    "subject": r["subject_name"] or "Tổng hợp",
                    "grade": r["grade_name"] or "Phổ thông",
                    "tutorName": r["tutor_name"] or "Gia sư OTSP",
                    "classUrl": f"/search?tab=classes&classroomId={r['classroom_id']}",
                }
                for r in rows
            ]
    except Exception as exc:
        logger.error("Lỗi khi tải danh sách lớp học: %s", exc)
        return []
    finally:
        conn.close()

def fetch_all_news_from_db() -> list[dict]:
    """Truy vấn các bài viết tin tức mới nhất từ bảng NewsArticles"""
    conn = get_db_connection()
    if not conn:
        return []
    try:
        with conn.cursor(cursor_factory=RealDictCursor) as cur:
            cur.execute("""
                SELECT 
                    n."Id" AS news_id,
                    n."Title" AS title,
                    n."Content" AS content,
                    n."Category" AS category,
                    n."PublishedAt" AS published_at
                FROM "NewsArticles" n
                ORDER BY n."Id" DESC
                LIMIT 15;
            """)
            rows = cur.fetchall()
            return [
                {
                    "newsId": r["news_id"],
                    "title": r["title"],
                    "snippet": (r["content"][:180] + "...") if r["content"] and len(r["content"]) > 180 else (r["content"] or ""),
                    "category": r["category"] or "Tin tức OTSP",
                    "publishedAt": str(r["published_at"]) if r["published_at"] else "",
                    "url": f"/news/{r['news_id']}"
                }
                for r in rows
            ]
    except Exception as exc:
        logger.error("Lỗi khi tải danh sách tin tức: %s", exc)
        return []
    finally:
        conn.close()

def fetch_all_documents_from_db() -> list[dict]:
    """Truy vấn các tài liệu ôn tập học tập từ bảng LearningDocuments"""
    conn = get_db_connection()
    if not conn:
        return []
    try:
        with conn.cursor(cursor_factory=RealDictCursor) as cur:
            cur.execute("""
                SELECT 
                    d."Id" AS doc_id,
                    d."Title" AS title,
                    d."Description" AS description,
                    d."FileUrl" AS file_url,
                    s."Name" AS subject_name,
                    gl."Name" AS grade_name,
                    u."FullName" AS tutor_name
                FROM "LearningDocuments" d
                LEFT JOIN "Subjects" s ON d."SubjectId" = s."Id"
                LEFT JOIN "GradeLevels" gl ON d."GradeLevelId" = gl."Id"
                LEFT JOIN "TutorProfiles" tp ON d."TutorId" = tp."Id"
                LEFT JOIN "Users" u ON tp."UserId" = u."Id"
                ORDER BY d."Id" DESC
                LIMIT 20;
            """)
            rows = cur.fetchall()
            return [
                {
                    "docId": r["doc_id"],
                    "title": r["title"],
                    "description": r["description"] or "",
                    "fileUrl": r["file_url"] or "",
                    "subject": r["subject_name"] or "Tổng hợp",
                    "grade": r["grade_name"] or "Phổ thông",
                    "tutorName": r["tutor_name"] or "OTSP",
                    "docUrl": "/documents"
                }
                for r in rows
            ]
    except Exception as exc:
        logger.error("Lỗi khi tải danh sách tài liệu: %s", exc)
        return []
    finally:
        conn.close()
