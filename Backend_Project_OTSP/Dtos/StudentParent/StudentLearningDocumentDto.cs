using System;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Dtos.StudentParent;

public class StudentLearningDocumentDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;
    public string TutorName { get; set; } = string.Empty;
    public string? TutorAvatarUrl { get; set; }
    public DateTime UploadedAt { get; set; }
    public DocumentScope Scope { get; set; }
    public int? ClassroomId { get; set; }
    public string? ClassroomName { get; set; }
    public ClassroomType? ClassroomType { get; set; }
    public string FileExtension { get; set; } = string.Empty;
}

public class StudentClassroomFilterOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ClassroomType Type { get; set; }
    public string SubjectName { get; set; } = string.Empty;
}

