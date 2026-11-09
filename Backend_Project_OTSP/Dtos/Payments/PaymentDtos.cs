using System;
using System.Collections.Generic;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Dtos.Payment;

public class StudentPaymentItemDto
{
    public int PaymentId { get; set; }
    public int ClassroomId { get; set; }
    public string ClassroomName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;
    public string TutorName { get; set; } = string.Empty;
    public string? TutorAvatarUrl { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime? PaidAt { get; set; }
    public int? InvoiceId { get; set; }
    public ClassroomType ClassroomType { get; set; }
    public int SessionsPerWeek { get; set; } = 2;
    public double HoursPerSession { get; set; } = 2.0;
    public DateOnly? StartDate { get; set; }
}

public class StudentPaymentOverviewVm
{
    public bool IsMinor { get; set; }
    public string? GuardianName { get; set; }
    public List<StudentPaymentItemDto> PendingPayments { get; set; } = [];
    public List<StudentPaymentItemDto> HistoryPayments { get; set; } = [];
}

public class PaymentCheckoutVm
{
    public int PaymentId { get; set; }
    public int ClassroomId { get; set; }
    public string ClassroomName { get; set; } = string.Empty;
    public string TutorName { get; set; } = string.Empty;
    public string SubjectGrade { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string BankName { get; set; } = "MBBank";
    public string AccountNo { get; set; } = "0984391940";
    public string AccountName { get; set; } = "TONG NGOC TUAN";
    public string TransferContent { get; set; } = string.Empty;
    public string QrImageUrl { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public bool IsPaid { get; set; }
}

public class SePayWebhookDto
{
    public long id { get; set; }
    public string? gateway { get; set; }
    public string? transactionDate { get; set; }
    public string? accountNumber { get; set; }
    public string? code { get; set; }
    public string? content { get; set; }
    public string? transferType { get; set; }
    public decimal transferAmount { get; set; }
    public decimal accumulated { get; set; }
    public string? subAccount { get; set; }
    public string? referenceCode { get; set; }
    public string? description { get; set; }
}

public class SePayCheckResultDto
{
    public bool IsConfigured { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public bool HasSecretKey { get; set; }
    public bool HasApiKey { get; set; }
    public int? HttpStatusCode { get; set; }
    public bool CanConnectToSePay { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class InvoiceDetailsDto
{
    public int InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public int PaymentId { get; set; }
    public DateTime IssuedDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? GatewayTransactionCode { get; set; }

    // Student Info
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string? StudentPhone { get; set; }
    public string? GuardianName { get; set; }

    // Tutor Info
    public string TutorName { get; set; } = string.Empty;
    public string? TutorEmail { get; set; }

    // Course Info
    public int ClassroomId { get; set; }
    public string ClassroomName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string GradeLevelName { get; set; } = string.Empty;
    public string ClassroomType { get; set; } = string.Empty;

    // Platform Info
    public string PlatformName { get; set; } = "Hệ thống kết nối Gia sư OTSP";
    public string PlatformHotline { get; set; } = "1900 6868";
    public string PlatformEmail { get; set; } = "hotro@otsp.edu.vn";
    public string PlatformAddress { get; set; } = "Khu Công Nghệ Cao, TP. Thủ Đức, TP. Hồ Chí Minh";
}