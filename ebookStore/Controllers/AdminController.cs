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

        [HttpGet("Admin/AddBook")]
        public IActionResult AddBook()
        {
            var username = HttpContext.Session.GetString("Username") ?? "test";
            if (!IsUserAdmin(username))
                return Unauthorized("You do not have permission to access this page.");

            return View(new Book());
        }

        [HttpPost("Admin/AddBook")]
        public IActionResult AddBook(Book book)
        {
            var username = HttpContext.Session.GetString("Username") ?? "test";
            if (!IsUserAdmin(username))
                return Unauthorized("You do not have permission to add books.");

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                string query = @"
                    INSERT INTO Books (Title, AuthorName, Publisher, PriceBuy, PriceBorrowing, YearOfPublish, Genre, CoverImagePath)
                    VALUES (@Title, @AuthorName, @Publisher, @PriceBuy, @PriceBorrowing, @YearOfPublish, @Genre, @CoverImagePath)
                    RETURNING ID;";

                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@Title", book.Title);
                command.Parameters.AddWithValue("@AuthorName", book.AuthorName);
                command.Parameters.AddWithValue("@Publisher", book.Publisher);
                command.Parameters.AddWithValue("@PriceBuy", book.PriceBuy);
                command.Parameters.AddWithValue("@PriceBorrowing", book.PriceBorrowing);
                command.Parameters.AddWithValue("@YearOfPublish", book.YearOfPublish);
                command.Parameters.AddWithValue("@Genre", book.Genre ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CoverImagePath", book.CoverImagePath ?? (object)DBNull.Value);

                int bookId = (int)command.ExecuteScalar();

                string priceQuery = @"
                    INSERT INTO Prices (bookId, currentpricebuy, currentpriceborrow, originalpricebuy, originalpriceborrow, isdiscounted, discountenddate)
                    VALUES (@bookid, @CurrentPriceBuy, @CurrentPriceBorrow, @OriginalPriceBuy, @OriginalPriceBorrow, false, NULL);";

                using var priceCommand = new NpgsqlCommand(priceQuery, connection);
                priceCommand.Parameters.AddWithValue("@bookid", bookId);
                priceCommand.Parameters.AddWithValue("@CurrentPriceBuy", book.PriceBuy);
                priceCommand.Parameters.AddWithValue("@CurrentPriceBorrow", book.PriceBorrowing);
                priceCommand.Parameters.AddWithValue("@OriginalPriceBuy", book.PriceBuy);
                priceCommand.Parameters.AddWithValue("@OriginalPriceBorrow", book.PriceBorrowing);
                priceCommand.ExecuteNonQuery();
                // Insert formats for the book in BookFormats table
                string insertFormatsQuery = @"
        INSERT INTO BookFormats (bookId, format)
        VALUES (@BookId, @Format);
    ";
                using var insertFormatCommand = new NpgsqlCommand(insertFormatsQuery, connection);
                insertFormatCommand.Parameters.AddWithValue("@BookId", bookId);
                // Add all formats
                string[] formats = { "EPUB", "FB2", "MOBI", "PDF" };
                foreach (var format in formats)
                {
                    insertFormatCommand.Parameters["@Format"].Value = format;
                    insertFormatCommand.ExecuteNonQuery();
                }
                TempData["Success"] = "Book added successfully with all formats.";
                return RedirectToAction("AddBook");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                ModelState.AddModelError("", "An error occurred while adding the book.");
                return View(book);
            }
        }

        [HttpGet("Admin/ManageBooks")]
        public IActionResult ManageBooks(string searchQuery)
        {
            var username = HttpContext.Session.GetString("Username") ?? "test";
            if (!IsUserAdmin(username))
                return Unauthorized("You do not have permission to access this page.");

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

        [HttpPost("Admin/DeleteBook")]
        public IActionResult DeleteBook(int bookId)
        {
            var username = HttpContext.Session.GetString("Username") ?? "test";
            if (!IsUserAdmin(username))
                return Unauthorized("You do not have permission to access this page.");

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            string query = "DELETE FROM Books WHERE ID = @ID";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@ID", bookId);
            command.ExecuteNonQuery();

            TempData["Message"] = "Book deleted successfully!";
            return RedirectToAction("ManageBooks");
        }

        [HttpGet("Admin/EditBook/{id}")]
        public IActionResult EditBook(int id)
        {
            var username = HttpContext.Session.GetString("Username") ?? "test";
            if (!IsUserAdmin(username))
                return Unauthorized("You do not have permission to access this page.");

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

[HttpPost]
public IActionResult EditBook(Book book, IFormFile EPUBFile, IFormFile FB2File, IFormFile MOBIFile, IFormFile PDFFile)
{
    var username = HttpContext.Session.GetString("Username") ?? "test";
    if (!IsUserAdmin(username))
        return Unauthorized("You do not have permission to access this page.");

    try
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            connection.Open();
            using (var transaction = connection.BeginTransaction())
            {
                // Update book details in Books table
                string updateBooksQuery = @"
                    UPDATE Books
                    SET Title = @Title, AuthorName = @AuthorName, Publisher = @Publisher, 
                        PriceBuy = @PriceBuy, PriceBorrowing = @PriceBorrowing, 
                        YearOfPublish = @YearOfPublish, Genre = @Genre, CoverImagePath = @CoverImagePath
                    WHERE ID = @ID";

                using (var bookCommand = new NpgsqlCommand(updateBooksQuery, connection))
                {
                    bookCommand.Parameters.AddWithValue("@ID", book.ID);
                    bookCommand.Parameters.AddWithValue("@Title", book.Title);
                    bookCommand.Parameters.AddWithValue("@AuthorName", book.AuthorName);
                    bookCommand.Parameters.AddWithValue("@Publisher", book.Publisher);
                    bookCommand.Parameters.AddWithValue("@PriceBuy", book.PriceBuy);
                    bookCommand.Parameters.AddWithValue("@PriceBorrowing", book.PriceBorrowing);
                    bookCommand.Parameters.AddWithValue("@YearOfPublish", book.YearOfPublish);
                    bookCommand.Parameters.AddWithValue("@Genre", book.Genre ?? (object)DBNull.Value);
                    bookCommand.Parameters.AddWithValue("@CoverImagePath", book.CoverImagePath ?? (object)DBNull.Value);

                    bookCommand.ExecuteNonQuery();
                }

                // Update prices in Prices table
                string updatePricesQuery = @"
                    UPDATE Prices
                    SET CurrentPriceBuy = @CurrentPriceBuy, CurrentPriceBorrow = @CurrentPriceBorrow
                    WHERE BookID = @BookID";

                using (var priceCommand = new NpgsqlCommand(updatePricesQuery, connection))
                {
                    priceCommand.Parameters.AddWithValue("@BookID", book.ID);
                    priceCommand.Parameters.AddWithValue("@CurrentPriceBuy", book.PriceBuy);
                    priceCommand.Parameters.AddWithValue("@CurrentPriceBorrow", book.PriceBorrowing);

                    priceCommand.ExecuteNonQuery();
                }

                // Handle file uploads and update formats
                string[] formats = { "EPUB", "FB2", "MOBI", "PDF" };
                IFormFile[] files = { EPUBFile, FB2File, MOBIFile, PDFFile };

                for (int i = 0; i < formats.Length; i++)
                {
                    if (files[i] != null && files[i].Length > 0)
                    {
                        // Save the uploaded file to a specific directory and get the file path
                        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "bookformats", $"{book.ID}_{formats[i]}.pdf");
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            files[i].CopyTo(stream);
                        }

                        // Update the format reference in BookFormats table
                        string updateFormatQuery = @"
                            UPDATE BookFormats 
                            SET Format = @Format, FilePath = @FilePath 
                            WHERE BookId = @BookId AND Format = @OldFormat";

                        using (var updateFormatCommand = new NpgsqlCommand(updateFormatQuery, connection))
                        {
                            updateFormatCommand.Parameters.AddWithValue("@BookId", book.ID);
                            updateFormatCommand.Parameters.AddWithValue("@Format", formats[i]);
                            updateFormatCommand.Parameters.AddWithValue("@FilePath", filePath);
                            updateFormatCommand.Parameters.AddWithValue("@OldFormat", formats[i]);  // Assuming you update the existing format

                            updateFormatCommand.ExecuteNonQuery();
                        }
                    }
                }

                transaction.Commit();
            }
        }

        TempData["Message"] = "Book updated successfully!";
        return RedirectToAction("BookDetails", new { id = book.ID });
    }
    catch (Exception ex)
    {
        TempData["Message"] = "An error occurred while updating the book.";
        return View(book);
    }
}


        [HttpPost("Admin/SetDiscount")]
        public IActionResult SetDiscount(int bookId, decimal discountedPriceBuy, decimal discountedPriceBorrow, DateTime discountEndDate)
        {
            var username = HttpContext.Session.GetString("Username") ?? "test";
            if (!IsUserAdmin(username))
                return Unauthorized("You do not have permission to access this page.");

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

                return RedirectToAction("ManageBooks");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to set discount: {ex.Message}");
                return View("Error");
            }
        }
        
        private bool IsUserAdmin(string username)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            string query = "SELECT Role FROM Users WHERE Username = @Username";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.Add(new NpgsqlParameter("@Username", NpgsqlTypes.NpgsqlDbType.Varchar) { Value = username });

            var role = command.ExecuteScalar()?.ToString();
            return role == "Admin";
        }
        //----------------------User Service------------------//
        //Delete User
        [HttpPost("Admin/DeleteUser")]
        public IActionResult DeleteUser(string username)
        {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();
                string query = "DELETE FROM Users WHERE Username = @Username";
                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@Username", username);
                command.ExecuteNonQuery();
                TempData["Message"] = "User removed successfully!";
            return RedirectToAction("ManageUsers");
        }
        // Manage Users with search functionality
        public IActionResult ManageUsers(string searchQuery)
        {
            var users = new List<User>();
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            // Adjusted query to handle search properly
            string query = "SELECT * FROM Users WHERE Username LIKE @SearchQuery OR Email LIKE @SearchQuery";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@SearchQuery", "%" + searchQuery + "%");
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var user = new User
                {
                    Username = reader.GetString(reader.GetOrdinal("Username")),
                    // Add other user properties here, assuming you have them in your User class
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    // Add more properties if needed
                };
                users.Add(user);
            }
            return View(users); // Return to the ManageUsers view with the list of users
        }
        // Waiting List 
        public IActionResult ManageWaitingList(string searchQuery)
        {
            var waitingList = new List<WaitingListEntry>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM WaitingList WHERE Username LIKE @SearchQuery ";
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SearchQuery", "%" + searchQuery + "%");
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var waitingListEntry = new WaitingListEntry
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                BookId = reader.GetInt32(reader.GetOrdinal("bookid")),
                                Username = reader.GetString(reader.GetOrdinal("username")),
                                DateAdded = reader.GetDateTime(reader.GetOrdinal("queuetime")),
                            };
                            waitingList.Add(waitingListEntry);
                        }
                    }
                }
            }

            return View(waitingList); 
        }


        [HttpPost]
        public IActionResult RemoveFromWaitingList(int waitingListId)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    string query = "DELETE FROM WaitingList WHERE id = @WaitingListId";
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@WaitingListId", waitingListId);
                        command.ExecuteNonQuery();
                    }

                    TempData["Message"] = "Entry successfully removed from the waiting list.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred: {ex.Message}";
            }

            return RedirectToAction("ManageWaitingList");
        }

    }
}