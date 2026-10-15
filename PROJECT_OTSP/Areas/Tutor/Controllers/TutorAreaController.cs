using System;
using Microsoft.AspNetCore.Mvc;

namespace PROJECT_OTSP.Areas.Tutor.Controllers;

/// <summary>
/// Shares the full-page and content-only rendering behavior for tutor index pages.
/// </summary>
public abstract class TutorAreaController : Controller
{
    protected IActionResult RenderIndex<TModel>(TModel model)
    {
        Response.Headers["Vary"] = "X-Requested-With";

        if (string.Equals(
                Request.Headers["X-Requested-With"].ToString(),
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase))
        {
            Response.Headers["X-OTSP-Partial"] = "true";
            return PartialView("Index", model);
        }

        return View("Index", model);
    }
}
