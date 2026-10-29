using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Tests.Builders;

/// <summary>
/// Testing Pattern: Test Data Builder cho ConnectionRequest
/// </summary>
public class ConnectionRequestBuilder
{
    private int _id = 1;
    private int _studentId = 1;
    private int _tutorId = 2;
    private string _studentUserId = "student_user_1";
    private string _tutorUserId = "tutor_user_2";
    private RequestStatus _status = RequestStatus.Accepted;

    public ConnectionRequestBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public ConnectionRequestBuilder WithStudent(int studentId, string userId)
    {
        _studentId = studentId;
        _studentUserId = userId;
        return this;
    }

    public ConnectionRequestBuilder WithTutor(int tutorId, string userId)
    {
        _tutorId = tutorId;
        _tutorUserId = userId;
        return this;
    }

    public ConnectionRequestBuilder WithStatus(RequestStatus status)
    {
        _status = status;
        return this;
    }

    public ConnectionRequest Build()
    {
        var studentUser = new ApplicationUser
        {
            Id = _studentUserId,
            UserName = "student@test.com",
            FullName = "Học Viên A"
        };

        var tutorUser = new ApplicationUser
        {
            Id = _tutorUserId,
            UserName = "tutor@test.com",
            FullName = "Gia Sư B"
        };

        var student = new StudentProfile
        {
            Id = _studentId,
            UserId = _studentUserId,
            User = studentUser
        };

        var tutor = new TutorProfile
        {
            Id = _tutorId,
            UserId = _tutorUserId,
            User = tutorUser
        };

        return new ConnectionRequest
        {
            Id = _id,
            StudentId = _studentId,
            Student = student,
            TutorId = _tutorId,
            Tutor = tutor,
            Status = _status,
            CreatedAt = DateTime.UtcNow
        };
    }
}

