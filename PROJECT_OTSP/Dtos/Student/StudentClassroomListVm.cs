using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos.Student;

public sealed class StudentClassroomSessionDto
{
    public int Index { get; init; }
    public int SessionId { get; init; }
    public DateTime ScheduledAtUtc { get; init; }
    public DateTime ScheduledAtVn { get; init; }
    public SessionStatus Status { get; init; }
    public bool CanJoin { get; init; }
    public bool IsFuture { get; init; }
    public bool HasReviewed { get; init; }
    public int? ReviewScore { get; init; }
    public string? ReviewComment { get; init; }
}

public sealed class StudentClassroomItemDto
{
    public int ClassroomId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public string GradeLevelName { get; init; } = string.Empty;
    public string TutorName { get; init; } = string.Empty;
    public string? TutorAvatarUrl { get; init; }
    public ClassroomType Type { get; init; }
    public decimal Fee { get; init; }
    public int SessionsPerWeek { get; init; }
    public double HoursPerSession { get; init; }
    public string ScheduleSummary { get; init; } = string.Empty;
    public DateTime JoinedAt { get; init; }
    public int TotalSessionsCount { get; init; }
    public DateTime? NextSessionScheduledAtVn { get; init; }
    public int? NextSessionId { get; init; }
    public ClassroomStatus Status { get; init; }
    public IReadOnlyList<StudentClassroomSessionDto> Sessions { get; init; } = Array.Empty<StudentClassroomSessionDto>();
}

public sealed class StudentClassroomListVm
{
    public IReadOnlyList<StudentClassroomItemDto> Classrooms { get; init; } = Array.Empty<StudentClassroomItemDto>();
    public int TotalClassesCount => Classrooms.Count;
}
