namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Cấp học hiển thị dạng lựa chọn trên thẻ môn học ở trang chủ.
/// </summary>
public class GradeOptionDto
{
    public int Id { get; set; }                    // GradeLevelId
    public string Name { get; set; } = string.Empty;
}
