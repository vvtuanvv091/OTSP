using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos;

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
    public string SubjectName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;

<<<<<<< HEAD
=======
    public int SessionsPerWeek { get; set; } = 1;
    public double HoursPerSession { get; set; } = 2.0;
    public string ScheduleSummary { get; set; } = string.Empty;
    public List<string> WeeklyScheduleItems { get; set; } = [];

>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
    public bool HasAvailableSeats => StudentCount < MaxStudents;
    public int AvailableSeats => Math.Max(0, MaxStudents - StudentCount);
}
