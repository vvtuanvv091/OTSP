namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Dữ liệu tổng hợp hiển thị toàn bộ trang chủ của hệ thống.
/// </summary>
public class HomeViewModel
{
    public int StudentCount { get; set; }
    public int TutorCount { get; set; }
    public int SubjectCount { get; set; }
    public int CompletedSessionCount { get; set; }
    public List<SubjectCardDto> Subjects { get; set; } = new();
    public List<ClassCardDto> Classes { get; set; } = new();
    public List<TutorCardDto> FeaturedTutors { get; set; } = new();
}
