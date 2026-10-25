using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;

namespace PROJECT_OTSP.Areas.Tutor.Controllers
{
    [Area("Tutor")]
    [Authorize(Roles = "Tutor")]
    public class TutorDashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
