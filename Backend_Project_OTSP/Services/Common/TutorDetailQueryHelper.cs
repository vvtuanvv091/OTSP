using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Dtos.Tutor;
using Backend_Project_OTSP.Services.Tutor;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Common;

/// <summary>
/// Truy vấn dùng chung thông tin công khai của gia sư cho cả trang công khai (/api/search/tutors/{id})
/// và luồng đặt lịch học viên (/api/student/connection-requests/tutors/{tutorId}).
/// </summary>
public static class TutorDetailQueryHelper
{
    private static readonly IReadOnlyDictionary<DayOfWeek, string> DayVnNames = new Dictionary<DayOfWeek, string>
    {
        [DayOfWeek.Monday] = "Thứ 2",
        [DayOfWeek.Tuesday] = "Thứ 3",
        [DayOfWeek.Wednesday] = "Thứ 4",
        [DayOfWeek.Thursday] = "Thứ 5",
        [DayOfWeek.Friday] = "Thứ 6",
        [DayOfWeek.Saturday] = "Thứ 7",
        [DayOfWeek.Sunday] = "Chủ nhật"
    };

    public sealed record PublicTutorDetailResult(
        object Tutor,
        object Subjects,
        object Qualifications,
        object Availabilities,
        List<TutorReviewDto> Reviews,
        List<TutorClassroomDto> Classrooms
    );

    public static async Task<PublicTutorDetailResult?> BuildPublicTutorDetailAsync(
        ApplicationDbContext db,
        int tutorId,
        IOpenGroupClassroomService? openGroupClassroomService,
        string? viewerUserId = null,
        bool isAdminViewer = false,
        CancellationToken cancellationToken = default)
    {
        var tutor = await db.TutorProfiles.AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.Qualifications.Where(q => q.Status == VerificationStatus.Approved))
            .Include(t => t.TutorSubjects)
                .ThenInclude(ts => ts.Subject)
            .Include(t => t.TutorSubjects)
                .ThenInclude(ts => ts.GradeLevel)
            .FirstOrDefaultAsync(t => t.Id == tutorId
                && t.User.Status == AccountStatus.Active
                && (isAdminViewer
                    || (!string.IsNullOrEmpty(viewerUserId) && t.UserId == viewerUserId)
                    || t.VerificationStatus == VerificationStatus.Approved),
                cancellationToken);

        if (tutor == null)
            return null;

        var reviews = await db.Reviews.AsNoTracking()
            .Where(r => r.ClassSession.Classroom.TutorId == tutorId)
            .OrderByDescending(r => r.ReviewedAt)
            .Take(20)
            .Select(r => new TutorReviewDto
            {
                Id = r.Id,
                StudentName = r.Student.User.FullName ?? string.Empty,
                StudentAvatarUrl = r.Student.User.AvatarUrl,
                Score = r.Score,
                Comment = r.Comment,
                ReviewedAt = r.ReviewedAt
            })
            .ToListAsync(cancellationToken);

        var classrooms = openGroupClassroomService != null
            ? await openGroupClassroomService.GetOpenGroupClassroomsAsync(tutorId, cancellationToken)
            : [];

        var completedSessions = await db.ClassSessions.AsNoTracking()
            .CountAsync(s => s.Classroom.TutorId == tutorId && s.Status == SessionStatus.Done, cancellationToken);

        var studentCount = await db.ClassroomStudents.AsNoTracking()
            .Where(cs => cs.Classroom.TutorId == tutorId)
            .Select(cs => cs.StudentId)
            .Distinct()
            .CountAsync(cancellationToken);

        var availabilitiesList = await db.TutorAvailabilities.AsNoTracking()
            .Where(a => a.TutorId == tutorId)
            .OrderBy(a => a.DayOfWeek)
            .ThenBy(a => a.StartTime)
            .ToListAsync(cancellationToken);

        var availabilities = availabilitiesList.Select(a => new
        {
            a.Id,
            DayOfWeek = (int)a.DayOfWeek,
            DayOfWeekName = DayVnNames.TryGetValue(a.DayOfWeek, out var name) ? name : a.DayOfWeek.ToString(),
            TimeRange = $"{a.StartTime:HH\\:mm} - {a.EndTime:HH\\:mm}",
            StartTime = $"{a.StartTime:HH\\:mm}",
            EndTime = $"{a.EndTime:HH\\:mm}"
        }).ToList();

        var allTutorScores = await db.Reviews.AsNoTracking()
            .Where(r => r.ClassSession.Classroom.TutorId == tutorId)
            .Select(r => (double)r.Score)
            .ToListAsync(cancellationToken);

        var actualAverageRating = allTutorScores.Count > 0
            ? Math.Round(allTutorScores.Average(), 1)
            : (tutor.AverageRating > 0 ? Math.Round(tutor.AverageRating, 1) : 0);

        return new PublicTutorDetailResult(
            Tutor: new
            {
                tutor.Id,
                UserId = tutor.UserId,
                FullName = tutor.User.FullName,
                AvatarUrl = tutor.User.AvatarUrl,
                Area = tutor.User.Address,
                TeachingFormat = tutor.TeachingFormat.ToString(),
                tutor.HourlyFee,
                tutor.Introduction,
                AverageRating = actualAverageRating,
                TotalReviews = allTutorScores.Count,
                TotalCompletedSessions = completedSessions,
                TotalStudents = studentCount
            },
            Subjects: tutor.TutorSubjects.Select(ts => new
            {
                ts.SubjectId,
                SubjectName = ts.Subject.Name,
                ts.GradeLevelId,
                GradeName = ts.GradeLevel.Name,
                HourlyFee = ts.HourlyFee > 0 ? ts.HourlyFee : tutor.HourlyFee
            }).ToList(),
            Qualifications: tutor.Qualifications.Select(q => new
            {
                q.Id,
                q.Title,
                q.FileUrl
            }).ToList(),
            Availabilities: availabilities,
            Reviews: reviews,
            Classrooms: classrooms
        );
    }
}
