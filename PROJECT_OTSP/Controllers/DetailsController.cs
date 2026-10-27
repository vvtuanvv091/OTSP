using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PROJECT_OTSP.Controllers;

[AllowAnonymous]
public class DetailsController : Controller
{
    [HttpGet]
    public IActionResult Index(int? id)
    {
        if (id.HasValue && id.Value > 0)
        {
            return RedirectToAction("Details", "Search", new { id = id.Value });
        }
        return RedirectToAction("Index", "Search");
    }
}
