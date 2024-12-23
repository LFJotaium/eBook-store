using Microsoft.AspNetCore.Mvc;

namespace ebookStore.Controllers;

public class HomeController: Controller
{
    public IActionResult Index()
    {
        return View();
    }
}