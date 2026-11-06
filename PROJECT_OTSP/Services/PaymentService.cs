using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos.Payment;
using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Services;

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PaymentService> _logger;
    private readonly IHttpClientFactory? _httpClientFactory;

    private readonly string _bankName;
    private readonly string _bankId;
    private readonly string _accountNo;
    private readonly string _accountName;

    public PaymentService(
        ApplicationDbContext db,
        IConfiguration config,
        IMemoryCache cache,
        ILogger<PaymentService> logger,
        IHttpClientFactory? httpClientFactory = null)
    {
        _db = db;
        _config = config;
        _cache = cache;
        _logger = logger;
        _httpClientFactory = httpClientFactory;

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

        // Tự động kiểm tra và tạo khoản thanh toán cho các lớp học mà học viên đã tham gia nhưng chưa có bản ghi Payment
        var enrolledClassroomIds = await _db.ClassroomStudents
            .Where(cs => cs.StudentId == student.Id)
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
                    // Lớp học thử hoặc lớp miễn phí thì KHÔNG tạo hóa đơn thanh toán
                    if (cls.Type == ClassroomType.OneToOne && (cls.Fee <= 0 || cls.Name.StartsWith("Học thử")))
                    {
                        continue;
                    }

                    var fee = cls.Fee;
                    if (fee <= 0)
                    {
                        var rawHours = cls.HoursPerSession >= 10.0 ? cls.HoursPerSession / 10.0 : (cls.HoursPerSession > 0 ? cls.HoursPerSession : 2.0);
                        var hours = (decimal)rawHours;
                        var sessions = Math.Clamp(cls.SessionsPerWeek > 0 ? cls.SessionsPerWeek : 2, 1, 3);
                        fee = 150000m * hours * sessions * 4;
                        cls.Fee = fee;
                    }

                    _db.Payments.Add(new Payment
                    {
                        StudentId = student.Id,
                        ClassroomId = cls.Id,
                        Amount = fee,
                        Method = "VietQR",
                        Status = PaymentStatus.Pending
                    });
                }

                await _db.SaveChangesAsync(ct);
            }
        }

        var isMinor = student.User?.DateOfBirth.HasValue == true
            && student.User.DateOfBirth.Value.AddYears(18) > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var guardianName = student.Guardians?.FirstOrDefault()?.FullName;

        // Truy vấn danh sách Payments của học viên
        var payments = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Classroom).ThenInclude(c => c.Subject)
            .Include(p => p.Classroom).ThenInclude(c => c.GradeLevel)
            .Include(p => p.Classroom).ThenInclude(c => c.Tutor).ThenInclude(t => t.User)
            .Include(p => p.Invoice)
            .Where(p => p.StudentId == student.Id)
            .OrderByDescending(p => p.Id)
            .ToListAsync(ct);

        var pendingItems = payments
            .Where(p => p.Status == PaymentStatus.Pending)
            .Select(p => MapToItemDto(p))
            .ToList();

        var historyItems = payments
            .Where(p => p.Status == PaymentStatus.Success)
            .Select(p => MapToItemDto(p))
            .ToList();

        return new StudentPaymentOverviewVm
        {
            IsMinor = isMinor,
            GuardianName = guardianName,
            PendingPayments = pendingItems,
            HistoryPayments = historyItems
        };
    }

    public async Task<PaymentCheckoutVm?> GetPaymentCheckoutAsync(int paymentId, string userId, CancellationToken ct = default)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Student)
            .Include(p => p.Classroom).ThenInclude(c => c.Subject)
            .Include(p => p.Classroom).ThenInclude(c => c.GradeLevel)
            .Include(p => p.Classroom).ThenInclude(c => c.Tutor).ThenInclude(t => t.User)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.Student.UserId == userId, ct);

        if (payment == null)
            return null;

        var transferContent = $"OTSP{payment.Id}";
        var amountLong = (long)payment.Amount;

        // Sinh link QR chuẩn SePay VietQR
        var qrUrl = $"https://qr.sepay.vn/img?acc={_accountNo}&bank={_bankName}&amount={amountLong}&des={transferContent}";

        return new PaymentCheckoutVm
        {
            PaymentId = payment.Id,
            ClassroomId = payment.ClassroomId,
            ClassroomName = payment.Classroom.Name,
            TutorName = payment.Classroom.Tutor?.User?.FullName ?? "Gia sư",
            SubjectGrade = $"{payment.Classroom.Subject.Name} - {payment.Classroom.GradeLevel.Name}",
            Amount = payment.Amount,
            BankName = _bankName,
            AccountNo = _accountNo,
            AccountName = _accountName,
            TransferContent = transferContent,
            QrImageUrl = qrUrl,
            Status = payment.Status,
            IsPaid = payment.Status == PaymentStatus.Success
        };
    }

    public async Task<bool> CheckPaymentStatusAsync(int paymentId, string userId, CancellationToken ct = default)
    {
        var payment = await _db.Payments
            .Include(p => p.Invoice)
            .Include(p => p.Student)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.Student.UserId == userId, ct);

        if (payment == null)
            return false;

        if (payment.Status == PaymentStatus.Success)
            return true;

        // Nếu có ApiKey và HttpClient, chủ động gọi SePay API để kiểm tra giao dịch mới nhất (hỗ trợ cả môi trường Localhost)
        var apiKey = _config["SePay:ApiKey"] ?? _config["SePay:ApiToken"];
        if (!string.IsNullOrWhiteSpace(apiKey) && _httpClientFactory != null)
        {
            var cacheKey = $"sepay_poll_{paymentId}";
            if (!_cache.TryGetValue(cacheKey, out bool _))
            {
                _cache.Set(cacheKey, true, TimeSpan.FromSeconds(2));
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(5);
                    client.DefaultRequestHeaders.Add("Authorization", $"Apikey {apiKey}");
                    var response = await client.GetAsync("https://my.sepay.vn/userapi/transactions/list?limit=20", ct);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(ct);
                        var targetKeyword = $"OTSP{paymentId}";
                        if (json.Contains(targetKeyword, StringComparison.OrdinalIgnoreCase))
                        {
                            payment.Status = PaymentStatus.Success;
                            payment.PaidAt = DateTime.UtcNow;
                            payment.GatewayTransactionCode = $"SEPAY_POLL_{DateTime.UtcNow.Ticks}";

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
                            _logger.LogInformation("Payment {PaymentId} auto-detected as paid via SePay API polling for user {UserId}", paymentId, userId);
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SePay API polling encountered error for payment {PaymentId}", paymentId);
                }
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

            if (response.IsSuccessStatusCode)
            {
                result.Message = "Kết nối API SePay thành công! Token hợp lệ.";
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                result.Message = "SePay trả về 401 Unauthorized: Chuỗi hiện tại trong appsettings là Webhook Secret (whsec_...). Để hệ thống chủ động gọi API SePay lấy lịch sử giao dịch (polling), bạn cần tạo thêm 'API Key' trên my.sepay.vn và cấu hình vào SePay:ApiKey. Webhook vẫn hoạt động bình thường khi SePay bắn dữ liệu về.";
            }
            else
            {
                result.Message = $"SePay trả về mã HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
            }
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
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.Student.UserId == userId, ct);

        if (payment == null || payment.Status == PaymentStatus.Success)
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
        _logger.LogInformation("Payment {PaymentId} confirmed successfully via Mock/Demo for User {UserId}", paymentId, userId);
        return true;
    }

    public async Task<bool> ProcessSePayWebhookAsync(SePayWebhookDto payload, CancellationToken ct = default)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.content))
        {
            _logger.LogWarning("SePay webhook received null or empty content payload");
            return false;
        }

        // Tìm mã OTSP123 hoặc số ID giao dịch trong nội dung chuyển khoản
        var match = Regex.Match(payload.content, @"OTSP(\d+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            // Thử tìm số độc lập
            match = Regex.Match(payload.content, @"\b(\d+)\b");
        }

        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var paymentId))
        {
            _logger.LogWarning("SePay webhook content does not contain a valid payment id: {Content}", payload.content);
            return false;
        }

        var payment = await _db.Payments
            .Include(p => p.Invoice)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

        if (payment == null)
        {
            _logger.LogWarning("Payment id {PaymentId} from SePay webhook not found in database", paymentId);
            return false;
        }

        if (payment.Status == PaymentStatus.Success)
        {
            _logger.LogInformation("Payment id {PaymentId} already marked as Success previously", paymentId);
            return true;
        }

        // Kiểm tra số tiền chuyển khoản
        if (payload.transferAmount < payment.Amount)
        {
            _logger.LogWarning("Payment {PaymentId}: Transfer amount {Amount} is less than required {Required}",
                paymentId, payload.transferAmount, payment.Amount);
            return false;
        }

        payment.Status = PaymentStatus.Success;
        payment.PaidAt = DateTime.UtcNow;
        payment.GatewayTransactionCode = payload.referenceCode ?? payload.id.ToString();

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
        _logger.LogInformation("Payment {PaymentId} marked as Success from SePay Webhook. Amount: {Amount}", paymentId, payload.transferAmount);
        return true;
    }

    private static StudentPaymentItemDto MapToItemDto(Payment p)
    {
        return new StudentPaymentItemDto
        {
            PaymentId = p.Id,
            ClassroomId = p.ClassroomId,
            ClassroomName = p.Classroom?.Name ?? string.Empty,
            SubjectName = p.Classroom?.Subject?.Name ?? string.Empty,
            GradeLevelName = p.Classroom?.GradeLevel?.Name ?? string.Empty,
            TutorName = p.Classroom?.Tutor?.User?.FullName ?? "Gia sư",
            TutorAvatarUrl = p.Classroom?.Tutor?.User?.AvatarUrl,
            Amount = p.Amount,
            Status = p.Status,
            PaidAt = p.PaidAt,
            InvoiceId = p.Invoice?.Id,
            ClassroomType = p.Classroom?.Type ?? ClassroomType.OneToOne
        };
    }
}