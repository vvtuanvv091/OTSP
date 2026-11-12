using System.Security.Claims;
using System.Text.Json;
using Backend_Project_OTSP.Controllers.Api.Student;
using Backend_Project_OTSP.Controllers.Api.Tutor;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Dtos.Tutor;
using Backend_Project_OTSP.Hubs;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Admin;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.Notifications;
using Backend_Project_OTSP.Services.Sessions;
using Backend_Project_OTSP.Services.Tutor;
using Backend_Project_OTSP.tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.tests.Services;

public class LifecycleAndNotificationTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ClaimsPrincipal CreatePrincipal(string userId, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth"));

    private static (Mock<IHubContext<ConnectionRequestHub>> Hub, Mock<IHubClients> Clients) CreateHubMock()
    {
        var clientProxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.User(It.IsAny<string>())).Returns(clientProxy.Object);
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxy.Object);
        clients.Setup(c => c.All).Returns(clientProxy.Object);
        var hub = new Mock<IHubContext<ConnectionRequestHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        return (hub, clients);
    }

    [Fact]
    public async Task T1_ResolveAdminRecipients_PrefersActiveAdminsAndSkipsSenderFallback()
    {
        var db = TestDbContextFactory.Create();
        var adminRole = new ApplicationRole { Id = "role-admin", Name = "Admin", NormalizedName = "ADMIN" };
        var activeAdmin = new ApplicationUser { Id = "adm-1", FullName = "Admin One", Email = "admin1@otsp.vn", Status = AccountStatus.Active };
        var lockedAdmin = new ApplicationUser { Id = "adm-2", FullName = "Admin Locked", Email = "locked@otsp.vn", Status = AccountStatus.Locked };
        db.Roles.Add(adminRole);
        db.Users.AddRange(activeAdmin, lockedAdmin);
        db.UserRoles.AddRange(
            new IdentityUserRole<string> { RoleId = adminRole.Id, UserId = activeAdmin.Id },
            new IdentityUserRole<string> { RoleId = adminRole.Id, UserId = lockedAdmin.Id });
        await db.SaveChangesAsync();

        var config = BuildConfig(new()
        {
            ["Smtp:From"] = "tongngoctuan09@gmail.com",
            ["AdminNotification:Email"] = "tongngoctuan09@gmail.com",
            ["AdminNotification:Emails:0"] = "support@otsp.vn"
        });

        var recipients = await AdminNotificationHelper.ResolveAdminRecipientEmailsAsync(db, config);

        Assert.Contains("admin1@otsp.vn", recipients);
        Assert.Contains("support@otsp.vn", recipients);
        Assert.DoesNotContain("tongngoctuan09@gmail.com", recipients);
        Assert.DoesNotContain("locked@otsp.vn", recipients);
    }

    [Fact]
    public async Task T1_EmailSender_ReturnsFalse_WhenSmtpUnconfigured()
    {
        var config = BuildConfig(new());
        var sender = new EmailSenderService(config, Mock.Of<ILogger<EmailSenderService>>());

        var ok = await sender.SendNotificationEmailAsync("student@otsp.vn", "Test", "<p>Hi</p>");

        Assert.False(ok);
    }

    [Fact]
    public async Task T2_ClassroomLifecycleHelper_And_AutoComplete_WorkEndToEnd()
    {
        Assert.Equal(2.0, ClassroomLifecycleHelper.NormalizeHoursPerSession(20));
        Assert.Equal(1.5, ClassroomLifecycleHelper.NormalizeHoursPerSession(90));
        Assert.Equal(2.0, ClassroomLifecycleHelper.NormalizeHoursPerSession(2.0));

        var start = new DateOnly(2027, 1, 5); // Tuesday
        var schedules = new[]
        {
            new ClassroomWeeklySchedule { DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(20, 0) },
            new ClassroomWeeklySchedule { DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(20, 0) }
        };
        var endDate = ClassroomLifecycleHelper.CalculateEndDateFromTotalSessions(start, 4, schedules);
        Assert.Equal(new DateOnly(2027, 1, 14), endDate);
        Assert.Equal(4, ClassroomLifecycleHelper.CountScheduledOccurrences(start, endDate!.Value, schedules));

        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "t-u1", FullName = "Gia sư" };
        var subject = new Subject { Id = 1, Code = "TOAN", Name = "Toán" };
        var grade = new GradeLevel { Id = 1, Name = "Lớp 10" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var classroom = new Classroom
        {
            Id = 50,
            NameClass = "Toán A",
            TutorId = 1,
            Tutor = tutor,
            SubjectId = 1,
            Subject = subject,
            GradeLevelId = 1,
            GradeLevel = grade,
            Status = ClassroomStatus.Active,
            TotalSessions = 2,
            StartDate = ClassroomLifecycleHelper.GetVietnamToday().AddDays(-10),
            EndDate = ClassroomLifecycleHelper.GetVietnamToday().AddDays(-1)
        };
        var s1 = new ClassSession { Id = 1, ClassroomId = 50, Classroom = classroom, ScheduledAt = DateTime.UtcNow.AddDays(-5), Status = SessionStatus.Done };
        var s2 = new ClassSession { Id = 2, ClassroomId = 50, Classroom = classroom, ScheduledAt = DateTime.UtcNow.AddDays(-2), Status = SessionStatus.Done };
        db.AddRange(tutorUser, subject, grade, tutor, classroom, s1, s2);
        await db.SaveChangesAsync();

        await ClassSessionService.TryAutoCompleteClassroomsAsync(db, new[] { 50 });
        Assert.Equal(ClassroomStatus.Completed, db.Classrooms.Single(c => c.Id == 50).Status);
    }

    [Fact]
    public async Task T2_TutorIncome_DoesNotInflateMonthsWithoutSessions()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "t-inc", FullName = "Gia sư A" };
        var subject = new Subject { Id = 1, Code = "LY", Name = "Lý" };
        var grade = new GradeLevel { Id = 1, Name = "Lớp 11" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var classroom = new Classroom
        {
            Id = 10,
            NameClass = "Lý 11",
            TutorId = 1,
            Tutor = tutor,
            SubjectId = 1,
            Subject = subject,
            GradeLevelId = 1,
            GradeLevel = grade,
            Fee = 1_600_000m,
            SessionsPerWeek = 2,
            TotalSessions = 8,
            HoursPerSession = 20, // legacy locale bug stored 20 meaning 2h
            Status = ClassroomStatus.Active
        };
        var nextYear = DateTime.UtcNow.AddYears(1);
        var futureSession = new ClassSession
        {
            Id = 101,
            ClassroomId = 10,
            Classroom = classroom,
            ScheduledAt = nextYear,
            Status = SessionStatus.Scheduled
        };
        db.AddRange(tutorUser, subject, grade, tutor, classroom, futureSession);
        await db.SaveChangesAsync();

        var controller = new TutorIncomeApiController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal("t-inc", "Tutor") }
            }
        };

        var currentMonth = ClassroomLifecycleHelper.GetVietnamNow().ToString("MM/yyyy");
        var res = Assert.IsType<OkObjectResult>(await controller.GetIncome(currentMonth, CancellationToken.None));
        var json = System.Text.Json.JsonSerializer.Serialize(res.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        Assert.Equal(0, doc.RootElement.GetProperty("totalSessions").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("upcomingSessions").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("totalTuition").GetDecimal());
    }

    [Fact]
    public async Task T3_GroupDeadlineAndLateJoin_AreEnforcedAcrossDiscoveryBookingAndAccept()
    {
        var db = TestDbContextFactory.Create();
        var todayVn = ClassroomLifecycleHelper.GetVietnamToday();
        var tutorUser = new ApplicationUser { Id = "tu-1", FullName = "Thầy An", Email = "an@otsp.vn", Status = AccountStatus.Active };
        var studentUser = new ApplicationUser { Id = "st-1", FullName = "Bạn Bình", Email = "binh@otsp.vn", Status = AccountStatus.Active };
        var subject = new Subject { Id = 1, Code = "ANH", Name = "Tiếng Anh" };
        var grade = new GradeLevel { Id = 1, Name = "Lớp 12" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser, VerificationStatus = VerificationStatus.Approved };
        var student = new StudentProfile { Id = 1, UserId = studentUser.Id, User = studentUser };
        db.TutorSubjects.Add(new TutorSubject { TutorId = 1, SubjectId = 1, GradeLevelId = 1, HourlyFee = 200000m });

        var openGroup = new Classroom
        {
            Id = 1,
            NameClass = "Nhóm Mở",
            Type = ClassroomType.Group,
            Status = ClassroomStatus.Active,
            TutorId = 1,
            Tutor = tutor,
            SubjectId = 1,
            Subject = subject,
            GradeLevelId = 1,
            GradeLevel = grade,
            MaxStudents = 5,
            StartDate = todayVn.AddDays(3),
            RegistrationDeadline = todayVn.AddDays(2)
        };
        var expiredGroup = new Classroom
        {
            Id = 2,
            NameClass = "Nhóm Hết Hạn",
            Type = ClassroomType.Group,
            Status = ClassroomStatus.Active,
            TutorId = 1,
            Tutor = tutor,
            SubjectId = 1,
            Subject = subject,
            GradeLevelId = 1,
            GradeLevel = grade,
            MaxStudents = 5,
            StartDate = todayVn.AddDays(2),
            RegistrationDeadline = todayVn.AddDays(-1)
        };
        db.AddRange(tutorUser, studentUser, subject, grade, tutor, student, openGroup, expiredGroup);
        await db.SaveChangesAsync();

        var openGroupSvc = new OpenGroupClassroomService(db);
        var openList = await openGroupSvc.GetOpenGroupClassroomsAsync(1);
        Assert.Single(openList);
        Assert.Equal(1, openList[0].Id);

        var (hub, _) = CreateHubMock();
        var service = new ConnectionRequestService(
            db,
            openGroupSvc,
            new AvailabilitySlotService(db),
            hub.Object,
            Mock.Of<INotificationDispatcher>(),
            Mock.Of<ILogger<ConnectionRequestService>>());

        // Booking into expired deadline group fails
        var createExpired = await service.CreateRequestAsync(new SendConnectionRequestDto
        {
            TutorId = 1,
            SubjectId = 1,
            GradeLevelId = 1,
            PreferredType = ClassroomType.Group,
            ClassroomId = 2
        }, "st-1");
        Assert.False(createExpired.Success);

        // Pending request whose deadline later passes cannot be accepted (anti-late-join)
        var pendingExpiredReq = new ConnectionRequest
        {
            Id = 77,
            StudentId = 1,
            TutorId = 1,
            SubjectId = 1,
            GradeLevelId = 1,
            PreferredType = ClassroomType.Group,
            ClassroomId = 2,
            Status = RequestStatus.Pending
        };
        db.ConnectionRequests.Add(pendingExpiredReq);
        await db.SaveChangesAsync();

        var acceptRes = await service.AcceptRequestAsync(77, "tu-1");
        Assert.False(acceptRes.Success);
        Assert.True(acceptRes.InvalidClassroom);
    }

    [Fact]
    public async Task T3_SubmitReview_BlocksUnstartedOrPreJoinSessions()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tu-r", FullName = "Thầy C" };
        var studentUser = new ApplicationUser { Id = "st-r", FullName = "Học viên D" };
        var subject = new Subject { Id = 1, Code = "HOA", Name = "Hóa" };
        var grade = new GradeLevel { Id = 1, Name = "Lớp 11" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var student = new StudentProfile { Id = 1, UserId = studentUser.Id, User = studentUser };
        var classroom = new Classroom
        {
            Id = 5,
            NameClass = "Hóa Group",
            Type = ClassroomType.Group,
            Status = ClassroomStatus.Active,
            TutorId = 1,
            SubjectId = 1,
            GradeLevelId = 1
        };
        var enrollment = new ClassroomStudent
        {
            ClassroomId = 5,
            Classroom = classroom,
            StudentId = 1,
            Student = student,
            JoinedAt = DateTime.UtcNow.AddDays(-2)
        };
        var beforeJoinSession = new ClassSession
        {
            Id = 11,
            ClassroomId = 5,
            Classroom = classroom,
            ScheduledAt = DateTime.UtcNow.AddDays(-5),
            Status = SessionStatus.Done
        };
        var notDoneSession = new ClassSession
        {
            Id = 12,
            ClassroomId = 5,
            Classroom = classroom,
            ScheduledAt = DateTime.UtcNow.AddDays(-1),
            Status = SessionStatus.Scheduled
        };
        db.AddRange(tutorUser, studentUser, subject, grade, tutor, student, classroom, enrollment, beforeJoinSession, notDoneSession);
        await db.SaveChangesAsync();

        var controller = new StudentClassroomApiController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal("st-r", "Student_Parent") }
            }
        };

        var r1 = await controller.SubmitReview(11, new SubmitReviewRequest { Score = 5, Comment = "Test" }, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r1);

        var r2 = await controller.SubmitReview(12, new SubmitReviewRequest { Score = 5, Comment = "Test" }, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r2);
    }

    [Fact]
    public async Task T4_AvailabilitySlotService_SplitsLongBlocks_AndRespectsDateRange()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tu-av", FullName = "Cô Lan" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var nextDay = ClassroomLifecycleHelper.GetVietnamToday().AddDays(1);
        db.TutorProfiles.Add(tutor);
        db.Users.Add(tutorUser);
        db.TutorAvailabilities.Add(new TutorAvailability
        {
            Id = 1,
            TutorId = 1,
            DayOfWeek = nextDay.DayOfWeek,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0) // 4-hour continuous block -> should yield 08-10 and 10-12
        });
        await db.SaveChangesAsync();

        var availService = new AvailabilitySlotService(db);
        var slots = await availService.GetAvailableSlotsAsync(1, 3);
        var daySlots = slots.Where(s => DateOnly.FromDateTime(ClassroomLifecycleHelper.ToVietnamTime(s.SlotUtc)) == nextDay).ToList();

        Assert.Equal(2, daySlots.Count);
        Assert.Equal(new TimeOnly(8, 0), daySlots[0].StartTime);
        Assert.Equal(new TimeOnly(10, 0), daySlots[0].EndTime);
        Assert.Equal(new TimeOnly(10, 0), daySlots[1].StartTime);
        Assert.Equal(new TimeOnly(12, 0), daySlots[1].EndTime);
    }

    [Fact]
    public async Task T4_StaleSessionCleanupWorker_DistinguishesInProgressFromScheduled()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tu-cl", FullName = "Thầy Dũng" };
        var subject = new Subject { Id = 1, Code = "TIN", Name = "Tin học" };
        var grade = new GradeLevel { Id = 1, Name = "Lớp 12" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var classroom = new Classroom
        {
            Id = 8,
            NameClass = "Tin 12",
            TutorId = 1,
            SubjectId = 1,
            GradeLevelId = 1,
            Status = ClassroomStatus.Active,
            TotalSessions = 10
        };
        var staleInProgress = new ClassSession
        {
            Id = 201,
            ClassroomId = 8,
            Classroom = classroom,
            ScheduledAt = DateTime.UtcNow.AddHours(-8),
            Status = SessionStatus.InProgress
        };
        var staleUnattendedScheduled = new ClassSession
        {
            Id = 202,
            ClassroomId = 8,
            Classroom = classroom,
            ScheduledAt = DateTime.UtcNow.AddHours(-8),
            Status = SessionStatus.Scheduled
        };
        db.AddRange(tutorUser, subject, grade, tutor, classroom, staleInProgress, staleUnattendedScheduled);
        await db.SaveChangesAsync();

        var worker = new StaleSessionCleanupWorker(new ServiceCollection().BuildServiceProvider(), Mock.Of<ILogger<StaleSessionCleanupWorker>>());
        await worker.ProcessStaleSessionsAsync(db);

        Assert.Equal(SessionStatus.Done, db.ClassSessions.Single(s => s.Id == 201).Status);
        Assert.Equal(SessionStatus.Cancelled, db.ClassSessions.Single(s => s.Id == 202).Status);
    }

    [Fact]
    public async Task T4_JoinSessionAsync_BlocksTutorFromOpeningFarFutureSession()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tu-jn", FullName = "Thầy Hải" };
        var subject = new Subject { Id = 1, Code = "VAN", Name = "Văn" };
        var grade = new GradeLevel { Id = 1, Name = "Lớp 9" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var classroom = new Classroom
        {
            Id = 9,
            NameClass = "Văn 9",
            TutorId = 1,
            Tutor = tutor,
            SubjectId = 1,
            Subject = subject,
            GradeLevelId = 1,
            GradeLevel = grade,
            Status = ClassroomStatus.Active,
            HoursPerSession = 2.0
        };
        var farFutureSession = new ClassSession
        {
            Id = 301,
            ClassroomId = 9,
            Classroom = classroom,
            ScheduledAt = DateTime.UtcNow.AddDays(30),
            Status = SessionStatus.Scheduled
        };
        db.AddRange(tutorUser, subject, grade, tutor, classroom, farFutureSession);
        await db.SaveChangesAsync();

        var (hub, _) = CreateHubMock();
        var service = new ClassSessionService(db, hub.Object, Mock.Of<ILogger<ClassSessionService>>());
        var res = await service.JoinSessionAsync(301, "tu-jn", isTutor: true);

        Assert.False(res.Success);
        Assert.Equal(SessionStatus.Scheduled, db.ClassSessions.Single(s => s.Id == 301).Status);
    }

    [Fact]
    public async Task T4_TutorClassroomUpdate_PreservesDoneAndMakeup_AndRejectsStudentConflict()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tu-upd", FullName = "Thầy Sơn", Email = "son@test.com" };
        var studentUser = new ApplicationUser { Id = "st-upd", FullName = "Bạn An", Email = "an@test.com" };
        var subject = new Subject { Id = 1, Code = "TOAN", Name = "Toán" };
        var grade = new GradeLevel { Id = 1, Name = "Lớp 10" };
        var tutor = new TutorProfile
        {
            Id = 1,
            UserId = tutorUser.Id,
            User = tutorUser,
            VerificationStatus = VerificationStatus.Approved
        };
        var student = new StudentProfile { Id = 1, UserId = studentUser.Id, User = studentUser };

        var todayVn = ClassroomLifecycleHelper.GetVietnamToday();
        var startDate = todayVn.AddDays(1);
        var endDate = startDate.AddDays(28);

        var classroom = new Classroom
        {
            Id = 10,
            NameClass = "Toán 10",
            TutorId = 1,
            Tutor = tutor,
            SubjectId = 1,
            Subject = subject,
            GradeLevelId = 1,
            GradeLevel = grade,
            Type = ClassroomType.OneToOne,
            Status = ClassroomStatus.Active,
            MaxStudents = 1,
            SessionsPerWeek = 1,
            HoursPerSession = 2.0,
            StartDate = startDate,
            EndDate = endDate,
            TotalSessions = 4,
            WeeklySchedules =
            [
                new ClassroomWeeklySchedule
                {
                    ClassroomId = 10,
                    DayOfWeek = DayOfWeek.Tuesday,
                    StartTime = new TimeOnly(18, 0),
                    EndTime = new TimeOnly(20, 0),
                    SessionIndex = 0
                }
            ]
        };

        // Another active classroom where the same student learns on Thursday 18:00-20:00
        var conflictClassroom = new Classroom
        {
            Id = 11,
            NameClass = "Lý 10",
            TutorId = 1,
            SubjectId = 1,
            GradeLevelId = 1,
            Type = ClassroomType.OneToOne,
            Status = ClassroomStatus.Active,
            MaxStudents = 1,
            SessionsPerWeek = 1,
            HoursPerSession = 2.0,
            StartDate = startDate,
            EndDate = endDate,
            TotalSessions = 4,
            WeeklySchedules =
            [
                new ClassroomWeeklySchedule
                {
                    ClassroomId = 11,
                    DayOfWeek = DayOfWeek.Thursday,
                    StartTime = new TimeOnly(18, 0),
                    EndTime = new TimeOnly(20, 0),
                    SessionIndex = 0
                }
            ]
        };

        var doneSession = new ClassSession
        {
            Id = 401,
            ClassroomId = 10,
            ScheduledAt = DateTime.UtcNow.AddDays(-3),
            DurationHours = 2.0,
            Status = SessionStatus.Done,
            IsMakeup = false
        };
        var makeupSession = new ClassSession
        {
            Id = 402,
            ClassroomId = 10,
            ScheduledAt = DateTime.UtcNow.AddDays(2),
            DurationHours = 2.0,
            Status = SessionStatus.Scheduled,
            IsMakeup = true
        };
        var regularFutureSession = new ClassSession
        {
            Id = 403,
            ClassroomId = 10,
            ScheduledAt = DateTime.UtcNow.AddDays(3),
            DurationHours = 2.0,
            Status = SessionStatus.Scheduled,
            IsMakeup = false
        };

        db.AddRange(
            tutorUser, studentUser, subject, grade, tutor, student, classroom, conflictClassroom,
            new ClassroomStudent { ClassroomId = 10, StudentId = 1, JoinedAt = DateTime.UtcNow.AddDays(-5) },
            new ClassroomStudent { ClassroomId = 11, StudentId = 1, JoinedAt = DateTime.UtcNow.AddDays(-5) },
            doneSession, makeupSession, regularFutureSession);
        await db.SaveChangesAsync();

        var controller = new TutorClassroomApiController(
            db,
            Mock.Of<IImageStorageService>(),
            Mock.Of<ILogger<TutorClassroomApiController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal("tu-upd", "Tutor") }
            }
        };

        // 1. Moving classroom 10 to Thursday 18:00-20:00 collides with student's classroom 11
        var conflictResult = await controller.Update(
            10,
            new UpdateClassroomRequest
            {
                WeeklySchedulesJson = "[{\"dayOfWeek\":4,\"startTime\":\"18:00\",\"endTime\":\"20:00\"}]"
            },
            null,
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(conflictResult);
        Assert.NotNull(db.ClassSessions.SingleOrDefault(s => s.Id == 403));

        // 2. Moving classroom 10 to Saturday 14:00-16:00 succeeds, keeps Done & IsMakeup, replaces future regular sessions without doubling
        var okResult = await controller.Update(
            10,
            new UpdateClassroomRequest
            {
                WeeklySchedulesJson = "[{\"dayOfWeek\":6,\"startTime\":\"14:00\",\"endTime\":\"16:00\"}]"
            },
            null,
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(okResult);

        var remainingSessions = db.ClassSessions.Where(s => s.ClassroomId == 10).ToList();
        Assert.Contains(remainingSessions, s => s.Id == 401 && s.Status == SessionStatus.Done);
        Assert.Contains(remainingSessions, s => s.Id == 402 && s.IsMakeup);
        Assert.DoesNotContain(remainingSessions, s => s.Id == 403);
        var autoFuture = remainingSessions.Where(s => !s.IsMakeup && s.Status == SessionStatus.Scheduled).ToList();
        Assert.NotEmpty(autoFuture);
        Assert.All(autoFuture, s => Assert.Equal(DayOfWeek.Saturday, ClassroomLifecycleHelper.ToVietnamTime(s.ScheduledAt).DayOfWeek));
    }

    [Fact]
    public async Task T4_TutorScheduleCreateSession_SetsIsMakeupAndRejectsOverlap()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tu-mk", FullName = "Cô Thu" };
        var subject = new Subject { Id = 1, Code = "ANH", Name = "Tiếng Anh" };
        var grade = new GradeLevel { Id = 1, Name = "Lớp 11" };
        var tutor = new TutorProfile
        {
            Id = 1,
            UserId = tutorUser.Id,
            User = tutorUser,
            VerificationStatus = VerificationStatus.Approved
        };
        var classroom = new Classroom
        {
            Id = 12,
            NameClass = "Anh 11",
            TutorId = 1,
            Tutor = tutor,
            SubjectId = 1,
            GradeLevelId = 1,
            Status = ClassroomStatus.Active,
            HoursPerSession = 2.0
        };
        var baseSlotUtc = DateTime.UtcNow.AddDays(3).Date.AddHours(11); // 18:00 VN
        var existingSession = new ClassSession
        {
            Id = 501,
            ClassroomId = 12,
            ScheduledAt = baseSlotUtc,
            DurationHours = 2.0,
            Status = SessionStatus.Scheduled,
            IsMakeup = false
        };
        db.AddRange(tutorUser, subject, grade, tutor, classroom, existingSession);
        await db.SaveChangesAsync();

        var (hub, _) = CreateHubMock();
        var controller = new TutorScheduleApiController(
            db,
            hub.Object,
            Mock.Of<ILogger<TutorScheduleApiController>>(),
            Mock.Of<IScheduleBlockService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal("tu-mk", "Tutor") }
            }
        };

        var pastRes = await controller.CreateSession(
            new CreateSessionRequest { ClassroomId = 12, ScheduledAtUtc = DateTime.UtcNow.AddHours(-2) },
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(pastRes);

        var overlapRes = await controller.CreateSession(
            new CreateSessionRequest { ClassroomId = 12, ScheduledAtUtc = baseSlotUtc.AddHours(1), DurationHours = 2.0 },
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(overlapRes);

        var cleanSlotUtc = baseSlotUtc.AddDays(1).AddHours(-8); // next day 10:00 VN
        var okRes = await controller.CreateSession(
            new CreateSessionRequest { ClassroomId = 12, ScheduledAtUtc = cleanSlotUtc, DurationHours = 2.0 },
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(okRes);

        var createdMakeup = db.ClassSessions.Single(s => s.ClassroomId == 12 && s.Id != 501);
        Assert.True(createdMakeup.IsMakeup);
        Assert.Equal(2.0, createdMakeup.DurationHours);
    }

    [Fact]
    public async Task RoleSwitch_BlocksDirtyAccounts_WithOngoingOrHistoricalRecords()
    {
        var db = TestDbContextFactory.Create();
        var adminUser = new ApplicationUser { Id = "adm-rs", Email = "adm@otsp.vn", FullName = "Admin", Status = AccountStatus.Active };
        var dirtyStudentUser = new ApplicationUser { Id = "st-dirty", Email = "sdirty@otsp.vn", FullName = "Dirty Student", Status = AccountStatus.Active };
        var dirtyTutorUser = new ApplicationUser { Id = "tu-dirty", Email = "tdirty@otsp.vn", FullName = "Dirty Tutor", Status = AccountStatus.Active };
        var subject = new Subject { Id = 1, Code = "TOAN", Name = "Toán" };
        var grade = new GradeLevel { Id = 1, Code = "THPT", Name = "THPT" };

        var studentProfile = new StudentProfile { Id = 10, UserId = dirtyStudentUser.Id, User = dirtyStudentUser };
        var tutorProfile = new TutorProfile { Id = 20, UserId = dirtyTutorUser.Id, User = dirtyTutorUser, VerificationStatus = VerificationStatus.Approved };
        var classroom = new Classroom
        {
            Id = 30,
            NameClass = "Toán 12",
            TutorId = 20,
            Tutor = tutorProfile,
            SubjectId = 1,
            GradeLevelId = 1,
            Status = ClassroomStatus.Active
        };
        var member = new ClassroomStudent { ClassroomId = 30, StudentId = 10, Classroom = classroom, Student = studentProfile };

        db.AddRange(adminUser, dirtyStudentUser, dirtyTutorUser, subject, grade, studentProfile, tutorProfile, classroom, member);
        await db.SaveChangesAsync();

        var userRoles = new Dictionary<string, IList<string>>
        {
            [adminUser.Id] = new List<string> { "Admin" },
            [dirtyStudentUser.Id] = new List<string> { "Student_Parent" },
            [dirtyTutorUser.Id] = new List<string> { "Tutor" }
        };

        var userMgr = IdentityMockHelper.MockUserManager();
        userMgr.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => db.Users.FirstOrDefault(u => u.Id == id));
        userMgr.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser u) => userRoles[u.Id]);

        var service = new AdminOperationsService(db, userMgr.Object, IdentityMockHelper.MockRoleManager().Object);

        var studentAttempt = await service.ChangeRoleAsync(dirtyStudentUser.Id, "Admin", adminUser.Id, "127.0.0.1", CancellationToken.None);
        Assert.False(studentAttempt.Success);
        Assert.Contains("lớp học", studentAttempt.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Single(db.StudentProfiles.Where(x => x.UserId == dirtyStudentUser.Id));

        var tutorAttempt = await service.ChangeRoleAsync(dirtyTutorUser.Id, "Admin", adminUser.Id, "127.0.0.1", CancellationToken.None);
        Assert.False(tutorAttempt.Success);
        Assert.Contains("lớp học", tutorAttempt.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Single(db.TutorProfiles.Where(x => x.UserId == dirtyTutorUser.Id));
    }

    [Fact]
    public async Task RoleSwitch_CleanAccounts_DeletesOldProfilesAndChildData_AndRotatesStamp()
    {
        var db = TestDbContextFactory.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("home_index_viewmodel", new object());

        var adminUser = new ApplicationUser { Id = "adm-c", Email = "adm@otsp.vn", FullName = "Admin", Status = AccountStatus.Active };
        var cleanStudentUser = new ApplicationUser { Id = "st-clean", Email = "sclean@otsp.vn", FullName = "Clean Student", SecurityStamp = "old-s-stamp", Status = AccountStatus.Active };
        var cleanTutorUser = new ApplicationUser { Id = "tu-clean", Email = "tclean@otsp.vn", FullName = "Clean Tutor", SecurityStamp = "old-t-stamp", Status = AccountStatus.Active };
        var subject = new Subject { Id = 1, Code = "TOAN", Name = "Toán" };
        var grade = new GradeLevel { Id = 1, Code = "THPT", Name = "THPT" };

        var stProfile = new StudentProfile { Id = 100, UserId = cleanStudentUser.Id, User = cleanStudentUser };
        var guardian = new Guardian { Id = 1, StudentProfileId = 100, StudentProfile = stProfile, FullName = "Phụ huynh A", ContactInfo = "0900000001" };
        var aiSession = new AiStudySession { Id = 1, StudentId = 100, Student = stProfile, Question = "Test", Suggestion = "Ok" };

        var tuProfile = new TutorProfile { Id = 200, UserId = cleanTutorUser.Id, User = cleanTutorUser, VerificationStatus = VerificationStatus.Approved };
        var tuSub = new TutorSubject { Id = 1, TutorId = 200, SubjectId = 1, GradeLevelId = 1 };
        var tuAvail = new TutorAvailability { Id = 1, TutorId = 200, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(10, 0) };
        var tuQual = new Qualification { Id = 1, TutorId = 200, Title = "Bằng ĐH", FileUrl = "https://example.com/cert.pdf", Status = VerificationStatus.Approved };
        var tuVerif = new ProfileVerification { Id = 1, TutorId = 200, Result = VerificationStatus.Approved };

        db.AddRange(adminUser, cleanStudentUser, cleanTutorUser, subject, grade, stProfile, guardian, aiSession, tuProfile, tuSub, tuAvail, tuQual, tuVerif);
        await db.SaveChangesAsync();

        var userRoles = new Dictionary<string, IList<string>>
        {
            [adminUser.Id] = new List<string> { "Admin" },
            [cleanStudentUser.Id] = new List<string> { "Student_Parent" },
            [cleanTutorUser.Id] = new List<string> { "Tutor" }
        };

        var userMgr = IdentityMockHelper.MockUserManager();
        userMgr.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => db.Users.FirstOrDefault(u => u.Id == id));
        userMgr.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser u) => userRoles[u.Id]);
        userMgr.Setup(m => m.RemoveFromRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync((ApplicationUser u, IEnumerable<string> r) =>
            {
                userRoles[u.Id] = userRoles[u.Id].Except(r).ToList();
                return IdentityResult.Success;
            });
        userMgr.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser u, string r) =>
            {
                userRoles[u.Id].Add(r);
                return IdentityResult.Success;
            });
        userMgr.Setup(m => m.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser u) =>
            {
                u.SecurityStamp = Guid.NewGuid().ToString("N");
                return IdentityResult.Success;
            });

        var service = new AdminOperationsService(db, userMgr.Object, IdentityMockHelper.MockRoleManager().Object, cache: cache);

        // 1. Clean Student -> Tutor deletes StudentProfile + Guardians + AiStudySessions and creates Pending TutorProfile
        var stSwitch = await service.ChangeRoleAsync(cleanStudentUser.Id, "Tutor", adminUser.Id, "127.0.0.1", CancellationToken.None);
        Assert.True(stSwitch.Success, stSwitch.Error);
        Assert.False(cache.TryGetValue("home_index_viewmodel", out _));
        Assert.Equal("Tutor", Assert.Single(userRoles[cleanStudentUser.Id]));
        Assert.NotEqual("old-s-stamp", cleanStudentUser.SecurityStamp);
        Assert.Empty(db.StudentProfiles.Where(x => x.UserId == cleanStudentUser.Id));
        Assert.Empty(db.Guardians.Where(x => x.StudentProfileId == 100));
        Assert.Empty(db.AiStudySessions.Where(x => x.StudentId == 100));
        var newTuProfile = Assert.Single(db.TutorProfiles.Where(x => x.UserId == cleanStudentUser.Id));
        Assert.Equal(VerificationStatus.Pending, newTuProfile.VerificationStatus);

        // 2. Clean Tutor -> Admin deletes TutorProfile + TutorSubjects + TutorAvailabilities + Qualifications + ProfileVerifications
        var tuSwitch = await service.ChangeRoleAsync(cleanTutorUser.Id, "Admin", adminUser.Id, "127.0.0.1", CancellationToken.None);
        Assert.True(tuSwitch.Success, tuSwitch.Error);
        Assert.Equal("Admin", Assert.Single(userRoles[cleanTutorUser.Id]));
        Assert.NotEqual("old-t-stamp", cleanTutorUser.SecurityStamp);
        Assert.Empty(db.TutorProfiles.Where(x => x.UserId == cleanTutorUser.Id));
        Assert.Empty(db.TutorSubjects.Where(x => x.TutorId == 200));
        Assert.Empty(db.TutorAvailabilities.Where(x => x.TutorId == 200));
        Assert.Empty(db.Qualifications.Where(x => x.TutorId == 200));
        Assert.Empty(db.ProfileVerifications.Where(x => x.TutorId == 200));
        Assert.Single(db.AdminProfiles.Where(x => x.UserId == cleanTutorUser.Id));
    }

    [Fact]
    public async Task SearchAndBootstrapSeed_EnforceRoleConsistency_AndSeedEmptyDatabaseSafely()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        await using var sp = services.BuildServiceProvider();
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();

            // Production-style bootstrap on wiped empty DB seeds Roles, Subjects, GradeLevels, Admin & default News
            await DbSeeder.SeedBootstrapAsync(scope.ServiceProvider, syncDefaultAdminPasswords: false);

            Assert.True(await db.Roles.AnyAsync(r => r.Name == "Admin"));
            Assert.True(await db.Roles.AnyAsync(r => r.Name == "Tutor"));
            Assert.True(await db.Roles.AnyAsync(r => r.Name == "Student_Parent"));
            Assert.NotEmpty(db.Subjects);
            Assert.NotEmpty(db.GradeLevels);
            Assert.NotEmpty(db.AdminProfiles);
            Assert.NotEmpty(db.NewsArticles);

            // Stale Approved TutorProfile belonging to an Admin user must be excluded from SearchService
            var adminRole = await db.Roles.SingleAsync(r => r.Name == "Admin");
            var tutorRole = await db.Roles.SingleAsync(r => r.Name == "Tutor");
            var realTutorUser = new ApplicationUser { Id = "u-real-t", Email = "real@otsp.vn", UserName = "real@otsp.vn", FullName = "Real Tutor", Status = AccountStatus.Active };
            var staleAdminUser = new ApplicationUser { Id = "u-stale-a", Email = "stale@otsp.vn", UserName = "stale@otsp.vn", FullName = "Stale Admin", Status = AccountStatus.Active };
            db.Users.AddRange(realTutorUser, staleAdminUser);
            db.UserRoles.AddRange(
                new IdentityUserRole<string> { UserId = realTutorUser.Id, RoleId = tutorRole.Id },
                new IdentityUserRole<string> { UserId = staleAdminUser.Id, RoleId = adminRole.Id });
            db.TutorProfiles.AddRange(
                new TutorProfile { Id = 11, UserId = realTutorUser.Id, VerificationStatus = VerificationStatus.Approved, HourlyFee = 150000 },
                new TutorProfile { Id = 12, UserId = staleAdminUser.Id, VerificationStatus = VerificationStatus.Approved, HourlyFee = 150000 });
            await db.SaveChangesAsync();

            var searchSvc = new SearchService(db, new MemoryCache(new MemoryCacheOptions()));
            var result = await searchSvc.SearchTutorsAsync(new Backend_Project_OTSP.Dtos.SearchQuery());
            var tutorCard = Assert.Single(result.Items);
            Assert.Equal("Real Tutor", tutorCard.FullName);
        }
    }
}
