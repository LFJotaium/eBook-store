using Microsoft.AspNetCore.Mvc;

namespace ebookStore.Controllers
{
    public class LogoutController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}