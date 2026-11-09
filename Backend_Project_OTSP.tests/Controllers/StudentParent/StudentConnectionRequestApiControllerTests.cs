using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using BACKEND_PROJECT_OTSP.Controllers.Api.Student;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Dtos.Tutor;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.StudentParent;
using Backend_Project_OTSP.Services.Tutor;
using Backend_Project_OTSP.tests.Helpers;
using Xunit;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.tests.Controllers.StudentParent;

public class StudentConnectionRequestApiControllerTests
{
    private readonly Mock<IConnectionRequestService> _mockConnectionRequestService;
    private readonly Mock<IOpenGroupClassroomService> _mockOpenGroupClassroomService;
    private readonly Mock<IAvailabilitySlotService> _mockAvailabilitySlotService;

    public StudentConnectionRequestApiControllerTests()
    {
        _mockConnectionRequestService = new Mock<IConnectionRequestService>();
        _mockOpenGroupClassroomService = new Mock<IOpenGroupClassroomService>();
        _mockAvailabilitySlotService = new Mock<IAvailabilitySlotService>();
    }

    private StudentConnectionRequestApiController CreateController(ApplicationDbContext db, string? userId = null)
    {
        var controller = new StudentConnectionRequestApiController(
            db,
            _mockConnectionRequestService.Object,
            _mockOpenGroupClassroomService.Object,
            _mockAvailabilitySlotService.Object);

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

    // =========================================================================
    // 1. GetTutorBookingDetails Tests
    // =========================================================================

    [Fact]
    public async Task GetTutorBookingDetails_WhenTutorNotFound_ReturnsNotFound()
    {
        var db = TestDbContextFactory.Create();
        var controller = CreateController(db, "usr-student-1");

        var result = await controller.GetTutorBookingDetails(99999, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task GetTutorBookingDetails_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tutor-u1", FullName = "Gia Sư Test", Status = AccountStatus.Active };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser, VerificationStatus = VerificationStatus.Approved };

        db.Users.Add(tutorUser);
        db.TutorProfiles.Add(tutor);
        await db.SaveChangesAsync();

        var controller = CreateController(db, userId: null); // Chưa đăng nhập

        var result = await controller.GetTutorBookingDetails(tutor.Id, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetTutorBookingDetails_WhenStudentProfileNotFound_ReturnsNotFound()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tutor-u2", FullName = "Gia Sư Test 2", Status = AccountStatus.Active };
        var tutor = new TutorProfile { Id = 2, UserId = tutorUser.Id, User = tutorUser, VerificationStatus = VerificationStatus.Approved };

        db.Users.Add(tutorUser);
        db.TutorProfiles.Add(tutor);
        await db.SaveChangesAsync();

        var controller = CreateController(db, userId: "usr-non-existent-student");

        var result = await controller.GetTutorBookingDetails(tutor.Id, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task GetTutorBookingDetails_WhenValidStudentAndApprovedTutor_ReturnsOkResult()
    {
        var db = TestDbContextFactory.Create();

        var tutorUser = new ApplicationUser { Id = "tutor-u3", FullName = "Gia Sư Chuẩn", Status = AccountStatus.Active };
        var tutor = new TutorProfile { Id = 3, UserId = tutorUser.Id, User = tutorUser, VerificationStatus = VerificationStatus.Approved, HourlyFee = 250000 };
        var studentUser = new ApplicationUser { Id = "student-u3", FullName = "Học Viên Chuẩn", Status = AccountStatus.Active };
        var student = new StudentProfile { Id = 10, UserId = studentUser.Id, User = studentUser };

        var subject = new Subject { Id = 1, Code = "MATH", Name = "Toán Học" };
        var gradeLevel = new GradeLevel { Id = 1, Name = "Lớp 12" };
        var tutorSubject = new TutorSubject { TutorId = tutor.Id, SubjectId = subject.Id, Subject = subject, GradeLevelId = gradeLevel.Id, GradeLevel = gradeLevel, HourlyFee = 250000 };

        db.Users.AddRange(tutorUser, studentUser);
        db.TutorProfiles.Add(tutor);
        db.StudentProfiles.Add(student);
        db.Subjects.Add(subject);
        db.GradeLevels.Add(gradeLevel);
        db.TutorSubjects.Add(tutorSubject);
        await db.SaveChangesAsync();

        _mockOpenGroupClassroomService
            .Setup(s => s.GetOpenGroupClassroomsAsync(tutor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TutorClassroomDto>());

        _mockAvailabilitySlotService
            .Setup(s => s.GetAvailableSlotsForStudentAsync(tutor.Id, student.Id, 14, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TutorAvailableSlotDto>());

        _mockConnectionRequestService
            .Setup(s => s.GetBookingEligibilityAsync(studentUser.Id, tutor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingEligibilityVm { TutorId = tutor.Id });

        var controller = CreateController(db, studentUser.Id);

        var result = await controller.GetTutorBookingDetails(tutor.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    // =========================================================================
    // 2. SendRequest Tests
    // =========================================================================

    [Fact]
    public async Task SendRequest_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var db = TestDbContextFactory.Create();
        var controller = CreateController(db, userId: null);

        var result = await controller.SendRequest(new SendConnectionRequestDto { TutorId = 1 }, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task SendRequest_WhenServiceFails_ReturnsBadRequest()
    {
        var db = TestDbContextFactory.Create();
        var userId = "student-u4";
        var dto = new SendConnectionRequestDto { TutorId = 1, SubjectId = 1, GradeLevelId = 1 };

        _mockConnectionRequestService
            .Setup(s => s.CreateRequestAsync(dto, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionRequestOperationResult
            {
                Success = false,
                Message = "Bạn đã có yêu cầu kết nối đang chờ phản hồi với môn học này."
            });

        var controller = CreateController(db, userId);

        var result = await controller.SendRequest(dto, CancellationToken.None);

        var badResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badResult.Value);
    }

    [Fact]
    public async Task SendRequest_WhenServiceSucceeds_ReturnsOkResult()
    {
        var db = TestDbContextFactory.Create();
        var userId = "student-u5";
        var dto = new SendConnectionRequestDto { TutorId = 2, SubjectId = 1, GradeLevelId = 1 };

        _mockConnectionRequestService
            .Setup(s => s.CreateRequestAsync(dto, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionRequestOperationResult
            {
                Success = true,
                Message = "Đã gửi yêu cầu kết nối gia sư thành công!",
                RequestId = 100
            });

        var controller = CreateController(db, userId);

        var result = await controller.SendRequest(dto, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    // =========================================================================
    // 3. GetMyRequests Tests
    // =========================================================================

    [Fact]
    public async Task GetMyRequests_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var db = TestDbContextFactory.Create();
        var controller = CreateController(db, userId: null);

        var result = await controller.GetMyRequests(null, 1, 10, null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetMyRequests_WhenAuthenticated_ReturnsPaginatedList()
    {
        var db = TestDbContextFactory.Create();

        var studentUser = new ApplicationUser { Id = "student-myreq-1", FullName = "Học Viên MyRequests", Status = AccountStatus.Active };
        var student = new StudentProfile { Id = 20, UserId = studentUser.Id, User = studentUser };

        var tutorUser = new ApplicationUser { Id = "tutor-myreq-1", FullName = "Gia Sư MyRequests", Status = AccountStatus.Active };
        var tutor = new TutorProfile { Id = 30, UserId = tutorUser.Id, User = tutorUser, VerificationStatus = VerificationStatus.Approved };

        var subject = new Subject { Id = 10, Code = "LIT", Name = "Ngữ Văn" };
        var gradeLevel = new GradeLevel { Id = 10, Name = "Lớp 11" };

        var req1 = new ConnectionRequest
        {
            Id = 1,
            StudentId = student.Id,
            Student = student,
            TutorId = tutor.Id,
            Tutor = tutor,
            SubjectId = subject.Id,
            Subject = subject,
            GradeLevelId = gradeLevel.Id,
            GradeLevel = gradeLevel,
            Status = RequestStatus.Pending,
            PreferredType = ClassroomType.OneToOne,
            Message = "Em cần học thử môn Văn ạ",
            CreatedAt = DateTime.UtcNow
        };

        db.Users.AddRange(studentUser, tutorUser);
        db.StudentProfiles.Add(student);
        db.TutorProfiles.Add(tutor);
        db.Subjects.Add(subject);
        db.GradeLevels.Add(gradeLevel);
        db.ConnectionRequests.Add(req1);
        await db.SaveChangesAsync();

        var controller = CreateController(db, studentUser.Id);

        var result = await controller.GetMyRequests(null, 1, 10, null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    // =========================================================================
    // 4. CancelRequest Tests
    // =========================================================================

    [Fact]
    public async Task CancelRequest_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var db = TestDbContextFactory.Create();
        var controller = CreateController(db, userId: null);

        var result = await controller.CancelRequest(1, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task CancelRequest_WhenRequestNotFound_ReturnsNotFound()
    {
        var db = TestDbContextFactory.Create();
        var controller = CreateController(db, "student-cancel-1");

        var result = await controller.CancelRequest(99999, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task CancelRequest_WhenStatusNotPending_ReturnsBadRequest()
    {
        var db = TestDbContextFactory.Create();
        var studentUser = new ApplicationUser { Id = "student-cancel-2", FullName = "Học Viên Cancel", Status = AccountStatus.Active };
        var student = new StudentProfile { Id = 21, UserId = studentUser.Id, User = studentUser };

        var tutorUser = new ApplicationUser { Id = "tutor-cancel-2", FullName = "Gia Sư Cancel", Status = AccountStatus.Active };
        var tutor = new TutorProfile { Id = 31, UserId = tutorUser.Id, User = tutorUser, VerificationStatus = VerificationStatus.Approved };

        var subject = new Subject { Id = 11, Code = "ENG", Name = "Tiếng Anh" };
        var gradeLevel = new GradeLevel { Id = 11, Name = "Lớp 9" };

        var req = new ConnectionRequest
        {
            Id = 5,
            StudentId = student.Id,
            Student = student,
            TutorId = tutor.Id,
            Tutor = tutor,
            SubjectId = subject.Id,
            Subject = subject,
            GradeLevelId = gradeLevel.Id,
            GradeLevel = gradeLevel,
            Status = RequestStatus.Accepted, // Đã chấp nhận, không thể tự ý hủy
            PreferredType = ClassroomType.OneToOne,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.AddRange(studentUser, tutorUser);
        db.StudentProfiles.Add(student);
        db.TutorProfiles.Add(tutor);
        db.Subjects.Add(subject);
        db.GradeLevels.Add(gradeLevel);
        db.ConnectionRequests.Add(req);
        await db.SaveChangesAsync();

        var controller = CreateController(db, studentUser.Id);

        var result = await controller.CancelRequest(req.Id, CancellationToken.None);

        var badResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badResult.Value);
    }

    [Fact]
    public async Task CancelRequest_WhenPending_UpdatesStatusToCancelled_ReturnsOk()
    {
        var db = TestDbContextFactory.Create();
        var studentUser = new ApplicationUser { Id = "student-cancel-3", FullName = "Học Viên Cancel 3", Status = AccountStatus.Active };
        var student = new StudentProfile { Id = 22, UserId = studentUser.Id, User = studentUser };

        var tutorUser = new ApplicationUser { Id = "tutor-cancel-3", FullName = "Gia Sư Cancel 3", Status = AccountStatus.Active };
        var tutor = new TutorProfile { Id = 32, UserId = tutorUser.Id, User = tutorUser, VerificationStatus = VerificationStatus.Approved };

        var subject = new Subject { Id = 12, Code = "BIO", Name = "Sinh Học" };
        var gradeLevel = new GradeLevel { Id = 12, Name = "Lớp 11" };

        var req = new ConnectionRequest
        {
            Id = 6,
            StudentId = student.Id,
            Student = student,
            TutorId = tutor.Id,
            Tutor = tutor,
            SubjectId = subject.Id,
            Subject = subject,
            GradeLevelId = gradeLevel.Id,
            GradeLevel = gradeLevel,
            Status = RequestStatus.Pending, // Đang chờ duyệt
            PreferredType = ClassroomType.OneToOne,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.AddRange(studentUser, tutorUser);
        db.StudentProfiles.Add(student);
        db.TutorProfiles.Add(tutor);
        db.Subjects.Add(subject);
        db.GradeLevels.Add(gradeLevel);
        db.ConnectionRequests.Add(req);
        await db.SaveChangesAsync();

        var controller = CreateController(db, studentUser.Id);

        var result = await controller.CancelRequest(req.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Kiểm tra trong CSDL trạng thái đã chuyển thành Cancelled
        var updatedReq = await db.ConnectionRequests.FindAsync(req.Id);
        Assert.NotNull(updatedReq);
        Assert.Equal(RequestStatus.Cancelled, updatedReq.Status);
    }
}
