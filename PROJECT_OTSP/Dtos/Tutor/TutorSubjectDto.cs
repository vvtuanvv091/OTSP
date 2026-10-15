namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Dòng môn học - cấp học gia sư đang đăng ký dạy.
/// </summary>
public class TutorSubjectDto
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int GradeLevelId { get; set; }
    public string GradeLevelName { get; set; } = string.Empty;
}
