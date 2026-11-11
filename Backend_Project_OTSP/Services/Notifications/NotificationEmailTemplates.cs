namespace Backend_Project_OTSP.Services.Notifications;

public static class NotificationEmailTemplates
{
    public static string Wrap(
        string title,
        string contentHtml,
        string? buttonText = null,
        string? buttonUrl = null,
        string? footerNote = null)
    {
        var buttonHtml = (!string.IsNullOrWhiteSpace(buttonText) && !string.IsNullOrWhiteSpace(buttonUrl))
            ? $@"
                <div style='margin: 28px 0; text-align: center;'>
                    <a href='{buttonUrl}' style='background-color: #dc2626; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 15px; display: inline-block; box-shadow: 0 2px 4px rgba(220, 38, 38, 0.2);'>
                        {buttonText}
                    </a>
                </div>"
            : "";

        var footerText = footerNote ?? "Thông báo tự động từ Hệ thống kết nối Gia sư và Học viên online OTSP.";

        return $@"
            <div style='background-color: #f8fafc; padding: 32px 12px; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6;'>
                <div style='max-width: 580px; margin: 0 auto; background-color: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05);'>
                    <div style='background: linear-gradient(135deg, #dc2626 0%, #b91c1c 100%); padding: 24px; text-align: center;'>
                        <h1 style='color: #ffffff; margin: 0; font-size: 20px; font-weight: 700; letter-spacing: 0.5px;'>OTSP · Nền tảng Gia sư Online</h1>
                    </div>
                    <div style='padding: 28px 24px;'>
                        <h2 style='color: #0f172a; margin-top: 0; margin-bottom: 16px; font-size: 18px; font-weight: 600;'>{title}</h2>
                        {contentHtml}
                        {buttonHtml}
                    </div>
                    <div style='background-color: #f1f5f9; padding: 16px 24px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #64748b; text-align: center;'>
                        <p style='margin: 0 0 6px 0;'>{footerText}</p>
                        <p style='margin: 0; color: #94a3b8;'>© {DateTime.UtcNow.Year} OTSP Platform. Mọi quyền được bảo lưu.</p>
                    </div>
                </div>
            </div>";
    }

    public static (string Subject, string Body) ForNewBooking(
        string tutorName,
        string studentName,
        string subjectName,
        string actionLabel,
        string? message,
        string actionUrl = "https://otsp-giasu.vercel.app/tutor/connection-requests")
    {
        var subject = $"[OTSP] Bạn có yêu cầu kết nối học mới từ học viên {studentName}";
        var noteHtml = string.IsNullOrWhiteSpace(message)
            ? ""
            : $"<blockquote style='background: #f8fafc; border-left: 4px solid #dc2626; padding: 12px 16px; margin: 16px 0; font-style: italic; color: #475569;'>Lời nhắn của học viên: \"{message.Trim()}\"</blockquote>";

        var content = $@"
            <p>Xin chào <strong>{tutorName}</strong>,</p>
            <p>Học viên <strong>{studentName}</strong> vừa gửi cho bạn một yêu cầu <strong>{actionLabel}</strong> môn <strong>{subjectName}</strong> trên nền tảng OTSP.</p>
            {noteHtml}
            <p>Vui lòng đăng nhập vào hệ thống để xem chi tiết thông tin và phản hồi học viên sớm nhất có thể.</p>";

        var body = Wrap("Yêu cầu kết nối học tập mới", content, "Xem và phản hồi yêu cầu", actionUrl);
        return (subject, body);
    }

    public static (string Subject, string Body) ForBookingAccepted(
        string studentName,
        string tutorName,
        string subjectName,
        bool isTrial,
        string actionUrl)
    {
        var subject = $"[OTSP] Gia sư {tutorName} đã chấp nhận yêu cầu học môn {subjectName}!";
        var typeDesc = isTrial ? "buổi học thử" : "vào lớp học chính thức";
        var btnText = isTrial ? "Xem lịch học thử" : "Xem lớp học & thời khóa biểu";

        var content = $@"
            <p>Xin chào <strong>{studentName}</strong>,</p>
            <p>Tuyệt vời! Gia sư <strong>{tutorName}</strong> đã đồng ý nhận yêu cầu <strong>{typeDesc}</strong> môn <strong>{subjectName}</strong> của bạn.</p>
            <p>Lịch học và thông tin lớp học đã được tự động cập nhật vào tài khoản của bạn trên nền tảng OTSP.</p>";

        var body = Wrap("Yêu cầu học đã được chấp nhận! 🎉", content, btnText, actionUrl);
        return (subject, body);
    }

    public static (string Subject, string Body) ForBookingRejected(
        string studentName,
        string tutorName,
        string subjectName,
        string? reason,
        string actionUrl = "https://otsp-giasu.vercel.app/tutors")
    {
        var subject = $"[OTSP] Gia sư {tutorName} đã phản hồi về yêu cầu học môn {subjectName}";
        var reasonBlock = string.IsNullOrWhiteSpace(reason)
            ? ""
            : $"<blockquote style='background: #fef2f2; border-left: 4px solid #dc2626; padding: 12px 16px; margin: 16px 0; color: #991b1b;'><strong>Lý do từ gia sư:</strong> {reason.Trim()}</blockquote>";

        var content = $@"
            <p>Xin chào <strong>{studentName}</strong>,</p>
            <p>Gia sư <strong>{tutorName}</strong> hiện tại chưa thể sắp xếp nhận yêu cầu học môn <strong>{subjectName}</strong> của bạn.</p>
            {reasonBlock}
            <p>Đừng lo lắng, nền tảng OTSP vẫn còn rất nhiều gia sư giỏi và tận tâm khác đang sẵn sàng đồng hành cùng bạn!</p>";

        var body = Wrap("Phản hồi yêu cầu học tập", content, "Tìm gia sư khác ngay", actionUrl);
        return (subject, body);
    }

    public static (string Subject, string Body) ForTutorApproved(
        string tutorName,
        string actionUrl = "https://otsp-giasu.vercel.app/tutor/classrooms")
    {
        var subject = "[OTSP] Chúc mừng! Hồ sơ gia sư của bạn đã được phê duyệt thành công 🎉";
        var content = $@"
            <p>Xin chào <strong>{tutorName}</strong>,</p>
            <p>Ban quản trị OTSP vui mừng thông báo: <strong>Hồ sơ đăng ký gia sư</strong> của bạn đã được thẩm định và phê duyệt chính thức thành công!</p>
            <p>Từ bây giờ, hồ sơ của bạn đã xuất hiện trên danh sách Gia sư OTSP. Bạn có thể bắt đầu tạo lớp học, xếp lịch rảnh và sẵn sàng tiếp nhận các yêu cầu kết nối từ học viên.</p>";

        var body = Wrap("Hồ sơ gia sư đã được duyệt! 🎉", content, "Quản lý lớp học ngay", actionUrl);
        return (subject, body);
    }

    public static (string Subject, string Body) ForTutorRejected(
        string tutorName,
        string? reason,
        string actionUrl = "https://otsp-giasu.vercel.app/tutor/profile")
    {
        var subject = "[OTSP] Thông báo kết quả thẩm định hồ sơ gia sư";
        var reasonText = string.IsNullOrWhiteSpace(reason) ? "Thông tin hồ sơ hoặc giấy tờ chưa đáp ứng quy định." : reason.Trim();
        var content = $@"
            <p>Xin chào <strong>{tutorName}</strong>,</p>
            <p>Rất tiếc, hồ sơ gia sư của bạn hiện chưa đáp ứng đầy đủ tiêu chuẩn xác thực trên nền tảng OTSP.</p>
            <blockquote style='background: #fef2f2; border-left: 4px solid #dc2626; padding: 12px 16px; margin: 16px 0; color: #991b1b;'>
                <strong>Lý do từ ban quản trị:</strong> {reasonText}
            </blockquote>
            <p>Bạn vui lòng đăng nhập vào hệ thống để cập nhật lại thông tin cá nhân, ảnh CCCD hoặc bằng cấp/chứng chỉ theo yêu cầu.</p>";

        var body = Wrap("Kết quả thẩm định hồ sơ gia sư", content, "Cập nhật lại hồ sơ", actionUrl);
        return (subject, body);
    }

    public static (string Subject, string Body) ForComplaintUpdated(
        string senderName,
        int complaintId,
        string statusText,
        string contentSnippet,
        string? resolution,
        string actionUrl = "https://otsp-giasu.vercel.app/complaints")
    {
        var subject = $"[OTSP] Cập nhật kết quả xử lý khiếu nại #{complaintId}";
        var resolutionBlock = string.IsNullOrWhiteSpace(resolution)
            ? ""
            : $"<blockquote style='background: #f8fafc; border-left: 4px solid #dc2626; padding: 12px 16px; margin: 16px 0; color: #1e293b;'><strong>Kết quả từ ban quản trị:</strong> {resolution.Trim()}</blockquote>";

        var content = $@"
            <p>Xin chào <strong>{senderName}</strong>,</p>
            <p>Ban quản trị OTSP đã xử lý và cập nhật trạng thái cho khiếu nại <strong>#{complaintId}</strong> của bạn: <strong>{statusText.ToUpperInvariant()}</strong>.</p>
            <p><strong>Nội dung khiếu nại:</strong> {contentSnippet}</p>
            {resolutionBlock}
            <p>Nếu bạn có bất kỳ thắc mắc nào thêm hoặc cần hỗ trợ chuyên sâu, vui lòng truy cập mục khiếu nại hoặc liên hệ đường dây hỗ trợ của OTSP.</p>";

        var body = Wrap("Phản hồi khiếu nại - OTSP", content, "Xem lịch sử khiếu nại", actionUrl);
        return (subject, body);
    }

    public static (string Subject, string Body) ForChatMessage(
        string receiverName,
        string senderName,
        string contentSnippet,
        string actionUrl = "https://otsp-giasu.vercel.app/chat")
    {
        var subject = $"[OTSP] Bạn có tin nhắn mới từ {senderName}";
        var content = $@"
            <p>Xin chào <strong>{receiverName}</strong>,</p>
            <p>Bạn vừa nhận được tin nhắn mới từ <strong>{senderName}</strong> trên nền tảng OTSP:</p>
            <blockquote style='background: #f8fafc; border-left: 4px solid #dc2626; padding: 12px 16px; margin: 16px 0; font-style: italic; color: #475569;'>
                ""{contentSnippet}""
            </blockquote>
            <p>Vui lòng đăng nhập vào hệ thống để tiếp tục trao đổi.</p>";

        var footerNote = "Để tránh làm phiền, hệ thống chỉ gửi tối đa 1 email mỗi 30 phút cho mỗi cuộc trò chuyện.";
        var body = Wrap("Tin nhắn mới - OTSP", content, "Vào trò chuyện ngay", actionUrl, footerNote);
        return (subject, body);
    }
}

