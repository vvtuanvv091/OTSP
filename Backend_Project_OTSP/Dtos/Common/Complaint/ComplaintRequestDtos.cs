namespace Backend_Project_OTSP.Dtos.Common.Complaint;

public class UpdateComplaintStatusRequest
{
    public string Status { get; set; } = "";
    public string? Resolution { get; set; }
}

