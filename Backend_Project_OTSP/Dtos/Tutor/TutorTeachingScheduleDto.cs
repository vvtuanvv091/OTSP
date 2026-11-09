using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Dtos.Tutor;

public sealed class TutorTeachingScheduleDto
{
    public int RequestId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string StudentUserId { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public string GradeLevelName { get; init; } = string.Empty;
    public ClassroomType PreferredType { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? TrialScheduledAt { get; init; }
    public ScheduleStatus? TrialStatus { get; init; }
    public int? ClassSessionId { get; init; }
    public SessionStatus? ClassSessionStatus { get; init; }
    public DateTime? ProposedTrialSlotUtc { get; init; }
    public List<DateTime> ProposedTrialSlotsUtc { get; init; } = new();
    public int? ClassroomId { get; init; }
    public string? ClassroomName { get; init; }
    public decimal HourlyFee { get; init; } = 150000;
    public decimal MonthlyFee { get; init; }
    public bool IsOfficialRequest { get; init; }
    public bool IsOfficialEnrolled { get; init; }
    public bool HasPaid { get; init; }
    public int? PendingPaymentId { get; init; }
    public List<string> WeeklyScheduleSummaries { get; init; } = new();
}
