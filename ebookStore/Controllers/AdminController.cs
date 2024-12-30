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
        public IActionResult ManageBooks(string searchQuery)
        {
            var books = new List<Book>();

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
    
            string query = @"
        SELECT b.ID, b.Title, b.AuthorName, b.Publisher, 
               p.CurrentPriceBuy, p.CurrentPriceBorrow, 
               p.OriginalPriceBuy, p.OriginalPriceBorrow, 
               p.IsDiscounted, p.DiscountEndDate, 
               b.YearOfPublish, b.Genre, b.CoverImagePath
        FROM Books b
        LEFT JOIN Prices p ON b.ID = p.BookID
        WHERE b.Title ILIKE @SearchQuery OR b.AuthorName ILIKE @SearchQuery OR b.Publisher ILIKE @SearchQuery
        ORDER BY b.Title";
    
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@SearchQuery", "%" + searchQuery + "%");

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var book = new Book
                {
                    ID = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    AuthorName = reader.GetString(2),
                    Publisher = reader.GetString(3),
                    YearOfPublish = reader.GetInt32(10),
                    Genre = reader.GetString(11),
                    CoverImagePath = reader.GetString(12),
                    Price = new Price
                    {
                        CurrentPriceBuy = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                        CurrentPriceBorrow = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                        OriginalPriceBuy = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                        OriginalPriceBorrow = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                        IsDiscounted = reader.IsDBNull(8) ? false : reader.GetBoolean(8),
                        DiscountEndDate = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9)
                    }
                };
                books.Add(book);
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
        [HttpPost("Admin/SetDiscount")]
        public IActionResult SetDiscount(int bookId, decimal discountedPriceBuy, decimal discountedPriceBorrow, DateTime discountEndDate)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();
                using var transaction = connection.BeginTransaction();

                var command = new NpgsqlCommand("UPDATE Prices SET IsDiscounted = @IsDiscounted, DiscountEndDate = @DiscountEndDate, CurrentPriceBuy = @DiscountedPriceBuy, CurrentPriceBorrow = @DiscountedPriceBorrow WHERE BookID = @BookID", connection);
                command.Parameters.AddWithValue("@IsDiscounted", true);
                command.Parameters.AddWithValue("@DiscountEndDate", discountEndDate);
                command.Parameters.AddWithValue("@DiscountedPriceBuy", discountedPriceBuy);
                command.Parameters.AddWithValue("@DiscountedPriceBorrow", discountedPriceBorrow);
                command.Parameters.AddWithValue("@BookID", bookId);

                command.ExecuteNonQuery();
                transaction.Commit();

                return RedirectToAction("ManageBooks"); // Redirect back to the Manage Books page
            }
            catch (Exception ex)
            {
                // Log or handle the exception
                Console.WriteLine($"Failed to set discount: {ex.Message}");
                return View("Error"); // You can render an error page
            }
        }

    }
}
