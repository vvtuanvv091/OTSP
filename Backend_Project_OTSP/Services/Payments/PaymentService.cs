using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Common.Notification;
using Backend_Project_OTSP.Dtos.Payment;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.Notifications;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Payments;

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PaymentService> _logger;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly IEmailSenderService? _emailSender;
    private readonly IEmailQueue? _emailQueue;

    private readonly string _bankName;
    private readonly string _bankId;
    private readonly string _accountNo;
    private readonly string _accountName;

    public PaymentService(
        ApplicationDbContext db,
        IConfiguration config,
        IMemoryCache cache,
        ILogger<PaymentService> logger,
        IHttpClientFactory? httpClientFactory = null,
        IEmailSenderService? emailSender = null,
        IEmailQueue? emailQueue = null)
    {
        _db = db;
        _config = config;
        _cache = cache;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _emailSender = emailSender;
        _emailQueue = emailQueue;

        _bankName = _config["SePay:BankName"] ?? _config["VietQR:BankName"] ?? "MBBank";
        _bankId = _config["SePay:BankId"] ?? _config["VietQR:BankId"] ?? "970422";
        _accountNo = _config["SePay:AccountNo"] ?? _config["VietQR:AccountNo"] ?? "0984391940";
        _accountName = _config["SePay:AccountName"] ?? _config["VietQR:AccountName"] ?? "TONG NGOC TUAN";
    }

    public async Task<StudentPaymentOverviewVm> GetStudentPaymentOverviewAsync(string userId, CancellationToken ct = default)
    {
        var student = await _db.StudentProfiles
            .Include(s => s.User)
            .Include(s => s.Guardians)
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (student == null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null)
                return new StudentPaymentOverviewVm();

            student = new StudentProfile { UserId = userId, User = user };
            _db.StudentProfiles.Add(student);
            await _db.SaveChangesAsync(ct);
        }

        // Tự động tạo bản ghi Payment cho các lớp ĐANG HOẠT ĐỘNG đã đăng ký nhưng chưa có payment record
        var enrolledClassroomIds = await _db.ClassroomStudents
            .Where(cs => cs.StudentId == student.Id && cs.Classroom.Status == ClassroomStatus.Active)
            .Select(cs => cs.ClassroomId)
            .ToListAsync(ct);

        if (enrolledClassroomIds.Any())
        {
            var existingPaymentClassroomIds = await _db.Payments
                .Where(p => p.StudentId == student.Id && enrolledClassroomIds.Contains(p.ClassroomId))
                .Select(p => p.ClassroomId)
                .ToListAsync(ct);

            var missingIds = enrolledClassroomIds.Except(existingPaymentClassroomIds).ToList();
            if (missingIds.Any())
            {
                var classrooms = await _db.Classrooms
                    .Where(c => missingIds.Contains(c.Id))
                    .ToListAsync(ct);

                foreach (var cls in classrooms)
                {
                    // Lớp học thử hoặc lớp miễn phí thì KHÔNG tạo hóa đơn
                    if (cls.Fee <= 0 || cls.NameClass.StartsWith("Học thử"))
                        continue;

                    _db.Payments.Add(new Payment
                    {
                        StudentId = student.Id,
                        ClassroomId = cls.Id,
                        Amount = cls.Fee,
                        Method = "BankTransfer",
                        Status = PaymentStatus.Pending
                    });
                }

                await _db.SaveChangesAsync(ct);
            }
        }

        // Load full payment data
        var payments = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Classroom).ThenInclude(c => c.Subject)
            .Include(p => p.Classroom).ThenInclude(c => c.GradeLevel)
            .Include(p => p.Classroom).ThenInclude(c => c.Tutor).ThenInclude(t => t.User)
            .Include(p => p.Invoice)
            .Where(p => p.StudentId == student.Id)
            .OrderByDescending(p => p.Id)
            .ToListAsync(ct);

        var pendingPayments = payments
            .Where(p => p.Status == PaymentStatus.Pending && p.Classroom != null && p.Classroom.Status == ClassroomStatus.Active)
            .Select(MapToItemDto)
            .ToList();

        var historyPayments = payments
            .Where(p => p.Status != PaymentStatus.Pending)
            .Select(MapToItemDto)
            .ToList();

        // Thông tin người giám hộ nếu là học viên dưới 18
        string? guardianName = null;
        bool isMinor = false;
        var guardian = student.Guardians?.FirstOrDefault();
        if (guardian != null)
        {
            isMinor = true;
            guardianName = guardian.FullName;
        }

        return new StudentPaymentOverviewVm
        {
            PendingPayments = pendingPayments,
            HistoryPayments = historyPayments,
            IsMinor = isMinor,
            GuardianName = guardianName
        };
    }

    public async Task<PaymentCheckoutVm?> GetPaymentCheckoutAsync(int paymentId, string userId, CancellationToken ct = default)
    {
        var student = await _db.StudentProfiles.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (student == null) return null;

        var payment = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Classroom).ThenInclude(c => c.Subject)
            .Include(p => p.Classroom).ThenInclude(c => c.GradeLevel)
            .Include(p => p.Classroom).ThenInclude(c => c.Tutor).ThenInclude(t => t.User)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.StudentId == student.Id, ct);

        if (payment == null || (payment.Status == PaymentStatus.Pending && payment.Classroom?.Status != ClassroomStatus.Active))
            return null;

        // Mã nội dung chuyển khoản chuẩn OTSP{paymentId}
        var transferContent = $"OTSP{paymentId}";

        var qrImageUrl = $"https://img.vietqr.io/image/{_bankId}-{_accountNo}-compact.png" +
                         $"?amount={(long)payment.Amount}" +
                         $"&addInfo={Uri.EscapeDataString(transferContent)}" +
                         $"&accountName={Uri.EscapeDataString(_accountName)}";

        return new PaymentCheckoutVm
        {
            PaymentId = payment.Id,
            ClassroomId = payment.ClassroomId,
            ClassroomName = payment.Classroom.NameClass,
            TutorName = payment.Classroom.Tutor?.User?.FullName ?? "Gia sư",
            SubjectGrade = $"{payment.Classroom.Subject.Name} - {payment.Classroom.GradeLevel.Name}",
            Amount = payment.Amount,
            BankName = _bankName,
            AccountNo = _accountNo,
            AccountName = _accountName,
            TransferContent = transferContent,
            QrImageUrl = qrImageUrl,
            Status = payment.Status,
            IsPaid = payment.Status == PaymentStatus.Success
        };
    }

    public async Task<InvoiceDetailsDto?> GetInvoiceDetailsAsync(int paymentId, string userId, CancellationToken ct = default)
    {
        var student = await _db.StudentProfiles
            .Include(s => s.User)
            .Include(s => s.Guardians)
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (student == null) return null;

        var payment = await _db.Payments
            .Include(p => p.Invoice)
            .Include(p => p.Student).ThenInclude(s => s.User)
            .Include(p => p.Classroom).ThenInclude(c => c.Subject)
            .Include(p => p.Classroom).ThenInclude(c => c.GradeLevel)
            .Include(p => p.Classroom).ThenInclude(c => c.Tutor).ThenInclude(t => t.User)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.StudentId == student.Id, ct);

        if (payment == null || (payment.Status == PaymentStatus.Pending && payment.Classroom?.Status != ClassroomStatus.Active))
            return null;

        // Đảm bảo có thực thể hóa đơn cho giao dịch
        if (payment.Invoice == null)
        {
            var issuedDate = payment.PaidAt.HasValue
                ? DateOnly.FromDateTime(payment.PaidAt.Value)
                : DateOnly.FromDateTime(DateTime.UtcNow);

            var newInvoice = new Invoice
            {
                PaymentId = payment.Id,
                TotalAmount = payment.Amount,
                IssuedDate = issuedDate
            };
            _db.Invoices.Add(newInvoice);
            await _db.SaveChangesAsync(ct);
            payment.Invoice = newInvoice;
        }

        var guardian = student.Guardians?.FirstOrDefault();
        var issuedDateTime = payment.Invoice.IssuedDate.ToDateTime(TimeOnly.MinValue);

        return new InvoiceDetailsDto
        {
            InvoiceId = payment.Invoice.Id,
            InvoiceCode = $"HD-{payment.Invoice.IssuedDate:yyyyMM}-{payment.Invoice.Id:D4}",
            PaymentId = payment.Id,
            IssuedDate = issuedDateTime,
            PaidAt = payment.PaidAt,
            Amount = payment.Amount,
            Method = payment.Method == "BankTransfer" ? "Chuyển khoản SePay / VietQR" : payment.Method,
            Status = payment.Status == PaymentStatus.Success ? "Đã thanh toán" : payment.Status.ToString(),
            GatewayTransactionCode = payment.GatewayTransactionCode ?? $"OTSP{payment.Id:D6}",
            StudentId = student.Id,
            StudentName = student.User?.FullName ?? "Học viên",
            StudentEmail = student.User?.Email ?? string.Empty,
            StudentPhone = student.User?.PhoneNumber,
            GuardianName = guardian?.FullName,
            TutorName = payment.Classroom?.Tutor?.User?.FullName ?? "Gia sư",
            TutorEmail = payment.Classroom?.Tutor?.User?.Email,
            ClassroomId = payment.ClassroomId,
            ClassroomName = payment.Classroom?.NameClass ?? "Lớp học OTSP",
            SubjectName = payment.Classroom?.Subject?.Name ?? "Môn học",
            GradeLevelName = payment.Classroom?.GradeLevel?.Name ?? "Cấp học",
            ClassroomType = payment.Classroom?.Type == ClassroomType.OneToOne ? "Kèm 1-1" : "Lớp nhóm"
        };
    }

    public async Task<bool> CheckPaymentStatusAsync(int paymentId, string userId, CancellationToken ct = default)
    {
        var student = await _db.StudentProfiles.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (student == null) return false;

        var cacheKey = $"payment_status_{paymentId}";
        if (_cache.TryGetValue(cacheKey, out bool cachedResult))
            return cachedResult;

        var payment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.StudentId == student.Id, ct);

        if (payment == null) return false;

        if (payment.Status == PaymentStatus.Success)
        {
            _cache.Set(cacheKey, true, TimeSpan.FromMinutes(5));
            return true;
        }

        // Polling SePay API (chỉ nếu có ApiKey riêng, không dùng SecretKey/whsec_)
        var apiKey = _config["SePay:ApiKey"] ?? _config["SePay:ApiToken"];
        if (!string.IsNullOrWhiteSpace(apiKey) && !apiKey.StartsWith("whsec_") && _httpClientFactory != null)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.Add("Authorization", $"Apikey {apiKey}");
                var response = await client.GetAsync(
                    $"https://my.sepay.vn/userapi/transactions/list?limit=20&reference_number=OTSP{paymentId}", ct);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    if (HasMatchingPaidTransaction(json, paymentId, payment.Amount))
                    {
                        // Xác nhận trong DB
                        var dbPayment = await _db.Payments
                            .Include(p => p.Invoice)
                            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);
                        if (dbPayment != null && dbPayment.Status != PaymentStatus.Success)
                        {
                            dbPayment.Status = PaymentStatus.Success;
                            dbPayment.PaidAt = DateTime.UtcNow;
                            if (dbPayment.Invoice == null)
                            {
                                _db.Invoices.Add(new Invoice
                                {
                                    PaymentId = dbPayment.Id,
                                    TotalAmount = dbPayment.Amount,
                                    IssuedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))
                                });
                            }
                            await _db.SaveChangesAsync(ct);
                            await TriggerPaymentSuccessNotificationsAsync(dbPayment, ct);
                        }
                        _cache.Set(cacheKey, true, TimeSpan.FromMinutes(5));
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SePay API polling encountered error for payment {PaymentId}", paymentId);
            }
        }

        return false;
    }

    public async Task<SePayCheckResultDto> CheckSePayApiConnectionAsync(CancellationToken ct = default)
    {
        var apiKey = _config["SePay:ApiKey"] ?? _config["SePay:ApiToken"];
        var secretKey = _config["SePay:SecretKey"];

        var result = new SePayCheckResultDto
        {
            BankName = _bankName,
            AccountNo = _accountNo,
            AccountName = _accountName,
            HasSecretKey = !string.IsNullOrWhiteSpace(secretKey),
            HasApiKey = !string.IsNullOrWhiteSpace(apiKey),
            IsConfigured = !string.IsNullOrWhiteSpace(_accountNo)
        };

        var tokenToTest = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : secretKey;
        if (string.IsNullOrWhiteSpace(tokenToTest))
        {
            result.Message = "Chưa cấu hình SePay:SecretKey hoặc SePay:ApiKey trong appsettings.json.";
            return result;
        }

        if (_httpClientFactory == null)
        {
            result.Message = "HttpClientFactory chưa được khởi tạo.";
            return result;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.Add("Authorization", $"Apikey {tokenToTest}");
            var response = await client.GetAsync("https://my.sepay.vn/userapi/transactions/list?limit=1", ct);
            result.HttpStatusCode = (int)response.StatusCode;
            result.CanConnectToSePay = response.IsSuccessStatusCode;

            result.Message = response.IsSuccessStatusCode
                ? "Kết nối API SePay thành công! Token hợp lệ."
                : response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? "SePay trả về 401 Unauthorized: Key hiện tại là Webhook Secret (whsec_...). Để polling lịch sử giao dịch, hãy tạo thêm 'API Key' tại my.sepay.vn và cấu hình vào SePay:ApiKey. Webhook nhận vẫn hoạt động bình thường."
                    : $"SePay trả về mã HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
        }
        catch (Exception ex)
        {
            result.CanConnectToSePay = false;
            result.Message = $"Không thể kết nối đến máy chủ SePay: {ex.Message}";
        }

        return result;
    }

    public async Task<bool> ConfirmManualPaymentMockAsync(int paymentId, string userId, CancellationToken ct = default)
    {
        var payment = await _db.Payments
            .Include(p => p.Invoice)
            .Include(p => p.Student).ThenInclude(s => s.User)
            .Include(p => p.Classroom).ThenInclude(c => c.Tutor).ThenInclude(t => t.User)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.Student.UserId == userId, ct);

        if (payment == null || payment.Status == PaymentStatus.Success || payment.Classroom?.Status != ClassroomStatus.Active)
            return false;

        payment.Status = PaymentStatus.Success;
        payment.PaidAt = DateTime.UtcNow;
        payment.GatewayTransactionCode = $"MOCK_{DateTime.UtcNow.Ticks}";

        if (payment.Invoice == null)
        {
            _db.Invoices.Add(new Invoice
            {
                PaymentId = payment.Id,
                TotalAmount = payment.Amount,
                IssuedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))
            });
        }

        await _db.SaveChangesAsync(ct);
        await TriggerPaymentSuccessNotificationsAsync(payment, ct);
        _logger.LogInformation("Payment {PaymentId} confirmed via Mock for User {UserId}", paymentId, userId);
        return true;
    }

    public async Task<bool> ProcessSePayWebhookAsync(SePayWebhookDto payload, CancellationToken ct = default)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.content))
        {
            _logger.LogWarning("SePay webhook received null or empty content payload");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(payload.transferType)
            && !payload.transferType.Equals("in", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("SePay webhook ignored non-incoming transfer type: {TransferType}", payload.transferType);
            return false;
        }

        // Chỉ nhận đúng mã OTSP{id} (không dính chữ số phía sau)
        var match = Regex.Match(payload.content, @"(?:^|[^A-Za-z0-9])OTSP(\d+)(?!\d)", RegexOptions.IgnoreCase);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var paymentId))
        {
            _logger.LogWarning("SePay webhook: Nội dung không chứa mã OTSP{{id}} hợp lệ.");
            return false;
        }

        var payment = await _db.Payments
            .Include(p => p.Invoice)
            .Include(p => p.Student).ThenInclude(s => s.User)
            .Include(p => p.Classroom).ThenInclude(c => c.Tutor).ThenInclude(t => t.User)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

        if (payment == null)
        {
            _logger.LogWarning("Payment id {PaymentId} from SePay webhook not found in database", paymentId);
            return false;
        }

        if (payment.Status == PaymentStatus.Success)
        {
            _cache.Set($"payment_status_{payment.Id}", true, TimeSpan.FromMinutes(5));
            _logger.LogInformation("Payment id {PaymentId} already marked as Success", paymentId);
            return true;
        }

        // Kiểm tra số tiền chuyển khoản
        if (payload.transferAmount <= 0 || payload.transferAmount < payment.Amount)
        {
            _logger.LogWarning("Payment {PaymentId}: Transfer amount {Amount} < required {Required}",
                paymentId, payload.transferAmount, payment.Amount);
            return false;
        }

        var txCode = !string.IsNullOrWhiteSpace(payload.referenceCode)
            ? payload.referenceCode.Trim()
            : payload.id.ToString();

        // Chống dùng lại cùng 1 mã giao dịch ngân hàng cho nhiều khoản thanh toán khác nhau
        if (!string.IsNullOrWhiteSpace(txCode) && txCode != "0")
        {
            var duplicateTxExists = await _db.Payments.AsNoTracking()
                .AnyAsync(p => p.Id != payment.Id
                            && p.Status == PaymentStatus.Success
                            && p.GatewayTransactionCode == txCode, ct);
            if (duplicateTxExists)
            {
                _logger.LogWarning("SePay webhook rejected duplicate GatewayTransactionCode {TxCode} for Payment {PaymentId}.", txCode, paymentId);
                return false;
            }
        }

        payment.Status = PaymentStatus.Success;
        payment.PaidAt = DateTime.UtcNow;
        payment.GatewayTransactionCode = txCode;

        if (payment.Invoice == null)
        {
            _db.Invoices.Add(new Invoice
            {
                PaymentId = payment.Id,
                TotalAmount = payment.Amount,
                IssuedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))
            });
        }

        await _db.SaveChangesAsync(ct);
        _cache.Set($"payment_status_{payment.Id}", true, TimeSpan.FromMinutes(5));
        await TriggerPaymentSuccessNotificationsAsync(payment, ct);
        _logger.LogInformation("Payment {PaymentId} marked as Success from SePay Webhook. Amount: {Amount}", paymentId, payload.transferAmount);
        return true;
    }

    private static bool HasMatchingPaidTransaction(string json, int paymentId, decimal requiredAmount)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("transactions", out var transactions)
                || transactions.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var codePattern = $@"(?:^|[^A-Za-z0-9])OTSP{paymentId}(?!\d)";
            foreach (var tx in transactions.EnumerateArray())
            {
                var content = tx.TryGetProperty("transaction_content", out var contentEl)
                    ? contentEl.GetString()
                    : null;
                var reference = tx.TryGetProperty("reference_number", out var refEl)
                    ? refEl.GetString()
                    : null;

                var hasCode = (!string.IsNullOrWhiteSpace(content) && Regex.IsMatch(content, codePattern, RegexOptions.IgnoreCase))
                    || (!string.IsNullOrWhiteSpace(reference) && Regex.IsMatch(reference, codePattern, RegexOptions.IgnoreCase));
                if (!hasCode) continue;

                decimal amountIn = 0;
                if (tx.TryGetProperty("amount_in", out var amountEl))
                {
                    if (amountEl.ValueKind == JsonValueKind.Number)
                        amountEl.TryGetDecimal(out amountIn);
                    else if (amountEl.ValueKind == JsonValueKind.String)
                        decimal.TryParse(amountEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out amountIn);
                }

                if (amountIn > 0 && amountIn >= requiredAmount)
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static StudentPaymentItemDto MapToItemDto(Payment p)
    {
        return new StudentPaymentItemDto
        {
            PaymentId = p.Id,
            ClassroomId = p.ClassroomId,
            ClassroomName = p.Classroom?.NameClass ?? string.Empty,
            SubjectName = p.Classroom?.Subject?.Name ?? string.Empty,
            GradeLevelName = p.Classroom?.GradeLevel?.Name ?? string.Empty,
            TutorName = p.Classroom?.Tutor?.User?.FullName ?? "Gia sư",
            TutorAvatarUrl = p.Classroom?.Tutor?.User?.AvatarUrl,
            Amount = p.Amount,
            Status = p.Status,
            PaidAt = p.PaidAt,
            InvoiceId = p.Invoice?.Id,
            ClassroomType = p.Classroom?.Type ?? ClassroomType.OneToOne,
            SessionsPerWeek = p.Classroom?.SessionsPerWeek ?? 2,
            HoursPerSession = p.Classroom?.HoursPerSession ?? 2.0,
            StartDate = p.Classroom?.StartDate
        };
    }

    private async Task TriggerPaymentSuccessNotificationsAsync(Payment payment, CancellationToken ct)
    {
        try
        {
            var p = await _db.Payments
                .Include(x => x.Student).ThenInclude(s => s.User)
                .Include(x => x.Classroom).ThenInclude(c => c.Tutor).ThenInclude(t => t.User)
                .Include(x => x.Classroom).ThenInclude(c => c.Subject)
                .FirstOrDefaultAsync(x => x.Id == payment.Id, ct) ?? payment;

            var studentUserId = p.Student?.UserId ?? p.Student?.User?.Id;
            var studentEmail = p.Student?.User?.Email;
            var studentName = p.Student?.User?.FullName ?? "Học viên";

            var tutorUserId = p.Classroom?.Tutor?.UserId ?? p.Classroom?.Tutor?.User?.Id;
            var tutorEmail = p.Classroom?.Tutor?.User?.Email;
            var tutorName = p.Classroom?.Tutor?.User?.FullName ?? "Gia sư";

            var className = p.Classroom?.NameClass ?? "Lớp học OTSP";
            var subjectName = p.Classroom?.Subject?.Name ?? "Môn học";
            var amountStr = $"{p.Amount:N0} đ";

            // 1. Thêm chuông thông báo In-App cho Học viên
            if (!string.IsNullOrEmpty(studentUserId))
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = studentUserId,
                    Title = "Thanh toán học phí thành công",
                    Content = $"Bạn đã thanh toán thành công {amountStr} cho lớp {className} ({subjectName}). Quyền vào phòng học trực tuyến đã được kích hoạt! [url:/student/payments]",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }

            // 2. Thêm chuông thông báo In-App cho Gia sư
            if (!string.IsNullOrEmpty(tutorUserId))
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = tutorUserId,
                    Title = "Học viên đã nộp học phí",
                    Content = $"Học viên {studentName} vừa thanh toán thành công {amountStr} học phí cho lớp {className}. [url:/tutor/income]",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }

            await _db.SaveChangesAsync(ct);

            // 3. Gửi Email Biên lai điện tử về Gmail cho Học viên qua hàng đợi nền
            if (!string.IsNullOrEmpty(studentEmail) && (_emailQueue != null || _emailSender != null))
            {
                var paidTimeStr = DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm:ss");
                var (subject, bodyHtml) = NotificationEmailTemplates.ForPaymentStudentReceipt(
                    p.Id, studentName, className, subjectName, tutorName, amountStr, paidTimeStr);
                if (_emailQueue != null)
                {
                    _emailQueue.Enqueue(new EmailQueueItemDto(studentEmail, subject, bodyHtml));
                }
                else if (_emailSender != null)
                {
                    await _emailSender.SendNotificationEmailAsync(studentEmail, subject, bodyHtml, ct);
                }
            }

            // 4. Gửi Email thông báo cho Gia sư qua hàng đợi nền
            if (!string.IsNullOrEmpty(tutorEmail) && (_emailQueue != null || _emailSender != null))
            {
                var (subject, bodyHtml) = NotificationEmailTemplates.ForPaymentTutorNotice(
                    tutorName, studentName, amountStr, className);
                if (_emailQueue != null)
                {
                    _emailQueue.Enqueue(new EmailQueueItemDto(tutorEmail, subject, bodyHtml));
                }
                else if (_emailSender != null)
                {
                    await _emailSender.SendNotificationEmailAsync(tutorEmail, subject, bodyHtml, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi gửi thông báo thanh toán cho Payment {PaymentId}", payment.Id);
        }
    }
}
