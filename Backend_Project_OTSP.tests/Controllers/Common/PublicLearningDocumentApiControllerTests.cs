using Backend_Project_OTSP.Controllers.Api.Common;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.tests.Controllers.Common;

public class PublicLearningDocumentApiControllerTests
{
    // Mức dễ: Trang tài liệu công khai chỉ trả về tài liệu Public đã được Approved, lọc bỏ tài liệu Pending hoặc Classroom.
    [Fact]
    public async Task GetPublicDocuments_ReturnsOnlyPublicApprovedDocuments()
    {
        using var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "pub-tutor-1", FullName = "Cô Lan" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var subject = new Subject { Id = 1, Code = "TOAN", Name = "Toán học" };
        var grade = new GradeLevel { Id = 1, Code = "THPT", Name = "THPT" };

        db.AddRange(tutorUser, tutor, subject, grade);
        db.LearningDocuments.AddRange(
            new LearningDocument
            {
                Title = "Đề cương Toán 12 (Công khai)",
                FileUrl = "https://res.cloudinary.com/demo/raw/upload/de-cuong.pdf",
                TutorId = 1,
                SubjectId = 1,
                GradeLevelId = 1,
                Scope = DocumentScope.Public,
                Status = VerificationStatus.Approved
            },
            new LearningDocument
            {
                Title = "Tài liệu đang chờ duyệt",
                FileUrl = "https://res.cloudinary.com/demo/raw/upload/cho-duyet.pdf",
                TutorId = 1,
                SubjectId = 1,
                GradeLevelId = 1,
                Scope = DocumentScope.Public,
                Status = VerificationStatus.Pending
            },
            new LearningDocument
            {
                Title = "Tài liệu nội bộ lớp",
                FileUrl = "https://res.cloudinary.com/demo/raw/upload/noi-bo.pdf",
                TutorId = 1,
                SubjectId = 1,
                GradeLevelId = 1,
                Scope = DocumentScope.Classroom,
                Status = VerificationStatus.Approved
            }
        );
        await db.SaveChangesAsync();

        var controller = new PublicLearningDocumentApiController(db);
        var result = await controller.GetPublicDocuments();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        var totalItems = okResult.Value.GetType().GetProperty("totalItems")?.GetValue(okResult.Value);
        Assert.Equal(1, totalItems);
    }

    // Mức trung bình: Tải tài liệu công khai hợp lệ lưu trên Cloudinary -> trả RedirectResult tới URL Cloudinary.
    [Fact]
    public async Task Download_WhenCloudinaryUrlValid_RedirectsToCloudinary()
    {
        using var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "pub-tutor-2", FullName = "Thầy Hùng" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var subject = new Subject { Id = 1, Code = "LY", Name = "Vật lý" };
        var grade = new GradeLevel { Id = 1, Code = "THCS", Name = "THCS" };
        const string cloudinaryUrl = "https://res.cloudinary.com/otsp/raw/upload/v1/otsp/learning-documents/vatly.pdf";

        var doc = new LearningDocument
        {
            Id = 15,
            Title = "Tổng hợp công thức Vật lý",
            FileUrl = cloudinaryUrl,
            TutorId = 1,
            SubjectId = 1,
            GradeLevelId = 1,
            Scope = DocumentScope.Public,
            Status = VerificationStatus.Approved
        };

        db.AddRange(tutorUser, tutor, subject, grade, doc);
        await db.SaveChangesAsync();

        var controller = new PublicLearningDocumentApiController(db);
        var result = await controller.Download(15, CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal(cloudinaryUrl, redirect.Url);
    }

    // Mức trung bình: Dữ liệu sai (tài liệu chưa duyệt hoặc FileUrl không phải URL Cloudinary) -> trả 404 NotFound, không đọc App_Data.
    [Fact]
    public async Task Download_WhenDocumentMissingOrNotCloudinaryUrl_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "pub-tutor-3", FullName = "Thầy Nam" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var subject = new Subject { Id = 1, Code = "HOA", Name = "Hóa học" };
        var grade = new GradeLevel { Id = 1, Code = "THPT", Name = "THPT" };

        db.AddRange(tutorUser, tutor, subject, grade);
        db.LearningDocuments.Add(new LearningDocument
        {
            Id = 16,
            Title = "File cũ chưa có link Cloudinary",
            FileUrl = "old-local-file.pdf",
            TutorId = 1,
            SubjectId = 1,
            GradeLevelId = 1,
            Scope = DocumentScope.Public,
            Status = VerificationStatus.Approved
        });
        await db.SaveChangesAsync();

        var controller = new PublicLearningDocumentApiController(db);

        Assert.IsType<NotFoundObjectResult>(await controller.Download(999, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.Download(16, CancellationToken.None));
    }
}
