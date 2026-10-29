using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Tests.Builders;

/// <summary>
/// Testing Pattern: Test Data Builder cho ClassSession và Classroom
/// </summary>
public class ClassSessionBuilder
{
    private int _sessionId = 1;
    private int _classroomId = 10;
    private int _tutorId = 100;
    private SessionStatus _status = SessionStatus.Scheduled;
    private DateTime _scheduledAt = DateTime.UtcNow.AddHours(2);
    private string? _videoRoom = null;
    private ClassroomStatus _classroomStatus = ClassroomStatus.Active;

    public ClassSessionBuilder WithSessionId(int id)
    {
        _sessionId = id;
        return this;
    }

    public ClassSessionBuilder WithClassroomId(int id)
    {
        _classroomId = id;
        return this;
    }

    public ClassSessionBuilder WithTutorId(int tutorId)
    {
        _tutorId = tutorId;
        return this;
    }

    public ClassSessionBuilder WithStatus(SessionStatus status)
    {
        _status = status;
        return this;
    }

    public ClassSessionBuilder WithScheduledAt(DateTime scheduledAt)
    {
        _scheduledAt = scheduledAt;
        return this;
    }

    public ClassSessionBuilder WithVideoRoom(string? room)
    {
        _videoRoom = room;
        return this;
    }

    public ClassSessionBuilder WithClassroomStatus(ClassroomStatus status)
    {
        _classroomStatus = status;
        return this;
    }

    public ClassSession Build()
    {
        var classroom = new Classroom
        {
            Id = _classroomId,
            TutorId = _tutorId,
            Status = _classroomStatus,
            Name = $"Lớp học thử nghiệm {_classroomId}",
            Fee = 150000m,
            HoursPerSession = 2.0
        };

        return new ClassSession
        {
            Id = _sessionId,
            ClassroomId = _classroomId,
            Classroom = classroom,
            Status = _status,
            ScheduledAt = _scheduledAt,
            VideoRoom = _videoRoom
        };
    }
}
