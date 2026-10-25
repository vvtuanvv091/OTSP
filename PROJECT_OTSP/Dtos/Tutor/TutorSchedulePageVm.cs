using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos;

public sealed class TutorScheduleCalendarSlotDto
{
    public int SessionId { get; init; }
    public int? RequestId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public string GradeLevelName { get; init; } = string.Empty;
    public ClassroomType ClassroomType { get; init; }
    public DateTime ScheduledAtVn { get; init; }
    public DateOnly Date => DateOnly.FromDateTime(ScheduledAtVn);
    public int Hour => ScheduledAtVn.Hour;
    public int Minute => ScheduledAtVn.Minute;
    public SessionStatus Status { get; init; }
    public bool IsTrial { get; init; }
    public string? VideoRoom { get; init; }
<<<<<<< HEAD
=======
    public int ClassroomId { get; init; }
    public string ClassroomName { get; init; } = string.Empty;
    public bool IsMakeup => VideoRoom != null && VideoRoom.Contains("MAKEUP");
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
}

public sealed class TutorSchedulePageVm
{
    public DateOnly SelectedMonday { get; init; }
    public DateOnly SelectedSunday { get; init; }
    public int WeekOffset { get; init; }
    public DateOnly TodayVn { get; init; }
    public IReadOnlyList<TutorTeachingScheduleDto> Requests { get; init; } = [];
    public IReadOnlyList<TutorAvailabilityDto> Availabilities { get; init; } = [];
    public IReadOnlyList<TutorScheduleCalendarSlotDto> CalendarSessions { get; init; } = [];
}
