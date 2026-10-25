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
/// Controller xá»­ lÃ½ xem há»“ sÆ¡ chi tiáº¿t gia sÆ° vÃ  gá»­i yÃªu cáº§u káº¿t ná»‘i há»c táº­p tá»« phÃ­a há»c viÃªn / phá»¥ huynh.
/// </summary>
[Area("Student_Parent")]
[Authorize(Roles = "Student_Parent")]
public class Send_Details_TutorController : Controller
{
   private readonly ApplicationDbContext _db;
   private readonly ILogger<Send_Details_TutorController> _logger;
   private readonly IOpenGroupClassroomService _openGroupClassroomService;
   private readonly IAvailabilitySlotService _availabilitySlotService;
   private readonly IHubContext<ConnectionRequestHub> _hubContext;

   public Send_Details_TutorController(
       ApplicationDbContext db,
       ILogger<Send_Details_TutorController> logger,
       IOpenGroupClassroomService openGroupClassroomService,
       IAvailabilitySlotService availabilitySlotService,
       IHubContext<ConnectionRequestHub> hubContext)
   {
       _db = db;
       _logger = logger;
       _openGroupClassroomService = openGroupClassroomService;
       _availabilitySlotService = availabilitySlotService;
       _hubContext = hubContext;
   }

   /// <summary>
   /// Hiá»ƒn thá»‹ há»“ sÆ¡ chi tiáº¿t gia sÆ° vÃ  form gá»­i yÃªu cáº§u káº¿t ná»‘i.
   /// URL: /Student_Parent/Send_Details_Tutor?id={tutorId} hoáº·c /Student_Parent/Send_Details_Tutor/Index/{id}
   /// </summary>
   [HttpGet]
   public async Task<IActionResult> Index(int? id, int? tutorId, CancellationToken cancellationToken = default)
   {
       var targetTutorId = id ?? tutorId;
       if (!targetTutorId.HasValue || targetTutorId.Value <= 0)
       {
           TempData["Error"] = "Vui lÃ²ng chá»n gia sÆ° Ä‘á»ƒ xem chi tiáº¿t.";
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
           TempData["Error"] = "KhÃ´ng tÃ¬m tháº¥y há»“ sÆ¡ gia sÆ° hoáº·c gia sÆ° hiá»‡n khÃ´ng hoáº¡t Ä‘á»™ng.";
           return RedirectToAction("Index", "Search", new { area = "Student_Parent" });
       }

       // Láº¥y danh sÃ¡ch Ä‘Ã¡nh giÃ¡ cá»§a há»c viÃªn vá» gia sÆ° nÃ y
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

       // Sá»‘ buá»•i há»c Ä‘Ã£ hoÃ n thÃ nh
       var completedSessions = await _db.ClassSessions.AsNoTracking()
           .CountAsync(s => s.Classroom.TutorId == targetTutorId.Value
                         && s.Status == SessionStatus.Done, cancellationToken);

       // Tá»•ng sá»‘ há»c viÃªn tá»«ng theo há»c
       var studentCount = await _db.ClassroomStudents.AsNoTracking()
           .Where(cs => cs.Classroom.TutorId == targetTutorId.Value)
           .Select(cs => cs.StudentId)
           .Distinct()
           .CountAsync(cancellationToken);

       // Kiá»ƒm tra xem há»c viÃªn hiá»‡n táº¡i Ä‘Ã£ gá»­i yÃªu cáº§u káº¿t ná»‘i tá»›i gia sÆ° nÃ y chÆ°a
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

       // Chuáº©n bá»‹ danh sÃ¡ch mÃ´n há»c - cáº¥p há»c Ä‘á»ƒ chá»n
       var subjectOptions = tutor.TutorSubjects
           .OrderBy(ts => ts.Subject.Name)
           .ThenBy(ts => ts.GradeLevel.Id)
           .Select(ts => new SelectListItem
           {
               Value = $"{ts.SubjectId}_{ts.GradeLevelId}",
               Text = $"{ts.Subject.Name} - {ts.GradeLevel.Name}"
           })
           .ToList();

       // Chá»‰ táº£i cÃ¡c lá»›p nhÃ³m Ä‘ang tuyá»ƒn sinh vÃ  cÃ²n chá»—.
       var openClassrooms = await _openGroupClassroomService.GetOpenGroupClassroomsAsync(tutor.Id, cancellationToken);
       var classroomOptions = openClassrooms
           .Select(classroom => new SelectListItem
           {
               Value = classroom.Id.ToString(),
               Text = $"{classroom.Name} ({classroom.SubjectName} - {classroom.GradeLevelName}) - CÃ²n {classroom.AvailableSeats} chá»—"
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
           AvailableTrialSlots = await _availabilitySlotService.GetAvailableSlotsAsync(tutor.Id, 14, cancellationToken),

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
   /// Tiáº¿p nháº­n vÃ  xá»­ lÃ½ gá»­i yÃªu cáº§u káº¿t ná»‘i tá»« há»c viÃªn/phá»¥ huynh tá»›i gia sÆ°.
   /// Há»— trá»£ cáº£ AJAX call vÃ  Postback tiÃªu chuáº©n.
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
               .FirstOrDefault() ?? "Dá»¯ liá»‡u gá»­i lÃªn khÃ´ng há»£p lá»‡.";

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
           const string msg = "Gia sÆ° khÃ´ng tá»“n táº¡i hoáº·c hiá»‡n khÃ´ng nháº­n há»c viÃªn má»›i.";
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
           const string msg = "Lá»›p Ä‘Ã£ chá»n khÃ´ng cÃ²n tuyá»ƒn sinh, Ä‘Ã£ háº¿t chá»— hoáº·c khÃ´ng khá»›p mÃ´n há»c vÃ  cáº¥p há»c.";
           if (IsAjaxRequest()) return Json(new { success = false, message = msg });
           TempData["Error"] = msg;
           return RedirectToAction(nameof(Index), new { id = model.TutorId });
       }

       // Kiá»ƒm tra xem Ä‘Ã£ cÃ³ yÃªu cáº§u káº¿t ná»‘i nÃ o Ä‘ang chá» pháº£n há»“i cho mÃ´n nÃ y chÆ°a
       var hasPendingRequest = await _db.ConnectionRequests.AnyAsync(cr =>
           cr.StudentId == student.Id &&
           cr.TutorId == tutor.Id &&
           cr.SubjectId == model.SubjectId &&
           cr.Status == RequestStatus.Pending, cancellationToken);

       if (hasPendingRequest)
       {
           const string msg = "Báº¡n Ä‘Ã£ cÃ³ má»™t yÃªu cáº§u káº¿t ná»‘i Ä‘ang chá» gia sÆ° pháº£n há»“i. Vui lÃ²ng chá» pháº£n há»“i trÆ°á»›c khi gá»­i yÃªu cáº§u má»›i.";
           if (IsAjaxRequest()) return Json(new { success = false, message = msg });
           TempData["Warning"] = msg;
           return RedirectToAction(nameof(Index), new { id = model.TutorId });
       }

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
                   ? "Vui lÃ²ng chá»n Ä‘á»§ 4 tiáº¿ng (2 buá»•i, má»—i buá»•i 2 tiáº¿ng) trá»±c tiáº¿p trÃªn khung lá»‹ch ráº£nh cá»§a gia sÆ°."
                   : "Báº¡n má»›i chá»n 1 buá»•i (2 tiáº¿ng). Lá»›p 1 kÃ¨m 1 báº¯t buá»™c pháº£i chá»n Ä‘á»§ 4 tiáº¿ng (2 buá»•i, má»—i buá»•i 2 tiáº¿ng) chia Ä‘á»u trong tuáº§n.";

               if (IsAjaxRequest()) return Json(new { success = false, message = msg });
               TempData["Error"] = msg;
               return RedirectToAction(nameof(Index), new { id = model.TutorId });
           }
       }

       // Äá»‹nh dáº¡ng lá»‹ch Ä‘á» xuáº¥t vÃ o Message
       var baseMessage = model.Message?.Trim();
       string? finalMessage = baseMessage;
       if (model.PreferredType == ClassroomType.OneToOne && selectedSlots.Count > 0)
       {
           var slotStrings = selectedSlots.Select((slotUtc, idx) =>
           {
               var vn = slotUtc.AddHours(7);
               return $"Buá»•i {idx + 1}: {vn.ToString("dddd, dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("vi-VN"))}";
           });
           var scheduleHeader = $"[Lá»‹ch há»c 1 kÃ¨m 1 Ä‘á» xuáº¥t ({selectedSlots.Count * 2} tiáº¿ng/tuáº§n): {string.Join(" | ", slotStrings)}]";
           finalMessage = string.IsNullOrWhiteSpace(baseMessage)
               ? scheduleHeader
               : $"{scheduleHeader}\n{baseMessage}";
       }

       // Táº¡o yÃªu cáº§u káº¿t ná»‘i má»›i
       var connectionRequest = new ConnectionRequest
       {
           StudentId = student.Id,
           TutorId = tutor.Id,
           SubjectId = model.SubjectId,
           GradeLevelId = model.GradeLevelId,
           PreferredType = model.PreferredType,
           ClassroomId = model.PreferredType == ClassroomType.Group ? model.ClassroomId : null,
           ProposedTrialSlotUtc = model.PreferredType == ClassroomType.OneToOne ? selectedSlots.FirstOrDefault() : null,
           Message = finalMessage,
           Status = RequestStatus.Pending,
           CreatedAt = DateTime.UtcNow
       };

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

       _db.ConnectionRequests.Add(connectionRequest);

       // Táº¡o thÃ´ng bÃ¡o cho gia sÆ°
       var studentName = student.User?.FullName ?? "Há»c viÃªn";
       var subject = await _db.Subjects.FindAsync(new object[] { model.SubjectId }, cancellationToken);
       var subjectName = subject?.Name ?? "mÃ´n há»c";

       var notification = new Notification
       {
           UserId = tutor.UserId,
           Title = "YÃªu cáº§u káº¿t ná»‘i má»›i",
           Content = $"Há»c viÃªn {studentName} vá»«a gá»­i cho báº¡n má»™t yÃªu cáº§u káº¿t ná»‘i há»c mÃ´n {subjectName}. HÃ£y xem chi tiáº¿t vÃ  pháº£n há»“i nhÃ©!",
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

       const string successMessage = "ÄÃ£ gá»­i yÃªu cáº§u káº¿t ná»‘i thÃ nh cÃ´ng! Gia sÆ° Ä‘Ã£ nháº­n Ä‘Æ°á»£c thÃ´ng bÃ¡o vÃ  sáº½ sá»›m pháº£n há»“i cho báº¡n.";

       if (IsAjaxRequest())
       {
           return Json(new { success = true, message = successMessage });
       }

       TempData["Success"] = successMessage;
       return RedirectToAction(nameof(Index), new { id = model.TutorId });
   }

   /// <summary>
   /// Danh sÃ¡ch cÃ¡c yÃªu cáº§u káº¿t ná»‘i mÃ  há»c viÃªn/phá»¥ huynh Ä‘Ã£ gá»­i tá»›i cÃ¡c gia sÆ°.
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
   /// Cho phÃ©p há»c viÃªn há»§y yÃªu cáº§u káº¿t ná»‘i Ä‘ang á»Ÿ tráº¡ng thÃ¡i Chá» duyá»‡t (Pending).
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
           TempData["Error"] = "Chá»‰ cÃ³ thá»ƒ há»§y yÃªu cáº§u Ä‘ang á»Ÿ tráº¡ng thÃ¡i Chá» pháº£n há»“i.";
           return RedirectToAction(nameof(MyRequests));
       }

       _db.ConnectionRequests.Remove(request);
       await _db.SaveChangesAsync(cancellationToken);

       await _hubContext.Clients.User(request.Tutor.UserId).SendAsync(
           "ConnectionRequestCancelled",
           new { requestId = id },
           cancellationToken);

       TempData["Success"] = "ÄÃ£ há»§y yÃªu cáº§u káº¿t ná»‘i thÃ nh cÃ´ng.";
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
