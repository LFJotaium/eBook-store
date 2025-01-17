using Microsoft.AspNetCore.Mvc;


namespace ebookStore.Controllers
{
    public class AccountController : Controller
    {   

        [HttpGet]
        [Route("Account/LogIn")]
        public IActionResult LogIn()
        {
            return View();
        }
        [HttpGet]
        [Route("Account/SignUp")]
        public IActionResult SignUp()
        {
            return View();
        }
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            return RedirectToAction("Index", "Home");
        }
        
    }
}