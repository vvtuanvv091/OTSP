using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Dtos.StudentParent;

public sealed class StudentClassroomSessionDto
{
    public int Index { get; init; }
    public int SessionId { get; init; }
    public DateTime ScheduledAtUtc { get; init; }
    public DateTime ScheduledAtVn { get; init; }
    public SessionStatus Status { get; init; }
    public string? VideoRoom { get; init; }
    public double DurationHours { get; init; } = 2.0;
    public string? TimeRangeLabel { get; init; }
    public bool IsMakeup { get; init; }
    public bool CanJoin { get; init; }
    public bool IsFuture { get; init; }
    public bool IsCompleted { get; init; }
    public bool HasReviewed { get; init; }
    public int? ReviewScore { get; init; }
    public string? ReviewComment { get; init; }
}

public sealed class StudentClassroomItemDto
{
    public int ClassroomId { get; init; }
    public string TutorUserId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ClassroomName { get => Name; init => Name = value; }
    public string SubjectName { get; init; } = string.Empty;
    public string GradeLevelName { get; init; } = string.Empty;
    public string TutorName { get; init; } = string.Empty;
    public string? TutorAvatarUrl { get; init; }
    public ClassroomType Type { get; init; }
    public decimal Fee { get; init; }
    public int SessionsPerWeek { get; init; }
    public double HoursPerSession { get; init; }
    public string ScheduleSummary { get; init; } = string.Empty;
    public string WeeklyScheduleSummary { get => ScheduleSummary; init => ScheduleSummary = value; }
    public DateTime JoinedAt { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? RegistrationDeadline { get; init; }
    public DateOnly? EndDate { get; init; }
    public int TotalSessionsCount { get; init; }
    public int ExpectedTotalSessions { get; init; }
    public int CompletedSessionsCount { get; init; }
    public DateTime? NextSessionScheduledAtVn { get; init; }
    public int? NextSessionId { get; init; }
    public ClassroomStatus Status { get; init; }
    public bool HasPaid { get; init; } = true;
    public int? PendingPaymentId { get; init; }
    public IReadOnlyList<StudentClassroomSessionDto> Sessions { get; init; } = Array.Empty<StudentClassroomSessionDto>();
}

public sealed class StudentClassroomListVm
{
    public IReadOnlyList<StudentClassroomItemDto> Classrooms { get; init; } = Array.Empty<StudentClassroomItemDto>();
    public int TotalClassesCount => Classrooms.Count;
}
