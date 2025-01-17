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
        book.CopiesAvailable = 3;
        string insertBookQuery = @"
            INSERT INTO Books (Title, AuthorName, Publisher, PriceBuy, PriceBorrowing, YearOfPublish, Genre, CoverImagePath, SoldCopies, AgeLimit, IsPopular, CopiesAvailable, Files, IsBuyOnly)
            VALUES (@Title, @AuthorName, @Publisher, @PriceBuy, @PriceBorrowing, @YearOfPublish, @Genre, @CoverImagePath, @SoldCopies, @AgeLimit, @IsPopular, @CopiesAvailable, @Files, @IsBuyOnly)
            RETURNING ID;";

        int bookId;
        using (var bookCommand = new NpgsqlCommand(insertBookQuery, connection))
        {
            bookCommand.Parameters.AddWithValue("@Title", book.Title);
            bookCommand.Parameters.AddWithValue("@AuthorName", book.AuthorName);
            bookCommand.Parameters.AddWithValue("@Publisher", book.Publisher);
            bookCommand.Parameters.AddWithValue("@PriceBuy", book.PriceBuy);
            bookCommand.Parameters.AddWithValue("@PriceBorrowing", book.PriceBorrowing ?? (object)DBNull.Value);
            bookCommand.Parameters.AddWithValue("@YearOfPublish", book.YearOfPublish);
            bookCommand.Parameters.AddWithValue("@Genre", book.Genre ?? (object)DBNull.Value);
            bookCommand.Parameters.AddWithValue("@CoverImagePath", book.CoverImagePath ?? (object)DBNull.Value);
            bookCommand.Parameters.AddWithValue("@SoldCopies", book.SoldCopies);
            bookCommand.Parameters.AddWithValue("@AgeLimit", book.AgeLimit ?? (object)DBNull.Value);
            bookCommand.Parameters.AddWithValue("@IsPopular", book.IsPopular);
            bookCommand.Parameters.AddWithValue("@CopiesAvailable", book.CopiesAvailable);
            bookCommand.Parameters.AddWithValue("@Files", book.Files ?? (object)DBNull.Value);
            bookCommand.Parameters.AddWithValue("@IsBuyOnly", book.IsBuyOnly);

            bookId = (int)bookCommand.ExecuteScalar();
        }

        string insertPriceQuery = @"
            INSERT INTO Prices (BookID, CurrentPriceBuy, CurrentPriceBorrow, OriginalPriceBuy, OriginalPriceBorrow, IsDiscounted, DiscountEndDate)
            VALUES (@BookID, @CurrentPriceBuy, @CurrentPriceBorrow, @OriginalPriceBuy, @OriginalPriceBorrow, @IsDiscounted, @DiscountEndDate);";

        using (var priceCommand = new NpgsqlCommand(insertPriceQuery, connection))
        {
            priceCommand.Parameters.AddWithValue("@BookID", bookId);
            priceCommand.Parameters.AddWithValue("@CurrentPriceBuy", book.PriceBuy);
            priceCommand.Parameters.AddWithValue("@CurrentPriceBorrow", book.PriceBorrowing ?? (object)DBNull.Value);
            priceCommand.Parameters.AddWithValue("@OriginalPriceBuy", book.PriceBuy);
            priceCommand.Parameters.AddWithValue("@OriginalPriceBorrow", book.PriceBorrowing ?? (object)DBNull.Value);
            priceCommand.Parameters.AddWithValue("@IsDiscounted", false); 
            priceCommand.Parameters.AddWithValue("@DiscountEndDate", DBNull.Value); 
            priceCommand.ExecuteNonQuery();
            TempData["Success"] = "Book added successfully!";
        }
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
            using var transaction = connection.BeginTransaction();
            try
            {
                string deletePricesQuery = "DELETE FROM Prices WHERE BookID = @BookID";
                using var deletePricesCommand = new NpgsqlCommand(deletePricesQuery, connection, transaction);
                deletePricesCommand.Parameters.AddWithValue("@BookID", bookId);
                deletePricesCommand.ExecuteNonQuery();

                string deleteBookQuery = "DELETE FROM Books WHERE ID = @ID";
                using var deleteBookCommand = new NpgsqlCommand(deleteBookQuery, connection, transaction);
                deleteBookCommand.Parameters.AddWithValue("@ID", bookId);
                deleteBookCommand.ExecuteNonQuery();
                transaction.Commit();
                TempData["Message"] = "Book deleted successfully!";
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["Error"] = $"Error deleting book: {ex.Message}";
            }

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
                        PriceBorrowing = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                        YearOfPublish = reader.GetInt32(6),
                        Genre = reader.GetString(7),
                        CoverImagePath = reader.GetString(8),
                        IsBuyOnly =  reader.GetBoolean(11),
                        SoldCopies = reader.GetInt32(10),
                        IsPopular = reader.GetBoolean(11),
                        AgeLimit = reader.IsDBNull(12) ? null : (int?)reader.GetInt32(12),
                        Files = reader.GetString(8)
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
public IActionResult EditBook(Book book)
{
    var username = HttpContext.Session.GetString("Username") ?? "test";
    if (!IsUserAdmin(username))
        return Unauthorized("You do not have permission to edit books.");

    try
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        // Update Books table
        string updateBookQuery = @"
            UPDATE Books
            SET Title = @Title, AuthorName = @AuthorName, Publisher = @Publisher, 
                PriceBuy = @PriceBuy, PriceBorrowing = @PriceBorrowing, 
                YearOfPublish = @YearOfPublish, Genre = @Genre, CoverImagePath = @CoverImagePath,
                SoldCopies = @SoldCopies, AgeLimit = @AgeLimit, IsPopular = @IsPopular, 
                CopiesAvailable = @CopiesAvailable, Files = @Files, IsBuyOnly = @IsBuyOnly
            WHERE ID = @ID;";

        using (var bookCommand = new NpgsqlCommand(updateBookQuery, connection))
        {
            bookCommand.Parameters.AddWithValue("@ID", book.ID);
            bookCommand.Parameters.AddWithValue("@Title", book.Title);
            bookCommand.Parameters.AddWithValue("@AuthorName", book.AuthorName);
            bookCommand.Parameters.AddWithValue("@Publisher", book.Publisher);
            bookCommand.Parameters.AddWithValue("@PriceBuy", book.PriceBuy);

            // Set PriceBorrowing to NULL if IsBuyOnly is true
            bookCommand.Parameters.AddWithValue("@PriceBorrowing", book.IsBuyOnly ? (object)DBNull.Value : book.PriceBorrowing ?? (object)DBNull.Value);

            bookCommand.Parameters.AddWithValue("@YearOfPublish", book.YearOfPublish);
            bookCommand.Parameters.AddWithValue("@Genre", book.Genre ?? (object)DBNull.Value);
            bookCommand.Parameters.AddWithValue("@CoverImagePath", book.CoverImagePath ?? (object)DBNull.Value);
            bookCommand.Parameters.AddWithValue("@SoldCopies", book.SoldCopies);
            bookCommand.Parameters.AddWithValue("@AgeLimit", book.AgeLimit ?? (object)DBNull.Value);
            bookCommand.Parameters.AddWithValue("@IsPopular", book.IsPopular);

            // Set CopiesAvailable to NULL if IsBuyOnly is true
            bookCommand.Parameters.AddWithValue("@CopiesAvailable", book.IsBuyOnly ? (object)DBNull.Value : book.CopiesAvailable ?? (object)DBNull.Value);

            bookCommand.Parameters.AddWithValue("@Files", book.Files ?? (object)DBNull.Value);
            bookCommand.Parameters.AddWithValue("@IsBuyOnly", book.IsBuyOnly);

            bookCommand.ExecuteNonQuery();
        }

        // Update Prices table
        string updatePriceQuery = @"
            UPDATE Prices
            SET CurrentPriceBuy = @CurrentPriceBuy, CurrentPriceBorrow = @CurrentPriceBorrow, 
                OriginalPriceBuy = @OriginalPriceBuy, OriginalPriceBorrow = @OriginalPriceBorrow
            WHERE BookID = @BookID;";

        using (var priceCommand = new NpgsqlCommand(updatePriceQuery, connection))
        {
            priceCommand.Parameters.AddWithValue("@BookID", book.ID);
            priceCommand.Parameters.AddWithValue("@CurrentPriceBuy", book.PriceBuy);

            // Set CurrentPriceBorrow to 0.00 if IsBuyOnly is true
            priceCommand.Parameters.AddWithValue("@CurrentPriceBorrow", book.IsBuyOnly ? 0.00m : book.PriceBorrowing ?? 0.00m);

            priceCommand.Parameters.AddWithValue("@OriginalPriceBuy", book.PriceBuy);

            // Set OriginalPriceBorrow to 0.00 if IsBuyOnly is true
            priceCommand.Parameters.AddWithValue("@OriginalPriceBorrow", book.IsBuyOnly ? 0.00m : book.PriceBorrowing ?? 0.00m);

            priceCommand.ExecuteNonQuery();
        }

        TempData["Message"] = "Book updated successfully!";
        return RedirectToAction("ManageBooks");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
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

                // Retrieve the original prices
                var getOriginalPricesCommand = new NpgsqlCommand("SELECT OriginalPriceBuy, OriginalPriceBorrow FROM Prices WHERE BookID = @BookID", connection);
                getOriginalPricesCommand.Parameters.AddWithValue("@BookID", bookId);

                using var reader = getOriginalPricesCommand.ExecuteReader();
                if (!reader.Read())
                {
                    return NotFound("Book not found.");
                }

                decimal originalPriceBuy = reader.GetDecimal(0);
                decimal originalPriceBorrow = reader.GetDecimal(1);
                reader.Close();

                // Validate discounted prices
                if (discountedPriceBuy > originalPriceBuy || discountedPriceBorrow > originalPriceBorrow)
                {
                    return BadRequest("Discounted prices cannot be higher than the original prices.");
                }

                // Update the prices with the discounted values
                var updateCommand = new NpgsqlCommand("UPDATE Prices SET IsDiscounted = @IsDiscounted, DiscountEndDate = @DiscountEndDate, CurrentPriceBuy = @DiscountedPriceBuy, CurrentPriceBorrow = @DiscountedPriceBorrow WHERE BookID = @BookID", connection);
                updateCommand.Parameters.AddWithValue("@IsDiscounted", true);
                updateCommand.Parameters.AddWithValue("@DiscountEndDate", discountEndDate);
                updateCommand.Parameters.AddWithValue("@DiscountedPriceBuy", discountedPriceBuy);
                updateCommand.Parameters.AddWithValue("@DiscountedPriceBorrow", discountedPriceBorrow);
                updateCommand.Parameters.AddWithValue("@BookID", bookId);

                updateCommand.ExecuteNonQuery();
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
                var currentusername = HttpContext.Session.GetString("Username") ?? "test";
    if (!IsUserAdmin(currentusername))
        return Unauthorized("You do not have permission to add books.");
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
            var currentusername = HttpContext.Session.GetString("Username") ?? "test";
            if (!IsUserAdmin(currentusername))
                return Unauthorized("You do not have permission to add books.");
            var users = new List<User>();
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            string query = "SELECT * FROM Users WHERE Username LIKE @SearchQuery OR Email LIKE @SearchQuery";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@SearchQuery", "%" + searchQuery + "%");
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var user = new User
                {
                    Username = reader.GetString(reader.GetOrdinal("Username")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                };
                users.Add(user);
            }
            return View(users);
        }
        // Waiting List 
        public IActionResult ManageWaitingList(string searchQuery)
        {
            var currentusername = HttpContext.Session.GetString("Username") ?? "test";
            if (!IsUserAdmin(currentusername))
                return Unauthorized("You do not have permission to add books.");
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
            var currentusername = HttpContext.Session.GetString("Username") ?? "test";
            if (!IsUserAdmin(currentusername))
                return Unauthorized("You do not have permission to add books.");
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