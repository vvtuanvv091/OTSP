namespace Backend_Project_OTSP.Dtos.Chat;

public class SendChatMessageRequest
{
    public string ContactUserId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
}

public sealed class EditChatMessageRequest
{
    public string Content { get; set; } = string.Empty;
}
