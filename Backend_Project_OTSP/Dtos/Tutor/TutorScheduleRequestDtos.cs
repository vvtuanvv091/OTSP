namespace Backend_Project_OTSP.Dtos.Tutor;

public class ConfirmTrialRequest
{
    public int RequestId { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
}

public class CreateSessionRequest
{
    public int ClassroomId { get; set; }
    public string? Title { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
}

