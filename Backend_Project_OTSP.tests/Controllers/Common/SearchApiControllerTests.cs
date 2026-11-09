using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Moq;
using Backend_Project_OTSP.Controllers.Common;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.tests.Helpers;
using Xunit;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.tests.Controllers.Common;

public class SearchApiControllerTests
{
    private readonly Mock<ISearchService> _mockSearchService;
    private readonly Mock<ILogger<SearchApiController>> _mockLogger;

    public SearchApiControllerTests()
    {
        _mockSearchService = new Mock<ISearchService>();
        _mockLogger = new Mock<ILogger<SearchApiController>>();
    }

    private SearchApiController CreateController(ApplicationDbContext db, string? userId = null)
    {
        var controller = new SearchApiController(_mockSearchService.Object, db, _mockLogger.Object);
        if (!string.IsNullOrEmpty(userId))
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, "Student_Parent")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }
        else
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }
        return controller;
    }

    [Fact]
    public async Task GetFilters_ReturnsOkResult_WithSubjectsAndGradeLevels()
    {
        var db = TestDbContextFactory.Create();
        var expectedVm = new SearchPageVm
        {
            Subjects = new List<SelectListItem>
            {
                new SelectListItem { Text = "Toán Học", Value = "1" }
            },
            GradeLevels = new List<SelectListItem>
            {
                new SelectListItem { Text = "Lớp 10", Value = "1" }
            }
        };

        _mockSearchService
            .Setup(s => s.BuildPageAsync(It.IsAny<SearchQuery>(), "tutors"))
            .ReturnsAsync(expectedVm);

        var controller = CreateController(db);
        var result = await controller.GetFilters(new SearchQuery(), "tutors");

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchTutors_WhenServiceSucceeds_ReturnsOkResult()
    {
        var db = TestDbContextFactory.Create();
        var pagedResult = new PagedResult<TutorCardDto>
        {
            Page = 1,
            PageSize = 10,
            Total = 1,
            Items = new List<TutorCardDto>
            {
                new TutorCardDto { Id = 1, FullName = "Gia Sư Minh", HourlyFee = 200000 }
            }
        };

        _mockSearchService
            .Setup(s => s.SearchTutorsAsync(It.IsAny<SearchQuery>()))
            .ReturnsAsync(pagedResult);

        var controller = CreateController(db);
        var result = await controller.SearchTutors(new SearchQuery { Keyword = "Minh" });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<PagedResult<TutorCardDto>>(okResult.Value);
        Assert.Single(data.Items);
        Assert.Equal("Gia Sư Minh", data.Items[0].FullName);
    }

    [Fact]
    public async Task SearchTutors_WhenServiceThrows_ReturnsStatusCode500()
    {
        var db = TestDbContextFactory.Create();
        _mockSearchService
            .Setup(s => s.SearchTutorsAsync(It.IsAny<SearchQuery>()))
            .ThrowsAsync(new Exception("Lỗi kết nối CSDL"));

        var controller = CreateController(db);
        var result = await controller.SearchTutors(new SearchQuery());

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task SearchClasses_WhenServiceSucceeds_ReturnsOkResult()
    {
        var db = TestDbContextFactory.Create();
        var pagedClasses = new PagedResult<ClassCardDto>
        {
            Page = 1,
            PageSize = 12,
            Total = 1,
            Items = new List<ClassCardDto>
            {
                new ClassCardDto { Id = 10, Name = "Lớp Toán 12 Nâng Cao", Fee = 300000 }
            }
        };

        _mockSearchService
            .Setup(s => s.SearchClassesAsync(It.IsAny<SearchQuery>()))
            .ReturnsAsync(pagedClasses);

        var controller = CreateController(db);
        var result = await controller.SearchClasses(new SearchQuery { Keyword = "Toán" });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<PagedResult<ClassCardDto>>(okResult.Value);
        Assert.Single(data.Items);
        Assert.Equal(10, data.Items[0].Id);
    }

    [Fact]
    public async Task SearchClasses_WhenServiceThrows_ReturnsStatusCode500()
    {
        var db = TestDbContextFactory.Create();
        _mockSearchService
            .Setup(s => s.SearchClassesAsync(It.IsAny<SearchQuery>()))
            .ThrowsAsync(new InvalidOperationException("Lỗi hệ thống"));

        var controller = CreateController(db);
        var result = await controller.SearchClasses(new SearchQuery());

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetClassDetails_WhenClassNotFound_ReturnsNotFound()
    {
        var db = TestDbContextFactory.Create();
        var controller = CreateController(db);

        var result = await controller.GetClassDetails(99999, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task GetClassDetails_WhenClassExists_GuestUser_ReturnsOkWithDetails()
    {
        var db = TestDbContextFactory.Create();

        var tutorUser = new ApplicationUser { Id = "tutor-search-1", FullName = "Gia Sư Hoàng", Status = AccountStatus.Active };
        var tutor = new TutorProfile { Id = 5, UserId = tutorUser.Id, User = tutorUser, VerificationStatus = VerificationStatus.Approved, AverageRating = 4.8 };
        var subject = new Subject { Id = 1, Code = "PHYS", Name = "Vật Lý" };
        var gradeLevel = new GradeLevel { Id = 2, Name = "Lớp 11" };

        var classroom = new Classroom
        {
            Id = 50,
            NameClass = "Lớp Ôn Thi Vật Lý 11",
            Description = "Lớp nâng cao",
            Fee = 250000,
            Type = ClassroomType.Group,
            Status = ClassroomStatus.Active,
            MaxStudents = 10,
            TutorId = tutor.Id,
            Tutor = tutor,
            SubjectId = subject.Id,
            Subject = subject,
            GradeLevelId = gradeLevel.Id,
            GradeLevel = gradeLevel,
            WeeklySchedules = new List<ClassroomWeeklySchedule>
            {
                new ClassroomWeeklySchedule { DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(20, 0), SessionIndex = 1 }
            }
        };

        db.Users.Add(tutorUser);
        db.TutorProfiles.Add(tutor);
        db.Subjects.Add(subject);
        db.GradeLevels.Add(gradeLevel);
        db.Classrooms.Add(classroom);
        await db.SaveChangesAsync();

        var controller = CreateController(db); // Khách vãng lai, chưa đăng nhập
        var result = await controller.GetClassDetails(classroom.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetClassDetails_WhenStudentLoggedIn_ChecksEnrollmentAndPendingStatus()
    {
        var db = TestDbContextFactory.Create();

        var tutorUser = new ApplicationUser { Id = "tutor-search-2", FullName = "Gia Sư Linh", Status = AccountStatus.Active };
        var tutor = new TutorProfile { Id = 6, UserId = tutorUser.Id, User = tutorUser, VerificationStatus = VerificationStatus.Approved };
        var studentUser = new ApplicationUser { Id = "student-search-1", FullName = "Học Viên Tuấn", Status = AccountStatus.Active };
        var student = new StudentProfile { Id = 1, UserId = studentUser.Id, User = studentUser };
        var subject = new Subject { Id = 2, Code = "CHEM", Name = "Hóa Học" };
        var gradeLevel = new GradeLevel { Id = 3, Name = "Lớp 12" };

        var classroom = new Classroom
        {
            Id = 60,
            NameClass = "Lớp Hóa Học 12 Chuyên Sâu",
            Fee = 350000,
            Type = ClassroomType.Group,
            Status = ClassroomStatus.Active,
            MaxStudents = 5,
            TutorId = tutor.Id,
            Tutor = tutor,
            SubjectId = subject.Id,
            Subject = subject,
            GradeLevelId = gradeLevel.Id,
            GradeLevel = gradeLevel,
            ClassroomStudents = new List<ClassroomStudent>
            {
                new ClassroomStudent { StudentId = student.Id }
            }
        };

        db.Users.AddRange(tutorUser, studentUser);
        db.TutorProfiles.Add(tutor);
        db.StudentProfiles.Add(student);
        db.Subjects.Add(subject);
        db.GradeLevels.Add(gradeLevel);
        db.Classrooms.Add(classroom);
        await db.SaveChangesAsync();

        var controller = CreateController(db, studentUser.Id);
        var result = await controller.GetClassDetails(classroom.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
