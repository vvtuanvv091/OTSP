namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Dữ liệu hiển thị cho một tài liệu học tập do gia sư tải lên.
/// </summary>
public class TutorLearningDocumentDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
