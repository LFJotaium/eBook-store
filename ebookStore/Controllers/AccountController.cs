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
            // Clear all session data
            HttpContext.Session.Clear();

            // Redirect to the home page or login page after logging out
            return RedirectToAction("Index", "Home");
        }
        
    }
}