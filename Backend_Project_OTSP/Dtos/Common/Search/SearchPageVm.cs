using Microsoft.AspNetCore.Mvc.Rendering;
namespace Backend_Project_OTSP.Dtos;
public class SearchPageVm
{
    public SearchQuery Query { get; set; } = new();
    public string Tab { get; set; } = "tutors";    // "tutors" | "classes"
    public List<SelectListItem> Subjects { get; set; } = new();
    public List<SelectListItem> GradeLevels { get; set; } = new();
}
