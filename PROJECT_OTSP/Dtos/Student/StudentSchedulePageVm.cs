using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos.Student;

public sealed class StudentScheduleItemDto
{
    public int SessionId { get; init; }
    public int ClassroomId { get; init; }
    public string ClassroomName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public string GradeLevelName { get; init; } = string.Empty;
    public string TutorName { get; init; } = string.Empty;
    public string? TutorAvatarUrl { get; init; }
    public ClassroomType ClassroomType { get; init; }
    public DateTime ScheduledAtUtc { get; init; }
    public DateTime ScheduledAtVn { get; init; }
    public SessionStatus Status { get; init; }
    public bool IsTrial { get; init; }
    public string? VideoRoom { get; init; }
    public double DurationHours { get; init; } = 2.0;
    public DateOnly Date => DateOnly.FromDateTime(ScheduledAtVn);
    public int Hour => ScheduledAtVn.Hour;
    public bool CanJoin { get; init; }
    public bool HasReviewed { get; init; }
    public bool IsMakeup => VideoRoom != null && VideoRoom.Contains("MAKEUP");
}

public sealed class StudentSchedulePageVm
{
    public DateOnly SelectedMonday { get; init; }
    public DateOnly SelectedSunday { get; init; }
    public int WeekOffset { get; init; }
    public DateOnly TodayVn { get; init; }
    public IReadOnlyList<StudentScheduleItemDto> Sessions { get; init; } = Array.Empty<StudentScheduleItemDto>();
    public int UpcomingCount { get; init; }
    public int CompletedCount { get; init; }
}
