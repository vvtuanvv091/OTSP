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

    public Task<List<TutorClassroomDto>> GetOpenGroupClassroomsAsync(
        int tutorId,
        CancellationToken cancellationToken = default)
    {
        return OpenClassroomQuery(tutorId)
            .OrderBy(classroom => classroom.Name)
            .Select(classroom => new TutorClassroomDto
            {
                Id = classroom.Id,
                Name = classroom.Name,
                Description = classroom.Description,
                CoverImageUrl = classroom.CoverImageUrl,
                Fee = classroom.Fee,
                Type = classroom.Type,
                StudentCount = classroom.ClassroomStudents.Count,
                MaxStudents = classroom.MaxStudents,
                SubjectName = classroom.Subject.Name,
                GradeLevelName = classroom.GradeLevel.Name
            })
            .ToListAsync(cancellationToken);
    }

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
