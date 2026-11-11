using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Sessions;

namespace Backend_Project_OTSP.Services.Sessions;

public interface IClassSessionService
{
    Task<ClassSessionJoinResultDto> JoinSessionAsync(int sessionId, string userId, bool isTutor, CancellationToken cancellationToken = default);
    Task<bool> LeaveSessionAsync(int sessionId, string userId, CancellationToken cancellationToken = default);
    Task<ClassSessionEndResultDto> EndSessionAsync(int sessionId, string tutorUserId, CancellationToken cancellationToken = default);
    Task<bool> ReopenSessionAsync(int sessionId, string tutorUserId, CancellationToken cancellationToken = default);
    Task<ClassSessionRoomVm?> GetRoomVmAsync(int? sessionId, string? roomName, string userId, bool isTutor, CancellationToken cancellationToken = default);
    Task<ClassSessionStatusDto?> GetRoomInfoAsync(int sessionId, string userId, CancellationToken cancellationToken = default);
    Task<bool> IsParticipantAsync(int classroomId, string userId, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> CancelSessionAsync(int sessionId, string tutorUserId, string reason, CancellationToken cancellationToken = default);
    Task EnsureWeeklySessionsForClassroomsAsync(
        IReadOnlyCollection<int> classroomIds,
        DateOnly rangeStartVn,
        DateOnly rangeEndVn,
        bool markNearbyInProgress = false,
        CancellationToken cancellationToken = default);
}

