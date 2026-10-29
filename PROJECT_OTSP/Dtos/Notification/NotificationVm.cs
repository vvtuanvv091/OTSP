namespace PROJECT_OTSP.Dtos.Notification;

public class NotificationItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public string TargetUrl { get; set; } = "/Notifications";
}

public class NotificationPageVm
{
    public IReadOnlyList<NotificationItemDto> Items { get; set; } = [];
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / Math.Max(PageSize, 1));
    public bool HasMore => CurrentPage < TotalPages;
}

