namespace PROJECT_OTSP.Dtos.ClassSession;

public sealed class ClassSessionJoinResultDto
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool RequiresPayment { get; init; }
    public string? PaymentRedirectUrl { get; init; }
    public bool IsForbidden { get; init; }
    public bool IsNotFound { get; init; }
    public int? SessionId { get; init; }
    public string? RoomName { get; init; }
    public string? DisplayName { get; init; }

    public static ClassSessionJoinResultDto Ok(int sessionId, string roomName, string displayName, string message = "Tham gia phòng học thành công.")
        => new()
        {
            Success = true,
            SessionId = sessionId,
            RoomName = roomName,
            DisplayName = displayName,
            Message = message
        };

    public static ClassSessionJoinResultDto Fail(string message)
        => new() { Success = false, Message = message };

    public static ClassSessionJoinResultDto NotFound(string message = "Không tìm thấy buổi học hoặc buổi học đã hoàn thành.")
        => new() { Success = false, IsNotFound = true, Message = message };

    public static ClassSessionJoinResultDto Forbidden(string message = "Bạn không có quyền tham gia buổi học này.")
        => new() { Success = false, IsForbidden = true, Message = message };

    public static ClassSessionJoinResultDto PaymentRequired(string message, string redirectUrl)
        => new()
        {
            Success = false,
            RequiresPayment = true,
            PaymentRedirectUrl = redirectUrl,
            Message = message
        };
}

public sealed class ClassSessionLeaveResultDto
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static ClassSessionLeaveResultDto Ok(string message = "Đã ghi nhận rời phòng học.")
        => new() { Success = true, Message = message };

    public static ClassSessionLeaveResultDto Fail(string message = "Không thể ghi nhận rời phòng học.")
        => new() { Success = false, Message = message };
}

public sealed class ClassSessionEndResultDto
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool IsNotFound { get; init; }
    public bool IsInvalidState { get; init; }

    public static ClassSessionEndResultDto Ok(string message = "Đã kết thúc buổi học thành công.")
        => new() { Success = true, Message = message };

    public static ClassSessionEndResultDto NotFound(string message = "Không tìm thấy buổi học hoặc bạn không có quyền.")
        => new() { Success = false, IsNotFound = true, Message = message };

    public static ClassSessionEndResultDto InvalidState(string message = "Chỉ có thể kết thúc buổi học đang diễn ra.")
        => new() { Success = false, IsInvalidState = true, Message = message };
}

public sealed class ClassSessionStatusDto
{
    public int SessionId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? RoomName { get; init; }
    public DateTime ScheduledAtUtc { get; init; }
    public int ClassroomId { get; init; }
    public string ClassroomName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public bool IsTutor { get; init; }
    public int ActiveParticipantCount { get; init; }
}

