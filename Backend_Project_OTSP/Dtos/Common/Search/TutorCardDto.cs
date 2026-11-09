using static Backend_Project_OTSP.Models.Enums;
namespace Backend_Project_OTSP.Dtos;
public class TutorCardDto
{
    public int Id { get; set; }                    
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Introduction { get; set; }
    public decimal HourlyFee { get; set; }
    public double AverageRating { get; set; }
    public string? Area { get; set; }
    public TeachingFormat TeachingFormat { get; set; }
    public int VerifiedQualificationCount { get; set; }
    public List<SubjectGradeDto> Teaching { get; set; } = new();

    public string Initials => string.Concat(
        FullName.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(w => w[0])).ToUpperInvariant();
}
