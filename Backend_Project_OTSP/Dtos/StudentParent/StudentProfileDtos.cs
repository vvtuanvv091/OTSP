namespace Backend_Project_OTSP.Dtos.StudentParent;

public class UpdateStudentProfileRequest
{
    public string? FullName { get; set; }
    public string? CitizenIdNumber { get; set; }
    public string? ResidenceAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public string? InterestedSubjects { get; set; }
    public string? Area { get; set; }
    public string? FreeSchedule { get; set; }
}

public class GuardianRequest
{
    public string FullName { get; set; } = string.Empty;
    public string ContactInfo { get; set; } = string.Empty;
    public string? CitizenIdNumber { get; set; }
    public string? ResidenceAddress { get; set; }
}

public class StudentProfileResponseDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? CitizenIdNumber { get; set; }
    public string? ResidenceAddress { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? InterestedSubjects { get; set; }
    public string? Area { get; set; }
    public string? FreeSchedule { get; set; }
    public bool IsMinor { get; set; }
}

public class GuardianDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string ContactInfo { get; set; } = string.Empty;
    public string? CitizenIdNumber { get; set; }
    public string? ResidenceAddress { get; set; }
}

public class StudentProfileDetailsDto
{
    public StudentProfileResponseDto Profile { get; set; } = new();
    public IEnumerable<GuardianDto> Guardians { get; set; } = [];
}
