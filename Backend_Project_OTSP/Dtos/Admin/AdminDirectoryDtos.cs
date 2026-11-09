using System.ComponentModel.DataAnnotations;
using Backend_Project_OTSP.Dtos;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Dtos.Admin;

public sealed class AdminStudentQuery
{
    [StringLength(100)] public string? Search { get; set; }
    public string? AgeGroup { get; set; }
    public AccountStatus? Status { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 10;
}

public sealed class AdminStudentDirectoryDto
{
    public AdminStudentStatsDto Stats { get; set; } = new();
    public PagedResult<AdminStudentItemDto> Students { get; set; } = new();
}

public sealed class AdminStudentStatsDto
{
    public int Total { get; set; }
    public int WithGuardians { get; set; }
    public int SelfManaged { get; set; }
    public int Active { get; set; }
    public int Locked { get; set; }
}

public sealed class AdminStudentItemDto
{
    public string UserId { get; set; } = string.Empty;
    public int StudentProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int? Age { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianContact { get; set; }
    public AccountStatus Status { get; set; }
    public bool IsOnline { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AdminTutorQuery
{
    [StringLength(100)] public string? Search { get; set; }
    public int? SubjectId { get; set; }
    public TeachingFormat? TeachingFormat { get; set; }
    public AccountStatus? Status { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 10;
}

public sealed class AdminTutorDirectoryDto
{
    public AdminTutorStatsDto Stats { get; set; } = new();
    public List<AdminSubjectOptionDto> SubjectOptions { get; set; } = new();
    public PagedResult<AdminTutorItemDto> Tutors { get; set; } = new();
}

public sealed class AdminTutorStatsDto
{
    public int Approved { get; set; }
    public int Online { get; set; }
    public int Offline { get; set; }
    public int Hybrid { get; set; }
    public int Active { get; set; }
    public int Locked { get; set; }
}

public sealed class AdminTutorItemDto
{
    public int TutorId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public TeachingFormat TeachingFormat { get; set; }
    public decimal HourlyFee { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int QualificationCount { get; set; }
    public AccountStatus Status { get; set; }
    public bool IsOnline { get; set; }
    public List<AdminTutorSubjectDto> Subjects { get; set; } = new();
}

public sealed class AdminTutorSubjectDto
{
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
}

public sealed class AdminSubjectOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class AdminPendingQualificationItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string Status { get; set; } = "Pending";
    public int TutorId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string? TutorEmail { get; set; }
    public string? TutorPhone { get; set; }
}

public sealed class AdminTutorApprovalDashboardDto
{
    public AdminTutorApprovalStatsDto Stats { get; set; } = new();
    public List<AdminSubjectOptionDto> SubjectOptions { get; set; } = new();
    public PagedResult<TutorApprovalViewModel> Tutors { get; set; } = new();
    public List<AdminPendingQualificationItemDto> PendingQualifications { get; set; } = new();
}

public sealed class AdminTutorApprovalQuery
{
    [StringLength(100)] public string? Search { get; set; }
    public int? SubjectId { get; set; }
    public TeachingFormat? TeachingFormat { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 10;
}

public sealed class AdminTutorApprovalStatsDto
{
    public int Pending { get; set; }
    public int PendingQualifications { get; set; }
    public int Online { get; set; }
    public int Offline { get; set; }
    public int Hybrid { get; set; }
}

public sealed class AdminUpdateStudentRequest
{
    [StringLength(100)] public string? FullName { get; set; }
    [Phone] public string? PhoneNumber { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(250)] public string? ResidenceAddress { get; set; }
    [StringLength(20)] public string? CitizenIdNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public AccountStatus? Status { get; set; }
    [StringLength(100)] public string? GuardianFullName { get; set; }
    [StringLength(100)] public string? GuardianContact { get; set; }
}

public sealed class AdminUpdateTutorRequest
{
    [StringLength(100)] public string? FullName { get; set; }
    [Phone] public string? PhoneNumber { get; set; }
    [Range(0, 50000000)] public decimal? HourlyFee { get; set; }
    public TeachingFormat? TeachingFormat { get; set; }
    [StringLength(2000)] public string? Introduction { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(250)] public string? ResidenceAddress { get; set; }
    [StringLength(20)] public string? CitizenIdNumber { get; set; }
    public AccountStatus? Status { get; set; }
    public VerificationStatus? VerificationStatus { get; set; }
}

