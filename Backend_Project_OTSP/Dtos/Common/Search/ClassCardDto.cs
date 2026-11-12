using static Backend_Project_OTSP.Models.Enums;
namespace Backend_Project_OTSP.Dtos;
public class ClassroomScheduleItemDto
{
    public int DayOfWeek { get; set; }
    public string DayOfWeekName { get; set; } = string.Empty;
    public string TimeRange { get; set; } = string.Empty;
}

public class ClassCardDto
{
    public int Id { get; set; }           
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }

    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int GradeLevelId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string IconClass { get; set; } = "bi-book";

    public decimal Fee { get; set; }
    public ClassroomType Type { get; set; }
    public int StudentCount { get; set; }
    public int MaxStudents { get; set; }

    public int TutorId { get; set; }               // TutorProfile.Id
    public string TutorUserId { get; set; } = string.Empty;
    public string TutorName { get; set; } = string.Empty;
    public string? TutorAvatarUrl { get; set; }
    public double TutorRating { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? RegistrationDeadline { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? TotalSessions { get; set; }
    public List<ClassroomScheduleItemDto> WeeklySchedules { get; set; } = new();
}