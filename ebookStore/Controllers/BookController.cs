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
    // GET: Books
    public IActionResult Index(string searchQuery)
    {
        var books = _context.Books.AsQueryable();

        if (!string.IsNullOrEmpty(searchQuery))
        {
            books = books.Where(b => b.Title.Contains(searchQuery) || 
                                     b.AuthorName.Contains(searchQuery) || 
                                     b.Publisher.Contains(searchQuery));
        }

        return View(books.ToList());
    }
    // Leave feedback
    [HttpPost]
    public IActionResult LeaveFeedback(int id, string comment, int rating)
    {
        var book = _context.Books.Find(id);
        if (book == null)
            return NotFound();

        _context.Feedbacks.Add(new Feedback
        {
            BookId = id,
            Username = User.Identity.Name,
            Comment = comment,
            Rating = rating,
            FeedbackDate = DateTime.Now
        });
        _context.SaveChanges();
        return Ok("Feedback added successfully.");
    }


    //------------------------------------------------------------------

    // POST: Books/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create([Bind("Title,AuthorName,Publshier,PriceBuy,PriceBorrowing,YearOfPublish,Genre,CoverImagePath")] Book book)
    {
        if (ModelState.IsValid)
        {
            _context.Add(book);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
        return View(book);
    }

}