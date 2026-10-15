using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Dữ liệu hiển thị trong danh sách yêu cầu kết nối phía gia sư quản lý.
/// </summary>
public class TutorConnectionRequestDto
{
    public int Id { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentAvatarUrl { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;
    public string? Message { get; set; }
    public ClassroomType PreferredType { get; set; }
    public string? ClassroomName { get; set; }
    public RequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
