using ebookStore.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ebookStore.Controllers
{
    public class AdminController : Controller
    {
        private readonly string _connectionString;

        public AdminController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnectionString");
        }

        // Display the Add Book Form
        [HttpGet("Admin/AddBook")]
        public IActionResult AddBook()
        {
            return View(new Book());
        }

        // Handle the Add Book Form Submission
        [HttpPost("Admin/AddBook")]
        public IActionResult AddBook(Book book)
        {
            if (!ModelState.IsValid)
            {
                return View(book);
            }

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                string query = @"
                INSERT INTO Books (Title, AuthorName, Publisher, PriceBuy, PriceBorrowing, YearOfPublish, Genre, CoverImagePath)
                VALUES (@Title, @AuthorName, @Publisher, @PriceBuy, @PriceBorrowing, @YearOfPublish, @Genre, @CoverImagePath)";
                using var command = new NpgsqlCommand(query, connection);

                command.Parameters.AddWithValue("@Title", book.Title);
                command.Parameters.AddWithValue("@AuthorName", book.AuthorName);
                command.Parameters.AddWithValue("@Publisher", book.Publisher);
                command.Parameters.AddWithValue("@PriceBuy", book.PriceBuy);
                command.Parameters.AddWithValue("@PriceBorrowing", book.PriceBorrowing);
                command.Parameters.AddWithValue("@YearOfPublish", book.YearOfPublish);
                command.Parameters.AddWithValue("@Genre", book.Genre ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CoverImagePath", book.CoverImagePath ?? (object)DBNull.Value);

                command.ExecuteNonQuery();
                TempData["Message"] = "Book added successfully!";
                return RedirectToAction("AddBook");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while adding the book.");
                return View(book);
            }
        }
    }
}
