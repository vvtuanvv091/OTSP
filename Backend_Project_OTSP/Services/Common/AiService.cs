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
        ILogger<AiService> logger)
    {
        _http = http;
        _db = db;
        _config = config;
        _logger = logger;

        _baseUrl = _config["AiService:BaseUrl"] ?? "http://localhost:8000";
        _apiKey = _config["AiService:InternalApiKey"] ?? "Qxzyo3UdQQ2RtWxJAFGOhhCIDwIbGSWTzsC-ShRA324";

        _http.BaseAddress = new Uri(_baseUrl);
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path, object? body = null)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-Internal-Key", _apiKey);
        if (body != null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        return req;
    }

    public async Task<AgentChatResponseDto> ChatWithAgentAsync(AgentChatRequestDto request, string? currentUserId, CancellationToken ct = default)
    {
        // 1. Enrich context if user is authenticated
        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId, ct);
            if (user != null)
            {
                request.SystemContext["userName"] = user.FullName;

                // If user is Student, find upcoming sessions
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
                else
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

        try
        {
            using var reqMsg = CreateAuthorizedRequest(HttpMethod.Post, "/v1/agent/chat", request);
            var response = await _http.SendAsync(reqMsg, ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<AgentChatResponseDto>(content, JsonOptions);
                if (result != null) return result;
            }
            else
            {
                _logger.LogWarning("AI agent request returned status {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call AI Service for Agent Chat");
        }

        // Fallback to Direct Gemini API if Python service is offline or sleeping
        return await CallDirectGeminiFallbackAsync(request, ct);
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
        try
        {
            using var reqMsg = CreateAuthorizedRequest(HttpMethod.Post, "/v1/study-assistant", payload);
            var response = await _http.SendAsync(reqMsg, ct);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                result = JsonSerializer.Deserialize<StudyAssistantResponseDto>(content, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call AI Service for Study Assistant");
        }

        result ??= new StudyAssistantResponseDto
        {
            Answer = "Hệ thống đã tiếp nhận câu hỏi của bạn. Vui lòng thử lại trong giây lát hoặc tham khảo tài liệu học tập trong mục Tài liệu.",
            ConceptSummary = "Kiến thức môn học",
            PracticeQuestion = "",
            PracticeSolution = ""
        };

        // Automatically persist into AiStudySessions table for Admin metrics and student history!
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
                _logger.LogInformation("Saved AiStudySession for studentId={StudentId}", student.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save AiStudySession in database");
            }
        }

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

    private async Task<AgentChatResponseDto> CallDirectGeminiFallbackAsync(AgentChatRequestDto request, CancellationToken ct)
    {
        try
        {
            var apiKey = _config["AiService:GeminiApiKey"] 
                ?? _config["GEMINI_API_KEY"] 
                ?? "";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Gemini API key is not configured in AiService:GeminiApiKey or GEMINI_API_KEY");
            }
            else
            {
                var model = _config["AiService:GeminiModel"] ?? "gemini-3.5-flash-lite";
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var systemPrompt = @"Bạn là WEIN - Trợ lý AI thông minh của nền tảng kết nối gia sư và lớp học trực tuyến OTSP (Online Tutor Student Platform).
Phong cách: Thân thiện, tận tình, xưng 'mình' hoặc 'WEIN', gọi người dùng là 'bạn'. Có thể dùng emoji nhẹ nhàng.
Nhiệm vụ:
- Giải đáp thắc mắc về tìm gia sư, đăng ký lớp học, học phí, lịch học trực tuyến trên OTSP.
- Giúp học sinh định hướng học tập, trả lời câu hỏi ngắn gọn, mạch lạc.
- Nếu người dùng chào hỏi hoặc hỏi bâng quơ (như 'alo', 'ủa', 'hi'), hãy chào mừng nhiệt tình và gợi ý các tính năng nổi bật: Tìm gia sư, Đăng ký lớp học, Xem lịch học.";

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
                    maxOutputTokens = 800,
                    temperature = 0.7
                }
            };

            using var directHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var json = JsonSerializer.Serialize(payload);
            using var reqMsg = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

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
                            var lower = request.Message.ToLower();
                            UIActionDto uiAction = new UIActionDto { Type = "NONE" };
                            if (lower.Contains("gia sư") || lower.Contains("tìm"))
                            {
                                uiAction = new UIActionDto { Type = "NAVIGATE", Target = "/search", Label = "Tìm kiếm gia sư" };
                            }
                            else if (lower.Contains("lớp") || lower.Contains("khóa"))
                            {
                                uiAction = new UIActionDto { Type = "NAVIGATE", Target = "/search?tab=classes", Label = "Xem danh sách lớp" };
                            }
                            else if (lower.Contains("lịch") || lower.Contains("thời khóa biểu"))
                            {
                                uiAction = new UIActionDto { Type = "NAVIGATE", Target = "/schedule", Label = "Xem thời khóa biểu" };
                            }

                            return new AgentChatResponseDto
                            {
                                Reply = text,
                                Intent = "GENERAL",
                                UiAction = uiAction,
                                SuggestedPrompts = new List<string> { "Tìm gia sư môn Toán", "Xem danh sách lớp học mới", "Xem thời khóa biểu của tôi" }
                            };
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

        return new AgentChatResponseDto
        {
            Reply = "Chào bạn! Mình là WEIN, trợ lý AI của OTSP đây ạ. 😊 Bạn cần tìm gia sư môn nào hay muốn tham khảo lớp học nào hôm nay? Mình luôn sẵn sàng hỗ trợ bạn!",
            Intent = "GENERAL",
            UiAction = new UIActionDto { Type = "NAVIGATE", Target = "/search", Label = "Tìm kiếm gia sư" },
            SuggestedPrompts = new List<string> { "Tìm gia sư môn Toán", "Xem các lớp học mới", "Xem thời khóa biểu" }
        };
    }
}

