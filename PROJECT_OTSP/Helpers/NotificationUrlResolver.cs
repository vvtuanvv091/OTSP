namespace PROJECT_OTSP.Helpers;

public static class NotificationUrlResolver
{
    public static string Resolve(string? title, string? content, bool isTutor)
    {
        // 0. Nếu trong Content có nhúng link đích trực tiếp [url:...]
        if (!string.IsNullOrWhiteSpace(content) && content.Contains("[url:"))
        {
            var start = content.IndexOf("[url:") + 5;
            var end = content.IndexOf("]", start);
            if (end > start)
            {
                var directUrl = content.Substring(start, end - start).Trim();
                if (!string.IsNullOrWhiteSpace(directUrl))
                {
                    return directUrl;
                }
            }
        }

        var t = title?.ToLowerInvariant() ?? "";
        var c = content?.ToLowerInvariant() ?? "";

        // 1. Tin nhắn trò chuyện
        if (t.Contains("tin nhắn") || c.Contains("tin nhắn"))
        {
            return "/Chat";
        }

        // 2. Yêu cầu kết nối
        if (t.Contains("kết nối") || c.Contains("kết nối"))
        {
            return isTutor 
                ? "/Tutor/TutorConnectionRequest" 
                : "/Student_Parent/Send_Details_Tutor/MyRequests";
        }

        // 3. Lớp học, lịch học, lịch dạy, buổi học
        if (t.Contains("lớp") || c.Contains("lớp") || t.Contains("buổi học") || c.Contains("buổi học") || t.Contains("lịch") || c.Contains("lịch"))
        {
            return isTutor 
                ? "/Tutor/TutorClassroom" 
                : "/Student_Parent/StudentClassroom";
        }

        // 4. Tài liệu học tập
        if (t.Contains("tài liệu") || c.Contains("tài liệu"))
        {
            return isTutor 
                ? "/Tutor/TutorLearningDocument" 
                : "/Student_Parent/StudentLearningDocument";
        }

        // 5. Hồ sơ, duyệt hồ sơ
        if (t.Contains("hồ sơ") || c.Contains("duyệt"))
        {
            return isTutor 
                ? "/Tutor/TutorProfile" 
                : "/Student_Parent/StudentProfile";
        }

        // 5. Đánh giá
        if (t.Contains("đánh giá") || c.Contains("đánh giá"))
        {
            return isTutor 
                ? "/Tutor/TutorReview" 
                : "/Student_Parent/StudentProfile";
        }

        return "/Notifications";
    }
}

