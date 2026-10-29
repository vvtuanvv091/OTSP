using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Dữ liệu hiển thị danh sách học viên trong trang "Học viên của tôi" dành cho gia sư.
/// </summary>
public class TutorStudentItemVm
{
    public int StudentId { get; set; }
    public string StudentUserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string? StudentAvatarUrl { get; set; }
    public int? ClassroomId { get; set; }
    public string ClassroomName { get; set; } = string.Empty;
    public ClassroomType? ClassroomType { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string Progress { get; set; } = string.Empty;
    public string NextSessionFormatted { get; set; } = string.Empty;
    public int? RequestId { get; set; }
}
