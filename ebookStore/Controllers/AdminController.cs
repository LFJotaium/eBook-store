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

        // Manage Books 
        [HttpGet("Admin/ManageBooks")]
        public IActionResult ManageBooks()
        {
            var books = new List<Book>();

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            string query = "SELECT * FROM Books ORDER BY Title";
            using var command = new NpgsqlCommand(query, connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                books.Add(new Book
                {
                    ID = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    AuthorName = reader.GetString(2),
                    Publisher = reader.GetString(3),
                    PriceBuy = reader.GetDecimal(4),
                    PriceBorrowing = reader.GetDecimal(5),
                    YearOfPublish = reader.GetInt32(6),
                    Genre = reader.GetString(7),
                    CoverImagePath = reader.GetString(8)
                });
            }

            return View(books);
        }

        // Deleting books action
        [HttpPost("Admin/DeleteBook")]
        public IActionResult DeleteBook(int bookId)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            string query = "DELETE FROM Books WHERE ID = @ID";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@ID", bookId);
            command.ExecuteNonQuery();

            TempData["Message"] = "Book deleted successfully!";
            return RedirectToAction("ManageBooks");
        }

        // Edit Book - GET
        [HttpGet("Admin/EditBook/{id}")]
        public IActionResult EditBook(int id)
        {
            Book book;
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM Books WHERE ID = @ID";
                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", id);
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    book = new Book
                    {
                        ID = reader.GetInt32(0),
                        Title = reader.GetString(1),
                        AuthorName = reader.GetString(2),
                        Publisher = reader.GetString(3),
                        PriceBuy = reader.GetDecimal(4),
                        PriceBorrowing = reader.GetDecimal(5),
                        YearOfPublish = reader.GetInt32(6),
                        Genre = reader.GetString(7),
                        CoverImagePath = reader.GetString(8)
                    };
                }
                else
                {
                    return NotFound();
                }
            }

            return View(book);
        }

        // Edit Book - POST
        [HttpPost("Admin/EditBook")]
        public IActionResult EditBook(Book book)
        {
            if (!ModelState.IsValid)
            {
                return View(book);
            }

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                string query = @"
                UPDATE Books
                SET Title = @Title, AuthorName = @AuthorName, Publisher = @Publisher, 
                    PriceBuy = @PriceBuy, PriceBorrowing = @PriceBorrowing, 
                    YearOfPublish = @YearOfPublish, Genre = @Genre, CoverImagePath = @CoverImagePath
                WHERE ID = @ID";
                using var command = new NpgsqlCommand(query, connection);

                command.Parameters.AddWithValue("@ID", book.ID);
                command.Parameters.AddWithValue("@Title", book.Title);
                command.Parameters.AddWithValue("@AuthorName", book.AuthorName);
                command.Parameters.AddWithValue("@Publisher", book.Publisher);
                command.Parameters.AddWithValue("@PriceBuy", book.PriceBuy);
                command.Parameters.AddWithValue("@PriceBorrowing", book.PriceBorrowing);
                command.Parameters.AddWithValue("@YearOfPublish", book.YearOfPublish);
                command.Parameters.AddWithValue("@Genre", book.Genre ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CoverImagePath", book.CoverImagePath ?? (object)DBNull.Value);

                command.ExecuteNonQuery();
            }

            TempData["Message"] = "Book updated successfully!";
            return RedirectToAction("ManageBooks");
        }
    }
}
