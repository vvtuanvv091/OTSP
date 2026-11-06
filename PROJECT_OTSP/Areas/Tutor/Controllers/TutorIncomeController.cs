using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Areas.Tutor.Controllers;

[Area("Tutor")]
[Authorize(Roles = "Tutor")]
public class TutorIncomeController : TutorAreaController
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _config;

    public TutorIncomeController(ApplicationDbContext dbContext, IConfiguration config)
    {
        _dbContext = dbContext;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? month, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        // 1. Tối ưu hóa truy vấn: Chỉ select các trường cần thiết, dùng AsNoTracking
        var tutor = await _dbContext.TutorProfiles
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => new
            {
                t.Id,
                FullName = t.User.FullName,
                CitizenId = t.User.CitizenIdNumber,
                Classrooms = t.Classrooms.Select(c => new
                {
                    c.Id,
                    c.Fee,
                    c.SessionsPerWeek,
                    Sessions = c.ClassSessions.Select(s => new
                    {
                        s.Id,
                        s.Status,
                        s.ScheduledAt
                    }).ToList()
                }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (tutor == null)
            return NotFound();

        // 2. Xử lý danh sách tháng để lọc
        var nowVn = DateTime.UtcNow.AddHours(7);
        var currentMonthStr = $"{nowVn.Month:D2}/{nowVn.Year}";
        var selectedMonth = string.IsNullOrWhiteSpace(month) ? currentMonthStr : month.Trim();

        var availableMonths = new List<string>();
        for (int i = 0; i < 4; i++)
        {
            var mDate = nowVn.AddMonths(-i);
            availableMonths.Add($"{mDate.Month:D2}/{mDate.Year}");
        }
        if (!availableMonths.Contains(selectedMonth))
        {
            availableMonths.Insert(0, selectedMonth);
        }

        // Tách tháng và năm từ selectedMonth (ví dụ: "10/2026")
        int filterMonth = nowVn.Month;
        int filterYear = nowVn.Year;
        var parts = selectedMonth.Split('/');
        if (parts.Length == 2 && int.TryParse(parts[0], out var pm) && int.TryParse(parts[1], out var py))
        {
            filterMonth = pm;
            filterYear = py;
        }

        // 3. Tối ưu thuật toán thống kê buổi học trong 1 vòng lặp O(N)
        int completed = 0;
        int pending = 0;
        int upcoming = 0;
        int totalExpected = 0;
        decimal totalTuition = 0m;
        var utcNow = DateTime.UtcNow;

        foreach (var cls in tutor.Classrooms)
        {
            var sessions = cls.Sessions;
            var classTotalExpected = sessions.Count > 0 ? sessions.Count : Math.Max(1, cls.SessionsPerWeek * 4);
            var feePerSession = classTotalExpected > 0 ? Math.Round(cls.Fee / classTotalExpected, 0) : 0m;

            // Lọc các buổi thuộc tháng được chọn (hoặc tất cả nếu lớp diễn ra trong tháng đó)
            var sessionsInMonth = sessions.Where(s =>
            {
                var local = s.ScheduledAt.AddHours(7);
                return local.Month == filterMonth && local.Year == filterYear;
            }).ToList();

            if (sessionsInMonth.Count > 0)
            {
                totalExpected += sessionsInMonth.Count;
                foreach (var s in sessionsInMonth)
                {
                    if (s.Status == SessionStatus.Done)
                    {
                        completed++;
                        totalTuition += feePerSession;
                    }
                    else if (s.Status == SessionStatus.Scheduled)
                    {
                        if (s.ScheduledAt < utcNow) pending++;
                        else upcoming++;
                    }
                }
            }
            else
            {
                // Nếu chưa sinh ClassSessions riêng cho tháng đó, cộng dồn lịch dự kiến
                totalExpected += classTotalExpected;
                upcoming += classTotalExpected;
            }
        }

        // Thuật toán tính hoa hồng 20% và thu nhập 80%
        decimal platformCommission = Math.Round(totalTuition * 0.20m, 0);
        decimal tutorPayout = totalTuition - platformCommission;

        decimal averageFee = completed > 0
            ? Math.Round(totalTuition / completed, 0)
            : (totalExpected > 0 && tutor.Classrooms.Count > 0 ? Math.Round(tutor.Classrooms.Sum(c => c.Fee) / Math.Max(1, totalExpected), 0) : 0m);

        double totalHours = completed * 2.0;
        double completionPercent = totalExpected > 0
            ? Math.Round((double)completed / totalExpected * 100.0, 1)
            : 0;

        // 4. Lấy lịch sử quyết toán thực tế từ bảng Reconciliation
        var dbReconciliations = await _dbContext.Reconciliations
            .AsNoTracking()
            .Where(r => r.TutorId == tutor.Id)
            .OrderByDescending(r => r.CreatedDate)
            .Take(12)
            .Select(r => new TutorPayoutHistoryItem
            {
                Period = r.Period,
                TuitionFee = r.TotalRevenue,
                CommissionFee = Math.Round(r.TotalRevenue * 0.20m, 0),
                TutorReceive = r.TotalRevenue - Math.Round(r.TotalRevenue * 0.20m, 0),
                Status = "Đã chuyển"
            })
            .ToListAsync(ct);

        // 5. Cấu hình Tài khoản nhận tiền & sinh mã QR VietQR tự động
        var defaultBank = _config["VietQR:BankId"] == "970422" ? "MBBank" : "Vietcombank";
        var defaultAccNo = _config["VietQR:AccountNo"] ?? "0984391940";
        var defaultHolder = _config["VietQR:AccountName"] ?? (!string.IsNullOrWhiteSpace(tutor.FullName) ? tutor.FullName.ToUpper() : "TONG NGOC TUAN");
        var defaultCccd = !string.IsNullOrWhiteSpace(tutor.CitizenId) ? tutor.CitizenId : "079*********";

        var savedBank = Request.Cookies["otsp_tutor_bank_name"] ?? defaultBank;
        var savedAccNumber = Request.Cookies["otsp_tutor_bank_acc"] ?? defaultAccNo;
        var savedHolder = Request.Cookies["otsp_tutor_bank_holder"] ?? defaultHolder;
        var savedCccd = Request.Cookies["otsp_tutor_bank_cccd"] ?? defaultCccd;

        // Sinh link mã QR VietQR hợp lệ không cần package bên ngoài
        var bankCode = MapBankNameToCode(savedBank);
        var qrImageUrl = $"https://img.vietqr.io/image/{bankCode}-{savedAccNumber}-compact.png?accountName={Uri.EscapeDataString(savedHolder)}";

        var vm = new TutorIncomeVm
        {
            SelectedMonth = selectedMonth,
            AvailableMonths = availableMonths,
            TotalSessions = totalExpected,
            CompletedSessions = completed,
            PendingConfirmSessions = pending,
            UpcomingSessions = upcoming,
            TotalTeachingHours = totalHours,
            AverageFeePerSession = averageFee,
            TotalTuition = totalTuition,
            PlatformCommission = platformCommission,
            TutorPayout = tutorPayout,
            CompletionPercentage = completionPercent,
            PayoutHistories = dbReconciliations,
            BankAccount = new TutorBankAccountDto
            {
                BankName = savedBank,
                AccountNumber = savedAccNumber,
                AccountHolder = savedHolder,
                CitizenId = savedCccd,
                QrImageUrl = qrImageUrl,
                IsUpdated = true
            }
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveBankAccount(TutorBankAccountDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.AccountNumber) || string.IsNullOrWhiteSpace(dto.AccountHolder))
        {
            TempData["Error"] = "Vui lòng nhập đầy đủ số tài khoản và tên chủ tài khoản.";
            return RedirectToAction(nameof(Index));
        }

        var cookieOptions = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        };

        Response.Cookies.Append("otsp_tutor_bank_name", dto.BankName ?? "MBBank", cookieOptions);
        Response.Cookies.Append("otsp_tutor_bank_acc", dto.AccountNumber.Trim(), cookieOptions);
        Response.Cookies.Append("otsp_tutor_bank_holder", dto.AccountHolder.Trim().ToUpper(), cookieOptions);
        if (!string.IsNullOrWhiteSpace(dto.CitizenId))
        {
            Response.Cookies.Append("otsp_tutor_bank_cccd", dto.CitizenId.Trim(), cookieOptions);
        }

        TempData["Success"] = "Đã lưu thông tin tài khoản và tạo mã VietQR thành công!";
        return RedirectToAction(nameof(Index));
    }

    private static string MapBankNameToCode(string bankName)
    {
        var n = (bankName ?? "").Trim().ToLowerInvariant();
        if (n.Contains("mb") || n.Contains("quân đội") || n.Contains("mbbank")) return "MB";
        if (n.Contains("vietcombank") || n == "vcb") return "vietcombank";
        if (n.Contains("techcombank") || n == "tcb") return "techcombank";
        if (n.Contains("acb")) return "ACB";
        if (n.Contains("vpbank") || n == "vpb") return "VPB";
        if (n.Contains("bidv")) return "BIDV";
        if (n.Contains("agribank") || n == "vba") return "agribank";
        if (n.Contains("tpbank") || n == "tpb") return "tpbank";
        if (n.Contains("sacombank") || n == "stb") return "sacombank";
        if (n.Contains("ocb")) return "OCB";
        return "MB";
    }
}

// ==================== VIEWMODELS TỰ QUẢN LÝ TẠI CONTROLLER (KHÔNG CẦN THÊM FILE DTOS) ====================
public class TutorIncomeVm
{
    public string SelectedMonth { get; set; } = "";
    public List<string> AvailableMonths { get; set; } = new();

    public int TotalSessions { get; set; }
    public int CompletedSessions { get; set; }
    public int PendingConfirmSessions { get; set; }
    public int UpcomingSessions { get; set; }
    public double TotalTeachingHours { get; set; }

    public decimal AverageFeePerSession { get; set; }
    public decimal TotalTuition { get; set; }
    public decimal PlatformCommission { get; set; }
    public decimal TutorPayout { get; set; }
    public double CompletionPercentage { get; set; }

    public List<TutorPayoutHistoryItem> PayoutHistories { get; set; } = new();
    public TutorBankAccountDto BankAccount { get; set; } = new();
}

public class TutorPayoutHistoryItem
{
    public string Period { get; set; } = "";
    public decimal TuitionFee { get; set; }
    public decimal CommissionFee { get; set; }
    public decimal TutorReceive { get; set; }
    public string Status { get; set; } = "Chờ chuyển";
    public bool IsTransferred => Status == "Đã chuyển";
}

public class TutorBankAccountDto
{
    public string BankName { get; set; } = "MBBank";
    public string AccountNumber { get; set; } = "0984391940";
    public string AccountHolder { get; set; } = "TONG NGOC TUAN";
    public string CitizenId { get; set; } = "079*********";
    public string QrImageUrl { get; set; } = string.Empty;
    public bool IsUpdated { get; set; } = true;
}