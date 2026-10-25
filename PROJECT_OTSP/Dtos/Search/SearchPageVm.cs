using Microsoft.AspNetCore.Mvc.Rendering;

namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Model cung cấp dữ liệu cho view Search/Index gồm tiêu chí tìm kiếm và các danh mục bộ lọc.
/// </summary>
public class SearchPageVm
{
    public SearchQuery Query { get; set; } = new();
    public string Tab { get; set; } = "tutors";    // "tutors" | "classes"
    public List<SelectListItem> Subjects { get; set; } = new();
    public List<SelectListItem> GradeLevels { get; set; } = new();
}
