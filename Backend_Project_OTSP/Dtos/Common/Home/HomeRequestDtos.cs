namespace Backend_Project_OTSP.Dtos.Common.Home;

public class VisitTrackRequest
{
    public string? Path { get; set; }
}

public class ContactFormRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Topic { get; set; } = "Tư vấn chọn gia sư / lớp học";
    public string Message { get; set; } = string.Empty;
}

