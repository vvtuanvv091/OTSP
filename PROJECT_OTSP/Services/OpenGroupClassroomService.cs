using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Services;

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

<<<<<<< HEAD
    public Task<List<TutorClassroomDto>> GetOpenGroupClassroomsAsync(
        int tutorId,
        CancellationToken cancellationToken = default)
    {
        return OpenClassroomQuery(tutorId)
            .OrderBy(classroom => classroom.Name)
            .Select(classroom => new TutorClassroomDto
=======
    public async Task<List<TutorClassroomDto>> GetOpenGroupClassroomsAsync(
        int tutorId,
        CancellationToken cancellationToken = default)
    {
        var rawList = await OpenClassroomQuery(tutorId)
            .OrderBy(classroom => classroom.Name)
            .Select(classroom => new
            {
                classroom.Id,
                classroom.Name,
                classroom.Description,
                classroom.CoverImageUrl,
                classroom.Fee,
                classroom.Type,
                StudentCount = classroom.ClassroomStudents.Count,
                classroom.MaxStudents,
                SubjectName = classroom.Subject.Name,
                GradeLevelName = classroom.GradeLevel.Name,
                classroom.SessionsPerWeek,
                classroom.HoursPerSession,
                WeeklySchedules = classroom.WeeklySchedules
                    .OrderBy(w => w.SessionIndex)
                    .Select(w => new { w.SessionIndex, w.DayOfWeek, w.StartTime, w.EndTime })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return rawList.Select(classroom =>
        {
            var scheduleItems = classroom.WeeklySchedules.Select(w =>
                $"Buổi {w.SessionIndex}: {ToVnDay(w.DayOfWeek)} ({w.StartTime:HH\\:mm} – {w.EndTime:HH\\:mm})"
            ).ToList();

            var summary = scheduleItems.Count > 0
                ? $"{classroom.SessionsPerWeek} buổi/tuần ({string.Join(", ", classroom.WeeklySchedules.Select(w => $"{ToShortVnDay(w.DayOfWeek)} {w.StartTime:HH\\:mm}"))} · {classroom.HoursPerSession}h/buổi)"
                : $"{classroom.SessionsPerWeek} buổi/tuần ({classroom.HoursPerSession}h/buổi)";

            return new TutorClassroomDto
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
            {
                Id = classroom.Id,
                Name = classroom.Name,
                Description = classroom.Description,
                CoverImageUrl = classroom.CoverImageUrl,
                Fee = classroom.Fee,
                Type = classroom.Type,
<<<<<<< HEAD
                StudentCount = classroom.ClassroomStudents.Count,
                MaxStudents = classroom.MaxStudents,
                SubjectName = classroom.Subject.Name,
                GradeLevelName = classroom.GradeLevel.Name
            })
            .ToListAsync(cancellationToken);
    }

=======
                StudentCount = classroom.StudentCount,
                MaxStudents = classroom.MaxStudents,
                SubjectName = classroom.SubjectName,
                GradeLevelName = classroom.GradeLevelName,
                SessionsPerWeek = classroom.SessionsPerWeek,
                HoursPerSession = classroom.HoursPerSession,
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

>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
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

    private IQueryable<PROJECT_OTSP.Models.Classroom> OpenClassroomQuery(int tutorId)
    {
        return _db.Classrooms
            .AsNoTracking()
            .Where(classroom => classroom.TutorId == tutorId
                && classroom.Type == ClassroomType.Group
                && classroom.Status == ClassroomStatus.Active
                && classroom.ClassroomStudents.Count < classroom.MaxStudents);
    }
}
