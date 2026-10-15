using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Dữ liệu tóm tắt của gia sư hiển thị trên danh sách tìm kiếm và trang chủ.
/// </summary>
public class TutorCardDto
{
    public int Id { get; set; }                    // TutorProfile.Id
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
