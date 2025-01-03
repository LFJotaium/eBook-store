using Microsoft.AspNetCore.Mvc;

namespace ebookStore.Controllers;

public class CartController: Controller
{
    private readonly string _paypalClientId;

    public CartController(IConfiguration configuration)
    {
        _paypalClientId = configuration["PaypalSettings:ClientId"];
    }

    [HttpGet]
    [Route("/cart/Checkout")]
    public IActionResult Checkout()
    {
        ViewBag.PaypalClientId = _paypalClientId;
        Console.WriteLine("Checkout Page Loaded from CartController");
        return View("Checkout"); // Ensure it points to the correct view
    }
}