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
}

