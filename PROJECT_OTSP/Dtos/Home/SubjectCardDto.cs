namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Thẻ thông tin môn học trên trang chủ, gồm tên môn, icon, số lượng gia sư và danh sách cấp học.
/// </summary>
public class SubjectCardDto
{
    public int Id { get; set; }                    // SubjectId
    public string Name { get; set; } = string.Empty;
    public string IconClass { get; set; } = "bi-book";
    public int TutorCount { get; set; }
    public List<GradeOptionDto> Grades { get; set; } = new();
}
