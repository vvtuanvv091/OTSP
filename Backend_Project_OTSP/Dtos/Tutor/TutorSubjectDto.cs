namespace Backend_Project_OTSP.Dtos.Tutor;

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
    public decimal HourlyFee { get; set; }
}

public class AddTutorSubjectRequest
{
    public int SubjectId { get; set; }
    public int GradeLevelId { get; set; }
    public decimal HourlyFee { get; set; }
}

public class EditTutorSubjectRequest
{
    public decimal HourlyFee { get; set; }
}
