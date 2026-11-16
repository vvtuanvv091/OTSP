using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.Tutor;
using Backend_Project_OTSP.tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.tests.Services;

public class LifecycleAndNotificationTests
{
    private static IConfiguration BuildEmptyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    [Fact]
    public async Task EmailSender_ReturnsFalse_WhenConfigIsEmpty()
    {
        var sender = new EmailSenderService(BuildEmptyConfig(), Mock.Of<ILogger<EmailSenderService>>());

        var sent = await sender.SendNotificationEmailAsync("student@otsp.vn", "Test", "<p>Hi</p>");

        Assert.False(sent);
    }

    [Fact]
    public void ClassroomLifecycleHelper_CalculatesEndDateCorrectly()
    {
        var start = new DateOnly(2027, 1, 5); // Thứ 3
        var schedules = new[]
        {
            new ClassroomWeeklySchedule { DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(20, 0) },
            new ClassroomWeeklySchedule { DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(20, 0) }
        };

        var endDate = ClassroomLifecycleHelper.CalculateEndDateFromTotalSessions(start, 4, schedules);

        Assert.Equal(new DateOnly(2027, 1, 14), endDate);
    }

    [Fact]
    public void ClassroomLifecycleHelper_BlocksGroupJoinAfterDeadline()
    {
        var todayVn = new DateOnly(2026, 11, 16);
        var openGroup = new Classroom
        {
            Type = ClassroomType.Group,
            Status = ClassroomStatus.Active,
            RegistrationDeadline = todayVn.AddDays(1),
            StartDate = todayVn.AddDays(2)
        };
        var expiredGroup = new Classroom
        {
            Type = ClassroomType.Group,
            Status = ClassroomStatus.Active,
            RegistrationDeadline = todayVn.AddDays(-1),
            StartDate = todayVn.AddDays(2)
        };

        Assert.True(ClassroomLifecycleHelper.IsGroupEnrollmentOpen(openGroup, todayVn, hasStartedOrCompletedSession: false));
        Assert.False(ClassroomLifecycleHelper.IsGroupEnrollmentOpen(expiredGroup, todayVn, hasStartedOrCompletedSession: false));
    }

    [Fact]
    public async Task AvailabilitySlotService_SplitsLongBlockIntoTwoHourSlots()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tu-av", FullName = "Cô Lan" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var nextDay = ClassroomLifecycleHelper.GetVietnamToday().AddDays(1);
        db.Users.Add(tutorUser);
        db.TutorProfiles.Add(tutor);
        db.TutorAvailabilities.Add(new TutorAvailability
        {
            Id = 1,
            TutorId = 1,
            DayOfWeek = nextDay.DayOfWeek,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0)
        });
        await db.SaveChangesAsync();

        var service = new AvailabilitySlotService(db);
        var slots = await service.GetAvailableSlotsAsync(1, 3);
        var daySlots = slots.Where(s => DateOnly.FromDateTime(ClassroomLifecycleHelper.ToVietnamTime(s.SlotUtc)) == nextDay).ToList();

        Assert.Equal(2, daySlots.Count);
        Assert.Equal(new TimeOnly(8, 0), daySlots[0].StartTime);
        Assert.Equal(new TimeOnly(10, 0), daySlots[1].StartTime);
    }
}
