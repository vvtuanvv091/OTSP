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
    public ClassroomType Type { get; set; }
    public DateOnly? StartDate { get; set; }
    public int SessionsPerWeek { get; set; } = 1;
    public double HoursPerSession { get; set; } = 2.0;
    public string ScheduleSummary { get; set; } = string.Empty;
    public bool CanHardDelete { get; set; }
    public bool HasSuccessfulPayment { get; set; }

    // Các thuộc tính bổ sung mới phục vụ giao diện thẻ 3x3 và tiến độ giảng dạy
    public int CompletedSessions { get; set; } = 0;
    public int TotalSessions { get; set; } = 12;
    public int ProgressPercent => TotalSessions > 0 ? (int)Math.Clamp(Math.Round((double)CompletedSessions / TotalSessions * 100), 0, 100) : 0;
    public string FormattedSchedule { get; set; } = string.Empty;
    public int DocumentCount { get; set; } = 0;
}
