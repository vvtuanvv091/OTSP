using System.Security.Claims;
using System.Text.Json;
using Backend_Project_OTSP.Controllers.Api.Student;
using Backend_Project_OTSP.Controllers.Api.Tutor;
using Backend_Project_OTSP.Controllers.Common;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Common.Notification;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Dtos.Tutor;
using Backend_Project_OTSP.Hubs;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Admin;
using BACKEND_PROJECT_OTSP.Services.Admin;
using Backend_Project_OTSP.Services.Chat;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.Notifications;
using Backend_Project_OTSP.Services.Payments;
using Backend_Project_OTSP.Services.Sessions;
using Backend_Project_OTSP.Services.StudentParent;
using Backend_Project_OTSP.Services.Tutor;
using Backend_Project_OTSP.tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
        var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)startDate.DayOfWeek + 7) % 7;
        var makeupDateVn = startDate.AddDays(daysUntilSunday);
        var makeupSession = new ClassSession
        {
            Id = 402,
            ClassroomId = 10,
            ScheduledAt = ClassroomLifecycleHelper.VietnamDateTimeToUtc(makeupDateVn, new TimeOnly(9, 0)),
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
        services.AddDataProtection();
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

    private sealed class RecordingRetryingStrategyFactory : Microsoft.EntityFrameworkCore.Storage.IExecutionStrategyFactory
    {
        public int ExecuteCallCount { get; private set; }
        private readonly Microsoft.EntityFrameworkCore.Storage.ExecutionStrategyDependencies _deps;

        public RecordingRetryingStrategyFactory(Microsoft.EntityFrameworkCore.Storage.ExecutionStrategyDependencies deps) => _deps = deps;

        public Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy Create()
            => new RecordingRetryingStrategy(_deps, () => ExecuteCallCount++);

        private sealed class RecordingRetryingStrategy : Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy
        {
            private readonly Action _onExecute;

            public RecordingRetryingStrategy(Microsoft.EntityFrameworkCore.Storage.ExecutionStrategyDependencies deps, Action onExecute)
                : base(deps, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(10))
                => _onExecute = onExecute;

            protected override bool ShouldRetryOn(Exception exception) => false;

            public override async Task<TResult> ExecuteAsync<TState, TResult>(
                TState state,
                Func<DbContext, TState, CancellationToken, Task<TResult>> operation,
                Func<DbContext, TState, CancellationToken, Task<Microsoft.EntityFrameworkCore.Storage.ExecutionResult<TResult>>>? verifySucceeded,
                CancellationToken cancellationToken = default)
            {
                _onExecute();
                return await operation(Dependencies.CurrentContext.Context, state, cancellationToken);
            }
        }
    }

    [Fact]
    public async Task ChangeRoleAsync_CleanStudentToAdmin_UsesExecutionStrategy_AndCompletesRoleSwitch()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddMemoryCache();
        services.AddDbContext<ApplicationDbContext>(opt => opt
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ReplaceService<Microsoft.EntityFrameworkCore.Storage.IExecutionStrategyFactory, RecordingRetryingStrategyFactory>());
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        await using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        await roleManager.CreateAsync(new ApplicationRole { Name = "Admin" });
        await roleManager.CreateAsync(new ApplicationRole { Name = "Student_Parent" });

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = new ApplicationUser { Id = "adm-real", UserName = "adm@otsp.vn", Email = "adm@otsp.vn", FullName = "Admin", Status = AccountStatus.Active };
        var student = new ApplicationUser { Id = "stu-real", UserName = "tongngoctuan09@gmail.com", Email = "tongngoctuan09@gmail.com", FullName = "Tống Ngọc Tuấn", Status = AccountStatus.Active };
        Assert.True((await userManager.CreateAsync(admin, "Pass123!")).Succeeded);
        Assert.True((await userManager.CreateAsync(student, "Pass123!")).Succeeded);
        await userManager.AddToRoleAsync(admin, "Admin");
        await userManager.AddToRoleAsync(student, "Student_Parent");

        var oldStamp = student.SecurityStamp;
        var profile = new StudentProfile { UserId = student.Id, Area = "HCM" };
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        db.Guardians.Add(new Guardian { StudentProfileId = profile.Id, FullName = "Phụ huynh", ContactInfo = "0900000000" });
        db.AiStudySessions.Add(new AiStudySession { StudentId = profile.Id, Question = "Q", Suggestion = "Ok" });
        await db.SaveChangesAsync();

        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var adminService = new AdminOperationsService(db, userManager, roleManager, cache: cache);

        var res = await adminService.ChangeRoleAsync(student.Id, "Admin", admin.Id, "203.0.113.5", CancellationToken.None);

        Assert.True(res.Success, res.Error);
        var resolvedFactory = db.GetService<Microsoft.EntityFrameworkCore.Storage.IExecutionStrategyFactory>() as RecordingRetryingStrategyFactory;
        Assert.NotNull(resolvedFactory);
        Assert.True(resolvedFactory!.ExecuteCallCount >= 1);
        Assert.Equal("Admin", Assert.Single(await userManager.GetRolesAsync(student)));
        Assert.NotEqual(oldStamp, student.SecurityStamp);
        Assert.Empty(db.StudentProfiles.Where(x => x.UserId == student.Id));
        Assert.Empty(db.Guardians.Where(x => x.StudentProfileId == profile.Id));
        Assert.Empty(db.AiStudySessions.Where(x => x.StudentId == profile.Id));
        Assert.Single(db.AdminProfiles.Where(x => x.UserId == student.Id));
        Assert.Contains(db.AuditLogs, l => l.Action == $"AdminChangeRole:{student.Id}:Student_Parent->Admin");
    }

    [Fact]
    public async Task T1Fix_CancelClassroom_TransitionsPendingPaymentsToFailed_AndPaymentOverviewExcludesCancelledClassroom()
    {
        var db = TestDbContextFactory.Create();
        var admin = new ApplicationUser { Id = "adm-1", FullName = "Admin", Status = AccountStatus.Active };
        var tutorUser = new ApplicationUser { Id = "tut-1", FullName = "Gia sư 1", Email = "tut1@otsp.vn", Status = AccountStatus.Active };
        var studentUser = new ApplicationUser { Id = "stu-1", FullName = "Học viên 1", Email = "stu1@otsp.vn", Status = AccountStatus.Active };
        var subject = new Subject { Id = 1, Code = "CN", Name = "Công nghệ" };
        var grade = new GradeLevel { Id = 1, Name = "Mầm non" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser, VerificationStatus = VerificationStatus.Approved };
        var student = new StudentProfile { Id = 1, UserId = studentUser.Id, User = studentUser };
        var classroom = new Classroom
        {
            Id = 101,
            NameClass = "Lớp 1-1: Công nghệ",
            TutorId = 1,
            Tutor = tutor,
            SubjectId = 1,
            Subject = subject,
            GradeLevelId = 1,
            GradeLevel = grade,
            Status = ClassroomStatus.Active,
            Fee = 120000m,
            Type = ClassroomType.OneToOne
        };
        db.Users.AddRange(admin, tutorUser, studentUser);
        db.Subjects.Add(subject);
        db.GradeLevels.Add(grade);
        db.TutorProfiles.Add(tutor);
        db.StudentProfiles.Add(student);
        db.Classrooms.Add(classroom);
        db.ClassroomStudents.Add(new ClassroomStudent { ClassroomId = classroom.Id, StudentId = student.Id });
        db.Payments.Add(new Payment
        {
            Id = 501,
            ClassroomId = classroom.Id,
            Classroom = classroom,
            StudentId = student.Id,
            Student = student,
            Amount = 120000m,
            Method = "BankTransfer",
            Status = PaymentStatus.Pending,
            GatewayTransactionCode = "OTSP-501"
        });
        await db.SaveChangesAsync();

        var adminClassroomSvc = new AdminClassroomManagementService(db);
        var cancelResult = await adminClassroomSvc.CancelClassroomAsync(classroom.Id, "Đóng lớp theo yêu cầu", admin.Id, "127.0.0.1");

        Assert.True(cancelResult.Success, cancelResult.ErrorMessage);
        Assert.Equal(ClassroomStatus.Cancelled, (await db.Classrooms.FindAsync(classroom.Id))!.Status);
        Assert.Equal(PaymentStatus.Failed, (await db.Payments.FindAsync(501))!.Status);

        var paymentSvc = new PaymentService(
            db,
            BuildConfig(new()),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ILogger<PaymentService>>());
        var overview = await paymentSvc.GetStudentPaymentOverviewAsync(studentUser.Id);

        Assert.Empty(overview.PendingPayments);
    }

    [Fact]
    public async Task T2Fix_UpdateStudentProfile_PersistsValidDateOfBirth_AndRejectsFutureDate()
    {
        var db = TestDbContextFactory.Create();
        var user = new ApplicationUser { Id = "stu-dob", FullName = "Tuấn", Status = AccountStatus.Active };
        var profile = new StudentProfile { Id = 11, UserId = user.Id, User = user };
        db.Users.Add(user);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        var svc = new StudentProfileService(db, Mock.Of<IImageStorageService>(), new MemoryCache(new MemoryCacheOptions()));
        var validDob = new DateOnly(2005, 8, 19);
        var ok = await svc.UpdateProfileAsync(user.Id, new UpdateStudentProfileRequest
        {
            FullName = "Tuấn Tống Ngọc",
            DateOfBirth = validDob
        });

        Assert.True(ok.Success, ok.ErrorMessage);
        Assert.Equal(validDob, (await db.Users.FindAsync(user.Id))!.DateOfBirth);

        var future = await svc.UpdateProfileAsync(user.Id, new UpdateStudentProfileRequest
        {
            DateOfBirth = ClassroomLifecycleHelper.GetVietnamToday().AddDays(2)
        });
        Assert.False(future.Success);
    }

    [Fact]
    public async Task T3Fix_AdminDeletesWithDependencies_RefuseDeletionWithoutMutatingDatabase()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        await using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var studentUser = new ApplicationUser { Id = "stu-dep", UserName = "studep@otsp.vn", Email = "studep@otsp.vn", FullName = "Học viên có lớp", Status = AccountStatus.Active };
        var tutorUser = new ApplicationUser { Id = "tut-dep", UserName = "tutdep@otsp.vn", Email = "tutdep@otsp.vn", FullName = "Gia sư có lớp", Status = AccountStatus.Active };
        Assert.True((await userManager.CreateAsync(studentUser, "Pass123!")).Succeeded);
        Assert.True((await userManager.CreateAsync(tutorUser, "Pass123!")).Succeeded);

        var subject = new Subject { Id = 1, Code = "TOAN", Name = "Toán" };
        var grade = new GradeLevel { Id = 1, Name = "Lớp 10" };
        var tutor = new TutorProfile { Id = 21, UserId = tutorUser.Id, VerificationStatus = VerificationStatus.Approved };
        var student = new StudentProfile { Id = 21, UserId = studentUser.Id };
        db.Subjects.Add(subject);
        db.GradeLevels.Add(grade);
        db.TutorProfiles.Add(tutor);
        db.StudentProfiles.Add(student);
        await db.SaveChangesAsync();

        var classroom = new Classroom
        {
            Id = 201,
            NameClass = "Lớp có lịch sử",
            TutorId = tutor.Id,
            SubjectId = subject.Id,
            GradeLevelId = grade.Id,
            Status = ClassroomStatus.Active,
            Fee = 100000m
        };
        db.Classrooms.Add(classroom);
        db.ClassroomStudents.Add(new ClassroomStudent { ClassroomId = classroom.Id, StudentId = student.Id });
        db.Payments.Add(new Payment { Id = 701, ClassroomId = classroom.Id, StudentId = student.Id, Amount = 100000m, Method = "BankTransfer", Status = PaymentStatus.Pending, GatewayTransactionCode = "TX-701" });
        await db.SaveChangesAsync();

        var dirSvc = new AdminDirectoryService(db, userManager, Mock.Of<IUserPresenceService>());
        var deleteStudent = await dirSvc.DeleteStudentAsync(studentUser.Id, "adm-1", "127.0.0.1");
        Assert.False(deleteStudent.Success);
        Assert.Contains("Khóa tài khoản", deleteStudent.ErrorMessage);
        Assert.Equal(AccountStatus.Active, (await db.Users.FindAsync(studentUser.Id))!.Status);

        var deleteTutor = await dirSvc.DeleteTutorAsync(tutorUser.Id, "adm-1", "127.0.0.1");
        Assert.False(deleteTutor.Success);
        Assert.Contains("Khóa tài khoản", deleteTutor.ErrorMessage);
        Assert.Equal(AccountStatus.Active, (await db.Users.FindAsync(tutorUser.Id))!.Status);
        Assert.Equal(VerificationStatus.Approved, (await db.TutorProfiles.FindAsync(tutor.Id))!.VerificationStatus);

        var clsSvc = new AdminClassroomManagementService(db);
        var deleteClassroom = await clsSvc.DeleteClassroomAsync(classroom.Id, "adm-1", "127.0.0.1");
        Assert.False(deleteClassroom.Success);
        Assert.False(deleteClassroom.NotFound);
        Assert.Contains("Đóng lớp", deleteClassroom.ErrorMessage);
        Assert.Equal(ClassroomStatus.Active, (await db.Classrooms.FindAsync(classroom.Id))!.Status);
        Assert.Equal(PaymentStatus.Pending, (await db.Payments.FindAsync(701))!.Status);
    }

    [Fact]
    public async Task T3Fix_AdminDeletesFreshAccountsAndEmptyClassroom_HardDeletesFromDatabase()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        await using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var freshStudentUser = new ApplicationUser { Id = "stu-new", UserName = "stunew@otsp.vn", Email = "stunew@otsp.vn", FullName = "Học viên mới", Status = AccountStatus.Active };
        var freshTutorUser = new ApplicationUser { Id = "tut-new", UserName = "tutnew@otsp.vn", Email = "tutnew@otsp.vn", FullName = "Gia sư mới", Status = AccountStatus.Active };
        var ownerTutorUser = new ApplicationUser { Id = "tut-owner", UserName = "owner@otsp.vn", Email = "owner@otsp.vn", FullName = "Gia sư giữ lớp", Status = AccountStatus.Active };
        Assert.True((await userManager.CreateAsync(freshStudentUser, "Pass123!")).Succeeded);
        Assert.True((await userManager.CreateAsync(freshTutorUser, "Pass123!")).Succeeded);
        Assert.True((await userManager.CreateAsync(ownerTutorUser, "Pass123!")).Succeeded);

        var subject = new Subject { Id = 2, Code = "LY", Name = "Vật lý" };
        var grade = new GradeLevel { Id = 2, Name = "Lớp 11" };
        var freshStudent = new StudentProfile { Id = 301, UserId = freshStudentUser.Id };
        var freshTutor = new TutorProfile { Id = 301, UserId = freshTutorUser.Id, VerificationStatus = VerificationStatus.Pending };
        var ownerTutor = new TutorProfile { Id = 302, UserId = ownerTutorUser.Id, VerificationStatus = VerificationStatus.Approved };
        db.Subjects.Add(subject);
        db.GradeLevels.Add(grade);
        db.StudentProfiles.Add(freshStudent);
        db.TutorProfiles.AddRange(freshTutor, ownerTutor);
        db.TutorSubjects.Add(new TutorSubject { TutorId = freshTutor.Id, SubjectId = subject.Id, GradeLevelId = grade.Id, HourlyFee = 120000m });
        // Simulate normal login auth logs & system notifications on brand-new accounts
        db.AuditLogs.AddRange(
            new AuditLog { UserId = freshStudentUser.Id, Category = "Auth", Action = "Login" },
            new AuditLog { UserId = freshTutorUser.Id, Category = "Auth", Action = "Register" });
        db.Notifications.AddRange(
            new Notification { UserId = freshStudentUser.Id, Title = "Chào mừng", Content = "Hi" },
            new Notification { UserId = freshTutorUser.Id, Title = "Chào mừng", Content = "Hi" });

        var emptyClassroom = new Classroom
        {
            Id = 401,
            NameClass = "Lớp mới chưa có học viên",
            TutorId = ownerTutor.Id,
            SubjectId = subject.Id,
            GradeLevelId = grade.Id,
            Status = ClassroomStatus.Active,
            Fee = 150000m
        };
        db.Classrooms.Add(emptyClassroom);
        db.ClassroomWeeklySchedules.Add(new ClassroomWeeklySchedule { ClassroomId = emptyClassroom.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(19, 30) });
        await db.SaveChangesAsync();

        var dirSvc = new AdminDirectoryService(db, userManager, Mock.Of<IUserPresenceService>());
        var deleteStudent = await dirSvc.DeleteStudentAsync(freshStudentUser.Id, "adm-1", "127.0.0.1");
        Assert.True(deleteStudent.Success, deleteStudent.ErrorMessage);
        Assert.Null(await db.StudentProfiles.FindAsync(freshStudent.Id));
        Assert.Null(await db.Users.FindAsync(freshStudentUser.Id));

        var deleteTutor = await dirSvc.DeleteTutorAsync(freshTutorUser.Id, "adm-1", "127.0.0.1");
        Assert.True(deleteTutor.Success, deleteTutor.ErrorMessage);
        Assert.Null(await db.TutorProfiles.FindAsync(freshTutor.Id));
        Assert.Null(await db.Users.FindAsync(freshTutorUser.Id));

        var clsSvc = new AdminClassroomManagementService(db);
        var deleteClassroom = await clsSvc.DeleteClassroomAsync(emptyClassroom.Id, "adm-1", "127.0.0.1");
        Assert.True(deleteClassroom.Success, deleteClassroom.ErrorMessage);
        Assert.Null(await db.Classrooms.FindAsync(emptyClassroom.Id));
        Assert.Empty(db.ClassroomWeeklySchedules.Where(w => w.ClassroomId == emptyClassroom.Id));
    }

    [Fact]
    public async Task T6Fix_OneToOneConnectionRequest_RequiresSelectedSlot_EvenWithMessage()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tut-slots", FullName = "Gia sư", Status = AccountStatus.Active };
        var studentUser = new ApplicationUser { Id = "stu-slots", FullName = "Học viên", Status = AccountStatus.Active };
        var subject = new Subject { Id = 1, Code = "ANH", Name = "Tiếng Anh" };
        var grade = new GradeLevel { Id = 1, Name = "Lớp 12" };
        var tutor = new TutorProfile { Id = 31, UserId = tutorUser.Id, User = tutorUser, VerificationStatus = VerificationStatus.Approved };
        var student = new StudentProfile { Id = 31, UserId = studentUser.Id, User = studentUser };
        db.Users.AddRange(tutorUser, studentUser);
        db.Subjects.Add(subject);
        db.GradeLevels.Add(grade);
        db.TutorProfiles.Add(tutor);
        db.StudentProfiles.Add(student);
        db.TutorSubjects.Add(new TutorSubject { TutorId = tutor.Id, SubjectId = subject.Id, GradeLevelId = grade.Id, HourlyFee = 100000m });
        await db.SaveChangesAsync();

        var (hub, _) = CreateHubMock();
        var service = new ConnectionRequestService(
            db,
            Mock.Of<IOpenGroupClassroomService>(),
            new AvailabilitySlotService(db),
            hub.Object,
            Mock.Of<INotificationDispatcher>(),
            Mock.Of<ILogger<ConnectionRequestService>>());

        var trialNoSlot = await service.CreateRequestAsync(new SendConnectionRequestDto
        {
            TutorId = tutor.Id,
            SubjectId = subject.Id,
            GradeLevelId = grade.Id,
            PreferredType = ClassroomType.OneToOne,
            IsTrial = true,
            Message = "Em muốn học thử dù gia sư chưa mở lịch"
        }, studentUser.Id);

        Assert.False(trialNoSlot.Success);

        var officialNoSlot = await service.CreateRequestAsync(new SendConnectionRequestDto
        {
            TutorId = tutor.Id,
            SubjectId = subject.Id,
            GradeLevelId = grade.Id,
            PreferredType = ClassroomType.OneToOne,
            IsTrial = false,
            DurationWeeks = 4,
            Message = "Em muốn học chính thức ngay"
        }, studentUser.Id);

        Assert.False(officialNoSlot.Success);
    }

    [Fact]
    public async Task T7Fix_SearchApiController_GetTutorDetails_AllowsAdminAndAnonymousPreview()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tut-pub", FullName = "Gia sư 32", Email = "gs32@otsp.vn", Status = AccountStatus.Active };
        var tutor = new TutorProfile { Id = 32, UserId = tutorUser.Id, User = tutorUser, VerificationStatus = VerificationStatus.Approved, TeachingFormat = TeachingFormat.Online };
        db.Users.Add(tutorUser);
        db.TutorProfiles.Add(tutor);
        await db.SaveChangesAsync();

        var controller = new SearchApiController(
            Mock.Of<ISearchService>(),
            db,
            Mock.Of<ILogger<SearchApiController>>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreatePrincipal("adm-viewer", "Admin")
            }
        };

        var result = await controller.GetTutorDetails(32, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task T8_NotificationAndEmailGaps_AreDispatchedForPayoutQualDocAndBookingAccepted()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tu-n8", FullName = "Thầy Email", Email = "tutor.email@otsp.vn", Status = AccountStatus.Active };
        var studentUser = new ApplicationUser { Id = "st-n8", FullName = "Học viên Email", Email = "student.email@otsp.vn", Status = AccountStatus.Active };
        var adminUser = new ApplicationUser { Id = "adm-n8", FullName = "Admin Email", Email = "admin.email@otsp.vn", Status = AccountStatus.Active };
        var tutor = new TutorProfile { Id = 88, UserId = tutorUser.Id, User = tutorUser, VerificationStatus = VerificationStatus.Approved };
        var student = new StudentProfile { Id = 88, UserId = studentUser.Id, User = studentUser };
        var subject = new Subject { Id = 88, Code = "TOAN88", Name = "Toán" };
        var grade = new GradeLevel { Id = 88, Name = "Lớp 12" };
        var qual = new Qualification { Id = 88, TutorId = tutor.Id, Tutor = tutor, Title = "Bằng Sư phạm", Status = VerificationStatus.Pending, FileUrl = "https://example.com/q.png" };
        var doc = new LearningDocument
        {
            Id = 88,
            TutorId = tutor.Id,
            Tutor = tutor,
            SubjectId = subject.Id,
            Subject = subject,
            GradeLevelId = grade.Id,
            GradeLevel = grade,
            Title = "Đề cương Toán 12",
            FileUrl = "https://example.com/doc.pdf",
            Scope = DocumentScope.Public,
            Status = VerificationStatus.Pending
        };
        db.AddRange(tutorUser, studentUser, adminUser, tutor, student, subject, grade, qual, doc);
        await db.SaveChangesAsync();

        var enqueued = new List<EmailQueueItemDto>();
        var queueMock = new Mock<IEmailQueue>();
        queueMock.Setup(q => q.Enqueue(It.IsAny<EmailQueueItemDto>())).Callback<EmailQueueItemDto>(enqueued.Add);

        var userMgr = IdentityMockHelper.MockUserManager();
        var roleMgr = IdentityMockHelper.MockRoleManager();
        var opsSvc = new AdminOperationsService(db, userMgr.Object, roleMgr.Object, emailQueue: queueMock.Object);
        var payoutRes = await opsSvc.SettleTutorPayoutAsync(tutor.Id, "2026-11", adminUser.Id, CancellationToken.None);
        Assert.True(payoutRes.Success);

        var approvalSvc = new AdminTutorApprovalService(db, Mock.Of<IAiService>(), emailQueue: queueMock.Object);
        var qualRes = await approvalSvc.ReviewQualificationAsync(qual.Id, adminUser.Id, true, null, null, CancellationToken.None);
        Assert.True(qualRes.Success);

        var docSvc = new AdminLearningDocumentService(db, emailQueue: queueMock.Object);
        var docRes = await docSvc.ReviewAsync(doc.Id, VerificationStatus.Approved, null, adminUser.Id, null, CancellationToken.None);
        Assert.True(docRes.Success);

        var (reqHub, _) = CreateHubMock();
        var chatHub = new Mock<IHubContext<ChatHub>>();
        chatHub.Setup(h => h.Clients).Returns(CreateHubMock().Clients.Object);
        var notifSvc = new NotificationService(db, reqHub.Object, chatHub.Object, Mock.Of<ILogger<NotificationService>>(), emailQueue: queueMock.Object);
        await notifSvc.DispatchBookingAcceptedAsync(new BookingAcceptedNotificationEvent(
            studentUser.Id,
            studentUser.Email,
            studentUser.FullName!,
            tutorUser.FullName!,
            subject.Name,
            999,
            null,
            false));

        Assert.Equal(4, enqueued.Count);
        Assert.Contains(enqueued, e => e.ToEmail == tutorUser.Email && e.Subject.Contains("Quyết toán thu nhập"));
        Assert.Contains(enqueued, e => e.ToEmail == tutorUser.Email && e.Subject.Contains("Bằng cấp/Chứng chỉ"));
        Assert.Contains(enqueued, e => e.ToEmail == tutorUser.Email && e.Subject.Contains("Tài liệu công khai"));
        Assert.Contains(enqueued, e => e.ToEmail == studentUser.Email && e.Subject.Contains("đã chấp nhận"));
        Assert.Contains(await db.Notifications.ToListAsync(), n => n.UserId == studentUser.Id && n.Title == "Yêu cầu học đã được chấp nhận");
    }

    [Fact]
    public async Task T9_ChatService_IncludesGroupClassroomsAndIsolatesDirectPreviews()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tu-c9", FullName = "Gia sư Nhóm", Email = "tuc9@otsp.vn", Status = AccountStatus.Active };
        var student1User = new ApplicationUser { Id = "st1-c9", FullName = "Học viên Một", Email = "st1@otsp.vn", Status = AccountStatus.Active };
        var student2User = new ApplicationUser { Id = "st2-c9", FullName = "Học viên Hai", Email = "st2@otsp.vn", Status = AccountStatus.Active };
        var tutor = new TutorProfile { Id = 91, UserId = tutorUser.Id, User = tutorUser, VerificationStatus = VerificationStatus.Approved };
        var st1 = new StudentProfile { Id = 91, UserId = student1User.Id, User = student1User };
        var st2 = new StudentProfile { Id = 92, UserId = student2User.Id, User = student2User };
        var subject = new Subject { Id = 91, Code = "LY91", Name = "Vật lý" };
        var grade = new GradeLevel { Id = 91, Name = "Lớp 11" };
        var groupClass = new Classroom
        {
            Id = 91,
            NameClass = "Vật lý Nhóm 11A",
            Type = ClassroomType.Group,
            Status = ClassroomStatus.Active,
            MaxStudents = 5,
            TutorId = tutor.Id,
            Tutor = tutor,
            SubjectId = subject.Id,
            Subject = subject,
            GradeLevelId = grade.Id,
            GradeLevel = grade
        };
        db.AddRange(
            tutorUser, student1User, student2User, tutor, st1, st2, subject, grade, groupClass,
            new ClassroomStudent { ClassroomId = groupClass.Id, StudentId = st1.Id },
            new ClassroomStudent { ClassroomId = groupClass.Id, StudentId = st2.Id });
        await db.SaveChangesAsync();

        var chatService = new ChatService(
            db,
            new MemoryCache(new MemoryCacheOptions()),
            BuildConfig(new() { ["Chat:MessageEditWindowMinutes"] = "15" }),
            Mock.Of<IUserPresenceService>());

        var groupMsg = await chatService.SendMessageToUserAsync(student1User.Id, "class_91", "Chào cả lớp ạ");
        Assert.Equal(91, groupMsg.ClassroomId);
        Assert.Equal("class_91", groupMsg.ReceiverId);

        var st2Conversations = await chatService.GetConversationsListAsync(student2User.Id);
        var groupConv = Assert.Single(st2Conversations, c => c.ContactUserId == "class_91");
        Assert.True(groupConv.IsGroupClass);
        Assert.Equal(3, groupConv.MemberCount);
        Assert.Contains("Chào cả lớp ạ", groupConv.LastMessageContent);
        Assert.Equal(1, groupConv.UnreadCount);

        // Tin nhắn trong nhóm lớp KHÔNG được lọt vào hội thoại 1-1 giữa st1 và gia sư
        var tutorConversations = await chatService.GetConversationsListAsync(tutorUser.Id);
        var directToSt1 = Assert.Single(tutorConversations, c => c.ContactUserId == student1User.Id);
        Assert.Equal("Bắt đầu cuộc trò chuyện...", directToSt1.LastMessageContent);
        Assert.Equal(0, directToSt1.UnreadCount);

        // Student <-> Student chat riêng vẫn bị chặn
        Assert.False(await chatService.CanUsersChatAsync(student1User.Id, student2User.Id));

        // Edit & Delete trên tin nhắn nhóm giữ nguyên ClassroomId và class_91
        var edited = await chatService.EditMessageAsync(groupMsg.Id, student1User.Id, "Chào thầy và các bạn");
        Assert.Equal(91, edited.ClassroomId);
        Assert.Equal("class_91", edited.ReceiverId);

        var deleted = await chatService.DeleteMessageAsync(groupMsg.Id, student1User.Id);
        Assert.Equal(91, deleted.ClassroomId);
        Assert.Equal("class_91", deleted.ReceiverId);
    }
}
