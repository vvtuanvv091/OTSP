using System.Security.Claims;
using Backend_Project_OTSP.Controllers.Api.Tutor;
using Backend_Project_OTSP.Dtos.Tutor;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.tests.Controllers.Tutor;

public class TutorLearningDocumentApiControllerTests
{
    private static TutorLearningDocumentApiController CreateController(
        Backend_Project_OTSP.Data.ApplicationDbContext db,
        Mock<IImageStorageService> storageMock,
        string userId)
    {
        var controller = new TutorLearningDocumentApiController(db, storageMock.Object);
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, "Tutor")
        ], "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private static IFormFile CreateFakePdf(string fileName = "tai-lieu.pdf")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 fake content");
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName);
    }

    [Fact]
    public async Task Upload_PublicScope_SavesPendingWithNullClassroomAndNotifiesAdmins()
    {
        using var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tu-doc-1", FullName = "Gia Sư A" };
        var adminUser = new ApplicationUser { Id = "admin-doc-1", FullName = "Admin" };
        var adminRole = new ApplicationRole { Id = "role-admin", Name = "Admin", NormalizedName = "ADMIN" };
        var adminUserRole = new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = adminUser.Id, RoleId = adminRole.Id };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var subject = new Subject { Id = 1, Code = "TOAN", Name = "Toán" };
        var grade = new GradeLevel { Id = 1, Code = "12", Name = "Lớp 12" };
        var tutorSubject = new TutorSubject { Id = 1, TutorId = 1, SubjectId = 1, GradeLevelId = 1 };

        db.AddRange(tutorUser, adminUser, adminRole, adminUserRole, tutor, subject, grade, tutorSubject);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IImageStorageService>();
        storageMock.Setup(s => s.UploadLearningDocumentAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredImage("https://res.cloudinary.com/demo/raw/upload/doc.pdf", "doc"));

        var controller = CreateController(db, storageMock, tutorUser.Id);
        var result = await controller.Upload(
            new UploadDocumentRequest
            {
                Title = "Đề cương Toán",
                Scope = DocumentScope.Public,
                ClassroomId = 0,
                SubjectId = 1,
                GradeLevelId = 1
            },
            CreateFakePdf(),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var doc = await db.LearningDocuments.SingleAsync();
        Assert.Equal(DocumentScope.Public, doc.Scope);
        Assert.Equal(VerificationStatus.Pending, doc.Status);
        Assert.Null(doc.ClassroomId);

        var adminNotif = await db.Notifications.SingleAsync(n => n.UserId == adminUser.Id);
        Assert.Contains("/admin/learning-documents", adminNotif.Content);
    }

    [Fact]
    public async Task Upload_GroupClassroomScope_SavesApprovedAndNotifiesStudentsWithValidRoute()
    {
        using var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tu-doc-2", FullName = "Gia Sư B" };
        var studentUser = new ApplicationUser { Id = "st-doc-2", FullName = "Học Viên B" };
        var tutor = new TutorProfile { Id = 2, UserId = tutorUser.Id, User = tutorUser };
        var student = new StudentProfile { Id = 2, UserId = studentUser.Id, User = studentUser };
        var subject = new Subject { Id = 2, Code = "LY", Name = "Vật lý" };
        var grade = new GradeLevel { Id = 2, Code = "11", Name = "Lớp 11" };
        var classroom = new Classroom
        {
            Id = 20,
            NameClass = "Lớp Nhóm Lý 11",
            Type = ClassroomType.Group,
            Status = ClassroomStatus.Active,
            TutorId = 2,
            SubjectId = 2,
            GradeLevelId = 2
        };
        var enrollment = new ClassroomStudent { ClassroomId = 20, StudentId = 2 };

        db.AddRange(tutorUser, studentUser, tutor, student, subject, grade, classroom, enrollment);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IImageStorageService>();
        storageMock.Setup(s => s.UploadLearningDocumentAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredImage("https://res.cloudinary.com/demo/raw/upload/group.pdf", "group"));

        var controller = CreateController(db, storageMock, tutorUser.Id);
        var result = await controller.Upload(
            new UploadDocumentRequest
            {
                Title = "Bài tập nhóm Lý",
                Scope = DocumentScope.Classroom,
                ClassroomId = 20
            },
            CreateFakePdf(),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var doc = await db.LearningDocuments.SingleAsync();
        Assert.Equal(DocumentScope.Classroom, doc.Scope);
        Assert.Equal(VerificationStatus.Approved, doc.Status);
        Assert.Equal(20, doc.ClassroomId);
        Assert.Equal(2, doc.SubjectId);
        Assert.Equal(2, doc.GradeLevelId);

        var studentNotif = await db.Notifications.SingleAsync(n => n.UserId == studentUser.Id);
        Assert.Contains("/student/documents", studentNotif.Content);
    }
}
