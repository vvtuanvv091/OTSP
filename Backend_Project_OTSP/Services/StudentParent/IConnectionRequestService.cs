using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.StudentParent;

public class ConnectionRequestOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? RequestId { get; set; }
}

public class AcceptConnectionRequestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool NotFound { get; set; }
    public bool AlreadyHandled { get; set; }
    public bool InvalidClassroom { get; set; }
    public bool ClassroomFull { get; set; }
    public bool SlotConflict { get; set; }
    public int? ClassroomId { get; set; }
    public int? SessionId { get; set; }
    public bool IsTrial { get; set; }
    public bool Enrolled { get; set; }
    public string? StudentUserId { get; set; }
}

public class RejectConnectionRequestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool NotFound { get; set; }
    public bool AlreadyHandled { get; set; }
    public string? StudentUserId { get; set; }
}

public interface IConnectionRequestService
{
    Task<ConnectionRequestOperationResult> CreateRequestAsync(
        SendConnectionRequestDto dto,
        string studentUserId,
        CancellationToken cancellationToken = default);

    Task<BookingEligibilityVm> GetBookingEligibilityAsync(
        string studentUserId,
        int tutorId,
        CancellationToken cancellationToken = default);

    Task<AcceptConnectionRequestResult> AcceptRequestAsync(
        int requestId,
        string tutorUserId,
        CancellationToken cancellationToken = default);

    Task<RejectConnectionRequestResult> RejectRequestAsync(
        int requestId,
        string tutorUserId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task GenerateUpcomingSessionsAsync(
        int classroomId,
        int daysAhead = 28,
        CancellationToken cancellationToken = default);
}
