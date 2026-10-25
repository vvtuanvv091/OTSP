using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace PROJECT_OTSP.Controllers
{
    public class SetLanguageController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public IActionResult SetPreferences(string? culture, string? theme, string? returnUrl)
        {
            var options = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            };

            if (culture is "vi" or "en")
                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                    options);

            if (theme is "light" or "dark")
                Response.Cookies.Append("otsp-theme", theme, options);

            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
        }
    }
}
