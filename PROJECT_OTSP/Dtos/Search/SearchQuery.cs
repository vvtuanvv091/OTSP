using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Tham số tìm kiếm truyền từ client qua query string.
/// Format chỉ áp dụng cho tab gia sư, ClassType chỉ áp dụng cho tab lớp học.
/// </summary>
public class SearchQuery
{
    public string? Keyword { get; set; }
    public int? SubjectId { get; set; }
    public int? GradeLevelId { get; set; }
    public TeachingFormat? Format { get; set; }
    public ClassroomType? ClassType { get; set; }
    public decimal? MaxFee { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}
