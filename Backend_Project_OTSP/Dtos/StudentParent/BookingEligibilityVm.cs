namespace Backend_Project_OTSP.Dtos.StudentParent;

public sealed class BookingEligibilityVm
{
    public int TutorId { get; init; }
    public IReadOnlyList<OneToOneBookingEligibilityDto> Subjects { get; init; } = Array.Empty<OneToOneBookingEligibilityDto>();
    public IReadOnlyList<GroupBookingEligibilityDto> GroupClassrooms { get; init; } = Array.Empty<GroupBookingEligibilityDto>();
}

public sealed class OneToOneBookingEligibilityDto
{
    public int SubjectId { get; init; }
    public int GradeLevelId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public string GradeLevelName { get; init; } = string.Empty;
    public bool CanBook { get; init; }
    public string? Reason { get; init; }
    public int? ExistingClassroomId { get; init; }
    public string? ExistingTutorName { get; init; }
}

public sealed class GroupBookingEligibilityDto
{
    public int ClassroomId { get; init; }
    public int SubjectId { get; init; }
    public int GradeLevelId { get; init; }
    public string ClassroomName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public string GradeLevelName { get; init; } = string.Empty;
    public decimal Fee { get; init; }
    public int StudentCount { get; init; }
    public int MaxStudents { get; init; }
    public int AvailableSeats { get; init; }
    public bool IsEnrolled { get; init; }
    public bool HasPendingRequest { get; init; }
    public bool CanJoin { get; init; }
    public string? Reason { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? RegistrationDeadline { get; init; }
    public DateOnly? EndDate { get; init; }
    public int? TotalSessions { get; init; }
}
