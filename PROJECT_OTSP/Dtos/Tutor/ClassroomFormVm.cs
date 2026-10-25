using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;

namespace PROJECT_OTSP.Dtos;

public sealed class ClassroomFormVm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên lớp.")]
    [StringLength(200, ErrorMessage = "Tên lớp không được vượt quá 200 ký tự.")]
    [Display(Name = "Tên lớp")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "1", "999999999", ErrorMessage = "Học phí phải lớn hơn 0.")]
    [Display(Name = "Học phí (VNĐ)")]
    public decimal Fee { get; set; }

    [Range(2, 50, ErrorMessage = "Sĩ số tối đa phải từ 2 đến 50 học viên.")]
    [Display(Name = "Sĩ số tối đa")]
    public int MaxStudents { get; set; } = 2;

    [Range(1, 7, ErrorMessage = "Số buổi học mỗi tuần phải từ 1 đến 7.")]
    [Display(Name = "Số buổi học / tuần")]
    public int SessionsPerWeek { get; set; } = 2;

    [Range(0.5, 5.0, ErrorMessage = "Thời lượng mỗi buổi phải từ 0.5 đến 5 giờ.")]
    [Display(Name = "Thời lượng mỗi buổi (giờ)")]
    public double HoursPerSession { get; set; } = 2.0;

    public List<ClassroomWeeklyScheduleInputDto> WeeklySchedules { get; set; } = [];

    [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1.000 ký tự.")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [StringLength(500, ErrorMessage = "Đường dẫn ảnh không được vượt quá 500 ký tự.")]
    [Display(Name = "URL ảnh bìa")]
    public string? CoverImageUrl { get; set; }

    [Display(Name = "Tải ảnh bìa lên")]
    public IFormFile? CoverImageFile { get; set; }

    public string? CurrentCoverImageUrl { get; set; }
    public bool RemoveCoverImage { get; set; }

    [DateNotInPast(ErrorMessage = "Ngày bắt đầu không được ở quá khứ.")]
    [Display(Name = "Ngày bắt đầu")]
    public DateOnly? StartDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn môn học và cấp học.")]
    [Display(Name = "Môn học - Cấp học")]
    public string SubjectGradeKey { get; set; } = string.Empty;

    public uint RowVersion { get; set; }
    public bool CanChangeSubjectGrade { get; set; } = true;
    public bool CanChangeFee { get; set; } = true;
    public string SubjectGradeLabel { get; set; } = string.Empty;
    public List<SelectListItem> SubjectGradeOptions { get; set; } = [];
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class DateNotInPastAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateOnly date)
            return ValidationResult.Success;

        var todayInVietnam = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        return date < todayInVietnam
            ? new ValidationResult(ErrorMessage ?? "Ngày bắt đầu không được ở quá khứ.")
            : ValidationResult.Success;
    }
}
