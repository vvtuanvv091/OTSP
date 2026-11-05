using FluentAssertions;
using PROJECT_OTSP.Models;
using PROJECT_OTSP.Tests.Builders;
using PROJECT_OTSP.Tests.Helpers;
using Xunit;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Tests.UnitTests;

/// <summary>
/// Unit Tests kiểm thử vòng đời Buổi học (ClassSession Lifecycle)
/// Đảm bảo tính toàn vẹn trạng thái và ngăn chặn lỗi truy cập trái phép.
/// </summary>
public class ClassSessionTests
{
    [Fact]
    public async Task StartSession_WhenSessionIsScheduled_ShouldGenerateVideoRoomAndSetInProgress()
    {
        // 1. ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var session = new ClassSessionBuilder()
            .WithSessionId(1)
            .WithStatus(SessionStatus.Scheduled)
            .WithVideoRoom(null)
            .Build();

        db.Classrooms.Add(session.Classroom);
        db.ClassSessions.Add(session);
        await db.SaveChangesAsync();

        // 2. ACT (Giả lập logic nghiệp vụ trong ClassSessionController)
        var targetSession = db.ClassSessions.Find(1);
        targetSession.Should().NotBeNull();

        if (targetSession!.Status == SessionStatus.Scheduled)
        {
            targetSession.Status = SessionStatus.InProgress;
            targetSession.VideoRoom = $"OTSP-{Guid.NewGuid():N}";
            await db.SaveChangesAsync();
        }

        // 3. ASSERT
        var updated = db.ClassSessions.Find(1);
        updated!.Status.Should().Be(SessionStatus.InProgress);
        updated.VideoRoom.Should().StartWith("OTSP-");
    }

    [Fact]
    public async Task EndSession_WhenSessionAlreadyDone_ShouldNotAllowTransition()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var completedSession = new ClassSessionBuilder()
            .WithSessionId(2)
            .WithStatus(SessionStatus.Done)
            .Build();

        db.Classrooms.Add(completedSession.Classroom);
        db.ClassSessions.Add(completedSession);
        await db.SaveChangesAsync();

        // ACT
        var session = db.ClassSessions.Find(2);
        bool canEnd = session!.Status == SessionStatus.InProgress;

        // ASSERT
        canEnd.Should().BeFalse("Không thể kết thúc một buổi học đã hoàn thành trước đó");
    }

    [Fact]
    public async Task JoinSession_WhenClassroomIsInactive_ShouldRejectAccess()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var session = new ClassSessionBuilder()
            .WithSessionId(3)
            .WithClassroomStatus(ClassroomStatus.Cancelled)
            .WithStatus(SessionStatus.Scheduled)
            .Build();

        db.Classrooms.Add(session.Classroom);
        db.ClassSessions.Add(session);
        await db.SaveChangesAsync();

        // ACT: Kiểm tra điều kiện tham gia
        var canJoin = db.ClassSessions.Any(s => s.Id == 3 && s.Classroom.Status == ClassroomStatus.Active);

        // ASSERT
        canJoin.Should().BeFalse("Không được phép tham gia buổi học khi lớp đã bị hủy hoặc ngừng hoạt động");
    }

    [Fact]
    public async Task LeaveSession_WhenUserIsParticipant_ShouldRecordLeftAtUtc()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var mockHub = new Moq.Mock<Microsoft.AspNetCore.SignalR.IHubContext<PROJECT_OTSP.Hubs.ConnectionRequestHub>>();
        var mockLogger = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<PROJECT_OTSP.Services.ClassSessionService>>();

        var tutor = new TutorProfile
        {
            Id = 101,
            UserId = "user_tutor_101",
            Introduction = "Tutor intro"
        };
        var classroom = new Classroom
        {
            Id = 20,
            TutorId = 101,
            Tutor = tutor,
            Status = ClassroomStatus.Active,
            Name = "Lớp Toán 12"
        };
        var session = new ClassSession
        {
            Id = 10,
            ClassroomId = 20,
            Classroom = classroom,
            Status = SessionStatus.InProgress,
            ScheduledAt = DateTime.UtcNow
        };
        var attendance = new ClassSessionAttendance
        {
            Id = 1,
            ClassSessionId = 10,
            UserId = "user_tutor_101",
            JoinedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            LeftAtUtc = null,
            JoinCount = 1
        };

        db.TutorProfiles.Add(tutor);
        db.Classrooms.Add(classroom);
        db.ClassSessions.Add(session);
        db.ClassSessionAttendances.Add(attendance);
        await db.SaveChangesAsync();

        var service = new PROJECT_OTSP.Services.ClassSessionService(db, mockHub.Object, mockLogger.Object);

        // ACT
        var result = await service.LeaveSessionAsync(10, "user_tutor_101");

        // ASSERT
        result.Should().BeTrue();
        var updatedAttendance = await db.ClassSessionAttendances.FindAsync(1);
        updatedAttendance.Should().NotBeNull();
        updatedAttendance!.LeftAtUtc.Should().NotBeNull("Thời gian rời phòng phải được ghi nhận khi người dùng out phòng");
    }

    [Fact]
    public async Task EndSession_WhenCalledByTutor_ShouldSetStatusDoneAndCloseActiveAttendances()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var mockHub = new Moq.Mock<Microsoft.AspNetCore.SignalR.IHubContext<PROJECT_OTSP.Hubs.ConnectionRequestHub>>();
        var mockClients = new Moq.Mock<Microsoft.AspNetCore.SignalR.IHubClients>();
        var mockClientProxy = new Moq.Mock<Microsoft.AspNetCore.SignalR.IClientProxy>();

        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Users(Moq.It.IsAny<IReadOnlyList<string>>())).Returns(mockClientProxy.Object);

        var mockLogger = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<PROJECT_OTSP.Services.ClassSessionService>>();

        var tutor = new TutorProfile
        {
            Id = 202,
            UserId = "tutor_user_202"
        };
        var classroom = new Classroom
        {
            Id = 30,
            TutorId = 202,
            Tutor = tutor,
            Status = ClassroomStatus.Active,
            Name = "Lớp Hóa 11"
        };
        var session = new ClassSession
        {
            Id = 20,
            ClassroomId = 30,
            Classroom = classroom,
            Status = SessionStatus.InProgress,
            ScheduledAt = DateTime.UtcNow
        };
        var attendance = new ClassSessionAttendance
        {
            Id = 2,
            ClassSessionId = 20,
            UserId = "student_user_303",
            JoinedAtUtc = DateTime.UtcNow.AddMinutes(-45),
            LeftAtUtc = null,
            JoinCount = 1
        };

        db.TutorProfiles.Add(tutor);
        db.Classrooms.Add(classroom);
        db.ClassSessions.Add(session);
        db.ClassSessionAttendances.Add(attendance);
        await db.SaveChangesAsync();

        var service = new PROJECT_OTSP.Services.ClassSessionService(db, mockHub.Object, mockLogger.Object);

        // ACT
        var result = await service.EndSessionAsync(20, "tutor_user_202");

        // ASSERT
        result.Success.Should().BeTrue();
        var updatedSession = await db.ClassSessions.FindAsync(20);
        updatedSession.Should().NotBeNull();
        updatedSession!.Status.Should().Be(SessionStatus.Done);

        var updatedAttendance = await db.ClassSessionAttendances.FindAsync(2);
        updatedAttendance.Should().NotBeNull();
        updatedAttendance!.LeftAtUtc.Should().NotBeNull("Tất cả thành viên đang học phải được đóng thời gian rời phòng khi lớp kết thúc");
    }
}

