namespace Backend_Project_OTSP.Dtos.StudentParent;

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
