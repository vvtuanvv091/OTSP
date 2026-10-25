using Microsoft.AspNetCore.Mvc.Rendering;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos;

/// <summary>
/// ViewModel toàn diện hiển thị hồ sơ chi tiết gia sư và form gửi yêu cầu kết nối dành cho học viên/phụ huynh.
/// </summary>
public class TutorDetailsVm
{
    // Thông tin cơ bản của gia sư
    public int TutorId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Area { get; set; }
    public decimal HourlyFee { get; set; }
    public TeachingFormat TeachingFormat { get; set; }
    public double AverageRating { get; set; }
    public string? Introduction { get; set; }
    public int TotalCompletedSessions { get; set; }
    public int TotalStudents { get; set; }

    public string Initials => string.Concat(
        FullName.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(w => w[0])).ToUpperInvariant();

    // Các danh sách thông tin chi tiết
    public List<TutorQualificationDto> Qualifications { get; set; } = new();
    public List<SubjectGradeDto> TeachingSubjects { get; set; } = new();
    public List<TutorAvailabilityDto> Availabilities { get; set; } = new();
    public List<TutorClassroomDto> Classrooms { get; set; } = new();
    public List<TutorReviewDto> Reviews { get; set; } = new();

    // Dữ liệu lựa chọn cho Form gửi yêu cầu
    public List<SelectListItem> SubjectOptions { get; set; } = new();
    public List<SelectListItem> ClassroomOptions { get; set; } = new();
<<<<<<< HEAD
=======
    public List<TutorAvailableSlotDto> AvailableTrialSlots { get; set; } = new();
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc

    // Trạng thái kết nối của học viên hiện tại với gia sư này
    public bool HasExistingPendingRequest { get; set; }
    public RequestStatus? ExistingRequestStatus { get; set; }
    public DateTime? ExistingRequestDate { get; set; }
    public string? ExistingRequestSubject { get; set; }

    // DTO phục vụ gửi yêu cầu
    public SendConnectionRequestDto Request { get; set; } = new();
}
