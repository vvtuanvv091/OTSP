using System.ComponentModel.DataAnnotations;
using Backend_Project_OTSP.Dtos;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Dtos.Admin;

public sealed class AdminConnectionQuery
{
    [StringLength(100)] public string? Search { get; set; }
    public RequestStatus? Status { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 10;
}

public sealed class AdminConnectionDashboardDto
{
    public AdminConnectionStatsDto Stats { get; set; } = new();
    public PagedResult<AdminConnectionItemDto> Requests { get; set; } = new();
}

public sealed class AdminConnectionStatsDto
{
    public int Pending { get; set; }
    public int Accepted { get; set; }
    public int Rejected { get; set; }
    public int Cancelled { get; set; }
}

public sealed class AdminConnectionItemDto
{
    public int Id { get; set; }
    public string StudentUserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string? StudentEmail { get; set; }
    public string TutorUserId { get; set; } = string.Empty;
    public string TutorName { get; set; } = string.Empty;
    public string? TutorEmail { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? ClassroomName { get; set; }
    public ClassroomType PreferredType { get; set; }
    public RequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public sealed class AdminCancelConnectionRequest
{
    [Required, StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class AdminClassroomQuery
{
    [StringLength(100)] public string? Search { get; set; }
    public ClassroomStatus? Status { get; set; }
    public ClassroomType? Type { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 10;
}

public sealed class AdminClassroomDashboardDto
{
    public AdminClassroomStatsDto Stats { get; set; } = new();
    public PagedResult<AdminClassroomItemDto> Classrooms { get; set; } = new();
}

public sealed class AdminClassroomStatsDto
{
    public int Active { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int UpcomingSessions { get; set; }
}

public sealed class AdminClassroomItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TutorName { get; set; } = string.Empty;
    public string? TutorEmail { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;
    public ClassroomType Type { get; set; }
    public ClassroomStatus Status { get; set; }
    public decimal Fee { get; set; }
    public int StudentCount { get; set; }
    public int MaxStudents { get; set; }
    public int SessionsPerWeek { get; set; }
    public int ScheduledSessionCount { get; set; }
    public int? NextSessionId { get; set; }
    public DateTime? NextSessionAt { get; set; }
}

public sealed class AdminCancelClassroomRequest
{
    [Required, StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class AdminCatalogItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int UsageCount { get; set; }
}

public sealed class SaveAdminCatalogItemRequest
{
    [Required, StringLength(20, MinimumLength = 2)]
    [RegularExpression("^[a-zA-Z0-9_-]+$", ErrorMessage = "Mã chỉ được chứa chữ, số, gạch ngang hoặc gạch dưới.")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }
}

public sealed class AdminLearningDocumentQuery
{
    [StringLength(100)] public string? Search { get; set; }
    public VerificationStatus? Status { get; set; }
    public DocumentScope? Scope { get; set; }
    public int? SubjectId { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 10;
}

public sealed class AdminLearningDocumentDashboardDto
{
    public AdminLearningDocumentStatsDto Stats { get; set; } = new();
    public List<AdminSubjectOptionDto> SubjectOptions { get; set; } = new();
    public PagedResult<AdminLearningDocumentItemDto> Documents { get; set; } = new();
}

public sealed class AdminLearningDocumentStatsDto
{
    public int PublicPending { get; set; }
    public int PublicApproved { get; set; }
    public int PublicRejected { get; set; }
    public int ClassroomDocuments { get; set; }
}

public sealed class AdminLearningDocumentItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string? TutorEmail { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;
    public string? ClassroomName { get; set; }
    public DocumentScope Scope { get; set; }
    public VerificationStatus Status { get; set; }
    public DateTime UploadedAt { get; set; }
    public string FileExtension { get; set; } = string.Empty;
}

public sealed class ReviewAdminDocumentRequest
{
    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class AdminUpdateClassroomRequest
{
    [StringLength(200)] public string? Name { get; set; }
    [Range(0, 100000000)] public decimal? Fee { get; set; }
    [Range(1, 100)] public int? MaxStudents { get; set; }
    [Range(1, 14)] public int? SessionsPerWeek { get; set; }
    public ClassroomStatus? Status { get; set; }
    public ClassroomType? Type { get; set; }
}

public sealed class AdminUpdateDocumentRequest
{
    [StringLength(200)] public string? Title { get; set; }
    [StringLength(2000)] public string? Description { get; set; }
    public DocumentScope? Scope { get; set; }
    public VerificationStatus? Status { get; set; }
}

