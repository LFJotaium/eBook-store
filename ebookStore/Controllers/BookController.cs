using Microsoft.AspNetCore.Mvc;
using ebookStore.Models;

namespace ebookStore.Controllers
{
    public class BooksController : Controller
    {
        private readonly EbookContext _context;

        public BooksController(EbookContext context)
        {
            _context = context;
        }

        // GET: Books
        public IActionResult Index()
        {
            var books = _context.Books.ToList();
            return View(books);
        }

        // GET: Books/Create
        public IActionResult Create()
        {
            return View();
        }

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
}