using System;

namespace PROJECT_OTSP.Dtos.Common;

public class PublicLearningDocumentDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;
    public string TutorName { get; set; } = string.Empty;
    public string? TutorAvatarUrl { get; set; }
    public DateTime UploadedAt { get; set; }
    public string FileExtension { get; set; } = string.Empty;
}

public class SubjectFilterItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class GradeLevelFilterItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

