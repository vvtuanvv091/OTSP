using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Dtos.Tutor;
using Backend_Project_OTSP.Services.Common;

namespace Backend_Project_OTSP.Services.Tutor;

public interface IOpenGroupClassroomService
{
    Task<List<TutorClassroomDto>> GetOpenGroupClassroomsAsync(int tutorId, CancellationToken cancellationToken = default);
    Task<bool> IsOpenGroupClassroomAsync(int tutorId, int classroomId, int subjectId, int gradeLevelId, CancellationToken cancellationToken = default);
}

public sealed class OpenGroupClassroomService : IOpenGroupClassroomService
{
    private readonly ApplicationDbContext _db;

    public OpenGroupClassroomService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<TutorClassroomDto>> GetOpenGroupClassroomsAsync(
        int tutorId,
        CancellationToken cancellationToken = default)
    {
        var rawList = await OpenClassroomQuery(tutorId)
            .OrderBy(classroom => classroom.NameClass)
            .Select(classroom => new
            {
                classroom.Id,
                classroom.NameClass,
                classroom.Description,
                classroom.CoverImageUrl,
                classroom.Fee,
                classroom.Type,
                classroom.SubjectId,
                classroom.GradeLevelId,
                StudentCount = classroom.ClassroomStudents.Count,
                classroom.MaxStudents,
                SubjectName = classroom.Subject.Name,
                GradeLevelName = classroom.GradeLevel.Name,
                classroom.SessionsPerWeek,
                classroom.HoursPerSession,
                classroom.StartDate,
                classroom.RegistrationDeadline,
                classroom.EndDate,
                classroom.TotalSessions,
                WeeklySchedules = classroom.WeeklySchedules
                    .OrderBy(w => w.SessionIndex)
                    .Select(w => new Models.ClassroomWeeklySchedule
                    {
                        SessionIndex = w.SessionIndex,
                        DayOfWeek = w.DayOfWeek,
                        StartTime = w.StartTime,
                        EndTime = w.EndTime
                    }).ToList()
            })
            .ToListAsync(cancellationToken);

        return rawList.Select(classroom =>
        {
            var scheduleItems = classroom.WeeklySchedules.Select(w =>
                $"Buổi {w.SessionIndex}: {ToVnDay(w.DayOfWeek)} ({w.StartTime:HH\\:mm} – {w.EndTime:HH\\:mm})"
            ).ToList();

            var safeHours = ClassroomLifecycleHelper.NormalizeHoursPerSession(classroom.HoursPerSession);
            var expectedTotal = ClassroomLifecycleHelper.CalculateExpectedTotalSessions(
                classroom.StartDate, classroom.EndDate, classroom.TotalSessions, classroom.SessionsPerWeek, classroom.WeeklySchedules);

            var summary = scheduleItems.Count > 0
                ? $"{classroom.SessionsPerWeek} buổi/tuần ({string.Join(", ", classroom.WeeklySchedules.Select(w => $"{ToShortVnDay(w.DayOfWeek)} {w.StartTime:HH\\:mm}"))} · {safeHours:0.#}h/buổi)"
                : $"{classroom.SessionsPerWeek} buổi/tuần ({safeHours:0.#}h/buổi)";

            return new TutorClassroomDto
            {
                Id = classroom.Id,
                Name = classroom.NameClass,
                Description = classroom.Description,
                CoverImageUrl = classroom.CoverImageUrl,
                Fee = classroom.Fee,
                Type = classroom.Type,
                SubjectId = classroom.SubjectId,
                GradeLevelId = classroom.GradeLevelId,
                StudentCount = classroom.StudentCount,
                MaxStudents = classroom.MaxStudents,
                SubjectName = classroom.SubjectName,
                GradeLevelName = classroom.GradeLevelName,
                SessionsPerWeek = classroom.SessionsPerWeek,
                HoursPerSession = safeHours,
                StartDate = classroom.StartDate,
                RegistrationDeadline = classroom.RegistrationDeadline,
                EndDate = classroom.EndDate,
                TotalSessions = expectedTotal,
                ScheduleSummary = summary,
                WeeklyScheduleItems = scheduleItems
            };
        }).ToList();
    }

    private static string ToVnDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Thứ Hai",
        DayOfWeek.Tuesday => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu",
        DayOfWeek.Saturday => "Thứ Bảy",
        DayOfWeek.Sunday => "Chủ Nhật",
        _ => day.ToString()
    };

    private static string ToShortVnDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "T2",
        DayOfWeek.Tuesday => "T3",
        DayOfWeek.Wednesday => "T4",
        DayOfWeek.Thursday => "T5",
        DayOfWeek.Friday => "T6",
        DayOfWeek.Saturday => "T7",
        DayOfWeek.Sunday => "CN",
        _ => day.ToString()
    };

    public Task<bool> IsOpenGroupClassroomAsync(
        int tutorId,
        int classroomId,
        int subjectId,
        int gradeLevelId,
        CancellationToken cancellationToken = default)
    {
        return OpenClassroomQuery(tutorId).AnyAsync(classroom =>
            classroom.Id == classroomId
            && classroom.SubjectId == subjectId
            && classroom.GradeLevelId == gradeLevelId,
            cancellationToken);
    }

    private IQueryable<Backend_Project_OTSP.Models.Classroom> OpenClassroomQuery(int tutorId)
    {
        var nowUtc = DateTime.UtcNow;
        var todayVn = ClassroomLifecycleHelper.GetVietnamToday(nowUtc);
        return _db.Classrooms
            .AsNoTracking()
            .Where(classroom => classroom.TutorId == tutorId
                && classroom.Type == ClassroomType.Group
                && classroom.Status == ClassroomStatus.Active
                && classroom.ClassroomStudents.Count < classroom.MaxStudents
                && (!classroom.RegistrationDeadline.HasValue || classroom.RegistrationDeadline.Value >= todayVn)
                && (!classroom.StartDate.HasValue
                    || classroom.StartDate.Value > todayVn
                    || (classroom.RegistrationDeadline.HasValue && classroom.RegistrationDeadline.Value == classroom.StartDate.Value && classroom.StartDate.Value >= todayVn))
                && !classroom.ClassSessions.Any(s =>
                    s.Status == SessionStatus.Done
                    || s.Status == SessionStatus.InProgress
                    || (s.Status == SessionStatus.Scheduled && s.ScheduledAt <= nowUtc)));
    }
}
