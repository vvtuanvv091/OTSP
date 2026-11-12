namespace Backend_Project_OTSP.Dtos.Tutor;

public class UpdateTutorProfileRequest
{
    public string? DisplayName { get; set; }
    public string? Introduction { get; set; }
    public string? CitizenIdNumber { get; set; }
    public string? ResidenceAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public decimal? HourlyFee { get; set; }
}

