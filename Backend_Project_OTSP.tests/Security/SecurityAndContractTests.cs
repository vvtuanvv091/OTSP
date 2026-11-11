using System.Security.Claims;
using BACKEND_PROJECT_OTSP.Controllers.Api;
using Backend_Project_OTSP.Controllers.Api.Student;
using Backend_Project_OTSP.Controllers.Api.Tutor;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Payment;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Chat;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.Payments;
using Backend_Project_OTSP.Services.Sessions;
using Backend_Project_OTSP.tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.tests.Security;

public class SecurityAndContractTests
{
    private static ClaimsPrincipal CreatePrincipal(string userId, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth"));

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public async Task SePayWebhook_WithoutValidApiKey_ReturnsUnauthorized()
    {
        var mockPaymentService = new Mock<IPaymentService>();
        var logger = Mock.Of<ILogger<PaymentApiController>>();
        var config = BuildConfig(new() { ["SePay:SecretKey"] = "whsec_test_secret_123" });

        var controller = new PaymentApiController(mockPaymentService.Object, logger, config)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var payload = new SePayWebhookDto { id = 1, content = "OTSP1", transferAmount = 500000m, transferType = "in" };
        var result = await controller.SePayWebhook(payload, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        mockPaymentService.Verify(s => s.ProcessSePayWebhookAsync(It.IsAny<SePayWebhookDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SePayWebhook_WithValidApiKey_InvokesPaymentProcessor()
    {
        var mockPaymentService = new Mock<IPaymentService>();
        mockPaymentService.Setup(s => s.ProcessSePayWebhookAsync(It.IsAny<SePayWebhookDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var logger = Mock.Of<ILogger<PaymentApiController>>();
        var config = BuildConfig(new() { ["SePay:SecretKey"] = "whsec_test_secret_123" });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Apikey whsec_test_secret_123";

        var controller = new PaymentApiController(mockPaymentService.Object, logger, config)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var payload = new SePayWebhookDto { id = 1, content = "OTSP1", transferAmount = 500000m, transferType = "in" };
        var result = await controller.SePayWebhook(payload, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        mockPaymentService.Verify(s => s.ProcessSePayWebhookAsync(payload, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessSePayWebhook_RejectsOutgoingTransferAndUnderpayment()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "t-u1", FullName = "Tutor One" };
        var studentUser = new ApplicationUser { Id = "s-u1", FullName = "Student One" };
        var subject = new Subject { Id = 1, Code = "TOAN", Name = "Toán" };
        var grade = new GradeLevel { Id = 1, Name = "Lớp 10" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var student = new StudentProfile { Id = 1, UserId = studentUser.Id, User = studentUser };
        var classroom = new Classroom { Id = 1, NameClass = "Toán 10", TutorId = 1, Tutor = tutor, SubjectId = 1, Subject = subject, GradeLevelId = 1, GradeLevel = grade, Fee = 500000m };
        var payment = new Payment { Id = 10, ClassroomId = 1, Classroom = classroom, StudentId = 1, Student = student, Amount = 500000m, Status = PaymentStatus.Pending };

        db.AddRange(tutorUser, studentUser, subject, grade, tutor, student, classroom, payment);
        await db.SaveChangesAsync();

        var service = new PaymentService(db, BuildConfig(new()), new MemoryCache(new MemoryCacheOptions()), Mock.Of<ILogger<PaymentService>>());

        var outgoingOk = await service.ProcessSePayWebhookAsync(new SePayWebhookDto
        {
            id = 1, content = "OTSP10", transferAmount = 500000m, transferType = "out"
        });
        Assert.False(outgoingOk);

        var underpaidOk = await service.ProcessSePayWebhookAsync(new SePayWebhookDto
        {
            id = 2, content = "OTSP10", transferAmount = 100000m, transferType = "in"
        });
        Assert.False(underpaidOk);

        var paidOk = await service.ProcessSePayWebhookAsync(new SePayWebhookDto
        {
            id = 3, content = "MBVCB OTSP10", transferAmount = 500000m, transferType = "in"
        });
        Assert.True(paidOk);
        Assert.Equal(PaymentStatus.Success, db.Payments.Single(p => p.Id == 10).Status);
    }

    [Fact]
    public async Task ChatService_ClassGroup_DeniesNonMembersAndAllowsEnrolledMembers()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tutor-u", FullName = "Gia sư" };
        var memberUser = new ApplicationUser { Id = "student-in", FullName = "Học viên lớp" };
        var outsiderUser = new ApplicationUser { Id = "student-out", FullName = "Người ngoài" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var member = new StudentProfile { Id = 1, UserId = memberUser.Id, User = memberUser };
        var outsider = new StudentProfile { Id = 2, UserId = outsiderUser.Id, User = outsiderUser };
        var subject = new Subject { Id = 1, Code = "VATLY", Name = "Vật lý" };
        var grade = new GradeLevel { Id = 1, Name = "Lớp 11" };
        var classroom = new Classroom { Id = 55, NameClass = "Lý 11A", TutorId = 1, Tutor = tutor, SubjectId = 1, Subject = subject, GradeLevelId = 1, GradeLevel = grade };
        var enrollment = new ClassroomStudent { ClassroomId = 55, Classroom = classroom, StudentId = 1, Student = member };

        db.AddRange(tutorUser, memberUser, outsiderUser, tutor, member, outsider, subject, grade, classroom, enrollment);
        await db.SaveChangesAsync();

        var chatService = new ChatService(
            db,
            new MemoryCache(new MemoryCacheOptions()),
            BuildConfig(new() { ["Chat:MessageEditWindowMinutes"] = "15" }),
            Mock.Of<IUserPresenceService>());

        Assert.True(await chatService.CanAccessClassroomChatAsync(55, tutorUser.Id));
        Assert.True(await chatService.CanAccessClassroomChatAsync(55, memberUser.Id));
        Assert.False(await chatService.CanAccessClassroomChatAsync(55, outsiderUser.Id));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            chatService.GetMessagesAsync(outsiderUser.Id, "class_55"));

        var memberMessages = await chatService.GetMessagesAsync(memberUser.Id, "class_55");
        Assert.Empty(memberMessages);
    }

    [Fact]
    public async Task StudentLearningDocument_Download_ForbidsUnenrolledStudent()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tu1", FullName = "Tutor" };
        var studentUser = new ApplicationUser { Id = "st-outsider", FullName = "Outsider" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var student = new StudentProfile { Id = 1, UserId = studentUser.Id, User = studentUser };
        var subject = new Subject { Id = 1, Code = "HOA", Name = "Hóa" };
        var grade = new GradeLevel { Id = 1, Name = "Lớp 12" };
        var classroom = new Classroom { Id = 77, NameClass = "Hóa 12", TutorId = 1, Tutor = tutor, SubjectId = 1, Subject = subject, GradeLevelId = 1, GradeLevel = grade, Status = ClassroomStatus.Active };
        var doc = new LearningDocument
        {
            Id = 9,
            Title = "Đề thi thử",
            FileUrl = "https://res.cloudinary.com/demo/raw/upload/test.pdf",
            TutorId = 1,
            Tutor = tutor,
            SubjectId = 1,
            Subject = subject,
            GradeLevelId = 1,
            GradeLevel = grade,
            Scope = DocumentScope.Classroom,
            ClassroomId = 77,
            Status = VerificationStatus.Approved
        };

        db.AddRange(tutorUser, studentUser, tutor, student, subject, grade, classroom, doc);
        await db.SaveChangesAsync();

        var controller = new StudentLearningDocumentApiController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(studentUser.Id, "Student_Parent") }
            }
        };

        var result = await controller.Download(9, CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);
    }

    [Fact]
    public async Task TutorIncome_WhenNoBankSaved_ReturnsEmptyBankWithoutAdminDefaultLeak()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "t-bank-1", FullName = "Nguyễn Gia Sư" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        db.AddRange(tutorUser, tutor);
        await db.SaveChangesAsync();

        var controller = new TutorIncomeApiController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(tutorUser.Id, "Tutor") }
            }
        };

        var result = await controller.GetIncome(null, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var bankProp = okResult.Value.GetType().GetProperty("bankAccount");
        var bankAccount = bankProp?.GetValue(okResult.Value);
        Assert.NotNull(bankAccount);

        var accNo = bankAccount!.GetType().GetProperty("accountNumber")?.GetValue(bankAccount) as string;
        var holder = bankAccount.GetType().GetProperty("accountHolder")?.GetValue(bankAccount) as string;
        var isUpdated = (bool)(bankAccount.GetType().GetProperty("isUpdated")?.GetValue(bankAccount) ?? true);

        Assert.Equal(string.Empty, accNo);
        Assert.Equal(string.Empty, holder);
        Assert.False(isUpdated);
    }
}
