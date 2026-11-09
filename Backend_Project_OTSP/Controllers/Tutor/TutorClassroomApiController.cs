using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Common;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Dtos.Tutor;

namespace Backend_Project_OTSP.Controllers.Api.Tutor;

[ApiController]
[Route("api/tutor/classrooms")]
[Authorize(Roles = "Tutor")]
public class TutorClassroomApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IImageStorageService _imageStorage;
    private readonly ILogger<TutorClassroomApiController> _logger;

    public TutorClassroomApiController(
        ApplicationDbContext db,
        IImageStorageService imageStorage,
        ILogger<TutorClassroomApiController> logger)
    {
        _db = db;
        _imageStorage = imageStorage;
        _logger = logger;
    }

    private static string ToShortVnDay(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "T2",
        DayOfWeek.Tuesday => "T3",
        DayOfWeek.Wednesday => "T4",
        DayOfWeek.Thursday => "T5",
        DayOfWeek.Friday => "T6",
        DayOfWeek.Saturday => "T7",
        DayOfWeek.Sunday => "CN",
        _ => d.ToString()
    };

    private async Task<int?> GetApprovedTutorIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return null;

        return await _db.TutorProfiles
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.VerificationStatus == VerificationStatus.Approved)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    [HttpGet("options")]
    public async Task<IActionResult> GetFormOptions(CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId == null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isPending = await _db.TutorProfiles.AnyAsync(t => t.UserId == userId && t.VerificationStatus == VerificationStatus.Pending, cancellationToken);
            if (isPending)
            {
                return BadRequest(new { message = "Hồ sơ gia sư của bạn đang chờ Admin phê duyệt trước khi có thể mở lớp học." });
            }
            return NotFound(new { message = "Không tìm thấy hồ sơ gia sư đã được phê duyệt." });
        }

        var subjects = await _db.TutorSubjects
            .AsNoTracking()
            .Where(ts => ts.TutorId == tutorId.Value)
            .OrderBy(ts => ts.Subject.Name)
            .ThenBy(ts => ts.GradeLevel.Name)
            .Select(ts => new
            {
                key = $"{ts.SubjectId}_{ts.GradeLevelId}",
                subjectId = ts.SubjectId,
                subjectName = ts.Subject.Name,
                gradeLevelId = ts.GradeLevelId,
                gradeLevelName = ts.GradeLevel.Name,
                label = $"{ts.Subject.Name} · {ts.GradeLevel.Name}",
                hourlyFee = ts.HourlyFee
            })
            .ToListAsync(cancellationToken);

        return Ok(new { subjectGradeOptions = subjects });
    }

    [HttpGet]
    public async Task<IActionResult> GetClassrooms(
        [FromQuery] ClassroomStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId == null) return NotFound(new { message = "Không tìm thấy hồ sơ gia sư." });

        var query = _db.Classrooms
            .AsNoTracking()
            .Where(c => c.TutorId == tutorId.Value);

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var classrooms = await query
            .OrderByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.NameClass,
                Type = c.Type.ToString(),
                SubjectName = c.Subject.Name,
                GradeLevelName = c.GradeLevel.Name,
                c.CoverImageUrl,
                c.Fee,
                StudentCount = c.ClassroomStudents.Count,
                c.MaxStudents,
                Status = c.Status.ToString(),
                c.StartDate,
                c.SessionsPerWeek,
                c.HoursPerSession,
                WeeklySchedules = c.WeeklySchedules.OrderBy(w => w.SessionIndex).Select(w => new
                {
                    w.SessionIndex,
                    w.DayOfWeek,
                    w.StartTime,
                    w.EndTime
                }).ToList(),
                CompletedSessions = c.ClassSessions.Count(s => s.Status == SessionStatus.Done),
                TotalSessions = c.ClassSessions.Count,
                DocumentCount = c.LearningDocuments.Count,
                CanHardDelete = !c.ClassroomStudents.Any() && !c.ClassSessions.Any()
            })
            .ToListAsync(cancellationToken);

        var items = classrooms.Select(c =>
        {
            string formattedSchedule;
            if (c.WeeklySchedules.Count > 0)
            {
                var days = string.Join(", ", c.WeeklySchedules.Select(w => ToShortVnDay(w.DayOfWeek)));
                var firstTime = c.WeeklySchedules[0];
                formattedSchedule = $"{days} ({firstTime.StartTime:HH\\:mm} - {firstTime.EndTime:HH\\:mm})";
            }
            else
            {
                formattedSchedule = $"{c.SessionsPerWeek} buổi/tuần";
            }

            return new
            {
                c.Id,
                c.NameClass,
                c.Type,
                c.SubjectName,
                c.GradeLevelName,
                c.CoverImageUrl,
                c.Fee,
                c.StudentCount,
                c.MaxStudents,
                c.Status,
                c.StartDate,
                c.SessionsPerWeek,
                c.HoursPerSession,
                c.WeeklySchedules,
                c.CompletedSessions,
                TotalSessions = c.TotalSessions > 0 ? c.TotalSessions : 8,
                c.DocumentCount,
                c.CanHardDelete,
                FormattedSchedule = formattedSchedule
            };
        }).ToList();

        return Ok(new
        {
            items,
            page,
            pageSize,
            totalItems,
            total = totalItems,
            totalPages,
            status = status?.ToString()
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetails(int id, CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId == null) return NotFound();

        var classroom = await _db.Classrooms
            .AsNoTracking()
            .Where(c => c.Id == id && c.TutorId == tutorId.Value)
            .Select(c => new
            {
                c.Id,
                c.NameClass,
                c.Description,
                Type = c.Type.ToString(),
                c.SubjectId,
                SubjectName = c.Subject.Name,
                c.GradeLevelId,
                GradeLevelName = c.GradeLevel.Name,
                c.CoverImageUrl,
                c.Fee,
                c.MaxStudents,
                c.SessionsPerWeek,
                c.HoursPerSession,
                c.StartDate,
                Status = c.Status.ToString(),
                StudentCount = c.ClassroomStudents.Count,
                CanChangeSubjectGrade = !c.ClassroomStudents.Any() && !_db.ConnectionRequests.Any(cr => cr.ClassroomId == c.Id),
                CanChangeFee = !_db.Payments.Any(p => p.ClassroomId == c.Id && p.Status == PaymentStatus.Success),
                Students = c.ClassroomStudents.Select(cs => new
                {
                    cs.StudentId,
                    StudentName = cs.Student.User.FullName,
                    StudentEmail = cs.Student.User.Email,
                    StudentPhone = cs.Student.User.PhoneNumber,
                    cs.JoinedAt
                }).ToList(),
                WeeklySchedules = c.WeeklySchedules.OrderBy(w => w.SessionIndex).Select(w => new
                {
                    w.SessionIndex,
                    DayOfWeek = (int)w.DayOfWeek,
                    DayOfWeekName = w.DayOfWeek.ToString(),
                    StartTime = $"{w.StartTime:HH\\:mm}",
                    EndTime = $"{w.EndTime:HH\\:mm}"
                }).ToList(),
                Sessions = c.ClassSessions.OrderBy(s => s.ScheduledAt).Select(s => new
                {
                    s.Id,
                    s.ScheduledAt,
                    Status = s.Status.ToString(),
                    s.VideoRoom
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (classroom == null) return NotFound(new { message = "Không tìm thấy lớp học." });

        return Ok(classroom);
    }

    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] CreateClassroomRequest request, IFormFile? coverImageFile, CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId == null)
        {
            return BadRequest(new { message = "Hồ sơ gia sư của bạn đang chờ Admin phê duyệt. Bạn cần được Admin duyệt hồ sơ trước khi có thể mở lớp giảng dạy." });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
            return BadRequest(new { message = "Tên lớp học phải từ 1 đến 200 ký tự." });

        var validPair = await _db.TutorSubjects.AnyAsync(
            ts => ts.TutorId == tutorId.Value && ts.SubjectId == request.SubjectId && ts.GradeLevelId == request.GradeLevelId,
            cancellationToken);

        if (!validPair)
        {
            return BadRequest(new { message = "Môn học và cấp học không nằm trong danh sách đăng ký dạy của bạn." });
        }

        string? coverImageUrl = request.CoverImageUrl;
        string? publicId = null;

        if (coverImageFile != null && coverImageFile.Length > 0)
        {
            try
            {
                var stored = await _imageStorage.UploadClassroomCoverAsync(coverImageFile, cancellationToken);
                coverImageUrl = stored.Url;
                publicId = stored.PublicId;
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi tải ảnh bìa: " + ex.Message });
            }
        }

        var classroom = new Classroom
        {
            NameClass = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Fee = request.Fee,
            MaxStudents = request.MaxStudents > 0 ? request.MaxStudents : 10,
            SessionsPerWeek = request.SessionsPerWeek > 0 ? request.SessionsPerWeek : 2,
            HoursPerSession = request.HoursPerSession > 0 ? request.HoursPerSession : 2.0,
            StartDate = request.StartDate ?? DateOnly.FromDateTime(DateTime.Today),
            CoverImageUrl = coverImageUrl,
            CoverImagePublicId = publicId,
            SubjectId = request.SubjectId,
            GradeLevelId = request.GradeLevelId,
            Type = request.Type,
            Status = ClassroomStatus.Active,
            TutorId = tutorId.Value
        };

        _db.Classrooms.Add(classroom);
        await _db.SaveChangesAsync(cancellationToken);

        // Parse and add weekly schedules if provided
        var schedules = ParseWeeklySchedules(request.WeeklySchedulesJson);
        if (schedules != null && schedules.Count > 0)
        {
            for (int i = 0; i < schedules.Count; i++)
            {
                var s = schedules[i];
                _db.ClassroomWeeklySchedules.Add(new ClassroomWeeklySchedule
                {
                    ClassroomId = classroom.Id,
                    SessionIndex = i + 1,
                    DayOfWeek = s.DayOfWeek,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime
                });
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { success = true, message = "Đã tạo lớp học thành công.", classroomId = classroom.Id });
    }

    [HttpPut("{id:int}")]
    [HttpPost("{id:int}/update")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Update(int id, [FromForm] UpdateClassroomRequest request, IFormFile? coverImageFile, CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId == null) return NotFound();

        var classroom = await _db.Classrooms
            .FirstOrDefaultAsync(c => c.Id == id && c.TutorId == tutorId.Value, cancellationToken);

        if (classroom == null) return NotFound(new { message = "Không tìm thấy lớp học." });

        if (!string.IsNullOrWhiteSpace(request.Name)) classroom.NameClass = request.Name.Trim();
        if (request.Description != null) classroom.Description = request.Description.Trim();
        if (request.Fee.HasValue && request.Fee.Value > 0) classroom.Fee = request.Fee.Value;
        if (request.MaxStudents.HasValue && request.MaxStudents.Value > 0) classroom.MaxStudents = request.MaxStudents.Value;
        if (request.SessionsPerWeek.HasValue && request.SessionsPerWeek.Value > 0) classroom.SessionsPerWeek = request.SessionsPerWeek.Value;
        if (request.HoursPerSession.HasValue && request.HoursPerSession.Value > 0) classroom.HoursPerSession = request.HoursPerSession.Value;
        if (request.StartDate.HasValue) classroom.StartDate = request.StartDate.Value;

        if (request.SubjectId.HasValue && request.GradeLevelId.HasValue &&
            (request.SubjectId.Value != classroom.SubjectId || request.GradeLevelId.Value != classroom.GradeLevelId))
        {
            var validPair = await _db.TutorSubjects.AnyAsync(
                ts => ts.TutorId == tutorId.Value && ts.SubjectId == request.SubjectId.Value && ts.GradeLevelId == request.GradeLevelId.Value,
                cancellationToken);
            if (validPair)
            {
                classroom.SubjectId = request.SubjectId.Value;
                classroom.GradeLevelId = request.GradeLevelId.Value;
            }
        }

        // Cover image handling
        if (coverImageFile != null && coverImageFile.Length > 0)
        {
            try
            {
                var stored = await _imageStorage.UploadClassroomCoverAsync(coverImageFile, cancellationToken);
                classroom.CoverImageUrl = stored.Url;
                classroom.CoverImagePublicId = stored.PublicId;
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi tải ảnh bìa: " + ex.Message });
            }
        }
        else if (request.RemoveCoverImage)
        {
            classroom.CoverImageUrl = null;
            classroom.CoverImagePublicId = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.CoverImageUrl))
        {
            classroom.CoverImageUrl = request.CoverImageUrl.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Update weekly schedules if provided
        var schedules = ParseWeeklySchedules(request.WeeklySchedulesJson);
        if (schedules != null)
        {
            var oldSchedules = await _db.ClassroomWeeklySchedules
                .Where(w => w.ClassroomId == classroom.Id)
                .ToListAsync(cancellationToken);
            _db.ClassroomWeeklySchedules.RemoveRange(oldSchedules);

            for (int i = 0; i < schedules.Count; i++)
            {
                var s = schedules[i];
                _db.ClassroomWeeklySchedules.Add(new ClassroomWeeklySchedule
                {
                    ClassroomId = classroom.Id,
                    SessionIndex = i + 1,
                    DayOfWeek = s.DayOfWeek,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime
                });
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { success = true, message = "Cập nhật lớp học thành công." });
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId == null) return NotFound();

        var classroom = await _db.Classrooms
            .FirstOrDefaultAsync(c => c.Id == id && c.TutorId == tutorId.Value, cancellationToken);

        if (classroom == null) return NotFound();

        var hasStudents = await _db.ClassroomStudents.AnyAsync(cs => cs.ClassroomId == id, cancellationToken);
        var hasSessions = await _db.ClassSessions.AnyAsync(cs => cs.ClassroomId == id, cancellationToken);

        if (!hasStudents && !hasSessions)
        {
            // Hard delete
            var schedules = await _db.ClassroomWeeklySchedules.Where(w => w.ClassroomId == id).ToListAsync(cancellationToken);
            _db.ClassroomWeeklySchedules.RemoveRange(schedules);
            _db.Classrooms.Remove(classroom);
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true, message = "Đã xóa vĩnh viễn lớp học." });
        }

        classroom.Status = ClassroomStatus.Cancelled;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, message = "Đã đóng/hủy lớp học thành công." });
    }

    private static List<ClassroomWeeklyScheduleInputDto>? ParseWeeklySchedules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<ClassroomWeeklyScheduleInputDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }
}

public class CreateClassroomRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Fee { get; set; }
    public int MaxStudents { get; set; } = 10;
    public int SessionsPerWeek { get; set; } = 2;
    public double HoursPerSession { get; set; } = 2.0;
    public DateOnly? StartDate { get; set; }
    public string? CoverImageUrl { get; set; }
    public int SubjectId { get; set; }
    public int GradeLevelId { get; set; }
    public ClassroomType Type { get; set; } = ClassroomType.Group;
    public string? WeeklySchedulesJson { get; set; }
}

public class UpdateClassroomRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Fee { get; set; }
    public int? MaxStudents { get; set; }
    public int? SessionsPerWeek { get; set; }
    public double? HoursPerSession { get; set; }
    public DateOnly? StartDate { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool RemoveCoverImage { get; set; }
    public int? SubjectId { get; set; }
    public int? GradeLevelId { get; set; }
    public string? WeeklySchedulesJson { get; set; }
}
