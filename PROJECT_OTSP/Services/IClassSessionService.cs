using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Dtos.ClassSession;

namespace PROJECT_OTSP.Services;

public interface IClassSessionService
{
    Task<ClassSessionJoinResultDto> JoinSessionAsync(int sessionId, string userId, bool isTutor, CancellationToken cancellationToken = default);
    Task<bool> LeaveSessionAsync(int sessionId, string userId, CancellationToken cancellationToken = default);
    Task<ClassSessionEndResultDto> EndSessionAsync(int sessionId, string tutorUserId, CancellationToken cancellationToken = default);
    Task<bool> ReopenSessionAsync(int sessionId, string tutorUserId, CancellationToken cancellationToken = default);
    Task<ClassSessionRoomVm?> GetRoomVmAsync(int? sessionId, string? roomName, string userId, bool isTutor, CancellationToken cancellationToken = default);
    Task<ClassSessionStatusDto?> GetRoomInfoAsync(int sessionId, string userId, CancellationToken cancellationToken = default);
    Task<bool> IsParticipantAsync(int classroomId, string userId, CancellationToken cancellationToken = default);
}

