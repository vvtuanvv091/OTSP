using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Common;

public class AiService : IAiService
{
    private readonly HttpClient _http;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AiService> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly IMemoryCache? _cache;
    private readonly string _baseUrl;
    private readonly string _apiKey;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AiService(
        HttpClient http,
        ApplicationDbContext db,
        IConfiguration config,
        ILogger<AiService> logger,
        IHttpContextAccessor? httpContextAccessor = null,
        IHttpClientFactory? httpClientFactory = null,
        IMemoryCache? cache = null)
    {
        _http = http;
        _db = db;
        _config = config;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
        _cache = cache;

        _baseUrl = _config["AiService:BaseUrl"] ?? "http://localhost:8000";
        _apiKey = _config["AiService:InternalApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("AiService:InternalApiKey is not configured; internal AI calls may be rejected.");
        }

        if (Uri.TryCreate(_baseUrl, UriKind.Absolute, out var uri))
        {
            _http.BaseAddress = uri;
        }
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path, object? body = null)
    {
        var req = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            req.Headers.Add("X-Internal-Key", _apiKey);
        }
        if (body != null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        return req;
    }

    public async Task<AgentChatResponseDto> ChatWithAgentAsync(AgentChatRequestDto request, string? currentUserId, CancellationToken ct = default)
    {
        // 0. Kiểm tra hạn mức câu hỏi trong ngày theo vai trò (Khách: 10, Học viên/Gia sư: 50, Admin: 500)
        var (allowed, currentCount, maxLimit, userRole, quotaCacheKey) = await CheckDailyQuotaAsync(currentUserId, ct);
        if (!allowed)
        {
            return BuildQuotaExceededResponse(userRole, maxLimit);
        }

        // 1. Enrich context if user is authenticated (lấy đúng vai trò hiện tại từ UserRoles)
        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId, ct);
            if (user != null)
            {
                request.SystemContext["userName"] = user.FullName ?? string.Empty;

                var roleNames = await _db.UserRoles.AsNoTracking()
                    .Where(ur => ur.UserId == currentUserId)
                    .Join(_db.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
                    .ToListAsync(ct);

                var isAdmin = roleNames.Contains("Admin");
                var isTutor = roleNames.Contains("Tutor");
                var isStudent = roleNames.Contains("Student_Parent");

                if (isAdmin)
                {
                    request.UserRole = "Admin";
                }
                else if (isStudent || roleNames.Count == 0)
                {
                    var student = await _db.StudentProfiles.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == currentUserId, ct);
                    if (student != null)
                    {
                        request.UserId = student.Id;
                        request.UserRole = "Student";

                        var upcoming = await (from cs in _db.ClassSessions.AsNoTracking()
                                              join c in _db.Classrooms.AsNoTracking() on cs.ClassroomId equals c.Id
                                              join csStudent in _db.ClassroomStudents.AsNoTracking() on c.Id equals csStudent.ClassroomId
                                              join tp in _db.TutorProfiles.AsNoTracking() on c.TutorId equals tp.Id
                                              join tutorUser in _db.Users.AsNoTracking() on tp.UserId equals tutorUser.Id
                                              join s in _db.Subjects.AsNoTracking() on c.SubjectId equals s.Id
                                              where csStudent.StudentId == student.Id && cs.ScheduledAt >= DateTime.UtcNow.AddHours(-1)
                                              orderby cs.ScheduledAt
                                              select new
                                              {
                                                  subject = s.Name,
                                                  date = cs.ScheduledAt.ToLocalTime().ToString("dd/MM/yyyy"),
                                                  time = cs.ScheduledAt.ToLocalTime().ToString("HH:mm"),
                                                  tutorName = tutorUser.FullName,
                                                  roomCode = cs.VideoRoom ?? $"OTSP-Room-{cs.Id}"
                                              }).Take(3).ToListAsync(ct);

                        request.SystemContext["upcomingSessions"] = upcoming;
                    }
                    else if (roleNames.Count == 0)
                    {
                        var tutor = await _db.TutorProfiles.AsNoTracking().FirstOrDefaultAsync(t => t.UserId == currentUserId, ct);
                        if (tutor != null)
                        {
                            request.UserId = tutor.Id;
                            request.UserRole = "Tutor";
                        }
                    }
                }
                else if (isTutor)
                {
                    var tutor = await _db.TutorProfiles.AsNoTracking().FirstOrDefaultAsync(t => t.UserId == currentUserId, ct);
                    if (tutor != null)
                    {
                        request.UserId = tutor.Id;
                        request.UserRole = "Tutor";
                    }
                }
            }
        }

        // 2. Thử gọi Python Agent service (https://otsp-1.onrender.com)
        if (!string.IsNullOrWhiteSpace(_baseUrl))
        {
            try
            {
                using var reqMsg = CreateAuthorizedRequest(HttpMethod.Post, "/v1/agent/chat", request);
                var response = await _http.SendAsync(reqMsg, ct);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<AgentChatResponseDto>(content, JsonOptions);
                    if (result != null)
                    {
                        // Đảm bảo tuyệt đối tắt tự động chuyển trang theo yêu cầu
                        result.AutoExecute = false;
                        if (result.UiAction != null) result.UiAction.AutoExecute = false;

                        var pythonTokens = EstimateTokens(request.Message, result.Reply);
                        await RecordAiUsageAsync(currentUserId, "Hỏi AI Wein (Chat)", request.Message, pythonTokens, null, ct);
                        IncrementDailyQuotaCache(quotaCacheKey, currentCount + 1);
                        return result;
                    }
                }
                else
                {
                    _logger.LogWarning("AI agent request returned status {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Python AI agent service offline or unreachable ({Msg})", ex.Message);
            }
        }

        return new AgentChatResponseDto
        {
            Reply = "Xin lỗi bạn, Wein đang bận xử lý dữ liệu. Bạn vui lòng thử lại sau giây lát hoặc truy cập [Tìm gia sư](/search) nhé!",
            Intent = "GENERAL",
            AutoExecute = false,
            SuggestedPrompts = new List<string> { "Tìm gia sư môn Toán", "Xem lớp học đang mở" }
        };
    }

    public async Task<StudyAssistantResponseDto> AskStudyAssistantAsync(StudyAssistantRequestDto request, string currentUserId, CancellationToken ct = default)
    {
        // 0. Kiểm tra hạn mức câu hỏi học tập trong ngày (50 câu/ngày)
        var (allowed, currentCount, maxLimit, userRole, quotaCacheKey) = await CheckDailyQuotaAsync(currentUserId, ct);
        if (!allowed)
        {
            return new StudyAssistantResponseDto
            {
                Answer = $"Bạn đã sử dụng hết hạn mức **{maxLimit} câu hỏi/ngày** của tài khoản hôm nay (hạn mức sẽ tự động làm mới vào lúc 00:00 ngày mai).\n\n" +
                         FounderContactMarkdown,
                ConceptSummary = "Hạn mức câu hỏi trong ngày",
                PracticeQuestion = "",
                PracticeSolution = ""
            };
        }

        var student = await _db.StudentProfiles.FirstOrDefaultAsync(s => s.UserId == currentUserId, ct);
        var studentId = student?.Id ?? 0;

        var payload = new
        {
            requestId = Guid.NewGuid().ToString("N"),
            studentId = studentId,
            question = request.Question,
            subject = request.Subject ?? "",
            gradeLevel = request.GradeLevel ?? ""
        };

        StudyAssistantResponseDto? result = null;
        int tokensUsed = 0;

        if (!string.IsNullOrWhiteSpace(_baseUrl))
        {
            try
            {
                using var reqMsg = CreateAuthorizedRequest(HttpMethod.Post, "/v1/study-assistant", payload);
                var response = await _http.SendAsync(reqMsg, ct);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(ct);
                    result = JsonSerializer.Deserialize<StudyAssistantResponseDto>(content, JsonOptions);
                    if (result != null && !string.IsNullOrWhiteSpace(result.Answer))
                    {
                        tokensUsed = EstimateTokens(request.Question, result.Answer);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Python AI Service for Study Assistant offline: {Msg}", ex.Message);
            }
        }

        result ??= new StudyAssistantResponseDto
        {
            Answer = "Hệ thống AI đang bận xử lý. Vui lòng thử lại trong giây lát hoặc tham khảo tài liệu học tập trong mục Tài liệu.",
            ConceptSummary = "Kiến thức môn học",
            PracticeQuestion = "",
            PracticeSolution = ""
        };
        if (tokensUsed <= 0)
        {
            tokensUsed = EstimateTokens(request.Question, result.Answer);
        }

        // Automatically persist into AiStudySessions table for Admin metrics and student history!
        int? savedSessionId = null;
        if (student != null)
        {
            try
            {
                var session = new AiStudySession
                {
                    StudentId = student.Id,
                    Question = request.Question,
                    Suggestion = result.Answer,
                    CreatedAt = DateTime.UtcNow
                };
                _db.AiStudySessions.Add(session);
                await _db.SaveChangesAsync(ct);
                savedSessionId = session.Id;
                _logger.LogInformation("Saved AiStudySession for studentId={StudentId}", student.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save AiStudySession in database");
            }
        }

        await RecordAiUsageAsync(currentUserId, "Trợ lý giải bài tập AI", request.Question, tokensUsed, savedSessionId, ct);
        IncrementDailyQuotaCache(quotaCacheKey, currentCount + 1);

        return result;
    }

    public async Task<ReviewModerationResponseDto> AnalyzeReviewAndAlertAsync(
        int reviewId, int score, string? comment, string studentName, string tutorName, string classroomName, CancellationToken ct = default)
    {
        var payload = new ReviewModerationRequestDto
        {
            ReviewId = reviewId,
            Score = score,
            Comment = comment ?? "",
            StudentName = studentName,
            TutorName = tutorName,
            ClassroomName = classroomName
        };

        ReviewModerationResponseDto? result = null;
        try
        {
            using var reqMsg = CreateAuthorizedRequest(HttpMethod.Post, "/v1/analyze-review", payload);
            var response = await _http.SendAsync(reqMsg, ct);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                result = JsonSerializer.Deserialize<ReviewModerationResponseDto>(content, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call AI Service for Review Moderation");
        }

        result ??= new ReviewModerationResponseDto
        {
            IsAbnormal = false,
            Category = "Clean",
            Confidence = 1.0,
            Reason = "Đánh giá bình thường",
            SuggestedAction = "Approve"
        };

        // If AI flagged review as abnormal, automatically generate ReviewAlert for Admin!
        if (result.IsAbnormal && reviewId > 0)
        {
            try
            {
                var exists = await _db.ReviewAlerts.AnyAsync(a => a.ReviewId == reviewId, ct);
                if (!exists)
                {
                    var alert = new ReviewAlert
                    {
                        ReviewId = reviewId,
                        Reason = $"[AI {result.Category}] {result.Reason}",
                        Status = AlertStatus.New,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.ReviewAlerts.Add(alert);
                    await _db.SaveChangesAsync(ct);
                    _logger.LogInformation("Generated ReviewAlert for reviewId={ReviewId}", reviewId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save ReviewAlert in database");
            }
        }

        return result;
    }

    public async Task<bool> SubmitFeedbackAsync(FeedbackRequestDto request, CancellationToken ct = default)
    {
        try
        {
            using var reqMsg = CreateAuthorizedRequest(HttpMethod.Post, "/v1/feedback", request);
            var response = await _http.SendAsync(reqMsg, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit AI feedback");
            return false;
        }
    }

    public async Task<VerifyCertificateResponseDto> VerifyCertificateAsync(VerifyCertificateRequestDto request, CancellationToken ct = default)
    {
        try
        {
            using var reqMsg = CreateAuthorizedRequest(HttpMethod.Post, "/v1/tutor/verify-certificate", request);
            var response = await _http.SendAsync(reqMsg, ct);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<VerifyCertificateResponseDto>(content, JsonOptions);
                if (result != null) return result;
            }
            _logger.LogWarning("AI Certificate Verification service returned status {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call AI certificate verification endpoint");
        }

        return new VerifyCertificateResponseDto
        {
            TutorName = request.ExpectedTutorName ?? "",
            CertificateType = request.QualificationType ?? "Chứng chỉ chuyên môn",
            IssuingOrganization = "Chưa trích xuất được",
            AuthenticityStatus = "UNCLEAR",
            ConfidenceScore = 0.5,
            Anomalies = new List<string> { "Dịch vụ AI kiểm định tạm thời không phản hồi. Vui lòng đối chiếu bằng mắt thường." },
            QualificationStandardMet = false,
            Recommendation = "MANUAL_REVIEW_REQUIRED",
            SummaryNotes = "Cần quản trị viên kiểm tra thủ công bằng mắt thường."
        };
    }

    private static int EstimateTokens(string? prompt, string? reply)
    {
        var chars = (prompt?.Length ?? 0) + (reply?.Length ?? 0);
        return Math.Max(150, (int)Math.Ceiling(chars / 3.0) + 120);
    }

    private async Task RecordAiUsageAsync(string? userId, string action, string? question, int tokensUsed, int? studySessionId, CancellationToken ct)
    {
        try
        {
            var httpCtx = _httpContextAccessor?.HttpContext;
            var ip = httpCtx?.Connection.RemoteIpAddress?.ToString();
            var ua = httpCtx?.Request.Headers.UserAgent.ToString();
            if (!string.IsNullOrEmpty(ua) && ua.Length > 480) ua = ua[..480];

            var qPreview = (question ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            if (qPreview.Length > 100) qPreview = qPreview[..100] + "...";

            var detail = studySessionId.HasValue
                ? $"tokens={tokensUsed};studySessionId={studySessionId.Value};q={qPreview}"
                : $"tokens={tokensUsed};q={qPreview}";

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
                Category = "AiUsage",
                Action = action,
                Detail = detail,
                IpAddress = ip,
                UserAgent = ua,
                Timestamp = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể ghi log sử dụng AI vào AuditLogs");
        }
    }

    private static readonly string FounderContactMarkdown =
        "---\n" +
        "📞 **THÔNG TIN SÁNG LẬP & LIÊN HỆ HỖ TRỢ:**\n" +
        "Người sáng lập và phát triển nền tảng OTSP là **Ngọc Tuấn (Tống Ngọc Tuấn)**.\n" +
        "Khi người dùng muốn liên hệ hợp tác, góp ý hoặc cần người hỗ trợ gấp, hãy cung cấp đầy đủ và thân thiện các kênh liên hệ chính thức sau:\n" +
        "- ✉️ **Email:** tongngoctuan09@gmail.com\n" +
        "- 📱 **Hotline / Zalo hỗ trợ gấp:** 0984391940 (Ngọc Tuấn)\n" +
        "- 🌐 **Facebook trao đổi trực tiếp:** [Facebook Ngọc Tuấn](https://www.facebook.com/tuan.ngoc.639510)\n\n" +
        "Luôn động viên, chào đón người dùng kết nối khi gặp khó khăn hoặc cần trợ giúp khẩn cấp.";

    private async Task<(bool allowed, int currentCount, int maxLimit, string role, string cacheKey)> CheckDailyQuotaAsync(string? userId, CancellationToken ct)
    {
        var httpCtx = _httpContextAccessor?.HttpContext;
        var clientIp = httpCtx?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var todayUtc = DateTime.UtcNow.Date;
        var todayStr = todayUtc.ToString("yyyyMMdd");

        string role = "Guest";
        int maxLimit = 10; // Khách vãng lai: 10 câu / ngày (theo IP)
        string cacheKey = string.IsNullOrWhiteSpace(userId)
            ? $"ai_quota:ip:{clientIp}:{todayStr}"
            : $"ai_quota:user:{userId}:{todayStr}";

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var roleNames = await _db.UserRoles.AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .Join(_db.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
                .ToListAsync(ct);

            if (roleNames.Contains("Admin"))
            {
                role = "Admin";
                maxLimit = 500; // Admin: 500 câu / ngày
            }
            else if (roleNames.Contains("Tutor"))
            {
                role = "Tutor";
                maxLimit = 50; // Gia sư: 50 câu / ngày
            }
            else
            {
                role = "Student";
                maxLimit = 50; // Học viên / Phụ huynh: 50 câu / ngày
            }
        }

        int currentCount = 0;
        if (_cache != null && _cache.TryGetValue<int>(cacheKey, out var cachedCount))
        {
            currentCount = cachedCount;
        }
        else
        {
            currentCount = await _db.AuditLogs.AsNoTracking()
                .CountAsync(x => x.Category == "AiUsage" &&
                                 (string.IsNullOrWhiteSpace(userId) ? (x.UserId == null && x.IpAddress == clientIp) : x.UserId == userId) &&
                                 x.Timestamp >= todayUtc, ct);

            var timeUntilMidnight = todayUtc.AddDays(1) - DateTime.UtcNow;
            if (timeUntilMidnight <= TimeSpan.Zero) timeUntilMidnight = TimeSpan.FromHours(1);
            _cache?.Set(cacheKey, currentCount, timeUntilMidnight);
        }

        bool allowed = currentCount < maxLimit;
        return (allowed, currentCount, maxLimit, role, cacheKey);
    }

    private void IncrementDailyQuotaCache(string cacheKey, int newCount)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var timeUntilMidnight = todayUtc.AddDays(1) - DateTime.UtcNow;
        if (timeUntilMidnight <= TimeSpan.Zero) timeUntilMidnight = TimeSpan.FromHours(1);
        _cache?.Set(cacheKey, newCount, timeUntilMidnight);
    }

    private static AgentChatResponseDto BuildQuotaExceededResponse(string role, int maxLimit)
    {
        if (role == "Guest")
        {
            return new AgentChatResponseDto
            {
                Reply = $"Bạn đã sử dụng hết hạn mức **{maxLimit} câu hỏi miễn phí/ngày** dành cho khách vãng lai.\n\n" +
                        "💡 **Mẹo:** Bạn hãy **[Đăng nhập](/login)** hoặc **[Đăng ký tài khoản](/register)** để nhận ngay **50 câu hỏi/ngày** và lưu lại lịch sử trao đổi nhé!\n\n" +
                        FounderContactMarkdown,
                Intent = "GENERAL",
                AutoExecute = false,
                UiAction = new UIActionDto
                {
                    Type = "NAVIGATE",
                    Target = "/login",
                    Label = "Đăng nhập để nhận thêm lượt",
                    AutoExecute = false
                },
                SuggestedPrompts = new List<string> { "Đăng nhập tài khoản", "Đăng ký thành viên", "Tìm gia sư OTSP" }
            };
        }

        return new AgentChatResponseDto
        {
            Reply = $"Bạn đã sử dụng hết hạn mức **{maxLimit} câu hỏi/ngày** của tài khoản hôm nay. Hạn mức sẽ tự động được làm mới vào lúc **00:00 ngày mai**.\n\n" +
                    "Nếu bạn đang cần người hỗ trợ gấp, muốn nâng hạn mức trải nghiệm hoặc liên hệ hợp tác:\n\n" +
                    FounderContactMarkdown,
            Intent = "GENERAL",
            AutoExecute = false,
            SuggestedPrompts = new List<string> { "Xem lớp học của tôi", "Tìm kiếm gia sư", "Lịch học hôm nay" }
        };
    }
}

