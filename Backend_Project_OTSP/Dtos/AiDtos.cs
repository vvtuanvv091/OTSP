using System.Text.Json.Serialization;

namespace Backend_Project_OTSP.Dtos;

public class ConversationMessageDto
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class UIActionDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "NONE"; // "NAVIGATE", "OPEN_LINK", "NONE"

    [JsonPropertyName("target")]
    public string Target { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("actionType")]
    public string ActionType { get; set; } = "";

    [JsonPropertyName("autoExecute")]
    public bool AutoExecute { get; set; } = false;
}

public class AgentChatRequestDto
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("userId")]
    public int? UserId { get; set; }

    [JsonPropertyName("userRole")]
    public string UserRole { get; set; } = "Guest";

    [JsonPropertyName("currentPath")]
    public string CurrentPath { get; set; } = "/";

    [JsonPropertyName("conversationHistory")]
    public List<ConversationMessageDto> ConversationHistory { get; set; } = new();

    [JsonPropertyName("systemContext")]
    public Dictionary<string, object> SystemContext { get; set; } = new();
}

public class AgentChatResponseDto
{
    [JsonPropertyName("reply")]
    public string Reply { get; set; } = "";

    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "GENERAL";

    [JsonPropertyName("uiAction")]
    public UIActionDto UiAction { get; set; } = new();

    [JsonPropertyName("suggestedPrompts")]
    public List<string> SuggestedPrompts { get; set; } = new();

    // [JsonPropertyName("sources")]
    // public List<string> Sources { get; set; } = new();

    // [JsonPropertyName("recommendedTutorIds")]
    // public List<int> RecommendedTutorIds { get; set; } = new();

        //2 DÒNG NÀY ĐỂ ĐÓN LỆNH TỰ HÀNH:
    [JsonPropertyName("autoExecute")]
    public bool AutoExecute { get; set; } = false;
    [JsonPropertyName("actionType")]
    public string ActionType { get; set; } = "";
}

public class StudyAssistantRequestDto
{
    public string Question { get; set; } = "";
    public string? Subject { get; set; }
    public string? GradeLevel { get; set; }
}

public class StudyAssistantResponseDto
{
    [JsonPropertyName("answer")]
    public string Answer { get; set; } = "";

    [JsonPropertyName("conceptSummary")]
    public string ConceptSummary { get; set; } = "";

    [JsonPropertyName("practiceQuestion")]
    public string PracticeQuestion { get; set; } = "";

    [JsonPropertyName("practiceSolution")]
    public string PracticeSolution { get; set; } = "";
}

public class ReviewModerationRequestDto
{
    [JsonPropertyName("reviewId")]
    public int ReviewId { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = "";

    [JsonPropertyName("studentName")]
    public string StudentName { get; set; } = "";

    [JsonPropertyName("tutorName")]
    public string TutorName { get; set; } = "";

    [JsonPropertyName("classroomName")]
    public string ClassroomName { get; set; } = "";
}

public class ReviewModerationResponseDto
{
    [JsonPropertyName("isAbnormal")]
    public bool IsAbnormal { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = "Clean";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("suggestedAction")]
    public string SuggestedAction { get; set; } = "Approve";
}

public class FeedbackRequestDto
{
    [JsonPropertyName("question")]
    public string Question { get; set; } = "";

    [JsonPropertyName("answer")]
    public string Answer { get; set; } = "";

    [JsonPropertyName("rating")]
    public string Rating { get; set; } = "UP"; // "UP" | "DOWN"

    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "GENERAL";

    [JsonPropertyName("feedbackText")]
    public string FeedbackText { get; set; } = "";

    [JsonPropertyName("studentId")]
    public int? StudentId { get; set; }
}

public class VerifyCertificateRequestDto
{
    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("imageBase64")]
    public string? ImageBase64 { get; set; }

    [JsonPropertyName("expectedTutorName")]
    public string? ExpectedTutorName { get; set; }

    [JsonPropertyName("qualificationType")]
    public string? QualificationType { get; set; }
}

public class VerifyCertificateResponseDto
{
    [JsonPropertyName("tutorName")]
    public string TutorName { get; set; } = "";

    [JsonPropertyName("certificateType")]
    public string CertificateType { get; set; } = "";

    [JsonPropertyName("issuingOrganization")]
    public string IssuingOrganization { get; set; } = "";

    [JsonPropertyName("majorOrSubject")]
    public string MajorOrSubject { get; set; } = "";

    [JsonPropertyName("scoreOrClassification")]
    public string ScoreOrClassification { get; set; } = "";

    [JsonPropertyName("issueDate")]
    public string IssueDate { get; set; } = "";

    [JsonPropertyName("expiryDate")]
    public string? ExpiryDate { get; set; }

    [JsonPropertyName("isNameMatched")]
    public bool IsNameMatched { get; set; } = true;

    [JsonPropertyName("authenticityStatus")]
    public string AuthenticityStatus { get; set; } = "GENUINE";

    [JsonPropertyName("confidenceScore")]
    public double ConfidenceScore { get; set; } = 0.9;

    [JsonPropertyName("anomalies")]
    public List<string> Anomalies { get; set; } = new();

    [JsonPropertyName("qualificationStandardMet")]
    public bool QualificationStandardMet { get; set; } = true;

    [JsonPropertyName("recommendation")]
    public string Recommendation { get; set; } = "APPROVED_RECOMMENDED";

    [JsonPropertyName("summaryNotes")]
    public string SummaryNotes { get; set; } = "";
}

