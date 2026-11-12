using System.Net;

namespace Backend_Project_OTSP.Services.Notifications;

public static class NotificationEmailTemplates
{
    private static string Enc(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    public static string Wrap(
        string title,
        string contentHtml,
        string? buttonText = null,
        string? buttonUrl = null,
        string? footerNote = null)
    {
        var btn = (!string.IsNullOrWhiteSpace(buttonText) && !string.IsNullOrWhiteSpace(buttonUrl))
            ? $"<p style='margin:20px 0;text-align:center'><a href='{Enc(buttonUrl)}' style='background:#dc2626;color:#fff;padding:10px 22px;text-decoration:none;border-radius:6px;font-weight:600;display:inline-block'>{Enc(buttonText)}</a></p>"
            : "";

        var note = Enc(footerNote ?? "Thông báo tự động từ Hệ thống Gia sư trực tuyến OTSP.");

        return $"<div style='font-family:Arial,sans-serif;max-width:560px;margin:0 auto;padding:20px;border:1px solid #e2e8f0;border-radius:8px;color:#1e293b;line-height:1.55'><h3 style='color:#dc2626;margin:0 0 12px;padding-bottom:10px;border-bottom:2px solid #dc2626'>{Enc(title)}</h3>{contentHtml}{btn}<hr style='border:0;border-top:1px solid #e2e8f0;margin:18px 0 10px'/><p style='font-size:12px;color:#64748b;margin:0;text-align:center'>{note}</p></div>";
    }

    public static (string Subject, string Body) ForNewBooking(
        string tutorName,
        string studentName,
        string subjectName,
        string actionLabel,
        string? message,
        string actionUrl = "https://otsp-giasu.vercel.app/tutor/connection-requests")
    {
        var subject = $"[OTSP] Yêu cầu kết nối mới từ {studentName}";
        var note = string.IsNullOrWhiteSpace(message)
            ? ""
            : $"<p style='background:#f8fafc;border-left:3px solid #dc2626;padding:8px 12px;margin:12px 0'>\"{Enc(message.Trim())}\"</p>";

        var content = $"<p>Xin chào <strong>{Enc(tutorName)}</strong>,</p><p>Học viên <strong>{Enc(studentName)}</strong> vừa gửi yêu cầu <strong>{Enc(actionLabel)}</strong> môn <strong>{Enc(subjectName)}</strong>.</p>{note}<p>Vui lòng đăng nhập để xem chi tiết và phản hồi sớm.</p>";
        return (subject, Wrap("Yêu cầu kết nối học tập mới", content, "Xem & phản hồi", actionUrl));
    }

    public static (string Subject, string Body) ForBookingAccepted(
        string studentName,
        string tutorName,
        string subjectName,
        bool isTrial,
        string actionUrl)
    {
        var subject = $"[OTSP] Gia sư {tutorName} đã chấp nhận yêu cầu môn {subjectName}";
        var typeDesc = isTrial ? "buổi học thử" : "lớp học chính thức";
        var btnText = isTrial ? "Xem lịch học thử" : "Xem lớp học";

        var content = $"<p>Xin chào <strong>{Enc(studentName)}</strong>,</p><p>Gia sư <strong>{Enc(tutorName)}</strong> đã đồng ý yêu cầu <strong>{typeDesc}</strong> môn <strong>{Enc(subjectName)}</strong> của bạn.</p><p>Thông tin lớp và lịch học đã được cập nhật vào tài khoản OTSP.</p>";
        return (subject, Wrap("Yêu cầu học đã được chấp nhận", content, btnText, actionUrl));
    }

    public static (string Subject, string Body) ForBookingRejected(
        string studentName,
        string tutorName,
        string subjectName,
        string? reason,
        string actionUrl = "https://otsp-giasu.vercel.app/tutors")
    {
        var subject = $"[OTSP] Phản hồi yêu cầu học môn {subjectName}";
        var reasonHtml = string.IsNullOrWhiteSpace(reason)
            ? ""
            : $"<p style='background:#fef2f2;border-left:3px solid #dc2626;padding:8px 12px;margin:12px 0'><strong>Lý do:</strong> {Enc(reason.Trim())}</p>";

        var content = $"<p>Xin chào <strong>{Enc(studentName)}</strong>,</p><p>Gia sư <strong>{Enc(tutorName)}</strong> chưa thể nhận yêu cầu môn <strong>{Enc(subjectName)}</strong> của bạn lúc này.</p>{reasonHtml}<p>Bạn có thể chọn gia sư phù hợp khác trên OTSP.</p>";
        return (subject, Wrap("Phản hồi yêu cầu kết nối", content, "Tìm gia sư khác", actionUrl));
    }

    public static (string Subject, string Body) ForTutorApproved(
        string tutorName,
        string actionUrl = "https://otsp-giasu.vercel.app/tutor/classrooms")
    {
        var subject = "[OTSP] Hồ sơ gia sư của bạn đã được phê duyệt";
        var content = $"<p>Xin chào <strong>{Enc(tutorName)}</strong>,</p><p>Hồ sơ gia sư của bạn đã được phê duyệt. Bạn có thể tạo lớp học, xếp lịch rảnh và tiếp nhận học viên ngay.</p>";
        return (subject, Wrap("Hồ sơ gia sư đã được duyệt", content, "Quản lý lớp học", actionUrl));
    }

    public static (string Subject, string Body) ForTutorRejected(
        string tutorName,
        string? reason,
        string actionUrl = "https://otsp-giasu.vercel.app/tutor/profile")
    {
        var subject = "[OTSP] Thông báo thẩm định hồ sơ gia sư";
        var reasonText = string.IsNullOrWhiteSpace(reason) ? "Hồ sơ chưa đáp ứng đầy đủ thông tin." : reason.Trim();
        var content = $"<p>Xin chào <strong>{Enc(tutorName)}</strong>,</p><p>Hồ sơ gia sư của bạn chưa được duyệt.</p><p style='background:#fef2f2;border-left:3px solid #dc2626;padding:8px 12px;margin:12px 0'><strong>Lý do:</strong> {Enc(reasonText)}</p><p>Vui lòng cập nhật lại hồ sơ hoặc bằng cấp để kiểm duyệt lại.</p>";
        return (subject, Wrap("Kết quả thẩm định hồ sơ", content, "Cập nhật hồ sơ", actionUrl));
    }

    public static (string Subject, string Body) ForComplaintCreated(
        int complaintId,
        string senderName,
        string? senderEmail,
        string timeStr,
        string content,
        string? evidenceUrl)
    {
        var subject = $"[OTSP - Khiếu nại mới #{complaintId}] Từ {senderName}";
        var evHtml = string.IsNullOrWhiteSpace(evidenceUrl)
            ? ""
            : $"<p><strong>Ảnh minh chứng:</strong> <a href='{Enc(evidenceUrl)}'>Xem ảnh</a></p>";
        var bodyHtml = $"<p><strong>Mã:</strong> #{complaintId} | <strong>Người gửi:</strong> {Enc(senderName)} ({Enc(senderEmail ?? "—")})</p><p><strong>Thời gian:</strong> {Enc(timeStr)}</p><p style='background:#f8fafc;border-left:3px solid #dc2626;padding:8px 12px;white-space:pre-wrap'>{Enc(content.Trim())}</p>{evHtml}";
        return (subject, Wrap($"Khiếu nại mới #{complaintId}", bodyHtml));
    }

    public static (string Subject, string Body) ForComplaintUpdated(
        string senderName,
        int complaintId,
        string statusText,
        string contentSnippet,
        string? resolution,
        string actionUrl = "https://otsp-giasu.vercel.app/complaints")
    {
        var subject = $"[OTSP] Cập nhật khiếu nại #{complaintId}";
        var resHtml = string.IsNullOrWhiteSpace(resolution)
            ? ""
            : $"<p style='background:#f8fafc;border-left:3px solid #dc2626;padding:8px 12px;margin:12px 0'><strong>Kết quả:</strong> {Enc(resolution.Trim())}</p>";

        var content = $"<p>Xin chào <strong>{Enc(senderName)}</strong>,</p><p>Khiếu nại <strong>#{complaintId}</strong> của bạn đã chuyển sang trạng thái: <strong>{Enc(statusText)}</strong>.</p><p><strong>Nội dung:</strong> {Enc(contentSnippet)}</p>{resHtml}";
        return (subject, Wrap($"Phản hồi khiếu nại #{complaintId}", content, "Xem khiếu nại", actionUrl));
    }

    public static (string Subject, string Body) ForContactSubmitted(
        string fullName,
        string contactInfo,
        string topic,
        string timeStr,
        string message)
    {
        var subject = $"[OTSP - Liên hệ mới] {topic} từ {fullName}";
        var bodyHtml = $"<p><strong>Họ tên:</strong> {Enc(fullName)} | <strong>Liên hệ:</strong> {Enc(contactInfo)}</p><p><strong>Chủ đề:</strong> {Enc(topic)} | <strong>Thời gian:</strong> {Enc(timeStr)}</p><p style='background:#f8fafc;border-left:3px solid #dc2626;padding:8px 12px;white-space:pre-wrap'>{Enc(message)}</p>";
        return (subject, Wrap("Yêu cầu liên hệ mới", bodyHtml));
    }

    public static (string Subject, string Body) ForPaymentStudentReceipt(
        int paymentId,
        string studentName,
        string className,
        string subjectName,
        string tutorName,
        string amountStr,
        string paidTimeStr)
    {
        var subject = $"[OTSP] Biên lai thanh toán - {className}";
        var content = $"<p>Xin chào <strong>{Enc(studentName)}</strong>,</p><p>Bạn đã thanh toán thành công học phí với thông tin:</p><ul style='padding-left:18px'><li>Mã đơn: <strong>OTSP{paymentId:D6}</strong></li><li>Lớp: <strong>{Enc(className)}</strong> ({Enc(subjectName)})</li><li>Gia sư: <strong>{Enc(tutorName)}</strong></li><li>Số tiền: <strong style='color:#16a34a'>{Enc(amountStr)}</strong></li><li>Thời gian: {Enc(paidTimeStr)}</li></ul><p>Quyền vào phòng học trực tuyến đã được kích hoạt.</p>";
        return (subject, Wrap("Biên lai thanh toán thành công", content));
    }

    public static (string Subject, string Body) ForPaymentTutorNotice(
        string tutorName,
        string studentName,
        string amountStr,
        string className)
    {
        var subject = $"[OTSP] Học viên {studentName} đã nộp học phí lớp {className}";
        var content = $"<p>Kính gửi <strong>{Enc(tutorName)}</strong>,</p><p>Học viên <strong>{Enc(studentName)}</strong> vừa thanh toán <strong>{Enc(amountStr)}</strong> cho lớp <strong>{Enc(className)}</strong>.</p><p>Hệ thống đã tự động cấp quyền vào phòng học cho học viên.</p>";
        return (subject, Wrap("Thông báo thu học phí mới", content));
    }

    public static (string Subject, string Body) ForSessionCancelled(
        string studentName,
        string tutorName,
        string className,
        string timeStr,
        string reason)
    {
        var subject = $"[OTSP] Thông báo hủy buổi học lớp {className}";
        var content = $"<p>Xin chào <strong>{Enc(studentName)}</strong>,</p><p>Gia sư <strong>{Enc(tutorName)}</strong> đã hủy buổi học lớp <strong>{Enc(className)}</strong> lúc <strong>{Enc(timeStr)}</strong>.</p><p><strong>Lý do:</strong> {Enc(reason)}</p><p>Vui lòng theo dõi Thời khóa biểu để cập nhật lịch bù.</p>";
        return (subject, Wrap("Thông báo hủy buổi học", content));
    }

    public static (string Subject, string Body) ForPasswordReset(
        string toEmail,
        string resetToken,
        string resetUrl)
    {
        var subject = "[OTSP] Hướng dẫn đặt lại mật khẩu";
        var content = $"<p>Xin chào,</p><p>Bạn đã yêu cầu đặt lại mật khẩu cho email <strong>{Enc(toEmail)}</strong>.</p><p style='background:#f1f5f9;padding:10px;border-radius:6px;text-align:center;font-weight:bold;letter-spacing:1.5px;color:#dc2626'>{Enc(resetToken)}</p>";
        return (subject, Wrap("Đặt lại mật khẩu OTSP", content, "Đặt lại mật khẩu", resetUrl, "Nếu bạn không thực hiện yêu cầu này, hãy bỏ qua email."));
    }

    public static (string Subject, string Body) ForChatMessage(
        string receiverName,
        string senderName,
        string contentSnippet,
        string actionUrl = "https://otsp-giasu.vercel.app/chat")
    {
        var subject = $"[OTSP] Tin nhắn mới từ {senderName}";
        var content = $"<p>Xin chào <strong>{Enc(receiverName)}</strong>,</p><p><strong>{Enc(senderName)}</strong> vừa nhắn cho bạn:</p><p style='background:#f8fafc;border-left:3px solid #dc2626;padding:8px 12px;font-style:italic'>\"{Enc(contentSnippet)}\"</p>";
        return (subject, Wrap("Tin nhắn mới trên OTSP", content, "Mở trò chuyện", actionUrl, "Hệ thống chỉ gửi tối đa 1 email mỗi 30 phút cho mỗi cuộc trò chuyện."));
    }

    public static (string Subject, string Body) ForClassroomScheduleUpdated(
        string studentName,
        string tutorName,
        string className,
        string scheduleSummary,
        string actionUrl)
    {
        var subject = $"[OTSP] Cập nhật lịch học lớp {className}";
        var content = $"<p>Xin chào <strong>{Enc(studentName)}</strong>,</p><p>Gia sư <strong>{Enc(tutorName)}</strong> vừa cập nhật lịch học định kỳ cho lớp <strong>{Enc(className)}</strong>.</p><p><strong>Lịch học mới:</strong> {Enc(scheduleSummary)}</p><p>Các buổi học chưa diễn ra trong tương lai đã được sắp xếp lại theo lịch mới. Vui lòng kiểm tra Thời khóa biểu để cập nhật.</p>";
        return (subject, Wrap("Cập nhật lịch học lớp học", content, "Xem thời khóa biểu", actionUrl));
    }

    public static (string Subject, string Body) ForClassroomCancelledByAdmin(
        string recipientName,
        string className,
        string reason,
        string actionUrl)
    {
        var subject = $"[OTSP] Thông báo đóng lớp {className}";
        var content = $"<p>Xin chào <strong>{Enc(recipientName)}</strong>,</p><p>Lớp học <strong>{Enc(className)}</strong> đã được quản trị viên đóng trên hệ thống OTSP.</p><p><strong>Lý do:</strong> {Enc(reason)}</p>";
        return (subject, Wrap("Lớp học đã được đóng", content, "Xem danh sách lớp", actionUrl));
    }

    public static (string Subject, string Body) ForSessionCancelledByAdmin(
        string recipientName,
        string className,
        string timeStr,
        string reason,
        string actionUrl)
    {
        var subject = $"[OTSP] Thông báo hủy buổi học lớp {className}";
        var content = $"<p>Xin chào <strong>{Enc(recipientName)}</strong>,</p><p>Buổi học lớp <strong>{Enc(className)}</strong> dự kiến lúc <strong>{Enc(timeStr)}</strong> đã được quản trị viên hủy.</p><p><strong>Lý do:</strong> {Enc(reason)}</p>";
        return (subject, Wrap("Buổi học đã được hủy", content, "Xem lịch học", actionUrl));
    }
}
