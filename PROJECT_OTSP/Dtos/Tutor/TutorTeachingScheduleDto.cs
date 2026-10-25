using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos;

public sealed class TutorTeachingScheduleDto
{
    public int RequestId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public string GradeLevelName { get; init; } = string.Empty;
    public ClassroomType PreferredType { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? TrialScheduledAt { get; init; }
    public ScheduleStatus? TrialStatus { get; init; }
    public int? ClassSessionId { get; init; }
    public SessionStatus? ClassSessionStatus { get; init; }
<<<<<<< HEAD
=======
    public DateTime? ProposedTrialSlotUtc { get; init; }
    public List<DateTime> ProposedTrialSlotsUtc { get; init; } = new();
    public int? ClassroomId { get; init; }
    public string? ClassroomName { get; init; }
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
}
