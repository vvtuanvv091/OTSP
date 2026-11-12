using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Dtos;

/// <summary>
/// Dữ liệu hiển thị trong danh sách yêu cầu kết nối phía gia sư quản lý.
/// </summary>
public class TutorConnectionRequestDto
{
    public int Id { get; set; }
    public string StudentUserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string? StudentAvatarUrl { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;
    public string? Message { get; set; }
    public ClassroomType PreferredType { get; set; }
    public bool IsTrial { get; set; }
    public int DurationWeeks { get; set; } = 4;
    public DateOnly? RequestedStartDate { get; set; }
    public DateOnly? RequestedEndDate { get; set; }
    public string? ClassroomName { get; set; }
    public RequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProposedTrialSlotUtc { get; set; }
    public List<DateTime> ProposedTrialSlotsUtc { get; set; } = new();
    public bool MatchesAvailability { get; set; }
    public bool HasCompletedTrial { get; set; }
}
