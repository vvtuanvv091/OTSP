using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PROJECT_OTSP.Dtos.Payment;
using PROJECT_OTSP.Models;
using PROJECT_OTSP.Services;
using PROJECT_OTSP.Tests.Helpers;
using Xunit;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Tests.UnitTests;

public class PaymentServiceTests
{
    private readonly IConfiguration _config;
    private readonly IMemoryCache _memoryCache;

    public PaymentServiceTests()
    {
        _config = new ConfigurationBuilder().Build();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public async Task ConfirmManualPaymentMockAsync_WhenPaymentPending_ShouldUpdateStatusToSuccessAndCreateInvoice()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var student = new StudentProfile
        {
            Id = 1,
            UserId = "user_student_1",
            User = new ApplicationUser { Id = "user_student_1", FullName = "Nguyễn Văn A" }
        };

        var tutor = new TutorProfile
        {
            Id = 1,
            UserId = "user_tutor_1",
            User = new ApplicationUser { Id = "user_tutor_1", FullName = "Trần Gia Sư" }
        };

        var subject = new Subject { Id = 1, Name = "Toán" };
        var grade = new GradeLevel { Id = 1, Name = "Lớp 9" };

        var classroom = new Classroom
        {
            Id = 1,
            Name = "Lớp 1-1 Toán 9",
            TutorId = tutor.Id,
            SubjectId = subject.Id,
            GradeLevelId = grade.Id,
            Fee = 1200000m,
            Type = ClassroomType.OneToOne
        };

        var payment = new Payment
        {
            Id = 10,
            ClassroomId = classroom.Id,
            StudentId = student.Id,
            Amount = 1200000m,
            Method = "SePay",
            Status = PaymentStatus.Pending
        };

        db.StudentProfiles.Add(student);
        db.TutorProfiles.Add(tutor);
        db.Subjects.Add(subject);
        db.GradeLevels.Add(grade);
        db.Classrooms.Add(classroom);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var service = new PaymentService(db, _config, _memoryCache, NullLogger<PaymentService>.Instance);

        // ACT
        var result = await service.ConfirmManualPaymentMockAsync(payment.Id, student.UserId);

        // ASSERT
        result.Should().BeTrue();

        var updatedPayment = await db.Payments.FindAsync(payment.Id);
        updatedPayment.Should().NotBeNull();
        updatedPayment!.Status.Should().Be(PaymentStatus.Success);
        updatedPayment.PaidAt.Should().NotBeNull();

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.PaymentId == payment.Id);
        invoice.Should().NotBeNull();
        invoice!.TotalAmount.Should().Be(1200000m);
    }

    [Fact]
    public async Task ProcessSePayWebhookAsync_WhenValidWebhook_ShouldMarkPaymentAsSuccessAndCreateInvoice()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var student = new StudentProfile
        {
            Id = 2,
            UserId = "user_student_2",
            User = new ApplicationUser { Id = "user_student_2", FullName = "Lê Thị B" }
        };

        var tutor = new TutorProfile
        {
            Id = 2,
            UserId = "user_tutor_2",
            User = new ApplicationUser { Id = "user_tutor_2", FullName = "Phạm Gia Sư" }
        };

        var classroom = new Classroom
        {
            Id = 2,
            Name = "Lớp Nhóm Tiếng Anh",
            TutorId = tutor.Id,
            SubjectId = 1,
            GradeLevelId = 1,
            Fee = 600000m,
            Type = ClassroomType.Group
        };

        var payment = new Payment
        {
            Id = 25,
            ClassroomId = classroom.Id,
            StudentId = student.Id,
            Amount = 600000m,
            Method = "SePay",
            Status = PaymentStatus.Pending
        };

        db.StudentProfiles.Add(student);
        db.TutorProfiles.Add(tutor);
        db.Classrooms.Add(classroom);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var service = new PaymentService(db, _config, _memoryCache, NullLogger<PaymentService>.Instance);

        var webhookPayload = new SePayWebhookDto
        {
            id = 9999,
            content = "MBVCB.12345.OTSP25.Chuyen hoc phi",
            transferAmount = 600000m,
            referenceCode = "REF_SEPAY_9999"
        };

        // ACT
        var result = await service.ProcessSePayWebhookAsync(webhookPayload);

        // ASSERT
        result.Should().BeTrue();

        var updated = await db.Payments.FindAsync(payment.Id);
        updated!.Status.Should().Be(PaymentStatus.Success);
        updated.GatewayTransactionCode.Should().Be("REF_SEPAY_9999");

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.PaymentId == payment.Id);
        invoice.Should().NotBeNull();
        invoice!.TotalAmount.Should().Be(600000m);
    }

    [Fact]
    public async Task GetStudentPaymentOverviewAsync_WhenEnrolledWithoutPayment_ShouldAutoCreatePendingPayment()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var student = new StudentProfile
        {
            Id = 3,
            UserId = "user_student_3",
            User = new ApplicationUser { Id = "user_student_3", FullName = "Nguyễn Văn C" }
        };

        var tutor = new TutorProfile
        {
            Id = 3,
            UserId = "user_tutor_3",
            User = new ApplicationUser { Id = "user_tutor_3", FullName = "Lê Gia Sư" }
        };

        var subject = new Subject { Id = 3, Name = "Vật Lý" };
        var grade = new GradeLevel { Id = 3, Name = "Lớp 12" };

        var classroom = new Classroom
        {
            Id = 3,
            Name = "Lớp Lý 12",
            TutorId = tutor.Id,
            SubjectId = subject.Id,
            GradeLevelId = grade.Id,
            Fee = 1500000m,
            Type = ClassroomType.OneToOne
        };

        var enrollment = new ClassroomStudent
        {
            ClassroomId = classroom.Id,
            StudentId = student.Id,
            Student = student,
            Classroom = classroom,
            JoinedAt = DateTime.UtcNow
        };

        db.StudentProfiles.Add(student);
        db.TutorProfiles.Add(tutor);
        db.Subjects.Add(subject);
        db.GradeLevels.Add(grade);
        db.Classrooms.Add(classroom);
        db.ClassroomStudents.Add(enrollment);
        await db.SaveChangesAsync();

        var service = new PaymentService(db, _config, _memoryCache, NullLogger<PaymentService>.Instance);

        // ACT
        var overview = await service.GetStudentPaymentOverviewAsync("user_student_3");

        // ASSERT
        overview.Should().NotBeNull();
        overview.PendingPayments.Should().HaveCount(1);
        overview.PendingPayments[0].Amount.Should().Be(1500000m);
        overview.PendingPayments[0].ClassroomName.Should().Be("Lớp Lý 12");

        var createdPayment = await db.Payments.FirstOrDefaultAsync(p => p.StudentId == student.Id);
        createdPayment.Should().NotBeNull();
        createdPayment!.Status.Should().Be(PaymentStatus.Pending);
        createdPayment.Amount.Should().Be(1500000m);
    }

    [Fact]
    public async Task ProcessSePayWebhookAsync_WhenAmountLessThanRequired_ShouldNotMarkAsSuccess()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var payment = new Payment
        {
            Id = 99,
            Amount = 500000m,
            Method = "SePay",
            Status = PaymentStatus.Pending
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var service = new PaymentService(db, _config, _memoryCache, NullLogger<PaymentService>.Instance);

        var webhookPayload = new SePayWebhookDto
        {
            id = 12345,
            content = "OTSP99 Chuyen khoan",
            transferAmount = 100000m // Thiếu tiền (yêu cầu 500.000)
        };

        // ACT
        var result = await service.ProcessSePayWebhookAsync(webhookPayload);

        // ASSERT
        result.Should().BeFalse();
        var check = await db.Payments.FindAsync(99);
        check!.Status.Should().Be(PaymentStatus.Pending);
    }
}