using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Services;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Dtos.Tutor;
using Backend_Project_OTSP.Security;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.StudentParent;
using Backend_Project_OTSP.Services.Tutor;
using System.Reflection.Metadata.Ecma335;

namespace BACKEND_PROJECT_OTSP.Controllers.Api.Student;

[ApiController]
[Route("api/student/connection-requests")]
[Authorize(Roles = AppRoles.StudentAccess)]
public class StudentConnectionRequestApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IConnectionRequestService _connectionRequestService;
    private readonly IOpenGroupClassroomService _openGroupClassroomService;
    private readonly IAvailabilitySlotService _availabilitySlotService;

    public StudentConnectionRequestApiController(
        ApplicationDbContext db,
        IConnectionRequestService connectionRequestService,
        IOpenGroupClassroomService openGroupClassroomService,
        IAvailabilitySlotService availabilitySlotService)
    {
        _db = db;
        _connectionRequestService = connectionRequestService;
        _openGroupClassroomService = openGroupClassroomService;
        _availabilitySlotService = availabilitySlotService;
    }
    
    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet("tutors/{tutorId:int}")]
    public async Task<IActionResult> GetTutorBookingDetails(int tutorId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var student = await _db.StudentProfiles.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (student == null) return NotFound(new { message = "Không tìm thấy hồ sơ học viên." });

        var details = await TutorDetailQueryHelper.BuildPublicTutorDetailAsync(
            _db,
            tutorId,
            _openGroupClassroomService,
            userId,
            isAdminViewer: false,
            cancellationToken);

        if (details == null) return NotFound(new { message = "Không tìm thấy hồ sơ gia sư." });

        var availableSlots = await _availabilitySlotService.GetAvailableSlotsForStudentAsync(
            tutorId,
            student.Id,
            14,
            cancellationToken);
        var bookingEligibility = await _connectionRequestService.GetBookingEligibilityAsync(
            userId,
            tutorId,
            cancellationToken);

        var existingRequest = await _db.ConnectionRequests.AsNoTracking()
            .Where(cr => cr.Student.UserId == userId
                      && cr.TutorId == tutorId
                      && (cr.Status == RequestStatus.Pending
                          || (cr.Status == RequestStatus.Accepted
                              && (!cr.ClassroomId.HasValue || cr.Classroom!.Status == ClassroomStatus.Active))))
            .OrderByDescending(cr => cr.CreatedAt)
            .Select(cr => new { cr.Id, cr.Status, cr.CreatedAt, SubjectName = cr.Subject.Name })
            .FirstOrDefaultAsync(cancellationToken);

        var enrolledClassroomIds = await _db.ClassroomStudents.AsNoTracking()
            .Where(cs => cs.StudentId == student.Id && cs.Classroom.TutorId == tutorId && cs.Classroom.Status == ClassroomStatus.Active)
            .Select(cs => cs.ClassroomId)
            .ToListAsync(cancellationToken);

        var activeOneToOneSubjectIds = await _db.ClassroomStudents.AsNoTracking()
            .Where(cs => cs.StudentId == student.Id && cs.Classroom.TutorId == tutorId && cs.Classroom.Type == ClassroomType.OneToOne && cs.Classroom.Status == ClassroomStatus.Active)
            .Select(cs => cs.Classroom.SubjectId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var pendingRequests = await _db.ConnectionRequests.AsNoTracking()
            .Where(cr => cr.StudentId == student.Id && cr.TutorId == tutorId && cr.Status == RequestStatus.Pending)
            .ToListAsync(cancellationToken);

        var pendingSubjectIds = pendingRequests.Select(r => r.SubjectId).Distinct().ToList();
        var pendingClassroomIds = pendingRequests.Where(r => r.ClassroomId.HasValue).Select(r => r.ClassroomId!.Value).Distinct().ToList();

        return Ok(new
        {
            tutor = details.Tutor,
            subjects = details.Subjects,
            qualifications = details.Qualifications,
            availabilities = details.Availabilities,
            reviews = details.Reviews,
            classrooms = details.Classrooms,
            availableSlots,
            bookingEligibility,
            existingRequest,
            enrolledClassroomIds,
            activeOneToOneSubjectIds,
            pendingSubjectIds,
            pendingClassroomIds
        });
    }

    [HttpPost]
    public async Task<IActionResult> SendRequest([FromBody] SendConnectionRequestDto model, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _connectionRequestService.CreateRequestAsync(model, userId, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        return Ok(new { success = true, message = result.Message });
    }

    [HttpGet("my-requests")]
    public async Task<IActionResult> GetMyRequests(
        [FromQuery] RequestStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var query = _db.ConnectionRequests
            .AsNoTracking()
            .Where(r => r.Student.UserId == userId);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim().ToLower();
            query = query.Where(r =>
                (r.Tutor.User.FullName != null && r.Tutor.User.FullName.ToLower().Contains(searchTerm)) ||
                r.Subject.Name.ToLower().Contains(searchTerm) ||
                r.GradeLevel.Name.ToLower().Contains(searchTerm) ||
                (r.Classroom != null && r.Classroom.NameClass.ToLower().Contains(searchTerm)));
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.TutorId,
                TutorUserId = r.Tutor.UserId,
                TutorName = r.Tutor.User.FullName,
                TutorAvatarUrl = r.Tutor.User.AvatarUrl,
                HourlyFee = r.Tutor.HourlyFee,
                SubjectName = r.Subject.Name,
                GradeLevelName = r.GradeLevel.Name,
                r.Message,
                PreferredType = r.PreferredType.ToString(),
                Status = r.Status.ToString(),
                r.CreatedAt,
                r.RespondedAt,
                TrialSlots = r.TrialSchedules.Select(ts => ts.ScheduledAt).ToList(),
                TrialScheduledAt = r.TrialSchedules.OrderBy(ts => ts.ScheduledAt).Select(ts => (DateTime?)ts.ScheduledAt).FirstOrDefault(),
                ClassSessionId = r.TrialSchedules.Where(ts => ts.ClassSessionId.HasValue).Select(ts => ts.ClassSessionId).FirstOrDefault(),
                ClassSessionStatus = r.TrialSchedules.Where(ts => ts.ClassSession != null).Select(ts => ts.ClassSession!.Status.ToString()).FirstOrDefault(),
                ClassroomId = r.ClassroomId,
                ClassroomName = r.Classroom != null ? r.Classroom.NameClass : null
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            items = requests,
            page,
            pageSize,
            totalItems,
            totalPages,
            status = status?.ToString()
        });
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> CancelRequest(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var request = await _db.ConnectionRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.Student.UserId == userId, cancellationToken);

        if (request == null) return NotFound(new { message = "Không tìm thấy yêu cầu." });

        if (request.Status != RequestStatus.Pending)
        {
            return BadRequest(new { message = "Chỉ có thể hủy yêu cầu đang ở trạng thái Chờ phản hồi." });
        }

        request.Status = RequestStatus.Cancelled;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, message = "Đã hủy yêu cầu kết nối thành công." });
    }
}
