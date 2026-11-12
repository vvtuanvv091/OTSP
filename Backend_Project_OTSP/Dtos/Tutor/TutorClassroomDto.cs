using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Dtos.Tutor;

/// <summary>
/// Lớp học đang mở của gia sư để học viên có thể xem và đăng ký tham gia.
/// </summary>
public class TutorClassroomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public decimal Fee { get; set; }
    public ClassroomType Type { get; set; }
    public int StudentCount { get; set; }
    public int MaxStudents { get; set; }
    public int SubjectId { get; set; }
    public int GradeLevelId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;

    public int SessionsPerWeek { get; set; } = 1;
    public double HoursPerSession { get; set; } = 2.0;
    public DateOnly? StartDate { get; set; }
    public DateOnly? RegistrationDeadline { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? TotalSessions { get; set; }
    public string ScheduleSummary { get; set; } = string.Empty;
    public List<string> WeeklyScheduleItems { get; set; } = [];

    public bool HasAvailableSeats => StudentCount < MaxStudents;
    public int AvailableSeats => Math.Max(0, MaxStudents - StudentCount);
}

public class CreateClassroomRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Fee { get; set; }
    public int MaxStudents { get; set; } = 10;
    public int SessionsPerWeek { get; set; } = 2;
    public double HoursPerSession { get; set; } = 2.0;
    public DateOnly? StartDate { get; set; }
    public DateOnly? RegistrationDeadline { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? TotalSessions { get; set; }
    public string? CoverImageUrl { get; set; }
    public int SubjectId { get; set; }
    public int GradeLevelId { get; set; }
    public ClassroomType Type { get; set; } = ClassroomType.Group;
    public string? WeeklySchedulesJson { get; set; }
}

public class UpdateClassroomRequest
{
    public string? Name { get; set; }
    public string? NameClass { get; set; }
    public string? Description { get; set; }
    public decimal? Fee { get; set; }
    public int? MaxStudents { get; set; }
    public int? SessionsPerWeek { get; set; }
    public double? HoursPerSession { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? RegistrationDeadline { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? TotalSessions { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool RemoveCoverImage { get; set; }
    public int? SubjectId { get; set; }
    public int? GradeLevelId { get; set; }
    public string? WeeklySchedulesJson { get; set; }
}
