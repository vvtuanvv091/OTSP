using System;
using System.Collections.Generic;
using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos.Payment;

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