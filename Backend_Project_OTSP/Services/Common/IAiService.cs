using Backend_Project_OTSP.Dtos;

namespace Backend_Project_OTSP.Services.Common;

public interface IAiService
{
    Task<AgentChatResponseDto> ChatWithAgentAsync(AgentChatRequestDto request, string? currentUserId, CancellationToken ct = default);
    Task<StudyAssistantResponseDto> AskStudyAssistantAsync(StudyAssistantRequestDto request, string currentUserId, CancellationToken ct = default);
    Task<ReviewModerationResponseDto> AnalyzeReviewAndAlertAsync(int reviewId, int score, string? comment, string studentName, string tutorName, string classroomName, CancellationToken ct = default);
    Task<bool> SubmitFeedbackAsync(FeedbackRequestDto request, CancellationToken ct = default);
    Task<VerifyCertificateResponseDto> VerifyCertificateAsync(VerifyCertificateRequestDto request, CancellationToken ct = default);
}

