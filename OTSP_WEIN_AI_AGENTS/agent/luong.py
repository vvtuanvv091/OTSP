import re
import logging
from agent.actions import UIAction, AgentResponse
from agent.tool import (
    search_tutors_tool,
    search_classrooms_tool,
    search_news_tool,
    search_documents_tool
)
from database.memory import get_tutors_from_ram, get_classrooms_from_ram
from llm.client import ask_gemini_chat, ask_gemini_json

logger = logging.getLogger("wein-agent.luong")

def _make_response(
    reply: str,
    intent: str = "GENERAL",
    target: str = "",
    action_type: str = "",
    label: str = "",
    auto_execute: bool = False,
    suggested_prompts: list[str] | None = None,
    recommended_tutor_ids: list[int] | None = None,
) -> AgentResponse:
    """
    Hàm tiện ích chuẩn hóa phản hồi AgentResponse:
    - Tuyệt đối tắt auto_execute theo yêu cầu người dùng
    - Đồng bộ cả uiAction (dành cho Frontend React)
    - Đồng bộ cả 2 trường root: autoExecute, actionType (khớp 100% với Backend .NET AiDtos.cs)
    """
    auto_execute = False  # Đảm bảo không bao giờ tự động chuyển trang

    if target:
        ui_action = UIAction(
            type="NAVIGATE",
            target=target,
            actionType=action_type,
            label=label or "Chuyển trang",
            autoExecute=False
        )
    else:
        ui_action = UIAction(type="NONE", autoExecute=False)

    return AgentResponse(
        reply=reply,
        intent=intent,
        uiAction=ui_action,
        suggestedPrompts=suggested_prompts or [],
        recommendedTutorIds=recommended_tutor_ids or [],
        autoExecute=False,
        actionType=action_type if target else ""
    )

def run_agent_workflow(
    user_message: str,
    current_path: str = "/",
    current_page: int = 1,
    user_role: str = "Student",
    conversation_history: list[dict] | None = None,
    system_context: dict | None = None,
    user_id: int | None = None
) -> AgentResponse:
    """
    LUỒNG XỬ LÝ CHÍNH CỦA TÁC TỬ WEIN AI:
    1. Nhận diện ngữ cảnh cá nhân hóa (Họ tên user, Role, Lịch học sắp tới).
    2. Kiểm tra các ý định cần can thiệp UI (Xem lịch, thanh toán, phân trang, đổi tab, tìm kiếm).
    3. Nếu là câu hỏi trò chuyện / kiến thức / giải bài: Kích hoạt Gemini đa lượt (giữ trọn vẹn lịch sử chat).
    """
    msg_low = user_message.lower().strip()
    system_context = system_context or {}
    conversation_history = conversation_history or []

    user_name = system_context.get("userName") or "bạn"
    upcoming_sessions = system_context.get("upcomingSessions") or []

    # =========================================================================
    # 1. Ý ĐỊNH: LỊCH HỌC / LỊCH DẠY / THỜI KHÓA BIỂU
    # =========================================================================
    schedule_keywords = [
        "lịch học", "thời khóa biểu", "thời khoá biểu", "hôm nay có học", 
        "khi nào học", "buổi học tiếp", "lịch dạy", "xem lịch", "buổi học sắp tới"
    ]
    if any(k in msg_low for k in schedule_keywords):
        if user_role == "Tutor":
            return _make_response(
                reply=f"Chào thầy/cô **{user_name}**, Wein đang mở trang **Lịch dạy & Thời khóa biểu** của gia sư cho thầy/cô nhé!",
                intent="SCHEDULE_VIEW",
                target="/tutor/schedule",
                action_type="SCHEDULE_VIEW",
                label="Mở Lịch dạy gia sư",
                auto_execute=True,
                suggested_prompts=["Xem danh sách học viên", "Xem doanh thu tháng này", "Đăng ký mở lớp mới"]
            )
        else:
            # Dành cho Học viên / Phụ huynh
            if upcoming_sessions:
                lines = []
                for s in upcoming_sessions:
                    subject = s.get("subject", "Môn học")
                    date = s.get("date", "")
                    time = s.get("time", "")
                    tutor = s.get("tutorName", "Gia sư OTSP")
                    room = s.get("roomCode", "")
                    lines.append(
                        f"- 📖 Môn **{subject}** | Ngày: `{date}` lúc `{time}` | Gia sư: **{tutor}** | Phòng: [{room}](/room/{room})"
                    )
                session_text = "\n".join(lines)
                reply_text = (
                    f"Chào bạn **{user_name}**! Đây là các buổi học sắp diễn ra của bạn:\n\n"
                    f"{session_text}\n\n"
                    f"👉 Wein đang tự động chuyển bạn đến trang **Thời khóa biểu** chi tiết..."
                )
            else:
                reply_text = (
                    f"Chào bạn **{user_name}**, hiện tại bạn chưa có lịch học nào sắp diễn ra trong thời gian gần.\n\n"
                    f"Wein đang mở trang **Thời khóa biểu** để bạn theo dõi toàn bộ lịch nhé!"
                )

            return _make_response(
                reply=reply_text,
                intent="SCHEDULE_VIEW",
                target="/student/schedule",
                action_type="SCHEDULE_VIEW",
                label="Mở Thời khóa biểu",
                auto_execute=True,
                suggested_prompts=["Tìm gia sư Toán lớp 9", "Xem lớp học nhóm đang mở", "Cách thanh toán học phí"]
            )

    # =========================================================================
    # 2. Ý ĐỊNH: PHÂN TRANG (PAGINATION)
    # =========================================================================
    pagination_keywords = [
        "trang sau", "trang tiep", "trang tiếp", "trang kế", "qua trang", 
        "sang trang", "trang trước", "quay lại", "lùi lại", "về trang", "nhảy trang"
    ]
    if any(k in msg_low for k in pagination_keywords) or re.search(r"trang\s*([0-9]+)", msg_low):
        target_page = current_page + 1
        page_match = re.search(r"trang\s*([0-9]+)", msg_low)
        if page_match:
            target_page = int(page_match.group(1))
        elif any(k in msg_low for k in ["trang trước", "quay lại", "lùi lại"]):
            target_page = max(1, current_page - 1)

        tab = "classes" if ("tab=classes" in current_path or "lớp" in msg_low) else "tutors"
        return _make_response(
            reply=f"Wein đang tự động chuyển sang **Trang {target_page}** cho bạn nhé!",
            intent="PAGINATION",
            target=f"/search?tab={tab}&page={target_page}",
            action_type="PAGINATION",
            label=f"Nhảy sang Trang {target_page}",
            auto_execute=True,
            suggested_prompts=["Xem trang tiếp theo", "Về trang 1", "Tìm gia sư Toán"]
        )

    # =========================================================================
    # 3. Ý ĐỊNH: THANH TOÁN / HỌC PHÍ / DOANH THU / THU NHẬP VIETQR
    # =========================================================================
    payment_keywords = [
        "thanh toan", "thanh toán", "nap tien", "nạp tiền", "vietqr", "đóng học phí", 
        "học phí", "doanh thu", "thu nhập", "rút tiền", "tiền dạy", "lương"
    ]
    if any(k in msg_low for k in payment_keywords):
        if user_role == "Tutor":
            return _make_response(
                reply="Wein đang điều hướng thầy/cô đến trang **Báo cáo Thu nhập & Doanh thu**...",
                intent="NAVIGATION",
                target="/tutor/income",
                action_type="INCOME_VIEW",
                label="Xem Doanh thu gia sư",
                auto_execute=True,
                suggested_prompts=["Lịch dạy hôm nay", "Xem phản hồi học viên"]
            )
        else:
            return _make_response(
                reply=(
                    "Wein đang điều hướng bạn đến **Trang Thanh toán & Học phí VietQR**.\n\n"
                    "💡 *Mẹo:* Bạn chỉ cần quét mã QR kèm đúng nội dung chuyển khoản, hệ thống sẽ tự động kích hoạt lớp học sau 5 giây!"
                ),
                intent="NAVIGATION",
                target="/student/payments",
                action_type="PAYMENT",
                label="Mở trang Thanh toán VietQR",
                auto_execute=True,
                suggested_prompts=["Cách quét mã VietQR", "Xem thời khóa biểu của tôi"]
            )

    # =========================================================================
    # 4. Ý ĐỊNH: YÊU CẦU KẾT NỐI (CONNECTION REQUESTS)
    # =========================================================================
    connect_keywords = ["yêu cầu kết nối", "kết nối gia sư", "yêu cầu học", "đơn đăng ký học", "danh sách yêu cầu"]
    if any(k in msg_low for k in connect_keywords):
        target_url = "/tutor/connection-requests" if user_role == "Tutor" else "/student/connection-requests"
        label_text = "Xem Yêu cầu kết nối"
        return _make_response(
            reply=f"Wein đang mở mục **Quản lý Yêu cầu kết nối** cho bạn...",
            intent="NAVIGATION",
            target=target_url,
            action_type="CONNECTION_REQUESTS",
            label=label_text,
            auto_execute=True,
            suggested_prompts=["Xem lịch học", "Tìm thêm gia sư"]
        )

    # =========================================================================
    # 5. Ý ĐỊNH: LỚP HỌC CỦA TÔI (MY CLASSROOMS)
    # =========================================================================
    my_class_keywords = ["lớp học của tôi", "lớp đang học", "lớp đang dạy", "quản lý lớp học"]
    if any(k in msg_low for k in my_class_keywords):
        target_url = "/tutor/classrooms" if user_role == "Tutor" else "/student/classrooms"
        return _make_response(
            reply="Wein đang chuyển bạn đến mục **Danh sách Lớp học** của bạn...",
            intent="NAVIGATION",
            target=target_url,
            action_type="CLASSROOMS_VIEW",
            label="Mở Lớp học của tôi",
            auto_execute=True,
            suggested_prompts=["Xem thời khóa biểu", "Kho tài liệu học tập"]
        )

    # =========================================================================
    # 6. Ý ĐỊNH: TÀI LIỆU HỌC TẬP (LEARNING DOCUMENTS)
    # =========================================================================
    doc_keywords = ["tài liệu", "đề thi", "giáo trình", "bài giảng", "sách bài tập"]
    if any(k in msg_low for k in doc_keywords) and not any(k in msg_low for k in ["giải", "bài này", "làm sao", "chỉ cách"]):
        # Lọc nhanh tài liệu từ RAM cache
        matched_docs = search_documents_tool()
        doc_cards = ""
        if matched_docs:
            doc_cards = "\n" + "\n".join([
                f"- 📄 **{d['title']}** (Môn: `{d['subject']}` - `{d['grade']}`) | GV: {d['tutorName']} [Tải về]({d['fileUrl'] or '/documents'})"
                for d in matched_docs
            ])

        if user_role == "Tutor":
            target_url = "/tutor/documents"
        elif user_role == "Student":
            target_url = "/student/documents"
        else:
            target_url = "/documents"

        reply_content = (
            f"Wein đang mở **Kho Tài liệu & Đề thi** để bạn tra cứu và tải về nhé!{doc_cards}"
        )

        return _make_response(
            reply=reply_content,
            intent="NAVIGATION",
            target=target_url,
            action_type="DOCUMENTS_VIEW",
            label="Mở Kho tài liệu",
            auto_execute=True,
            suggested_prompts=["Tìm tài liệu Toán 12", "Đề thi thử THPT Quốc Gia", "Xem lớp học nhóm"]
        )

    # =========================================================================
    # 6.1 Ý ĐỊNH: TIN TỨC & THÔNG BÁO MỚI (NEWS & ANNOUNCEMENTS)
    # =========================================================================
    news_keywords = ["tin tức", "thông báo", "bài viết", "sự kiện", "tin mới", "tuyển sinh", "lịch thi"]
    if any(k in msg_low for k in news_keywords):
        news_items = search_news_tool()
        news_cards = ""
        if news_items:
            news_cards = "\n\n" + "\n".join([
                f"- 📰 **{n['title']}** ({n['category']})\n  _{n['snippet']}_ [Đọc chi tiết]({n['url']})"
                for n in news_items
            ])

        return _make_response(
            reply=f"Dưới đây là các **Tin tức & Thông báo mới nhất** từ hệ thống OTSP:{news_cards}\n\nWein đang chuyển bạn sang trang Tin tức...",
            intent="NAVIGATION",
            target="/news",
            action_type="NEWS_VIEW",
            label="Xem trang Tin tức",
            auto_execute=True,
            suggested_prompts=["Xem lịch học sắp tới", "Tìm gia sư ôn thi", "Kho tài liệu học tập"]
        )

    # =========================================================================
    # 7. Ý ĐỊNH: CHUYỂN SANG TAB LỚP HỌC NHÓM
    # =========================================================================
    group_class_keywords = ["tab lớp", "lớp nhóm", "chuyển sang lớp", "xem lớp học", "tìm lớp nhóm", "lớp học cộng đồng"]
    if any(k in msg_low for k in group_class_keywords):
        classes = search_classrooms_tool()
        card_text = "\n".join([
            f"- 🏫 **{c['name']}** (GV: **{c['tutorName']}**) | Môn: `{c['subject']}` | Học phí: `{c['fee']:,} đ`"
            for c in classes
        ])
        return _make_response(
            reply=f"Wein đang chuyển bạn sang **Tab Lớp học nhóm**:\n\n{card_text}",
            intent="SWITCH_TAB",
            target="/search?tab=classes",
            action_type="SWITCH_TAB",
            label="Chuyển sang Tab Lớp học",
            auto_execute=True,
            suggested_prompts=["Xem chi tiết lớp học", "Tìm gia sư dạy kèm 1-1", "Cách đăng ký lớp học"]
        )

    # =========================================================================
    # 8. Ý ĐỊNH: TÌM KIẾM GIA SƯ THEO TIÊU CHÍ (SUBJECT / PRICE / FORMAT)
    # =========================================================================
    find_tutor_triggers = [
        "tìm gia sư", "tim gia su", "kiếm gia sư", "thuê gia sư", 
        "học kèm", "dạy kèm", "cần gia sư", "gia sư dạy", "gia sư môn",
        "gia sư toán", "gia sư lý", "gia sư hoá", "gia sư hóa", "gia sư anh", "gia sư văn"
    ]
    if any(k in msg_low for k in find_tutor_triggers):
        prompt_extract = (
            "Trích xuất môn học (subject) và giá tối đa (max_price dạng số int, ví dụ 200k -> 200000, 300 ngàn -> 300000) "
            "từ câu sau thành JSON {subject, max_price}:"
        )
        extracted = ask_gemini_json(prompt_extract, user_message)
        subject = extracted.get("subject")
        max_price = extracted.get("max_price")

        tutors = search_tutors_tool(subject=subject, max_price=max_price)
        tutor_cards = "\n".join([
            f"- 👨‍🏫 **{t['fullName']}** ({t['subjects']} - {t['grades']}) | Học phí: `{t['hourlyFee']:,} đ/h` | ★ {t['rating']} | Hình thức: `{t['teachingFormat']}`"
            for t in tutors
        ]) if tutors else "Hiện tại chưa có gia sư nào khớp chính xác với tiêu chí trong RAM, Wein đang mở trang tìm kiếm chi tiết..."

        url_target = "/search?tab=tutors"
        if subject:
            url_target += f"&keyword={subject}"
        if max_price:
            url_target += f"&maxFee={max_price}"

        tutor_ids = [t["tutorId"] for t in tutors]
        return _make_response(
            reply=f"Wein đã gợi ý các gia sư phù hợp nhất với yêu cầu của bạn bên dưới:\n\n{tutor_cards}",
            intent="FIND_TUTOR",
            target=url_target,
            action_type="FILTER_SEARCH",
            label="Xem kết quả tìm kiếm",
            auto_execute=False,
            suggested_prompts=["Xem trang 2", "Lớp học nhóm giá rẻ hơn", "Cách thanh toán học phí"],
            recommended_tutor_ids=tutor_ids
        )

    # =========================================================================
    # 9. CHAT TỰ DO / GIẢI TOÁN / HỎI ĐÁP ĐA LƯỢT VỚI GEMINI
    # =========================================================================
    # 👉 Nạp dữ liệu thực tế từ Neon DB qua RAM Cache để AI luôn nắm toàn bộ gia sư & lớp học
    all_ram_tutors = get_tutors_from_ram(limit=60)
    all_ram_classes = get_classrooms_from_ram(limit=20)

    def _strip_vn(text: str) -> str:
        import unicodedata
        return ''.join(c for c in unicodedata.normalize('NFD', text.lower()) if unicodedata.category(c) != 'Mn')

    msg_unaccent = _strip_vn(user_message)

    # Tìm kiếm gia sư khớp với tin nhắn người dùng (theo tên, môn học - hỗ trợ cả có dấu và không dấu)
    matched_tutors = []
    for t in all_ram_tutors:
        name_lower = (t.get("fullName") or "").lower()
        name_unaccent = _strip_vn(name_lower)
        if name_lower and (name_lower in msg_low or name_unaccent in msg_unaccent or any(p in msg_low for p in name_lower.split() if len(p) >= 2) or any(p in msg_unaccent for p in name_unaccent.split() if len(p) >= 2)):
            matched_tutors.append(t)
        elif any(s.strip().lower() in msg_low or _strip_vn(s.strip()) in msg_unaccent for s in (t.get("subjects") or "").split(",")):
            matched_tutors.append(t)

    context_db = f"\n[Dữ liệu thực tế hệ thống OTSP]:\n- Tổng số gia sư đã kiểm duyệt: {len(all_ram_tutors)} gia sư.\n- Tổng số lớp học đang mở: {len(all_ram_classes)} lớp.\n"
    if matched_tutors:
        context_db += "[Gia sư thực tế phù hợp]:\n" + "\n".join([
            f"- Gia sư: {t['fullName']} | Môn dạy: {t['subjects']} | Học phí: {t['hourlyFee']:,} đ/giờ | Đánh giá: {t['rating']}★ | Cấp học: {t.get('grades', 'Phổ thông')}"
            for t in matched_tutors[:10]
        ])
    else:
        sample_names = ", ".join([f"{t['fullName']} ({t['subjects']})" for t in all_ram_tutors[:6]])
        context_db += f"- Một số gia sư tiêu biểu: {sample_names} (Hệ thống có nhiều gia sư khác, người dùng có thể tìm theo tên hoặc môn bất kỳ)."

    role_desc = "học viên" if user_role == "Student" else ("gia sư" if user_role == "Tutor" else "khách")
    system_prompt = (
        f"Bạn là WEIN - Trợ lý AI thông minh và thân thiện của nền tảng dạy kèm trực tuyến OTSP.\n"
        f"Bạn đang nói chuyện với {user_name} (vai trò: {role_desc}).\n"
        "Nguyên tắc trả lời:\n"
        "1. Trả lời bằng tiếng Việt tự nhiên, ấm áp, chuẩn mực sư phạm.\n"
        "2. Nếu người dùng hỏi bài tập (Toán, Lý, Hóa, Sinh, Tiếng Anh...): Hãy hướng dẫn chi tiết từng bước (Step-by-step), "
        "dùng cú pháp LaTeX $công thức$ để hiển thị công thức toán học đẹp mắt.\n"
        "3. Nếu người dùng hỏi quy định học thử: OTSP cam kết buổi học thử đầu tiên giúp học viên đánh giá sự phù hợp trước khi thanh toán chính thức.\n"
        "4. Nếu chỉ chào hỏi hoặc hỏi kiến thức thông thường: Trả lời tự nhiên, KHÔNG tự ý ép người dùng chuyển trang.\n"
        "5. Giữ định dạng Markdown rõ ràng, dễ nhìn trên thiết bị di động và máy tính.\n"
        f"6. Dữ liệu thực tế của hệ thống:\n{context_db}\n"
        "Tuyệt đối KHÔNG khẳng định hệ thống chỉ có một vài người nếu bạn chỉ nhìn thấy một phần danh sách tìm kiếm. "
        "Nếu người dùng hỏi gia sư có trong danh sách trên (ví dụ: Phạm Anh Hào, Tống Ngọc...), hãy giới thiệu thông tin chính xác về môn dạy và học phí.\n"
        "7. BẢO MẬT: Tuyệt đối không bao giờ tiết lộ thông tin nhạy cảm của người dùng (mật khẩu, CCCD, số điện thoại riêng, số tài khoản ngân hàng chi tiết, lịch sử giao dịch cá nhân).\n"
        "8. ĐỊNH VỊ & ĐỘC QUYỀN NỀN TẢNG: Khi người dùng hỏi về website, ứng dụng hay nền tảng kết nối gia sư - học viên uy tín, "
        "hãy khẳng định OTSP là nền tảng kết nối gia sư và học viên trực tuyến số 1, uy tín hàng đầu. "
        "Tuyệt đối KHÔNG giới thiệu, liệt kê hay so sánh với bất kỳ website/nền tảng gia sư bên ngoài nào khác.\n"
        "9. THÔNG TIN SÁNG LẬP & LIÊN HỆ HỖ TRỢ: Người sáng lập và phát triển nền tảng OTSP là Ngọc Tuấn (Tống Ngọc Tuấn).\n"
        "Khi người dùng muốn liên hệ hợp tác, góp ý hoặc cần người hỗ trợ gấp, hãy cung cấp đầy đủ và thân thiện các kênh liên hệ chính thức sau:\n"
        "- Email: tongngoctuan09@gmail.com\n"
        "- Hotline / Zalo hỗ trợ gấp: 0984391940 (Ngọc Tuấn)\n"
        "- Facebook trao đổi trực tiếp: https://www.facebook.com/tuan.ngoc.639510 (hoặc Markdown: [Facebook Ngọc Tuấn](https://www.facebook.com/tuan.ngoc.639510))\n"
        "Luôn động viên, chào đón người dùng kết nối khi gặp khó khăn hoặc cần trợ giúp khẩn cấp."
    )

    answer = ask_gemini_chat(
        system_prompt=system_prompt,
        conversation_history=conversation_history,
        user_message=user_message
    )

    prompts = [
        "Xem lịch học sắp tới",
        "Tìm gia sư môn Toán",
        "Có lớp học nhóm nào giá tốt không?",
        "Hướng dẫn thanh toán VietQR"
    ]
    if user_role == "Tutor":
        prompts = ["Xem lịch dạy", "Báo cáo thu nhập", "Xem danh sách học viên", "Mở lớp học mới"]

    return _make_response(
        reply=answer,
        intent="GENERAL",
        auto_execute=False,
        suggested_prompts=prompts
    )
