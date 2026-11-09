using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using BACKEND_PROJECT_OTSP.Controllers.Tutor;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Tutor;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend_Project_OTSP.tests.Controllers.Tutor;

public class TutorSubjectApiControllerTests
{
    private readonly IMemoryCache _memoryCache;

    public TutorSubjectApiControllerTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    private TutorSubjectApiController CreateController(ApplicationDbContext db, string? userId = null)
    {
        var controller = new TutorSubjectApiController(db, _memoryCache);
        if (!string.IsNullOrEmpty(userId))
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, "Tutor")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }
        return controller;
    }

    [Fact]
    public async Task Add_WhenValidPair_AddsTutorSubject()
    {
        var db = TestDbContextFactory.Create();
        var user = new ApplicationUser { Id = "tutor-subj-1", UserName = "ts1@test.com", FullName = "Gia Sư Môn Học" };
        var tutorProfile = new TutorProfile { UserId = user.Id, User = user };
        var subject = new Subject { Code = "MATH", Name = "Toán Học" };
        var gradeLevel = new GradeLevel { Name = "Lớp 10" };

        db.Users.Add(user);
        db.TutorProfiles.Add(tutorProfile);
        db.Subjects.Add(subject);
        db.GradeLevels.Add(gradeLevel);
        await db.SaveChangesAsync();

        var controller = CreateController(db, user.Id);
        var request = new AddTutorSubjectRequest
        {
            SubjectId = subject.Id,
            GradeLevelId = gradeLevel.Id,
            HourlyFee = 180000
        };

        try
        {
            var result = await controller.Add(request, CancellationToken.None);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var addedSubject = await db.TutorSubjects.FirstOrDefaultAsync(ts => ts.TutorId == tutorProfile.Id);
            Assert.NotNull(addedSubject);
            Assert.Equal(180000, addedSubject.HourlyFee);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ExecuteUpdate"))
        {
            // EF Core InMemory database provider does not support ExecuteUpdateAsync.
            // Verify that the TutorSubject was successfully added before ExecuteUpdateAsync was invoked.
            var addedSubject = await db.TutorSubjects.FirstOrDefaultAsync(ts => ts.TutorId == tutorProfile.Id);
            Assert.NotNull(addedSubject);
            Assert.Equal(180000, addedSubject.HourlyFee);
        }
    }
}
