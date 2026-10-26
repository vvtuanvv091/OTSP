using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Areas.Tutor.Controllers;

[Area("Tutor")]
[Authorize(Roles = "Tutor")]
public class TutorStudentController : TutorAreaController
{
    private readonly ApplicationDbContext _dbContext;

    public TutorStudentController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var tutor = await _dbContext.TutorProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        var studentsList = new List<TutorStudentItemVm>();

        if (tutor != null)
        {
            var classroomStudents = await _dbContext.ClassroomStudents
                .AsNoTracking()
                .Where(cs => cs.Classroom.TutorId == tutor.Id)
                .Include(cs => cs.Student).ThenInclude(s => s.User)
                .Include(cs => cs.Classroom).ThenInclude(c => c.Subject)
                .Include(cs => cs.Classroom).ThenInclude(c => c.ClassSessions)
                .ToListAsync(cancellationToken);

            var acceptedRequests = await _dbContext.ConnectionRequests
                .AsNoTracking()
                .Where(r => r.TutorId == tutor.Id && r.Status == RequestStatus.Accepted)
                .ToListAsync(cancellationToken);

            foreach (var cs in classroomStudents)
            {
                var classroom = cs.Classroom;
                var totalSessions = classroom.ClassSessions.Count > 0
                    ? classroom.ClassSessions.Count
                    : Math.Max(1, classroom.SessionsPerWeek * 4);
                var doneSessions = classroom.ClassSessions.Count(s => s.Status == SessionStatus.Done);
                var progressText = $"{doneSessions}/{totalSessions} buổi";

                var nextSession = classroom.ClassSessions
                    .Where(s => s.ScheduledAt >= DateTime.UtcNow && s.Status == SessionStatus.Scheduled)
                    .OrderBy(s => s.ScheduledAt)
                    .FirstOrDefault();

                string nextSessionFormatted = "Chưa có lịch";
                if (nextSession != null)
                {
                    var vnTime = nextSession.ScheduledAt.AddHours(7);
                    var dayName = vnTime.DayOfWeek switch
                    {
                        DayOfWeek.Sunday => "Chủ nhật",
                        DayOfWeek.Monday => "Thứ 2",
                        DayOfWeek.Tuesday => "Thứ 3",
                        DayOfWeek.Wednesday => "Thứ 4",
                        DayOfWeek.Thursday => "Thứ 5",
                        DayOfWeek.Friday => "Thứ 6",
                        DayOfWeek.Saturday => "Thứ 7",
                        _ => ""
                    };
                    var endTime = vnTime.AddHours(classroom.HoursPerSession > 0 ? classroom.HoursPerSession : 2.0);
                    nextSessionFormatted = $"{dayName} {vnTime:dd/MM} · {vnTime:HH:mm}-{endTime:HH:mm}";
                }

                var req = acceptedRequests.FirstOrDefault(r => r.StudentId == cs.StudentId);

                studentsList.Add(new TutorStudentItemVm
                {
                    StudentId = cs.StudentId,
                    StudentName = cs.Student?.User?.FullName ?? cs.Student?.User?.UserName ?? "Học viên",
                    StudentAvatarUrl = cs.Student?.User?.AvatarUrl,
                    ClassroomId = cs.ClassroomId,
                    ClassroomName = classroom?.Name ?? (classroom?.Type == ClassroomType.Group ? "Lớp nhóm" : "Lớp 1 kèm 1"),
                    ClassroomType = classroom?.Type ?? ClassroomType.Group,
                    SubjectName = classroom?.Subject?.Name ?? "Môn học",
                    Progress = progressText,
                    NextSessionFormatted = nextSessionFormatted,
                    RequestId = req?.Id
                });
            }

            // Với yêu cầu 1 kèm 1 đã chấp nhận học thử nhưng gia sư chưa tạo Classroom chính thức
            var pendingOneToOneAccepted = acceptedRequests
                .Where(r => r.PreferredType == ClassroomType.OneToOne && !studentsList.Any(s => s.StudentId == r.StudentId))
                .ToList();

            foreach (var req in pendingOneToOneAccepted)
            {
                var student = await _dbContext.StudentProfiles
                    .AsNoTracking()
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.Id == req.StudentId, cancellationToken);
                var subject = await _dbContext.Subjects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == req.SubjectId, cancellationToken);

                if (student != null)
                {
                    string trialSlotText = "Đang xếp lịch";
                    if (req.ProposedTrialSlotUtc.HasValue)
                    {
                        var tVn = req.ProposedTrialSlotUtc.Value.AddHours(7);
                        var dayName = tVn.DayOfWeek switch
                        {
                            DayOfWeek.Sunday => "Chủ nhật",
                            DayOfWeek.Monday => "Thứ 2",
                            DayOfWeek.Tuesday => "Thứ 3",
                            DayOfWeek.Wednesday => "Thứ 4",
                            DayOfWeek.Thursday => "Thứ 5",
                            DayOfWeek.Friday => "Thứ 6",
                            DayOfWeek.Saturday => "Thứ 7",
                            _ => ""
                        };
                        trialSlotText = $"{dayName} {tVn:dd/MM} · {tVn:HH:mm}-{tVn.AddHours(1):HH:mm}";
                    }

                    studentsList.Add(new TutorStudentItemVm
                    {
                        StudentId = student.Id,
                        StudentName = student.User?.FullName ?? student.User?.UserName ?? "Học viên",
                        StudentAvatarUrl = student.User?.AvatarUrl,
                        ClassroomId = null,
                        ClassroomName = "Kèm 1-1 (Chờ tạo lớp)",
                        ClassroomType = ClassroomType.OneToOne,
                        SubjectName = subject?.Name ?? "Môn học",
                        Progress = "Chờ xếp lớp",
                        NextSessionFormatted = trialSlotText,
                        RequestId = req.Id
                    });
                }
            }
        }

        if (studentsList.Count == 0)
        {
            studentsList.Add(new TutorStudentItemVm
            {
                StudentId = 1,
                StudentName = "Nguyễn An",
                ClassroomId = 1,
                ClassroomName = "Lớp Tiếng Anh 10 - Nhóm A",
                ClassroomType = ClassroomType.Group,
                SubjectName = "Tiếng Anh",
                Progress = "2/5 buổi",
                NextSessionFormatted = "Chủ nhật 04/10 · 10:17-11:17",
                RequestId = null
            });
            studentsList.Add(new TutorStudentItemVm
            {
                StudentId = 2,
                StudentName = "Trần Gia Bảo",
                ClassroomId = 2,
                ClassroomName = "Lớp Kèm 1-1 Tiếng Anh THPT",
                ClassroomType = ClassroomType.OneToOne,
                SubjectName = "Tiếng Anh",
                Progress = "1/4 buổi",
                NextSessionFormatted = "Chủ nhật 04/10 · 13:22-14:22",
                RequestId = null
            });
        }

        return RenderIndex(studentsList);
    }
}
