using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Hubs;
using PROJECT_OTSP.Models;
using PROJECT_OTSP.Services;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Areas.Student_Parent.Controllers;

/// <summary>
/// Controller xử lý xem hồ sơ chi tiết gia sư và gửi yêu cầu kết nối học tập từ phía học viên / phụ huynh.
/// </summary>
[Area("Student_Parent")]
[Authorize(Roles = "Student_Parent")]
public class Send_Details_TutorController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<Send_Details_TutorController> _logger;
    private readonly IOpenGroupClassroomService _openGroupClassroomService;
<<<<<<< HEAD
=======
    private readonly IAvailabilitySlotService _availabilitySlotService;
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
    private readonly IHubContext<ConnectionRequestHub> _hubContext;

    public Send_Details_TutorController(
        ApplicationDbContext db,
        ILogger<Send_Details_TutorController> logger,
        IOpenGroupClassroomService openGroupClassroomService,
<<<<<<< HEAD
=======
        IAvailabilitySlotService availabilitySlotService,
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
        IHubContext<ConnectionRequestHub> hubContext)
    {
        _db = db;
        _logger = logger;
        _openGroupClassroomService = openGroupClassroomService;
<<<<<<< HEAD
=======
        _availabilitySlotService = availabilitySlotService;
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
        _hubContext = hubContext;
    }

    /// <summary>
    /// Hiển thị hồ sơ chi tiết gia sư và form gửi yêu cầu kết nối.
    /// URL: /Student_Parent/Send_Details_Tutor?id={tutorId} hoặc /Student_Parent/Send_Details_Tutor/Index/{id}
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(int? id, int? tutorId, CancellationToken cancellationToken = default)
    {
        var targetTutorId = id ?? tutorId;
        if (!targetTutorId.HasValue || targetTutorId.Value <= 0)
        {
            TempData["Error"] = "Vui lòng chọn gia sư để xem chi tiết.";
            return RedirectToAction("Index", "Search", new { area = "Student_Parent" });
        }

        var tutor = await _db.TutorProfiles.AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.Qualifications.Where(q => q.Status == VerificationStatus.Approved))
            .Include(t => t.TutorSubjects)
                .ThenInclude(ts => ts.Subject)
            .Include(t => t.TutorSubjects)
                .ThenInclude(ts => ts.GradeLevel)
            .Include(t => t.Availabilities)
            .FirstOrDefaultAsync(t => t.Id == targetTutorId.Value
                                   && t.VerificationStatus == VerificationStatus.Approved
                                   && t.User.Status == AccountStatus.Active, cancellationToken);

        if (tutor == null)
        {
            TempData["Error"] = "Không tìm thấy hồ sơ gia sư hoặc gia sư hiện không hoạt động.";
            return RedirectToAction("Index", "Search", new { area = "Student_Parent" });
        }

        // Lấy danh sách đánh giá của học viên về gia sư này
        var reviews = await _db.Reviews.AsNoTracking()
            .Where(r => r.ClassSession.Classroom.TutorId == targetTutorId.Value)
            .OrderByDescending(r => r.ReviewedAt)
            .Take(20)
            .Select(r => new TutorReviewDto
            {
                Id = r.Id,
                StudentName = r.Student.User.FullName,
                StudentAvatarUrl = r.Student.User.AvatarUrl,
                Score = r.Score,
                Comment = r.Comment,
                ReviewedAt = r.ReviewedAt
            })
            .ToListAsync(cancellationToken);

        // Số buổi học đã hoàn thành
        var completedSessions = await _db.ClassSessions.AsNoTracking()
            .CountAsync(s => s.Classroom.TutorId == targetTutorId.Value
                          && s.Status == SessionStatus.Done, cancellationToken);

        // Tổng số học viên từng theo học
        var studentCount = await _db.ClassroomStudents.AsNoTracking()
            .Where(cs => cs.Classroom.TutorId == targetTutorId.Value)
            .Select(cs => cs.StudentId)
            .Distinct()
            .CountAsync(cancellationToken);

        // Kiểm tra xem học viên hiện tại đã gửi yêu cầu kết nối tới gia sư này chưa
        var currentStudentId = await GetStudentIdAsync(cancellationToken);
        ConnectionRequest? recentRequest = null;
        if (currentStudentId.HasValue)
        {
            recentRequest = await _db.ConnectionRequests.AsNoTracking()
                .Include(cr => cr.Subject)
                .Where(cr => cr.StudentId == currentStudentId.Value && cr.TutorId == targetTutorId.Value)
                .OrderByDescending(cr => cr.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Chuẩn bị danh sách môn học - cấp học để chọn
        var subjectOptions = tutor.TutorSubjects
            .OrderBy(ts => ts.Subject.Name)
            .ThenBy(ts => ts.GradeLevel.Id)
            .Select(ts => new SelectListItem
            {
                Value = $"{ts.SubjectId}_{ts.GradeLevelId}",
                Text = $"{ts.Subject.Name} - {ts.GradeLevel.Name}"
            })
            .ToList();

        // Chỉ tải các lớp nhóm đang tuyển sinh và còn chỗ.
        var openClassrooms = await _openGroupClassroomService.GetOpenGroupClassroomsAsync(tutor.Id, cancellationToken);
        var classroomOptions = openClassrooms
            .Select(classroom => new SelectListItem
            {
                Value = classroom.Id.ToString(),
                Text = $"{classroom.Name} ({classroom.SubjectName} - {classroom.GradeLevelName}) - Còn {classroom.AvailableSeats} chỗ"
            })
            .ToList();

        var firstSubject = tutor.TutorSubjects.FirstOrDefault();

        var vm = new TutorDetailsVm
        {
            TutorId = tutor.Id,
            UserId = tutor.UserId,
            FullName = tutor.User.FullName,
            AvatarUrl = tutor.User.AvatarUrl,
            Email = tutor.User.Email,
            PhoneNumber = tutor.User.PhoneNumber,
            Area = tutor.User.Address,
            HourlyFee = tutor.HourlyFee,
            TeachingFormat = tutor.TeachingFormat,
            AverageRating = tutor.AverageRating,
            Introduction = tutor.Introduction,
            TotalCompletedSessions = completedSessions,
            TotalStudents = studentCount,

            Qualifications = tutor.Qualifications.Select(q => new TutorQualificationDto
            {
                Id = q.Id,
                Title = q.Title,
                FileUrl = q.FileUrl,
                Status = q.Status
            }).ToList(),

            TeachingSubjects = tutor.TutorSubjects.Select(ts => new SubjectGradeDto
            {
                SubjectId = ts.SubjectId,
                SubjectName = ts.Subject.Name,
                GradeLevelId = ts.GradeLevelId,
                GradeName = ts.GradeLevel.Name
            }).ToList(),

            Availabilities = tutor.Availabilities
                .OrderBy(a => a.DayOfWeek)
                .ThenBy(a => a.StartTime)
                .Select(a => new TutorAvailabilityDto
                {
                    DayOfWeek = a.DayOfWeek,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime
                }).ToList(),

            Classrooms = openClassrooms,
<<<<<<< HEAD
=======
            AvailableTrialSlots = await _availabilitySlotService.GetAvailableSlotsAsync(tutor.Id, 14, cancellationToken),
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc

            Reviews = reviews,
            SubjectOptions = subjectOptions,
            ClassroomOptions = classroomOptions,

            HasExistingPendingRequest = recentRequest != null && recentRequest.Status == RequestStatus.Pending,
            ExistingRequestStatus = recentRequest?.Status,
            ExistingRequestDate = recentRequest?.CreatedAt,
            ExistingRequestSubject = recentRequest?.Subject?.Name,

            Request = new SendConnectionRequestDto
            {
                TutorId = tutor.Id,
                SubjectId = firstSubject?.SubjectId ?? 0,
                GradeLevelId = firstSubject?.GradeLevelId ?? 0,
                PreferredType = ClassroomType.OneToOne
            }
        };

        return View(vm);
    }

    /// <summary>
    /// Tiếp nhận và xử lý gửi yêu cầu kết nối từ học viên/phụ huynh tới gia sư.
    /// Hỗ trợ cả AJAX call và Postback tiêu chuẩn.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendRequest([Bind(Prefix = "Request")] SendConnectionRequestDto model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault() ?? "Dữ liệu gửi lên không hợp lệ.";

            if (IsAjaxRequest())
                return Json(new { success = false, message = firstError });

            TempData["Error"] = firstError;
            return RedirectToAction(nameof(Index), new { id = model.TutorId });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var student = await _db.StudentProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (student == null)
        {
            student = new StudentProfile { UserId = userId };
            _db.StudentProfiles.Add(student);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var tutor = await _db.TutorProfiles
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == model.TutorId
                                   && t.VerificationStatus == VerificationStatus.Approved
                                   && t.User.Status == AccountStatus.Active, cancellationToken);

        if (tutor == null)
        {
            const string msg = "Gia sư không tồn tại hoặc hiện không nhận học viên mới.";
            if (IsAjaxRequest()) return Json(new { success = false, message = msg });
            TempData["Error"] = msg;
            return RedirectToAction("Index", "Search", new { area = "Student_Parent" });
        }

        if (model.PreferredType == ClassroomType.Group
            && model.ClassroomId.HasValue
            && !await _openGroupClassroomService.IsOpenGroupClassroomAsync(
                tutor.Id,
                model.ClassroomId.Value,
                model.SubjectId,
                model.GradeLevelId,
                cancellationToken))
        {
            const string msg = "Lớp đã chọn không còn tuyển sinh, đã hết chỗ hoặc không khớp môn học và cấp học.";
            if (IsAjaxRequest()) return Json(new { success = false, message = msg });
            TempData["Error"] = msg;
            return RedirectToAction(nameof(Index), new { id = model.TutorId });
        }

        // Kiểm tra xem đã có yêu cầu kết nối nào đang chờ phản hồi cho môn này chưa
        var hasPendingRequest = await _db.ConnectionRequests.AnyAsync(cr =>
            cr.StudentId == student.Id &&
            cr.TutorId == tutor.Id &&
            cr.SubjectId == model.SubjectId &&
            cr.Status == RequestStatus.Pending, cancellationToken);

        if (hasPendingRequest)
        {
            const string msg = "Bạn đã có một yêu cầu kết nối đang chờ gia sư phản hồi. Vui lòng chờ phản hồi trước khi gửi yêu cầu mới.";
            if (IsAjaxRequest()) return Json(new { success = false, message = msg });
            TempData["Warning"] = msg;
            return RedirectToAction(nameof(Index), new { id = model.TutorId });
        }

<<<<<<< HEAD
=======
        var selectedSlots = (model.ProposedTrialSlotsUtc ?? new List<DateTime>())
            .Where(s => s > DateTime.UtcNow)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        if (selectedSlots.Count == 0 && model.ProposedTrialSlotUtc.HasValue && model.ProposedTrialSlotUtc.Value > DateTime.UtcNow)
        {
            selectedSlots.Add(model.ProposedTrialSlotUtc.Value);
        }

        if (model.PreferredType == ClassroomType.OneToOne)
        {
            if (selectedSlots.Count < 2)
            {
                var msg = selectedSlots.Count == 0
                    ? "Vui lòng chọn đủ 4 tiếng (2 buổi, mỗi buổi 2 tiếng) trực tiếp trên khung lịch rảnh của gia sư."
                    : "Bạn mới chọn 1 buổi (2 tiếng). Lớp 1 kèm 1 bắt buộc phải chọn đủ 4 tiếng (2 buổi, mỗi buổi 2 tiếng) chia đều trong tuần.";

                if (IsAjaxRequest()) return Json(new { success = false, message = msg });
                TempData["Error"] = msg;
                return RedirectToAction(nameof(Index), new { id = model.TutorId });
            }
        }

        // Định dạng lịch đề xuất vào Message
        var baseMessage = model.Message?.Trim();
        string? finalMessage = baseMessage;
        if (model.PreferredType == ClassroomType.OneToOne && selectedSlots.Count > 0)
        {
            var slotStrings = selectedSlots.Select((slotUtc, idx) =>
            {
                var vn = slotUtc.AddHours(7);
                return $"Buổi {idx + 1}: {vn.ToString("dddd, dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("vi-VN"))}";
            });
            var scheduleHeader = $"[Lịch học 1 kèm 1 đề xuất ({selectedSlots.Count * 2} tiếng/tuần): {string.Join(" | ", slotStrings)}]";
            finalMessage = string.IsNullOrWhiteSpace(baseMessage)
                ? scheduleHeader
                : $"{scheduleHeader}\n{baseMessage}";
        }

>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
        // Tạo yêu cầu kết nối mới
        var connectionRequest = new ConnectionRequest
        {
            StudentId = student.Id,
            TutorId = tutor.Id,
            SubjectId = model.SubjectId,
            GradeLevelId = model.GradeLevelId,
            PreferredType = model.PreferredType,
            ClassroomId = model.PreferredType == ClassroomType.Group ? model.ClassroomId : null,
<<<<<<< HEAD
            Message = model.Message?.Trim(),
=======
            ProposedTrialSlotUtc = model.PreferredType == ClassroomType.OneToOne ? selectedSlots.FirstOrDefault() : null,
            Message = finalMessage,
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
            Status = RequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

<<<<<<< HEAD
=======
        if (model.PreferredType == ClassroomType.OneToOne)
        {
            foreach (var slotUtc in selectedSlots)
            {
                connectionRequest.TrialSchedules.Add(new TrialSchedule
                {
                    ScheduledAt = slotUtc,
                    Status = ScheduleStatus.Pending
                });
            }
        }

>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
        _db.ConnectionRequests.Add(connectionRequest);

        // Tạo thông báo cho gia sư
        var studentName = student.User?.FullName ?? "Học viên";
        var subject = await _db.Subjects.FindAsync(new object[] { model.SubjectId }, cancellationToken);
        var subjectName = subject?.Name ?? "môn học";

        var notification = new Notification
        {
            UserId = tutor.UserId,
            Title = "Yêu cầu kết nối mới",
            Content = $"Học viên {studentName} vừa gửi cho bạn một yêu cầu kết nối học môn {subjectName}. Hãy xem chi tiết và phản hồi nhé!",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        _db.Notifications.Add(notification);

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _hubContext.Clients.User(tutor.UserId).SendAsync(
                "ConnectionRequestReceived",
                new
                {
                    requestId = connectionRequest.Id,
                    studentName,
                    subjectName
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not send realtime alert for ConnectionRequest {RequestId}", connectionRequest.Id);
        }

        _logger.LogInformation("Student {StudentId} sent ConnectionRequest {RequestId} to Tutor {TutorId}",
            student.Id, connectionRequest.Id, tutor.Id);

        const string successMessage = "Đã gửi yêu cầu kết nối thành công! Gia sư đã nhận được thông báo và sẽ sớm phản hồi cho bạn.";

        if (IsAjaxRequest())
        {
            return Json(new { success = true, message = successMessage });
        }

        TempData["Success"] = successMessage;
        return RedirectToAction(nameof(Index), new { id = model.TutorId });
    }

    /// <summary>
    /// Danh sách các yêu cầu kết nối mà học viên/phụ huynh đã gửi tới các gia sư.
    /// URL: /Student_Parent/Send_Details_Tutor/MyRequests
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> MyRequests(RequestStatus? status, CancellationToken cancellationToken = default)
    {
        var studentId = await GetStudentIdAsync(cancellationToken);
        if (studentId == null)
            return RedirectToAction("Index", "Search", new { area = "Student_Parent" });

        var query = _db.ConnectionRequests.AsNoTracking()
            .Where(cr => cr.StudentId == studentId.Value);

        if (status.HasValue)
            query = query.Where(cr => cr.Status == status.Value);

        var requests = await query
            .OrderByDescending(cr => cr.CreatedAt)
            .Select(cr => new StudentConnectionRequestDto
            {
                Id = cr.Id,
                TutorId = cr.TutorId,
                TutorName = cr.Tutor.User.FullName,
                TutorAvatarUrl = cr.Tutor.User.AvatarUrl,
                HourlyFee = cr.Tutor.HourlyFee,
                SubjectName = cr.Subject.Name,
                GradeLevelName = cr.GradeLevel.Name,
                PreferredType = cr.PreferredType,
                ClassroomName = cr.Classroom != null ? cr.Classroom.Name : null,
                Message = cr.Message,
                Status = cr.Status,
                CreatedAt = cr.CreatedAt,
                RespondedAt = cr.RespondedAt,
                TrialScheduledAt = cr.TrialSchedules
                    .Where(schedule => schedule.Status == ScheduleStatus.Confirmed)
                    .OrderByDescending(schedule => schedule.ScheduledAt)
                    .Select(schedule => (DateTime?)schedule.ScheduledAt)
                    .FirstOrDefault(),
                ClassSessionId = cr.TrialSchedules
                    .Where(schedule => schedule.Status == ScheduleStatus.Confirmed)
                    .OrderByDescending(schedule => schedule.ScheduledAt)
                    .Select(schedule => schedule.ClassSessionId)
                    .FirstOrDefault(),
                ClassSessionStatus = cr.TrialSchedules
                    .Where(schedule => schedule.Status == ScheduleStatus.Confirmed)
                    .OrderByDescending(schedule => schedule.ScheduledAt)
                    .Select(schedule => schedule.ClassSession == null
                        ? (SessionStatus?)null
                        : schedule.ClassSession.Status)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        ViewBag.SelectedStatus = status;
        return View(requests);
    }

    /// <summary>
    /// Cho phép học viên hủy yêu cầu kết nối đang ở trạng thái Chờ duyệt (Pending).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelRequest(int id, CancellationToken cancellationToken = default)
    {
        var studentId = await GetStudentIdAsync(cancellationToken);
        if (studentId == null)
            return Forbid();

        var request = await _db.ConnectionRequests
            .Include(cr => cr.Tutor)
            .FirstOrDefaultAsync(cr => cr.Id == id && cr.StudentId == studentId.Value, cancellationToken);

        if (request == null)
            return NotFound();

        if (request.Status != RequestStatus.Pending)
        {
            TempData["Error"] = "Chỉ có thể hủy yêu cầu đang ở trạng thái Chờ phản hồi.";
            return RedirectToAction(nameof(MyRequests));
        }

        _db.ConnectionRequests.Remove(request);
        await _db.SaveChangesAsync(cancellationToken);

        await _hubContext.Clients.User(request.Tutor.UserId).SendAsync(
            "ConnectionRequestCancelled",
            new { requestId = id },
            cancellationToken);

        TempData["Success"] = "Đã hủy yêu cầu kết nối thành công.";
        return RedirectToAction(nameof(MyRequests));
    }

    // ----- Helpers -----

    private async Task<int?> GetStudentIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return null;

        return await _db.StudentProfiles.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private bool IsAjaxRequest()
    {
        return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
            || (Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase));
    }
}
