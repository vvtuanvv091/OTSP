using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos;

public sealed class ClassroomListItemVm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public decimal Fee { get; set; }
    public int StudentCount { get; set; }
    public int MaxStudents { get; set; }
    public ClassroomStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
<<<<<<< HEAD
=======
    public int SessionsPerWeek { get; set; } = 1;
    public double HoursPerSession { get; set; } = 2.0;
    public string ScheduleSummary { get; set; } = string.Empty;
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
    public bool CanHardDelete { get; set; }
    public bool HasSuccessfulPayment { get; set; }
}
