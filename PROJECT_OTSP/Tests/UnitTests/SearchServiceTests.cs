using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Models;
using PROJECT_OTSP.Services;
using PROJECT_OTSP.Tests.Builders;
using PROJECT_OTSP.Tests.Helpers;
using Xunit;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Tests.UnitTests;

/// <summary>
/// Unit Tests cho ISearchService áp dụng các Testing Patterns:
/// - AAA Pattern (Arrange - Act - Assert)
/// - Test Data Builder Pattern (TutorProfileBuilder)
/// - Test Double (In-Memory Database & Mock IMemoryCache)
/// </summary>
public class SearchServiceTests
{
    private readonly IMemoryCache _memoryCache;

    public SearchServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public async Task SearchTutorsAsync_WhenMaxFeeFilterApplied_ShouldReturnOnlyTutorsWithinBudget()
    {
        // 1. ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var affordableTutor = new TutorProfileBuilder()
            .WithId(1)
            .WithName("Gia Sư Giá Rẻ")
            .WithFee(150000m)
            .WithVerificationStatus(VerificationStatus.Approved)
            .WithAccountStatus(AccountStatus.Active)
            .Build();

        var expensiveTutor = new TutorProfileBuilder()
            .WithId(2)
            .WithName("Gia Sư Giá Cao")
            .WithFee(400000m)
            .WithVerificationStatus(VerificationStatus.Approved)
            .WithAccountStatus(AccountStatus.Active)
            .Build();

        db.TutorProfiles.AddRange(affordableTutor, expensiveTutor);
        await db.SaveChangesAsync();

        var service = new SearchService(db, _memoryCache);
        var query = new SearchQuery { MaxFee = 200000m };

        // 2. ACT
        var result = await service.SearchTutorsAsync(query);

        // 3. ASSERT (FluentAssertions)
        result.Should().NotBeNull();
        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items.First().HourlyFee.Should().BeLessOrEqualTo(200000m);
        result.Items.First().FullName.Should().Be("Gia Sư Giá Rẻ");
    }

    [Fact]
    public async Task SearchTutorsAsync_WhenTutorIsNotApproved_ShouldNotBeIncludedInResults()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var pendingTutor = new TutorProfileBuilder()
            .WithId(10)
            .WithName("Gia Sư Chờ Duyệt")
            .WithVerificationStatus(VerificationStatus.Pending)
            .WithAccountStatus(AccountStatus.Active)
            .Build();

        var approvedTutor = new TutorProfileBuilder()
            .WithId(11)
            .WithName("Gia Sư Đã Duyệt")
            .WithVerificationStatus(VerificationStatus.Approved)
            .WithAccountStatus(AccountStatus.Active)
            .Build();

        db.TutorProfiles.AddRange(pendingTutor, approvedTutor);
        await db.SaveChangesAsync();

        var service = new SearchService(db, _memoryCache);
        var query = new SearchQuery();

        // ACT
        var result = await service.SearchTutorsAsync(query);

        // ASSERT
        result.Items.Should().NotContain(t => t.Id == 10);
        result.Items.Should().Contain(t => t.Id == 11);
    }

    [Fact]
    public async Task SearchTutorsAsync_WhenUserAccountIsLocked_ShouldBeExcludedFromResults()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var lockedTutor = new TutorProfileBuilder()
            .WithId(20)
            .WithName("Gia Sư Bị Khóa")
            .WithVerificationStatus(VerificationStatus.Approved)
            .WithAccountStatus(AccountStatus.Locked)
            .Build();

        db.TutorProfiles.Add(lockedTutor);
        await db.SaveChangesAsync();

        var service = new SearchService(db, _memoryCache);
        var query = new SearchQuery();

        // ACT
        var result = await service.SearchTutorsAsync(query);

        // ASSERT
        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchTutorsAsync_WhenPaginationRequested_ShouldReturnCorrectPageAndSize()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        for (int i = 1; i <= 5; i++)
        {
            var tutor = new TutorProfileBuilder()
                .WithId(100 + i)
                .WithName($"Gia Sư {i}")
                .WithFee(100000m + i * 10000m)
                .WithVerificationStatus(VerificationStatus.Approved)
                .WithAccountStatus(AccountStatus.Active)
                .Build();
            db.TutorProfiles.Add(tutor);
        }
        await db.SaveChangesAsync();

        var service = new SearchService(db, _memoryCache);
        var query = new SearchQuery { Page = 2, PageSize = 2 };

        // ACT
        var result = await service.SearchTutorsAsync(query);

        // ASSERT
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Total.Should().Be(5);
        result.Items.Count.Should().Be(2);
    }
}

