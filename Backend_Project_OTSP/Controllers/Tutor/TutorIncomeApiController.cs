using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using System.ComponentModel.DataAnnotations;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Controllers.Api.Tutor;

[ApiController]
[Route("api/tutor/income")]
[Authorize(Roles = "Tutor")]
public class TutorIncomeApiController : ControllerBase
{
    private const int VietnamUtcOffsetHours = 7;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public TutorIncomeApiController(ApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetIncome([FromQuery] string? month, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var nowUtc = DateTime.UtcNow;
        var nowVietnam = nowUtc.AddHours(VietnamUtcOffsetHours);
        var selectedDate = nowVietnam;
        if (!string.IsNullOrWhiteSpace(month)
            && DateTime.TryParseExact(month, "MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedMonth))
        {
            selectedDate = parsedMonth;
        }

        var selectedMonth = $"{selectedDate.Month:D2}/{selectedDate.Year}";
        var availableMonths = Enumerable.Range(0, 4)
            .Select(offset => nowVietnam.AddMonths(-offset))
            .Select(date => $"{date.Month:D2}/{date.Year}")
            .ToList();
        if (!availableMonths.Contains(selectedMonth))
            availableMonths.Insert(0, selectedMonth);

        var tutor = await _db.TutorProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => new
            {
                profile.Id,
                FullName = profile.User.FullName,
                CitizenId = profile.User.CitizenIdNumber,
                profile.BankName,
                profile.BankAccountNumber,
                profile.BankAccountHolder,
                profile.BankCitizenId,
                Classrooms = profile.Classrooms.Select(classroom => new
                {
                    classroom.Fee,
                    classroom.SessionsPerWeek,
                    Sessions = classroom.ClassSessions.Select(session => new
                    {
                        session.Status,
                        session.ScheduledAt
                    }).ToList()
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (tutor == null)
            return NotFound(new { message = "Không tìm thấy hồ sơ gia sư." });

        var completedSessions = 0;
        var pendingConfirmSessions = 0;
        var upcomingSessions = 0;
        var totalSessions = 0;
        decimal totalTuition = 0;

        foreach (var classroom in tutor.Classrooms)
        {
            var expectedForClass = classroom.Sessions.Count > 0
                ? classroom.Sessions.Count
                : Math.Max(1, classroom.SessionsPerWeek * 4);
            var feePerSession = Math.Round(classroom.Fee / expectedForClass, 0);
            var sessionsInMonth = classroom.Sessions
                .Where(session =>
                {
                    var localTime = session.ScheduledAt.AddHours(VietnamUtcOffsetHours);
                    return localTime.Month == selectedDate.Month && localTime.Year == selectedDate.Year;
                })
                .ToList();

            if (sessionsInMonth.Count == 0)
            {
                totalSessions += expectedForClass;
                upcomingSessions += expectedForClass;
                continue;
            }

            totalSessions += sessionsInMonth.Count;
            foreach (var session in sessionsInMonth)
            {
                if (session.Status == SessionStatus.Done)
                {
                    completedSessions++;
                    totalTuition += feePerSession;
                }
                else if (session.Status == SessionStatus.Scheduled)
                {
                    if (session.ScheduledAt < nowUtc)
                        pendingConfirmSessions++;
                    else
                        upcomingSessions++;
                }
            }
        }

        var platformCommission = Math.Round(totalTuition * 0.20m, 0);
        var tutorPayout = totalTuition - platformCommission;
        var averageFeePerSession = completedSessions > 0
            ? Math.Round(totalTuition / completedSessions, 0)
            : totalSessions > 0 && tutor.Classrooms.Count > 0
                ? Math.Round(tutor.Classrooms.Sum(classroom => classroom.Fee) / Math.Max(1, totalSessions), 0)
                : 0m;
        var completionPercentage = totalSessions > 0
            ? Math.Round((double)completedSessions / totalSessions * 100, 1)
            : 0;

        var reconciliations = await _db.Reconciliations
            .AsNoTracking()
            .Where(item => item.TutorId == tutor.Id)
            .OrderByDescending(item => item.CreatedDate)
            .Take(12)
            .Select(item => new { item.Period, item.TotalRevenue })
            .ToListAsync(cancellationToken);
        var payoutHistories = reconciliations.Select(item =>
        {
            var commission = Math.Round(item.TotalRevenue * 0.20m, 0);
            return new
            {
                item.Period,
                tuitionFee = item.TotalRevenue,
                commissionFee = commission,
                tutorReceive = item.TotalRevenue - commission,
                status = "Đã chuyển"
            };
        });

        var defaultBank = _configuration["VietQR:BankId"] == "970422" ? "MBBank" : "Vietcombank";
        var defaultAccountNumber = _configuration["VietQR:AccountNo"] ?? "0984391940";
        var defaultAccountHolder = _configuration["VietQR:AccountName"]
            ?? tutor.FullName?.ToUpperInvariant()
            ?? "TONG NGOC TUAN";
        var bankAccount = CreateBankAccount(
            tutor.BankName ?? Request.Cookies["otsp_tutor_bank_name"] ?? defaultBank,
            tutor.BankAccountNumber ?? Request.Cookies["otsp_tutor_bank_acc"] ?? defaultAccountNumber,
            tutor.BankAccountHolder ?? Request.Cookies["otsp_tutor_bank_holder"] ?? defaultAccountHolder,
            tutor.BankCitizenId ?? Request.Cookies["otsp_tutor_bank_cccd"] ?? tutor.CitizenId ?? "079*********");

        return Ok(new
        {
            selectedMonth,
            availableMonths,
            totalSessions,
            completedSessions,
            pendingConfirmSessions,
            upcomingSessions,
            totalTeachingHours = completedSessions * 2.0,
            averageFeePerSession,
            totalTuition,
            platformCommission,
            tutorPayout,
            completionPercentage,
            payoutHistories,
            bankAccount
        });
    }

    [HttpPost("bank-account")]
    public async Task<IActionResult> SaveBankAccount([FromBody] TutorIncomeBankAccountRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var supportedBanks = new[]
        {
            "MBBank", "Vietcombank", "Techcombank", "ACB", "VPBank",
            "BIDV", "Agribank", "TPBank", "Sacombank", "OCB"
        };
        var bankName = request.BankName?.Trim();
        var accountNumber = request.AccountNumber?.Trim();
        var accountHolder = request.AccountHolder?.Trim();
        var citizenId = request.CitizenId?.Trim() ?? "";

        if (bankName == null || !supportedBanks.Contains(bankName, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "Vui lòng chọn ngân hàng hợp lệ." });
        if (string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Length > 50
            || string.IsNullOrWhiteSpace(accountHolder) || accountHolder.Length > 200
            || citizenId.Length > 30)
        {
            return BadRequest(new { message = "Vui lòng kiểm tra lại thông tin tài khoản nhận tiền." });
        }

        var canonicalBankName = supportedBanks.First(name =>
            string.Equals(name, bankName, StringComparison.OrdinalIgnoreCase));

        // Lưu trực tiếp vào Database cho Gia sư để Admin có thể đọc chính xác
        var tutorProfile = await _db.TutorProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (tutorProfile != null)
        {
            tutorProfile.BankName = canonicalBankName;
            tutorProfile.BankAccountNumber = accountNumber;
            tutorProfile.BankAccountHolder = accountHolder.ToUpperInvariant();
            tutorProfile.BankCitizenId = citizenId;
            await _db.SaveChangesAsync(ct);
        }

        var cookieOptions = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/"
        };

        Response.Cookies.Append("otsp_tutor_bank_name", canonicalBankName, cookieOptions);
        Response.Cookies.Append("otsp_tutor_bank_acc", accountNumber, cookieOptions);
        Response.Cookies.Append("otsp_tutor_bank_holder", accountHolder.ToUpperInvariant(), cookieOptions);
        if (string.IsNullOrWhiteSpace(citizenId))
            Response.Cookies.Delete("otsp_tutor_bank_cccd", cookieOptions);
        else
            Response.Cookies.Append("otsp_tutor_bank_cccd", citizenId, cookieOptions);

        return Ok(new
        {
            message = "Đã lưu thông tin tài khoản và tạo mã VietQR thành công.",
            bankAccount = CreateBankAccount(canonicalBankName, accountNumber, accountHolder.ToUpperInvariant(), citizenId)
        });
    }

    private static object CreateBankAccount(string bankName, string accountNumber, string accountHolder, string citizenId)
    {
        var bankCode = MapBankNameToCode(bankName);
        var qrImageUrl = string.IsNullOrWhiteSpace(accountNumber)
            ? ""
            : $"https://img.vietqr.io/image/{bankCode}-{Uri.EscapeDataString(accountNumber)}-compact.png?accountName={Uri.EscapeDataString(accountHolder)}";

        return new
        {
            bankName,
            accountNumber,
            accountHolder,
            citizenId,
            qrImageUrl,
            isUpdated = true
        };
    }

    private static string MapBankNameToCode(string bankName)
    {
        var normalizedName = bankName.Trim().ToLowerInvariant();
        if (normalizedName.Contains("mb") || normalizedName.Contains("quân đội")) return "MB";
        if (normalizedName.Contains("vietcombank")) return "vietcombank";
        if (normalizedName.Contains("techcombank")) return "techcombank";
        if (normalizedName == "acb") return "ACB";
        if (normalizedName.Contains("vpbank")) return "VPB";
        if (normalizedName.Contains("bidv")) return "BIDV";
        if (normalizedName.Contains("agribank")) return "agribank";
        if (normalizedName.Contains("tpbank")) return "tpbank";
        if (normalizedName.Contains("sacombank")) return "sacombank";
        if (normalizedName.Contains("ocb")) return "OCB";
        return "MB";
    }
}

public sealed class TutorIncomeBankAccountRequest
{
    [Required]
    public string? BankName { get; set; }

    [Required]
    public string? AccountNumber { get; set; }

    [Required]
    public string? AccountHolder { get; set; }

    public string? CitizenId { get; set; }
}
