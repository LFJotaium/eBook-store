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
    // Borrow a book
    public IActionResult Borrow(int id)
    {
        var book = _context.Books.Find(id);
        if (book == null)
            return NotFound();

        var borrowedBooks = _context.BorrowedBooks.Where(b => b.Username == User.Identity.Name).ToList();

        // Limit check: Max 3 books borrowed
        if (borrowedBooks.Count >= 3)
            return BadRequest("You can only borrow up to 3 books.");

        // Check if available
        if (_context.BorrowedBooks.Any(b => b.BookId == id))
        {
            // Add user to waiting list
            _context.WaitingListEntries.Add(new WaitingListEntry
            {
                BookId = id,
                Username = User.Identity.Name,
                DateAdded = DateTime.Now
            });
            _context.SaveChanges();
            return Ok("Added to waiting list.");
        }

        // Borrow book
        _context.BorrowedBooks.Add(new BorrowedBook
        {
            BookId = id,
            Username = User.Identity.Name,
            BorrowDate = DateTime.Now,
            ReturnDate = DateTime.Now.AddDays(30)
        });
        _context.SaveChanges();
        return Ok("Book borrowed successfully.");
    }
    // Buy a book
    public IActionResult Buy(int id)
    {
        var book = _context.Books.Find(id);
        if (book == null)
            return NotFound();

        _context.PurchasedBooks.Add(new PurchasedBook
        {
            BookId = id,
            Username = User.Identity.Name,
            PurchaseDate = DateTime.Now
        });
        _context.SaveChanges();
        return Ok("Book purchased successfully.");
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