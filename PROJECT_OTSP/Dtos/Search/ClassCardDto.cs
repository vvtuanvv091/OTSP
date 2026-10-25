using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Dữ liệu tóm tắt của lớp học hiển thị trên danh sách tìm kiếm và trang chủ.
/// </summary>
public class ClassCardDto
{
    public int Id { get; set; }                    // Classroom.Id
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }

    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int GradeLevelId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string IconClass { get; set; } = "bi-book";

    public decimal Fee { get; set; }
    public ClassroomType Type { get; set; }
    public int StudentCount { get; set; }
    public int MaxStudents { get; set; }

    public int TutorId { get; set; }               // TutorProfile.Id
    public string TutorName { get; set; } = string.Empty;
}
