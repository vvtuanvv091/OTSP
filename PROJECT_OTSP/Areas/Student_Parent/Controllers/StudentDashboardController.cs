using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PROJECT_OTSP.Areas.Student_Parent.Controllers;

[Area("Student_Parent")]
[Authorize(Roles = "Student_Parent")]
public class StudentDashboardController : Controller
{
    public IActionResult Index() => View();
}
