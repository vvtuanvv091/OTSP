using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private static readonly HttpClient SharedGeminiFallbackClient = new() { Timeout = TimeSpan.FromSeconds(25) };

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
        IHttpClientFactory? httpClientFactory = null)
    {
        _http = http;
        _db = db;
        _config = config;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;

        _baseUrl = _config["AiService:BaseUrl"] ?? "http://localhost:8000";
        _apiKey = _config["AiService:InternalApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("AiService:InternalApiKey is not configured; internal AI calls will fall back if rejected.");
        }

        if (Uri.TryCreate(_baseUrl, UriKind.Absolute, out var uri))
        {
            _http.BaseAddress = uri;
        }
        // Timeout 8s: vừa đủ cho service Python phản hồi nhanh, nếu quá 8s sẽ fallback sang Gemini trực tiếp
        _http.Timeout = TimeSpan.FromSeconds(8);
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
                _logger.LogWarning("Python AI agent service offline or unreachable ({Msg}), falling back to direct Gemini", ex.Message);
            }
        }

        // 3. Fallback sang Direct Gemini API tốc độ cao, luôn chạy mượt cả trên Render & Vercel
        var (fallbackResult, geminiTokens) = await CallDirectGeminiFallbackAsync(request, ct);
        await RecordAiUsageAsync(currentUserId, "Hỏi AI Wein (Chat)", request.Message, geminiTokens, null, ct);
        return fallbackResult;
    }

    public async Task<StudyAssistantResponseDto> AskStudyAssistantAsync(StudyAssistantRequestDto request, string currentUserId, CancellationToken ct = default)
    {
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

        // Fallback sang Gemini giải bài tập trực tiếp nếu Python service chưa bật
        if (result == null || string.IsNullOrWhiteSpace(result.Answer))
        {
            (result, tokensUsed) = await SolveStudyQuestionWithGeminiAsync(request, ct);
        }

        result ??= new StudyAssistantResponseDto
        {
            Answer = "Hệ thống đã tiếp nhận câu hỏi của bạn. Vui lòng thử lại trong giây lát hoặc tham khảo tài liệu học tập trong mục Tài liệu.",
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

    private string GetGeminiApiKey()
    {
        var apiKey = _config["AiService:GeminiApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey)) return apiKey;

        apiKey = _config["GEMINI_API_KEY"];
        if (!string.IsNullOrWhiteSpace(apiKey)) return apiKey;

        apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrWhiteSpace(apiKey)) return apiKey;

        // Tự động tìm nạp từ file .env nếu có (local hoặc trên Render)
        var paths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "OTSP_WEIN_AI_AGENTS", ".env")
        };
        foreach (var p in paths)
        {
            try
            {
                if (File.Exists(p))
                {
                    foreach (var line in File.ReadAllLines(p))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("GEMINI_API_KEY=", StringComparison.OrdinalIgnoreCase))
                        {
                            var val = trimmed.Substring("GEMINI_API_KEY=".Length).Trim(' ', '"', '\'');
                            if (!string.IsNullOrWhiteSpace(val)) return val;
                        }
                    }
                }
            }
            catch { }
        }

        return "";
    }

    private async Task<(AgentChatResponseDto Response, int TokensUsed)> CallDirectGeminiFallbackAsync(AgentChatRequestDto request, CancellationToken ct)
    {
        try
        {
            var apiKey = GetGeminiApiKey();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Gemini API key is not configured. Please set GEMINI_API_KEY in environment variables or appsettings.json");
            }
            else
            {

            var model = _config["AiService:GeminiModel"];
            if (string.IsNullOrWhiteSpace(model)) model = _config["GEMINI_MODEL"];
            if (string.IsNullOrWhiteSpace(model)) model = "gemini-3.5-flash-lite";

            // Làm giàu ngữ cảnh bằng dữ liệu thực tế từ PostgreSQL (Toàn diện, thông minh & bảo mật tuyệt đối)
            var lowerMsg = (request.Message ?? "").ToLower();
            var liveContextBuilder = new StringBuilder();

            try
            {
                // 1. Thống kê tổng quan hệ thống để AI hiểu đúng quy mô thực tế của OTSP
                var enforceRole = await _db.Roles.AsNoTracking().AnyAsync(ct);
                var activeTutorProfiles = _db.TutorProfiles.AsNoTracking()
                    .Where(t => t.User.Status == AccountStatus.Active
                             && (!enforceRole || _db.UserRoles.Any(ur =>
                                 ur.UserId == t.UserId && _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Tutor"))));

                var totalTutors = await activeTutorProfiles.CountAsync(t => t.VerificationStatus == VerificationStatus.Approved, ct);
                var totalClasses = await _db.Classrooms.CountAsync(c => c.Status == ClassroomStatus.Active, ct);

                liveContextBuilder.AppendLine("\n[Dữ liệu hệ thống OTSP thời gian thực]:");
                liveContextBuilder.AppendLine($"- Tổng số gia sư chính thức đã kiểm duyệt trên nền tảng: {totalTutors} gia sư.");
                liveContextBuilder.AppendLine($"- Tổng số lớp học nhóm đang mở đăng ký: {totalClasses} lớp.");

                // 2. Tải toàn bộ danh sách gia sư trên hệ thống (CHỈ LẤY CÁC TRƯỜNG CÔNG KHAI - BẢO MẬT TUYỆT ĐỐI)
                // Lưu ý: Tuyệt đối không lấy Password, CCCD, Email, Phone, BankAccount để đảm bảo an toàn.
                var rawTutors = await activeTutorProfiles
                    .Include(t => t.User)
                    .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
                    .Select(t => new
                    {
                        t.Id,
                        Name = t.User.FullName ?? "",
                        Status = t.VerificationStatus,
                        HourlyRate = t.HourlyFee,
                        Rating = t.AverageRating,
                        Format = t.TeachingFormat == TeachingFormat.Online ? "Online" :
                                 t.TeachingFormat == TeachingFormat.Offline ? "Trực tiếp" : "Online & Trực tiếp",
                        SubjectNames = t.TutorSubjects.Select(ts => ts.Subject.Name).ToList()
                    })
                    .ToListAsync(ct);

                var allTutors = rawTutors.Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Status,
                    t.HourlyRate,
                    t.Rating,
                    t.Format,
                    Subjects = string.Join(", ", t.SubjectNames.Distinct())
                }).ToList();

                var unaccentMsg = RemoveDiacritics(lowerMsg);

                // Tìm gia sư khớp với câu hỏi người dùng (theo tên, môn học, từ khóa - hỗ trợ cả có dấu & không dấu)
                var matchedTutors = allTutors.Where(t =>
                {
                    var nameLower = (t.Name ?? "").ToLower();
                    var nameUnaccent = RemoveDiacritics(nameLower);
                    if (!string.IsNullOrEmpty(nameLower))
                    {
                        if (lowerMsg.Contains(nameLower) || unaccentMsg.Contains(nameUnaccent)) return true;
                        var parts = nameLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Any(p => p.Length >= 2 && lowerMsg.Contains(p))) return true;
                        var partsUnaccent = nameUnaccent.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (partsUnaccent.Any(p => p.Length >= 2 && unaccentMsg.Contains(p))) return true;
                    }

                    var subLower = (t.Subjects ?? "").ToLower();
                    var subUnaccent = RemoveDiacritics(subLower);
                    if (!string.IsNullOrEmpty(subLower))
                    {
                        var subs = subLower.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        if (subs.Any(s => lowerMsg.Contains(s.Trim()) || unaccentMsg.Contains(RemoveDiacritics(s.Trim())))) return true;
                    }

                    return false;
                }).ToList();

                // Nếu không khớp tên/môn cụ thể, nhưng người dùng hỏi về gia sư nói chung thì lấy top gia sư tiêu biểu đã duyệt
                if (!matchedTutors.Any() && (lowerMsg.Contains("gia sư") || lowerMsg.Contains("thầy") || lowerMsg.Contains("cô") || lowerMsg.Contains("học") || lowerMsg.Contains("dạy")))
                {
                    matchedTutors = allTutors.Where(t => t.Status == VerificationStatus.Approved).OrderByDescending(t => t.Rating).ThenBy(t => t.HourlyRate).Take(5).ToList();
                }

                if (matchedTutors.Any())
                {
                    liveContextBuilder.AppendLine("\n[Danh sách Gia sư thực tế liên quan trong hệ thống]:");
                    foreach (var t in matchedTutors.Take(8))
                    {
                        var subjectsText = string.IsNullOrWhiteSpace(t.Subjects) ? "Đang cập nhật" : t.Subjects;
                        var statusText = t.Status == VerificationStatus.Approved ? "Đã duyệt chính thức" : "Hồ sơ đang chờ duyệt";
                        liveContextBuilder.AppendLine($"- Gia sư: {t.Name} | Trạng thái: {statusText} | Môn dạy: {subjectsText} | Học phí: {t.HourlyRate:N0} đ/giờ | Đánh giá: {t.Rating:F1}★ | Hình thức: {t.Format}");
                    }
                }

                // 3. Tải danh sách lớp học nhóm (CHỈ LẤY CÁC TRƯỜNG CÔNG KHAI)
                var allClasses = await _db.Classrooms
                    .AsNoTracking()
                    .Include(c => c.Subject)
                    .Include(c => c.Tutor).ThenInclude(t => t.User)
                    .Where(c => c.Status == ClassroomStatus.Active)
                    .Select(c => new
                    {
                        c.Id,
                        c.NameClass,
                        Subject = c.Subject.Name,
                        Tutor = c.Tutor.User.FullName,
                        c.Fee,
                        StudentCount = c.ClassroomStudents.Count,
                        c.MaxStudents
                    })
                    .ToListAsync(ct);

                var matchedClasses = allClasses.Where(c =>
                {
                    var cName = (c.NameClass ?? "").ToLower();
                    var cSub = (c.Subject ?? "").ToLower();
                    var cTutor = (c.Tutor ?? "").ToLower();
                    return lowerMsg.Contains(cName) ||
                           lowerMsg.Contains(cSub) ||
                           lowerMsg.Contains(cTutor) ||
                           cName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(w => w.Length >= 3 && lowerMsg.Contains(w));
                }).ToList();

                if (!matchedClasses.Any() && (lowerMsg.Contains("lớp") || lowerMsg.Contains("nhóm") || lowerMsg.Contains("khoá") || lowerMsg.Contains("tuyển sinh")))
                {
                    matchedClasses = allClasses.Take(5).ToList();
                }

                if (matchedClasses.Any())
                {
                    liveContextBuilder.AppendLine("\n[Dữ liệu lớp học nhóm thực tế trên OTSP]:");
                    foreach (var c in matchedClasses.Take(6))
                    {
                        liveContextBuilder.AppendLine($"- Lớp: {c.NameClass} | Môn: {c.Subject} | Gia sư phụ trách: {c.Tutor} | Học phí: {c.Fee:N0} đ | Sĩ số: {c.StudentCount}/{c.MaxStudents}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Không thể nạp dữ liệu DB vào AI Context: {Msg}", ex.Message);
            }

            var systemPrompt = $@"Bạn là WEIN - Trợ lý AI thông minh và thân thiện của nền tảng kết nối gia sư và lớp học trực tuyến OTSP (Online Tutor Student Platform).
Phong cách: Thân thiện, tận tình, xưng 'mình' hoặc 'WEIN', gọi người dùng là 'bạn'. Có thể dùng emoji nhẹ nhàng, văn phong sư phạm chuẩn mực.
Nhiệm vụ:
- Giải đáp thắc mắc về tìm gia sư, đăng ký lớp học, học phí, lịch học trực tuyến, chính sách học thử và thanh toán VietQR trên OTSP.
- Trả lời bài tập toán, lý, hóa, văn, anh, công nghệ hoặc các thắc mắc học tập rõ ràng, mạch lạc theo từng bước.
- NGUYÊN TẮC VỀ DỮ LIỆU THỰC TẾ:
  + Căn cứ vào dữ liệu hệ thống được cung cấp bên dưới để trả lời chính xác.
  + OTSP có nhiều gia sư và lớp học trên toàn quốc. Tuyệt đối KHÔNG khẳng định hệ thống chỉ có vài gia sư nếu chỉ nhìn thấy một phần danh sách gợi ý.
  + Khi người dùng hỏi đích danh một gia sư (ví dụ: Phạm Anh Hào, Tống Ngọc...), nếu có trong danh sách nạp bên dưới thì giới thiệu rõ môn dạy, học phí và điểm đánh giá của gia sư đó.
  + Nếu người dùng hỏi một gia sư hoàn toàn không có trong hệ thống, hãy trả lời lịch sự rằng chưa tìm thấy hồ sơ đó và gợi ý tìm theo môn học hoặc liên hệ hỗ trợ.
- BẢO MẬT: Tuyệt đối KHÔNG tiết lộ thông tin nhạy cảm của người dùng (mật khẩu, CCCD, số điện thoại cá nhân, số tài khoản ngân hàng chi tiết, lịch sử giao dịch cá nhân).
- ĐIỀU HƯỚNG: KHÔNG tự động chuyển trang web của người dùng. Chỉ gợi ý liên kết dạng Markdown [Tên nút](/duong-dan) hoặc nút hành động khi cần.
{liveContextBuilder}";

            var contents = new List<object>();

            // Nạp tối đa 6 tin nhắn lịch sử gần nhất nếu có
            if (request.ConversationHistory != null && request.ConversationHistory.Count > 0)
            {
                foreach (var h in request.ConversationHistory.TakeLast(6))
                {
                    if (string.IsNullOrWhiteSpace(h.Content)) continue;
                    var role = (h.Role == "assistant" || h.Role == "model") ? "model" : "user";
                    contents.Add(new { role = role, parts = new[] { new { text = h.Content } } });
                }
            }

            contents.Add(new { role = "user", parts = new[] { new { text = request.Message } } });

            var payload = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = contents,
                generationConfig = new
                {
                    maxOutputTokens = 1200,
                    temperature = 0.7
                }
            };

            var directHttp = _httpClientFactory?.CreateClient("Gemini") ?? SharedGeminiFallbackClient;
            var json = JsonSerializer.Serialize(payload);

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
            using var reqMsg = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            reqMsg.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

            var res = await directHttp.SendAsync(reqMsg, ct);

            // Nếu model 3.5 gặp lỗi, thử tiếp fallback qua gemini-2.5-flash
            if (!res.IsSuccessStatusCode && model != "gemini-2.5-flash")
            {
                var retryUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
                using var retryMsg = new HttpRequestMessage(HttpMethod.Post, retryUrl)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                retryMsg.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
                res = await directHttp.SendAsync(retryMsg, ct);
            }

            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var first = candidates[0];
                    var parts = first.GetProperty("content").GetProperty("parts");
                    if (parts.GetArrayLength() > 0)
                    {
                        var text = parts[0].GetProperty("text").GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var lower = (request.Message ?? string.Empty).ToLower();
                            UIActionDto uiAction = new UIActionDto { Type = "NONE", AutoExecute = false };
                            if (lower.Contains("gia sư") || lower.Contains("tìm"))
                            {
                                uiAction = new UIActionDto { Type = "NAVIGATE", Target = "/search", Label = "Tìm kiếm gia sư", AutoExecute = false };
                            }
                            else if (lower.Contains("lớp") || lower.Contains("khóa"))
                            {
                                uiAction = new UIActionDto { Type = "NAVIGATE", Target = "/search?tab=classes", Label = "Xem danh sách lớp", AutoExecute = false };
                            }
                            else if (lower.Contains("lịch") || lower.Contains("thời khóa biểu"))
                            {
                                uiAction = new UIActionDto { Type = "NAVIGATE", Target = "/student/schedule", Label = "Xem thời khóa biểu", AutoExecute = false };
                            }

                            var realTokens = ExtractGeminiTokens(doc, systemPrompt + "\n" + request.Message, text);
                            return (new AgentChatResponseDto
                            {
                                Reply = text,
                                Intent = "GENERAL",
                                UiAction = uiAction,
                                AutoExecute = false,
                                SuggestedPrompts = new List<string> { "Tìm gia sư môn Toán", "Xem danh sách lớp học mới", "Xem thời khóa biểu của tôi" }
                            }, realTokens);
                        }
                    }
                }
            }
            else
            {
                var err = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Direct Gemini fallback returned status {StatusCode}: {Error}", res.StatusCode, err);
            }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Direct Gemini fallback");
        }

        var defaultReply = "Chào bạn! Mình là WEIN, trợ lý AI của OTSP đây ạ. 😊 Bạn cần tìm gia sư môn nào hay muốn tham khảo lớp học nào hôm nay? Mình luôn sẵn sàng hỗ trợ bạn!";
        return (new AgentChatResponseDto
        {
            Reply = defaultReply,
            Intent = "GENERAL",
            UiAction = new UIActionDto { Type = "NAVIGATE", Target = "/search", Label = "Tìm kiếm gia sư", AutoExecute = false },
            AutoExecute = false,
            SuggestedPrompts = new List<string> { "Tìm gia sư môn Toán", "Xem các lớp học mới", "Xem thời khóa biểu" }
        }, EstimateTokens(request.Message, defaultReply));
    }

    private async Task<(StudyAssistantResponseDto Response, int TokensUsed)> SolveStudyQuestionWithGeminiAsync(StudyAssistantRequestDto request, CancellationToken ct)
    {
        try
        {
            var apiKey = GetGeminiApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Gemini API key is not configured for Study Assistant.");
                return (new StudyAssistantResponseDto
                {
                    Answer = "Hệ thống AI đang được bảo trì. Vui lòng thử lại sau.",
                    ConceptSummary = "Kiến thức",
                    PracticeQuestion = "",
                    PracticeSolution = ""
                }, 0);
            }

            var model = _config["AiService:GeminiModel"];
            if (string.IsNullOrWhiteSpace(model)) model = _config["GEMINI_MODEL"];
            if (string.IsNullOrWhiteSpace(model)) model = "gemini-3.5-flash-lite";

            var systemPrompt = @"Bạn là Chuyên gia Sư phạm & Trợ lý Giải bài tập WEIN của nền tảng OTSP.
Nhiệm vụ: Giải bài tập cho học sinh theo phương pháp sư phạm mẫu mực:
1. Nêu rõ phương pháp / kiến thức trọng tâm cần dùng.
2. Trình bày lời giải chi tiết, từng bước kèm giải thích dễ hiểu.
3. Đưa ra kết luận cuối cùng rõ ràng.
4. Gợi ý 1 bài tập tương tự để học sinh tự luyện tập.";

            var promptText = $"Môn học: {request.Subject ?? "Chung"}, Khối lớp: {request.GradeLevel ?? "Phổ thông"}\nĐề bài / Câu hỏi: {request.Question}";

            var payload = new
            {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = promptText } } } },
                generationConfig = new { maxOutputTokens = 1500, temperature = 0.5 }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
            var directHttp = _httpClientFactory?.CreateClient("Gemini") ?? SharedGeminiFallbackClient;
            var json = JsonSerializer.Serialize(payload);
            using var reqMsg = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            reqMsg.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

            var res = await directHttp.SendAsync(reqMsg, ct);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var first = candidates[0];
                    var parts = first.GetProperty("content").GetProperty("parts");
                    if (parts.GetArrayLength() > 0)
                    {
                        var text = parts[0].GetProperty("text").GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var realTokens = ExtractGeminiTokens(doc, systemPrompt + "\n" + promptText, text);
                            return (new StudyAssistantResponseDto
                            {
                                Answer = text,
                                ConceptSummary = $"Kiến thức trọng tâm môn {request.Subject ?? "Học tập"}",
                                PracticeQuestion = "Bạn có muốn luyện thêm một bài tập tương tự dạng này không?",
                                PracticeSolution = ""
                            }, realTokens);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to solve study question with direct Gemini");
        }

        var fallbackAnswer = "Hệ thống đã tiếp nhận câu hỏi của bạn. Vui lòng thử lại trong giây lát hoặc tham khảo tài liệu học tập trong mục Tài liệu.";
        return (new StudyAssistantResponseDto
        {
            Answer = fallbackAnswer,
            ConceptSummary = "Kiến thức môn học",
            PracticeQuestion = "",
            PracticeSolution = ""
        }, EstimateTokens(request.Question, fallbackAnswer));
    }

    private static int ExtractGeminiTokens(JsonDocument doc, string prompt, string reply)
    {
        try
        {
            if (doc.RootElement.TryGetProperty("usageMetadata", out var usage))
            {
                if (usage.TryGetProperty("totalTokenCount", out var totalProp) && totalProp.TryGetInt32(out var total) && total > 0)
                {
                    return total;
                }

                int promptTok = usage.TryGetProperty("promptTokenCount", out var p) && p.TryGetInt32(out var pt) ? pt : 0;
                int candTok = usage.TryGetProperty("candidatesTokenCount", out var c) && c.TryGetInt32(out var ctVal) ? ctVal : 0;
                if (promptTok + candTok > 0)
                {
                    return promptTok + candTok;
                }
            }
        }
        catch { }

        return EstimateTokens(prompt, reply);
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

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? "";
        var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalizedString)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
    }
}

