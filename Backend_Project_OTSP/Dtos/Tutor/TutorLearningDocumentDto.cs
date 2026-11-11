using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Dtos.Tutor;

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
    public DocumentScope Scope { get; set; } = DocumentScope.Classroom;
    public int? ClassroomId { get; set; }
    public string? ClassroomName { get; set; }
    public ClassroomType? ClassroomType { get; set; }
    public VerificationStatus Status { get; set; } = VerificationStatus.Approved;
    public string FileExtension { get; set; } = string.Empty;
}

/// <summary>
/// Dữ liệu lựa chọn lớp học đang dạy của gia sư để liên kết tài liệu.
/// </summary>
public class TutorActiveClassroomOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ClassroomType Type { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int GradeLevelId { get; set; }
    public string GradeLevelName { get; set; } = string.Empty;
    public int CurrentStudents { get; set; }
    public int MaxStudents { get; set; }
    public string? ScheduleSummary { get; set; }
}

public class UploadDocumentRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SubjectId { get; set; }
    public int GradeLevelId { get; set; }
    public DocumentScope Scope { get; set; } = DocumentScope.Public;
    public int? ClassroomId { get; set; }
}
