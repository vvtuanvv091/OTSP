using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.StudentParent;

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
