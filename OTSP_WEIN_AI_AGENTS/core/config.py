import os
from pathlib import Path
from dotenv import load_dotenv

# Tự động nạp file .env từ thư mục gốc
ROOT_DIR = Path(__file__).resolve().parent.parent
load_dotenv(ROOT_DIR / ".env")

class Settings:
    GEMINI_API_KEY: str = os.getenv("GEMINI_API_KEY", "").strip()
    GEMINI_MODEL: str = os.getenv("GEMINI_MODEL", "gemini-3.5-flash-lite").strip()
    INTERNAL_API_KEY: str = os.getenv("INTERNAL_API_KEY", "").strip()
    DATABASE_URL: str = os.getenv("DATABASE_URL", "").strip()
    PORT: int = int(os.getenv("PORT", "8000"))

settings = Settings()