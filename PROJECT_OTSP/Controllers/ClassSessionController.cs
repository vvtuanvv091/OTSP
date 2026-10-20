using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Hubs;
using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Controllers;

[Authorize(Roles = "Student_Parent,Tutor")]
public sealed class ClassSessionController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<ConnectionRequestHub> _hubContext;

    public ClassSessionController(ApplicationDbContext db, IHubContext<ConnectionRequestHub> hubContext)
    {
        _db = db;
        _hubContext = hubContext;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(item => item.Id == id
                && (item.Status == SessionStatus.Scheduled || item.Status == SessionStatus.InProgress)
                && item.Classroom.Status == ClassroomStatus.Active,
                cancellationToken);
        if (session is null)
            return NotFound();

        if (!await IsParticipantAsync(session.ClassroomId, userId, cancellationToken))
            return Forbid();

        var isTutor = User.IsInRole("Tutor");
        var started = session.Status == SessionStatus.Scheduled && isTutor;
        if (started)
            session.Status = SessionStatus.InProgress;

        var roomCreated = string.IsNullOrWhiteSpace(session.VideoRoom);
        if (roomCreated)
        {
            session.VideoRoom = $"OTSP-{Guid.NewGuid():N}";
        }

        if (started || roomCreated)
            await _db.SaveChangesAsync(cancellationToken);

        await RecordAttendanceJoinAsync(session.Id, userId, cancellationToken);

        if (started)
            await NotifyParticipantsAsync(session.ClassroomId, session.Id, SessionStatus.InProgress, cancellationToken);

        return RedirectToAction(nameof(Room), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var session = await _db.ClassSessions.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id, item.ClassroomId })
            .FirstOrDefaultAsync(cancellationToken);
        if (session is null)
            return NotFound();

        if (!await IsParticipantAsync(session.ClassroomId, userId, cancellationToken))
            return Forbid();

        await _db.ClassSessionAttendances
            .Where(attendance => attendance.ClassSessionId == id && attendance.UserId == userId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(attendance => attendance.LeftAtUtc, DateTime.UtcNow), cancellationToken);

        return NoContent();
    }

    [HttpPost]
    [Authorize(Roles = "Tutor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> End(int id, CancellationToken cancellationToken)
    {
        var tutorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(item => item.Id == id && item.Classroom.Tutor.UserId == tutorUserId,
                cancellationToken);
        if (session is null)
            return NotFound();
        if (session.Status != SessionStatus.InProgress && session.Status != SessionStatus.Scheduled)
        {
            TempData["Error"] = "Chỉ có thể kết thúc buổi học đang diễn ra.";
            return RedirectToAction("Index", "TutorSchedule", new { area = "Tutor" });
        }

        session.Status = SessionStatus.Done;
        await _db.SaveChangesAsync(cancellationToken);
        await _db.ClassSessionAttendances
            .Where(attendance => attendance.ClassSessionId == id && attendance.LeftAtUtc == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(attendance => attendance.LeftAtUtc, DateTime.UtcNow), cancellationToken);
        await NotifyParticipantsAsync(session.ClassroomId, session.Id, SessionStatus.Done, cancellationToken);

        TempData["Success"] = "Đã kết thúc buổi học. Gia sư có thể mở lại nếu cần.";
        return RedirectToAction("Index", "TutorSchedule", new { area = "Tutor" });
    }

    [HttpPost]
    [Authorize(Roles = "Tutor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(int id, CancellationToken cancellationToken)
    {
        var tutorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(item => item.Id == id && item.Classroom.Tutor.UserId == tutorUserId,
                cancellationToken);
        if (session is null)
            return NotFound();
        if (session.Status != SessionStatus.Done)
        {
            TempData["Error"] = "Chỉ có thể mở lại buổi học đã kết thúc.";
            return RedirectToAction("Index", "TutorSchedule", new { area = "Tutor" });
        }

        session.Status = SessionStatus.InProgress;
        await _db.SaveChangesAsync(cancellationToken);
        await NotifyParticipantsAsync(session.ClassroomId, session.Id, SessionStatus.InProgress, cancellationToken);

        TempData["Success"] = "Đã mở lại buổi học. Học viên có thể tham gia lại.";
        return RedirectToAction("Index", "TutorSchedule", new { area = "Tutor" });
    }

    [HttpGet]
    public async Task<IActionResult> Room(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var session = await _db.ClassSessions
            .Include(item => item.Classroom)
                .ThenInclude(c => c.Subject)
            .FirstOrDefaultAsync(item => item.Id == id
                && (item.Status == SessionStatus.InProgress || item.Status == SessionStatus.Scheduled)
                && item.Classroom.Status == ClassroomStatus.Active,
                cancellationToken);
        if (session is null)
            return NotFound();

        if (!await IsParticipantAsync(session.ClassroomId, userId, cancellationToken))
            return Forbid();

        var isTutor = User.IsInRole("Tutor");
        var started = session.Status == SessionStatus.Scheduled && isTutor;
        if (started)
            session.Status = SessionStatus.InProgress;

        var roomCreated = string.IsNullOrWhiteSpace(session.VideoRoom);
        if (roomCreated)
        {
            session.VideoRoom = $"OTSP-{Guid.NewGuid():N}";
        }

        if (started || roomCreated)
            await _db.SaveChangesAsync(cancellationToken);

        // Ghi nhận điểm danh / vào lại phòng, không bị lỗi 403 khi reload (F5)
        await RecordAttendanceJoinAsync(session.Id, userId, cancellationToken);

        if (started)
            await NotifyParticipantsAsync(session.ClassroomId, session.Id, SessionStatus.InProgress, cancellationToken);

        var displayName = await _db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? (isTutor ? "Gia sư" : "Học viên");

        return View(new ClassSessionRoomVm
        {
            SessionId = session.Id,
            ClassroomName = session.Classroom.Name,
            SubjectName = session.Classroom.Subject.Name,
            ScheduledAtUtc = session.ScheduledAt,
            RoomName = session.VideoRoom ?? string.Empty,
            DisplayName = displayName
        });
    }

    private async Task RecordAttendanceJoinAsync(int sessionId, string userId, CancellationToken cancellationToken)
    {
        var attendance = await _db.ClassSessionAttendances
            .FirstOrDefaultAsync(a => a.ClassSessionId == sessionId && a.UserId == userId, cancellationToken);

        if (attendance is null)
        {
            _db.ClassSessionAttendances.Add(new ClassSessionAttendance
            {
                ClassSessionId = sessionId,
                UserId = userId,
                JoinedAtUtc = DateTime.UtcNow,
                LeftAtUtc = null,
                JoinCount = 1
            });
        }
        else
        {
            attendance.LeftAtUtc = null;
            attendance.JoinCount++;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private Task<bool> IsParticipantAsync(int classroomId, string userId, CancellationToken cancellationToken)
    {
        return _db.Classrooms.AsNoTracking().AnyAsync(classroom =>
            classroom.Id == classroomId
            && classroom.Status == ClassroomStatus.Active
            && (classroom.Tutor.UserId == userId
                || classroom.ClassroomStudents.Any(student => student.Student.UserId == userId)),
            cancellationToken);
    }

    private async Task NotifyParticipantsAsync(
        int classroomId,
        int sessionId,
        SessionStatus status,
        CancellationToken cancellationToken)
    {
        var userIds = await _db.Classrooms.AsNoTracking()
            .Where(classroom => classroom.Id == classroomId)
            .Select(classroom => classroom.Tutor.UserId)
            .ToListAsync(cancellationToken);
        var studentUserIds = await _db.ClassroomStudents.AsNoTracking()
            .Where(student => student.ClassroomId == classroomId)
            .Select(student => student.Student.UserId)
            .ToListAsync(cancellationToken);
        userIds.AddRange(studentUserIds);

        await _hubContext.Clients.Users(userIds.Distinct())
            .SendAsync("ClassSessionStatusChanged", new { classSessionId = sessionId, status = status.ToString() }, cancellationToken);
    }
}
