using FluentAssertions;
using PROJECT_OTSP.Models;
using PROJECT_OTSP.Services;
using PROJECT_OTSP.Tests.Helpers;
using Xunit;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Tests.UnitTests;

/// <summary>
/// Unit Tests cho IAvailabilitySlotService
/// Kiểm tra logic tính toán lịch rảnh của gia sư và loại trừ các ca học bị xung đột thời gian.
/// </summary>
public class AvailabilitySlotServiceTests
{
    [Fact]
    public async Task GetAvailableSlotsAsync_WhenTutorHasNoRegisteredAvailability_ShouldReturnEmptyList()
    {
        // 1. ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var service = new AvailabilitySlotService(db);

        // 2. ACT
        var slots = await service.GetAvailableSlotsAsync(tutorId: 999, daysAhead: 7);

        // 3. ASSERT
        slots.Should().NotBeNull();
        slots.Should().BeEmpty("Gia sư chưa đăng ký khung giờ rảnh nào thì không có slot mở");
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_WhenTutorHasAvailability_ShouldGenerateSlotsWithinWindow()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var tutor = new TutorProfile
        {
            Id = 5,
            UserId = "tutor_5_user",
            HourlyFee = 200000m,
            User = new ApplicationUser { Id = "tutor_5_user", UserName = "tutor5@test.com", FullName = "Thầy Hùng" }
        };

        // Đăng ký lịch rảnh vào tất cả các ngày trong tuần từ 18:00 đến 20:00
        for (int day = 0; day <= 6; day++)
        {
            db.TutorAvailabilities.Add(new TutorAvailability
            {
                TutorId = 5,
                DayOfWeek = (DayOfWeek)day,
                StartTime = new TimeOnly(18, 0),
                EndTime = new TimeOnly(20, 0)
            });
        }

        db.TutorProfiles.Add(tutor);
        await db.SaveChangesAsync();

        var service = new AvailabilitySlotService(db);

        // ACT
        var slots = await service.GetAvailableSlotsAsync(tutorId: 5, daysAhead: 3);

        // ASSERT
        slots.Should().NotBeNull();
        slots.Should().NotBeEmpty();
        slots.All(s => !string.IsNullOrEmpty(s.DateLabel) && !string.IsNullOrEmpty(s.TimeRange)).Should().BeTrue();
    }
}
