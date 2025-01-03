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
            Console.WriteLine("account.cs");
            return View();
        }
        
    }
}