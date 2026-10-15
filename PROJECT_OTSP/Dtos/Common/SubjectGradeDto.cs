namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Đại diện cho một cặp môn học - cấp học gia sư đăng ký giảng dạy.
/// </summary>
public class SubjectGradeDto
{
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int GradeLevelId { get; set; }
    public string GradeName { get; set; } = string.Empty;
}
