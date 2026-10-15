using Microsoft.AspNetCore.Mvc;

namespace PROJECT_OTSP.Services
{
    public class ChatService : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
