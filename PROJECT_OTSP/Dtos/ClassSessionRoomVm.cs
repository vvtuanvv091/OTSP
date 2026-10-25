namespace PROJECT_OTSP.Dtos;

public sealed class ClassSessionRoomVm
{
    public int SessionId { get; init; }
    public string ClassroomName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public DateTime ScheduledAtUtc { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
