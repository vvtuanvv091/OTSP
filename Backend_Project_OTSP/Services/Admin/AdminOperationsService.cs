using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Admin;

public sealed class AdminOperationsService : IAdminOperationsService
{
    private const decimal PlatformFeeRate = 0.20m;
    private static readonly string[] ManagedRoles = ["Student_Parent", "Tutor", "Admin"];
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;

    public AdminOperationsService(ApplicationDbContext db, UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles)
    {
        _db = db;
        _users = users;
        _roles = roles;
    }

    public async Task<object> GetReviewsAsync(AdminOperationsQuery query, CancellationToken ct)
    {
        var rows = _db.Reviews.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            rows = rows.Where(x => (x.Comment != null && x.Comment.ToLower().Contains(term)) ||
                                   x.Student.User.FullName.ToLower().Contains(term) ||
                                   x.ClassSession.Classroom.Tutor.User.FullName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && query.Status != "all")
        {
            if (query.Status == "Hidden") rows = rows.Where(x => x.IsHidden);
            else if (query.Status == "Visible") rows = rows.Where(x => !x.IsHidden);
            else if (query.Status == "Alerted") rows = rows.Where(x => x.ReviewAlerts.Any(a => a.Status == AlertStatus.New));
            else if (int.TryParse(query.Status, out var starScore) && starScore >= 1 && starScore <= 5)
                rows = rows.Where(x => x.Score == starScore);
        }

        var stats = await _db.Reviews.AsNoTracking().GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(),
            Average = g.Average(x => x.Score),
            LowScore = g.Count(x => x.Score <= 2),
            Reported = g.Sum(x => x.ReviewAlerts.Count(a => a.Status == AlertStatus.New)),
            HiddenCount = g.Count(x => x.IsHidden)
        }).FirstOrDefaultAsync(ct);

        var page = await PageAsync(rows.OrderByDescending(x => x.ReviewedAt).ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Score,
                x.Comment,
                x.ReviewedAt,
                x.IsHidden,
                x.AdminNote,
                StudentName = x.Student.User.FullName,
                TutorName = x.ClassSession.Classroom.Tutor.User.FullName,
                ClassroomName = x.ClassSession.Classroom.NameClass,
                SubjectName = x.ClassSession.Classroom.Subject.Name,
                AlertCount = x.ReviewAlerts.Count
            }), query.Page, query.PageSize, ct);

        return new { stats = stats ?? new { Total = 0, Average = 0d, LowScore = 0, Reported = 0, HiddenCount = 0 }, reviews = page };
    }

    public async Task<(bool Success, string? Error, bool? IsHidden)> ToggleReviewVisibilityAsync(int id, string adminUserId, string? ipAddress, CancellationToken ct)
    {
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (review == null) return (false, "Không tìm thấy đánh giá.", null);
        review.IsHidden = !review.IsHidden;
        AddAudit(adminUserId, ipAddress, $"AdminToggleReviewVisibility:{id}:IsHidden={review.IsHidden}");
        await _db.SaveChangesAsync(ct);
        return (true, null, review.IsHidden);
    }

    public async Task<(bool Success, string? Error)> DeleteReviewAsync(int id, string adminUserId, string? ipAddress, CancellationToken ct)
    {
        var review = await _db.Reviews.Include(r => r.ReviewAlerts).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (review == null) return (false, "Không tìm thấy đánh giá.");
        _db.ReviewAlerts.RemoveRange(review.ReviewAlerts);
        _db.Reviews.Remove(review);
        AddAudit(adminUserId, ipAddress, $"AdminDeleteReview:{id}");
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> FlagReviewAlertAsync(int id, string reason, string adminUserId, string? ipAddress, CancellationToken ct)
    {
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (review == null) return (false, "Không tìm thấy đánh giá.");
        var adminProfile = await EnsureAdminProfileAsync(adminUserId, ct);
        _db.ReviewAlerts.Add(new ReviewAlert
        {
            ReviewId = id,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Quản trị viên gắn cờ cần rà soát tiêu chuẩn cộng đồng" : reason.Trim(),
            Status = AlertStatus.New,
            Severity = "High",
            Category = "ReviewViolation",
            CreatedAt = DateTime.UtcNow,
            ReviewedByAdminId = adminProfile.Id
        });
        AddAudit(adminUserId, ipAddress, $"AdminFlagReviewAlert:{id}");
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<object> GetReportedConversationsAsync(AdminOperationsQuery query, CancellationToken ct)
    {
        var reports = await _db.Complaints.AsNoTracking().Where(x => x.TargetUserId != null)
            .Select(x => new { x.Id, x.SenderId, TargetId = x.TargetUserId!, x.CreatedAt, x.Status })
            .ToListAsync(ct);
        var reportPairs = reports.GroupBy(x => Pair(x.SenderId, x.TargetId))
            .ToDictionary(g => g.Key, g => new { Count = g.Count(), Last = g.Max(x => x.CreatedAt), ReportIds = g.Select(x => x.Id).Order().ToArray() });
        var participantIds = reportPairs.Keys.SelectMany(x => new[] { x.Left, x.Right }).Distinct().ToArray();
        if (participantIds.Length == 0) return new { stats = new { conversations = 0, reports = reports.Count }, conversations = Array.Empty<object>() };

        // Chỉ tổng hợp metadata cho các bên có khiếu nại; không đọc hoặc trả nội dung tin nhắn riêng tư.
        var messageStats = await _db.Messagers.AsNoTracking()
            .Where(x => participantIds.Contains(x.SenderId) && participantIds.Contains(x.ReceiverId) && !x.IsDeleted)
            .GroupBy(x => new { x.SenderId, x.ReceiverId })
            .Select(g => new { g.Key.SenderId, g.Key.ReceiverId, Count = g.Count(), Last = g.Max(x => x.SentAt) })
            .ToListAsync(ct);
        var users = await _db.Users.AsNoTracking().Where(x => participantIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FullName }).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var conversations = messageStats.GroupBy(x => Pair(x.SenderId, x.ReceiverId))
            .Where(g => reportPairs.ContainsKey(g.Key))
            .Select(g =>
            {
                var key = g.Key;
                var report = reportPairs[key];
                return new
                {
                    userOneId = key.Left, userOneName = users.GetValueOrDefault(key.Left, "Tài khoản"),
                    userTwoId = key.Right, userTwoName = users.GetValueOrDefault(key.Right, "Tài khoản"),
                    messageCount = g.Sum(x => x.Count), lastActivity = g.Max(x => x.Last),
                    reportCount = report.Count, latestReportAt = report.Last, reportIds = report.ReportIds
                };
            })
            .Where(x => string.IsNullOrWhiteSpace(query.Search) ||
                x.userOneName.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                x.userTwoName.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.latestReportAt).Take(100).ToList();
        return new { stats = new { conversations = conversations.Count, reports = reports.Count }, conversations };
    }

    public async Task<object> GetComplaintsAsync(AdminOperationsQuery query, CancellationToken ct)
    {
        var rows = _db.Complaints.AsNoTracking().AsQueryable();
        if (Enum.TryParse<ComplaintStatus>(query.Status, true, out var status)) rows = rows.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(x => x.Content.Contains(term) || x.Sender.FullName.Contains(term) || (x.TargetUser != null && x.TargetUser.FullName.Contains(term)));
        }
        var stats = await _db.Complaints.AsNoTracking().GroupBy(x => x.Status).Select(g => new { status = g.Key.ToString(), count = g.Count() }).ToListAsync(ct);
        var page = await PageAsync(rows.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id, x.Content, x.EvidenceUrl, status = x.Status.ToString(), x.Resolution, x.CreatedAt,
                senderName = x.Sender.FullName, senderEmail = x.Sender.Email,
                targetName = x.TargetUser == null ? null : x.TargetUser.FullName,
                targetEmail = x.TargetUser == null ? null : x.TargetUser.Email
            }), query.Page, query.PageSize, ct);
        return new { stats, complaints = page };
    }

    public async Task<(bool Success, string? Error)> ReviewComplaintAsync(int id, AdminComplaintReviewRequest request, string adminUserId, string? ipAddress, CancellationToken ct)
    {
        if (request.Status is not (ComplaintStatus.Processing or ComplaintStatus.Resolved or ComplaintStatus.Rejected))
            return (false, "Trạng thái xử lý không hợp lệ.");
        if (request.Status is ComplaintStatus.Resolved or ComplaintStatus.Rejected && string.IsNullOrWhiteSpace(request.Resolution))
            return (false, "Nhập kết quả xử lý trước khi đóng khiếu nại.");
        var item = await _db.Complaints.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item == null) return (false, "Không tìm thấy khiếu nại.");
        item.Status = request.Status;
        item.Resolution = string.IsNullOrWhiteSpace(request.Resolution) ? null : request.Resolution.Trim();
        item.HandledByAdminId = (await EnsureAdminProfileAsync(adminUserId, ct)).Id;
        var statusText = request.Status switch
        {
            ComplaintStatus.Processing => "đang được tiếp nhận xử lý",
            ComplaintStatus.Resolved => "đã được giải quyết",
            ComplaintStatus.Rejected => "đã bị từ chối",
            _ => "đã được cập nhật"
        };
        _db.Notifications.Add(new Notification
        {
            UserId = item.SenderId,
            Title = $"Khiếu nại #{item.Id} {statusText}",
            Content = string.IsNullOrWhiteSpace(item.Resolution) ? $"Quản trị viên cập nhật khiếu nại của bạn thành trạng thái: {statusText}." : $"Kết quả xử lý khiếu nại: {item.Resolution}",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });
        AddAudit(adminUserId, ipAddress, $"AdminComplaint:{id}:{request.Status}");
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteComplaintAsync(int id, string adminUserId, string? ipAddress, CancellationToken ct)
    {
        var item = await _db.Complaints.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item == null) return (false, "Không tìm thấy khiếu nại.");
        _db.Complaints.Remove(item);
        AddAudit(adminUserId, ipAddress, $"AdminDeleteComplaint:{id}");
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<object> GetNewsAsync(AdminOperationsQuery query, CancellationToken ct)
    {
        var rows = _db.NewsArticles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(x => x.Title.Contains(term) || x.Category.Contains(term) || x.Content.Contains(term));
        }
        var categories = await _db.NewsArticles.AsNoTracking().Select(x => x.Category).Distinct().OrderBy(x => x).ToListAsync(ct);
        var page = await PageAsync(rows.OrderByDescending(x => x.PublishedAt).ThenByDescending(x => x.Id)
            .Select(x => new { x.Id, x.Title, x.Category, x.Content, x.PublishedAt, adminName = x.Admin.User.FullName }), query.Page, query.PageSize, ct);
        return new { categories, articles = page };
    }

    public async Task<object> CreateNewsAsync(AdminSaveNewsRequest request, string adminUserId, string? ipAddress, CancellationToken ct)
    {
        var admin = await EnsureAdminProfileAsync(adminUserId, ct);
        var article = new NewsArticle { Title = request.Title.Trim(), Category = request.Category.Trim(), Content = request.Content.Trim(), AdminId = admin.Id, PublishedAt = DateTime.UtcNow };
        _db.NewsArticles.Add(article);
        AddAudit(adminUserId, ipAddress, $"AdminCreateNews:{article.Title}");
        await _db.SaveChangesAsync(ct);
        return new { success = true, id = article.Id };
    }

    public async Task<(bool Success, string? Error)> UpdateNewsAsync(int id, AdminSaveNewsRequest request, string adminUserId, string? ipAddress, CancellationToken ct)
    {
        var article = await _db.NewsArticles.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (article == null) return (false, "Không tìm thấy bài viết.");
        article.Title = request.Title.Trim(); article.Category = request.Category.Trim(); article.Content = request.Content.Trim();
        AddAudit(adminUserId, ipAddress, $"AdminUpdateNews:{id}");
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteNewsAsync(int id, string adminUserId, string? ipAddress, CancellationToken ct)
    {
        var article = await _db.NewsArticles.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (article == null) return (false, "Không tìm thấy bài viết.");
        _db.NewsArticles.Remove(article);
        AddAudit(adminUserId, ipAddress, $"AdminDeleteNews:{id}");
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<object> GetPaymentsAsync(AdminOperationsQuery query, CancellationToken ct)
    {
        var rows = _db.Payments.AsNoTracking().AsQueryable();
        if (Enum.TryParse<PaymentStatus>(query.Status, true, out var status)) rows = rows.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(x => (x.Student.User.FullName.Contains(term)) || x.Classroom.Tutor.User.FullName.Contains(term) ||
                (x.GatewayTransactionCode != null && x.GatewayTransactionCode.Contains(term)) || x.Classroom.NameClass.Contains(term));
        }
        var stats = await _db.Payments.AsNoTracking().GroupBy(x => x.Status)
            .Select(g => new
            {
                status = g.Key.ToString(),
                count = g.Count(),
                amount = g.Where(x => x.Status == PaymentStatus.Success).Sum(x => x.Amount),
                platformFee = decimal.Round(g.Where(x => x.Status == PaymentStatus.Success).Sum(x => x.Amount) * PlatformFeeRate, 0),
                tutorPayout = g.Where(x => x.Status == PaymentStatus.Success).Sum(x => x.Amount) - decimal.Round(g.Where(x => x.Status == PaymentStatus.Success).Sum(x => x.Amount) * PlatformFeeRate, 0)
            }).ToListAsync(ct);

        var totalSuccessfulAmount = await _db.Payments.AsNoTracking().Where(x => x.Status == PaymentStatus.Success).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var totalPlatformFee = decimal.Round(totalSuccessfulAmount * PlatformFeeRate, 0);
        var totalTutorPayout = totalSuccessfulAmount - totalPlatformFee;

        var page = await PageAsync(rows.OrderByDescending(x => x.PaidAt).ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Amount,
                platformFee = decimal.Round(x.Amount * PlatformFeeRate, 0),
                tutorPayout = x.Amount - decimal.Round(x.Amount * PlatformFeeRate, 0),
                x.Method,
                status = x.Status.ToString(),
                x.GatewayTransactionCode,
                x.PaidAt,
                studentName = x.Student.User.FullName,
                tutorName = x.Classroom.Tutor.User.FullName,
                classroomName = x.Classroom.NameClass
            }), query.Page, query.PageSize, ct);
        return new { stats, summary = new { totalAmount = totalSuccessfulAmount, totalPlatformFee, totalTutorPayout }, payments = page };
    }

    public async Task<object> GetReconciliationAsync(string? period, CancellationToken ct)
    {
        var selectedPeriod = DateTime.TryParseExact(period, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed : DateTime.UtcNow;
        var periodStr = selectedPeriod.ToString("yyyy-MM");
        var from = new DateTime(selectedPeriod.Year, selectedPeriod.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);
        var rows = await _db.Payments.AsNoTracking().Where(x => x.Status == PaymentStatus.Success && x.PaidAt >= from && x.PaidAt < to)
            .GroupBy(x => new { x.Classroom.TutorId, TutorName = x.Classroom.Tutor.User.FullName, TutorEmail = x.Classroom.Tutor.User.Email, TutorUserId = x.Classroom.Tutor.UserId })
            .Select(g => new { tutorId = g.Key.TutorId, tutorName = g.Key.TutorName, tutorEmail = g.Key.TutorEmail, tutorUserId = g.Key.TutorUserId, transactionCount = g.Count(), gross = g.Sum(x => x.Amount) })
            .OrderByDescending(x => x.gross).ToListAsync(ct);

        var settledReconciliations = await _db.Reconciliations.AsNoTracking()
            .Where(r => r.Period == periodStr)
            .ToListAsync(ct);
        var settledMap = settledReconciliations.ToDictionary(r => r.TutorId);

        var tutorIds = rows.Select(r => r.tutorId).Distinct().ToList();
        var tutorProfiles = await _db.TutorProfiles.AsNoTracking()
            .Where(tp => tutorIds.Contains(tp.Id))
            .ToDictionaryAsync(tp => tp.Id, ct);

        var defaultBank = "MBBank";
        var defaultAccountNo = "0984391940";

        var items = rows.Select(x =>
        {
            var platformFee = decimal.Round(x.gross * PlatformFeeRate, 0);
            var tutorNet = x.gross - platformFee;
            var isSettled = settledMap.TryGetValue(x.tutorId, out var rec);

            tutorProfiles.TryGetValue(x.tutorId, out var tp);
            var bankName = !string.IsNullOrWhiteSpace(tp?.BankName) ? tp.BankName : defaultBank;
            var accountNo = !string.IsNullOrWhiteSpace(tp?.BankAccountNumber) ? tp.BankAccountNumber : defaultAccountNo;
            // ƯU TIÊN LẤY TÊN CHỦ TÀI KHOẢN DO GIA SƯ ĐĂNG KÝ THỰC TẾ TRÊN HỆ THỐNG
            var accountHolder = !string.IsNullOrWhiteSpace(tp?.BankAccountHolder)
                ? tp.BankAccountHolder.ToUpperInvariant()
                : (!string.IsNullOrWhiteSpace(x.tutorName) ? x.tutorName.ToUpperInvariant() : "TONG NGOC TUAN");

            var bankCode = MapBankNameToCode(bankName);
            var qrUrl = $"https://img.vietqr.io/image/{bankCode}-{Uri.EscapeDataString(accountNo)}-compact.png?amount={(long)tutorNet}&addInfo={Uri.EscapeDataString($"OTSP Luong T{selectedPeriod.Month}/{selectedPeriod.Year}")}&accountName={Uri.EscapeDataString(accountHolder)}";

            return new
            {
                x.tutorId,
                x.tutorName,
                x.tutorEmail,
                x.tutorUserId,
                x.transactionCount,
                totalRevenue = x.gross,
                platformFee,
                tutorNet,
                isSettled,
                settledAt = isSettled ? rec?.CreatedDate.ToString("yyyy-MM-dd") : null,
                bankAccount = new
                {
                    bankName,
                    accountNumber = accountNo,
                    accountHolder,
                    qrImageUrl = qrUrl
                }
            };
        }).ToList();

        return new
        {
            period = periodStr,
            platformFeeRate = PlatformFeeRate,
            totals = new
            {
                transactionCount = items.Sum(x => x.transactionCount),
                totalRevenue = items.Sum(x => x.totalRevenue),
                platformFee = items.Sum(x => x.platformFee),
                tutorNet = items.Sum(x => x.tutorNet),
                settledCount = items.Count(x => x.isSettled),
                pendingCount = items.Count(x => !x.isSettled)
            },
            items
        };
    }

    public async Task<(bool Success, string? Error)> SettleTutorPayoutAsync(int tutorId, string period, string adminUserId, CancellationToken ct)
    {
        var tutor = await _db.TutorProfiles.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == tutorId, ct);
        if (tutor == null) return (false, "Không tìm thấy hồ sơ gia sư.");

        var periodStr = string.IsNullOrWhiteSpace(period) ? DateTime.UtcNow.ToString("yyyy-MM") : period.Trim();
        var exists = await _db.Reconciliations.AnyAsync(r => r.TutorId == tutorId && r.Period == periodStr, ct);
        if (exists) return (false, $"Gia sư {tutor.User.FullName} đã được quyết toán cho kỳ {periodStr}.");

        var adminProfile = await _db.AdminProfiles.FirstOrDefaultAsync(a => a.UserId == adminUserId, ct);
        if (adminProfile == null)
        {
            adminProfile = new AdminProfile { UserId = adminUserId };
            _db.AdminProfiles.Add(adminProfile);
            await _db.SaveChangesAsync(ct);
        }

        var selectedPeriod = DateTime.TryParseExact(periodStr, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : DateTime.UtcNow;
        var from = new DateTime(selectedPeriod.Year, selectedPeriod.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);
        var gross = await _db.Payments.AsNoTracking()
            .Where(x => x.Status == PaymentStatus.Success && x.Classroom.TutorId == tutorId && x.PaidAt >= from && x.PaidAt < to)
            .SumAsync(x => x.Amount, ct);

        var net = gross - decimal.Round(gross * PlatformFeeRate, 0);

        var rec = new Reconciliation
        {
            Period = periodStr,
            TotalRevenue = gross,
            CreatedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TutorId = tutorId,
            AdminId = adminProfile.Id
        };
        _db.Reconciliations.Add(rec);

        _db.Notifications.Add(new Notification
        {
            UserId = tutor.UserId,
            Title = $"Quyết toán thu nhập kỳ {periodStr}",
            Content = $"Admin đã quyết toán và giải ngân khoản thu nhập {net:N0} đ kỳ {periodStr} cho bạn. Bạn có thể kiểm tra chi tiết tại trang Thu nhập.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });

        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<object> GetAccountsAsync(AdminOperationsQuery query, CancellationToken ct)
    {
        var users = _users.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            users = users.Where(x => x.FullName.Contains(term) || (x.Email != null && x.Email.Contains(term)));
        }
        if (Enum.TryParse<AccountStatus>(query.Status, true, out var status)) users = users.Where(x => x.Status == status);
        var total = await users.CountAsync(ct);
        var pageNumber = Math.Max(1, query.Page); var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var rows = await users.OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.FullName, x.Email, status = x.Status.ToString(), x.CreatedAt }).ToListAsync(ct);
        var userIds = rows.Select(x => x.Id).ToArray();
        var memberships = await (from membership in _db.UserRoles.AsNoTracking()
                                 join roleEntry in _db.Roles.AsNoTracking() on membership.RoleId equals roleEntry.Id
                                 where userIds.Contains(membership.UserId) && ManagedRoles.Contains(roleEntry.Name!)
                                 select new { membership.UserId, Role = roleEntry.Name! }).ToListAsync(ct);
        var roleByUser = memberships.GroupBy(x => x.UserId)
            .ToDictionary(group => group.Key, group => ManagedRoles.LastOrDefault(role => group.Any(item => item.Role == role)) ?? "Chưa phân quyền");
        var items = rows.Select(x => new { x.Id, x.FullName, x.Email, x.status, x.CreatedAt, role = roleByUser.GetValueOrDefault(x.Id, "Chưa phân quyền") }).ToList();
        return new { accounts = new PagedResult<object> { Page = pageNumber, PageSize = pageSize, Total = total, Items = items.Cast<object>().ToList() }, availableRoles = ManagedRoles };
    }

    public async Task<(bool Success, string? Error, string? TemporaryPassword)> CreateAccountAsync(AdminCreateAccountRequest request, string adminUserId, string? ipAddress, CancellationToken ct)
    {
        if (!ManagedRoles.Contains(request.Role, StringComparer.Ordinal)) return (false, "Vai trò không hợp lệ.", null);
        if (!await _roles.RoleExistsAsync(request.Role)) return (false, "Vai trò chưa được khởi tạo trong hệ thống.", null);
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(15)) + "Aa1!";
        var user = new ApplicationUser { UserName = request.Email.Trim(), Email = request.Email.Trim(), FullName = request.FullName.Trim(), EmailConfirmed = true, Status = AccountStatus.Active };
        var created = await _users.CreateAsync(user, password);
        if (!created.Succeeded) return (false, string.Join(" ", created.Errors.Select(x => x.Description)), null);
        var added = await _users.AddToRoleAsync(user, request.Role);
        if (!added.Succeeded)
        {
            await _users.DeleteAsync(user);
            return (false, string.Join(" ", added.Errors.Select(x => x.Description)), null);
        }
        AddRoleProfile(user.Id, request.Role);
        AddAudit(adminUserId, ipAddress, $"AdminCreateAccount:{user.Email}:{request.Role}");
        await _db.SaveChangesAsync(ct);
        return (true, null, password);
    }

    public async Task<(bool Success, string? Error)> ChangeRoleAsync(string id, string role, string adminUserId, string? ipAddress, CancellationToken ct)
    {
        if (!ManagedRoles.Contains(role, StringComparer.Ordinal)) return (false, "Vai trò không hợp lệ.");
        if (id == adminUserId && role != "Admin") return (false, "Không thể tự gỡ quyền Admin đang đăng nhập.");
        var user = await _users.FindByIdAsync(id);
        if (user == null) return (false, "Không tìm thấy tài khoản.");
        var oldRoles = await _users.GetRolesAsync(user);
        if (oldRoles.Contains("Admin") && role != "Admin" && (await _users.GetUsersInRoleAsync("Admin")).Count <= 1)
            return (false, "Không thể gỡ quyền Admin cuối cùng.");
        var result = await _users.RemoveFromRolesAsync(user, oldRoles);
        if (!result.Succeeded) return (false, string.Join(" ", result.Errors.Select(x => x.Description)));
        result = await _users.AddToRoleAsync(user, role);
        if (!result.Succeeded)
        {
            await _users.AddToRolesAsync(user, oldRoles);
            return (false, string.Join(" ", result.Errors.Select(x => x.Description)));
        }
        if (role == "Admin" && !await _db.AdminProfiles.AnyAsync(x => x.UserId == user.Id, ct))
            _db.AdminProfiles.Add(new AdminProfile { UserId = user.Id, Rank = "Quản trị viên" });
        if (role == "Tutor" && !await _db.TutorProfiles.AnyAsync(x => x.UserId == user.Id, ct))
            _db.TutorProfiles.Add(new TutorProfile { UserId = user.Id });
        if (role == "Student_Parent" && !await _db.StudentProfiles.AnyAsync(x => x.UserId == user.Id, ct))
            _db.StudentProfiles.Add(new StudentProfile { UserId = user.Id });
        AddAudit(adminUserId, ipAddress, $"AdminChangeRole:{id}:{string.Join(',', oldRoles)}->{role}");
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SetAccountStatusAsync(string id, bool locked, string adminUserId, string? ipAddress, CancellationToken ct)
    {
        if (id == adminUserId && locked) return (false, "Không thể tự khóa tài khoản Admin đang đăng nhập.");
        var user = await _users.FindByIdAsync(id);
        if (user == null) return (false, "Không tìm thấy tài khoản.");
        if (locked && (await _users.GetRolesAsync(user)).Contains("Admin") && (await _users.GetUsersInRoleAsync("Admin")).Count <= 1)
            return (false, "Không thể khóa Admin cuối cùng.");
        user.Status = locked ? AccountStatus.Locked : AccountStatus.Active;
        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded) return (false, string.Join(" ", result.Errors.Select(x => x.Description)));
        AddAudit(adminUserId, ipAddress, $"AdminSetAccountStatus:{id}:{user.Status}");
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<object> GetAuditLogsAsync(AdminOperationsQuery query, CancellationToken ct)
    {
        var rows = _db.AuditLogs.AsNoTracking().Include(x => x.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            rows = rows.Where(x => (x.Action != null && x.Action.ToLower().Contains(term)) ||
                                   (x.Detail != null && x.Detail.ToLower().Contains(term)) ||
                                   (x.Category != null && x.Category.ToLower().Contains(term)) ||
                                   (x.User != null && x.User.FullName != null && x.User.FullName.ToLower().Contains(term)) ||
                                   (x.User != null && x.User.Email != null && x.User.Email.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && query.Status != "all")
        {
            var cat = query.Status.Trim();
            rows = rows.Where(x => x.Category == cat);
        }

        var stats = await _db.AuditLogs.AsNoTracking()
            .GroupBy(x => x.Category ?? "System")
            .Select(g => new { category = g.Key, count = g.Count() })
            .ToListAsync(ct);

        var page = await PageAsync(rows.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Action,
                category = x.Category ?? "System",
                x.Detail,
                x.Timestamp,
                x.IpAddress,
                x.UserAgent,
                userId = x.UserId,
                adminName = x.User != null ? x.User.FullName : "Người dùng",
                adminEmail = x.User != null ? x.User.Email : "—",
                userRole = _db.UserRoles.Where(ur => ur.UserId == x.UserId)
                    .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                    .FirstOrDefault() ?? "Thành viên"
            }), query.Page, query.PageSize, ct);

        return new { stats, logs = page };
    }

    public async Task<object> GetAlertsAsync(AdminOperationsQuery query, CancellationToken ct)
    {
        var rows = _db.ReviewAlerts.AsNoTracking()
            .Include(x => x.Review).ThenInclude(r => r.Student).ThenInclude(s => s.User)
            .Include(x => x.Review).ThenInclude(r => r.ClassSession).ThenInclude(cs => cs.Classroom).ThenInclude(c => c.Tutor).ThenInclude(t => t.User)
            .AsQueryable();

        if (Enum.TryParse<AlertStatus>(query.Status, true, out var status)) rows = rows.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            rows = rows.Where(x => x.Reason.ToLower().Contains(term) ||
                                   (x.Review.Comment != null && x.Review.Comment.ToLower().Contains(term)) ||
                                   (x.Review.Student.User.FullName != null && x.Review.Student.User.FullName.ToLower().Contains(term)) ||
                                   (x.Review.ClassSession.Classroom.Tutor.User.FullName != null && x.Review.ClassSession.Classroom.Tutor.User.FullName.ToLower().Contains(term)));
        }

        var stats = await _db.ReviewAlerts.AsNoTracking().GroupBy(x => x.Status).Select(g => new { status = g.Key.ToString(), count = g.Count() }).ToListAsync(ct);
        var page = await PageAsync(rows.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Reason,
                severity = x.Severity ?? (x.Review.Score <= 1 ? "High" : "Medium"),
                category = x.Category ?? "Review",
                status = x.Status.ToString(),
                x.CreatedAt,
                reviewId = x.ReviewId,
                reviewScore = x.Review.Score,
                reviewComment = x.Review.Comment,
                studentName = x.Review.Student.User.FullName,
                tutorName = x.Review.ClassSession.Classroom.Tutor.User.FullName,
                classroomName = x.Review.ClassSession.Classroom.NameClass
            }), query.Page, query.PageSize, ct);

        return new { stats, alerts = page };
    }

    private static string MapBankNameToCode(string bankName)
    {
        if (string.IsNullOrWhiteSpace(bankName)) return "MB";
        var n = bankName.Trim().ToLowerInvariant();
        if (n.Contains("mb") || n.Contains("quân đội")) return "MB";
        if (n.Contains("vietcombank") || n.Contains("vcb")) return "vietcombank";
        if (n.Contains("techcombank") || n.Contains("tcb")) return "techcombank";
        if (n == "acb") return "ACB";
        if (n.Contains("vpbank") || n.Contains("vpb")) return "VPB";
        if (n.Contains("bidv")) return "BIDV";
        if (n.Contains("agribank")) return "agribank";
        if (n.Contains("tpbank")) return "tpbank";
        if (n.Contains("sacombank")) return "sacombank";
        if (n.Contains("ocb")) return "OCB";
        return "MB";
    }

    public async Task<(bool Success, string? Error)> UpdateAlertAsync(int id, AlertStatus status, string adminUserId, string? ipAddress, CancellationToken ct)
    {
        if (status is not (AlertStatus.Reviewed or AlertStatus.Dismissed)) return (false, "Trạng thái cảnh báo không hợp lệ.");
        var alert = await _db.ReviewAlerts.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (alert == null) return (false, "Không tìm thấy cảnh báo.");
        alert.Status = status;
        alert.ReviewedByAdminId = (await EnsureAdminProfileAsync(adminUserId, ct)).Id;
        AddAudit(adminUserId, ipAddress, $"AdminReviewAlert:{id}:{status}");
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<object> GetAiUsageAsync(CancellationToken ct)
    {
        var total = await _db.AiStudySessions.AsNoTracking().CountAsync(ct);
        var today = DateTime.UtcNow.Date;
        var todayCount = await _db.AiStudySessions.AsNoTracking().CountAsync(x => x.CreatedAt >= today, ct);
        var byDay = await _db.AiStudySessions.AsNoTracking().Where(x => x.CreatedAt >= today.AddDays(-13))
            .GroupBy(x => x.CreatedAt.Date).Select(g => new { date = g.Key, count = g.Count() }).OrderBy(x => x.date).ToListAsync(ct);
        var students = await _db.AiStudySessions.AsNoTracking().Select(x => x.StudentId).Distinct().CountAsync(ct);
        return new { totalSessions = total, sessionsToday = todayCount, activeStudents = students, recentActivity = byDay };
    }

    private void AddAudit(string userId, string? ipAddress, string action) => _db.AuditLogs.Add(new AuditLog { UserId = userId, IpAddress = ipAddress, Action = action, Timestamp = DateTime.UtcNow });

    private void AddRoleProfile(string userId, string role)
    {
        if (role == "Admin") _db.AdminProfiles.Add(new AdminProfile { UserId = userId, Rank = "Quản trị viên" });
        else if (role == "Tutor") _db.TutorProfiles.Add(new TutorProfile { UserId = userId });
        else if (role == "Student_Parent") _db.StudentProfiles.Add(new StudentProfile { UserId = userId });
    }

    private async Task<AdminProfile> EnsureAdminProfileAsync(string userId, CancellationToken ct)
    {
        var profile = await _db.AdminProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (profile != null) return profile;
        profile = new AdminProfile { UserId = userId, Rank = "Quản trị viên" };
        _db.AdminProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    private static async Task<PagedResult<T>> PageAsync<T>(IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(ct);
        var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var items = await query.Skip(skip).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T> { Page = page, PageSize = pageSize, Total = total, Items = items };
    }

    private static (string Left, string Right) Pair(string first, string second) => string.CompareOrdinal(first, second) <= 0 ? (first, second) : (second, first);
}
