using ebookStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class BooksController : Controller
{
    private readonly EbookContext _context;

    public BooksController(EbookContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        var books = _context.Books.Include(b => b.Price).ToList();
        return View(books);
    }


    public IActionResult Create()
    {
        return View();
    }
    

    public IActionResult SetDiscount(int id, decimal discountedPriceBuy, decimal discountedPriceBorrow, DateTime endDate)
    {
        var price = _context.Prices.FirstOrDefault(p => p.BookID == id);
        if (price != null)
        {
            price.IsDiscounted = true;
            price.CurrentPriceBuy = discountedPriceBuy;
            price.CurrentPriceBorrow = discountedPriceBorrow;
            price.DiscountEndDate = endDate;
            _context.SaveChanges();
        }
        return RedirectToAction(nameof(Index));
    }
}