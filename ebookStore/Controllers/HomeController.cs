using ebookStore.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ebookStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly string _connectionString;

        public HomeController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnectionString");
        }

        // Helper method to check if the user is logged in
        private bool IsUserLoggedIn()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("Username"));
        }

        public IActionResult Index(
            string searchQuery,
            string titleFilter, // New parameter for title filter
            string genreFilter,
            string authorFilter,
            decimal? minPrice,
            decimal? maxPrice,
            string sortOrder)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account"); // Redirect to login page if not logged in
            }

            // Fetch session data here
            var username = HttpContext.Session.GetString("Username") ?? "Guest";
            var firstName = HttpContext.Session.GetString("FirstName") ?? "";
            var lastName = HttpContext.Session.GetString("LastName") ?? "";
            var email = HttpContext.Session.GetString("Email") ?? "";
            var role = HttpContext.Session.GetString("Role") ?? "";

            List<Book> books = new List<Book>();
            List<string> allGenres = new List<string>();

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                // Fetch all distinct genres from the database
                string genreQuery = "SELECT DISTINCT Genre FROM Books WHERE Genre IS NOT NULL";
                using (var genreCommand = new NpgsqlCommand(genreQuery, connection))
                {
                    using var genreReader = genreCommand.ExecuteReader();
                    while (genreReader.Read())
                    {
                        allGenres.Add(genreReader.GetString(0));
                    }
                }

                // Start with the base query
                string query = @"
                    SELECT b.ID, b.Title, b.AuthorName, b.Publisher, 
                           p.CurrentPriceBuy, p.CurrentPriceBorrow, 
                           p.OriginalPriceBuy, p.OriginalPriceBorrow, 
                           p.IsDiscounted, p.DiscountEndDate, 
                           b.YearOfPublish, b.Genre, b.CoverImagePath
                    FROM Books b
                    LEFT JOIN Prices p ON b.ID = p.BookID
                    WHERE 1=1";

                // Apply filters dynamically based on provided parameters
                if (!string.IsNullOrEmpty(titleFilter))
                {
                    query += " AND Title ILIKE @TitleFilter";
                }
                if (!string.IsNullOrEmpty(authorFilter))
                {
                    query += " AND AuthorName ILIKE @AuthorFilter";
                }
                if (!string.IsNullOrEmpty(genreFilter))
                {
                    query += " AND Genre = @Genre";
                }
                if (minPrice.HasValue)
                {
                    query += " AND PriceBuy >= @MinPrice";
                }
                if (maxPrice.HasValue)
                {
                    query += " AND PriceBuy <= @MaxPrice";
                }

                // Apply sorting based on sortOrder
                query += sortOrder switch
                {
                    "price_asc" => " ORDER BY PriceBuy ASC",
                    "price_desc" => " ORDER BY PriceBuy DESC",
                    "year_asc" => " ORDER BY YearOfPublish ASC",
                    "year_desc" => " ORDER BY YearOfPublish DESC",
                    "popularity" => " ORDER BY SoldCopies DESC",
                    _ => " ORDER BY Title ASC" // Default sorting by Title
                };

                // Prepare and execute the query
                using var command = new NpgsqlCommand(query, connection);

                // Add parameters to avoid SQL injection
                if (!string.IsNullOrEmpty(titleFilter))
                {
                    command.Parameters.AddWithValue("@TitleFilter", $"%{titleFilter}%");
                }
                if (!string.IsNullOrEmpty(authorFilter))
                {
                    command.Parameters.AddWithValue("@AuthorFilter", $"%{authorFilter}%");
                }
                if (!string.IsNullOrEmpty(genreFilter))
                {
                    command.Parameters.AddWithValue("@Genre", genreFilter);
                }
                if (minPrice.HasValue)
                {
                    command.Parameters.AddWithValue("@MinPrice", minPrice.Value);
                }
                if (maxPrice.HasValue)
                {
                    command.Parameters.AddWithValue("@MaxPrice", maxPrice.Value);
                }
                
                using var reader = command.ExecuteReader();

                // Read the results and populate the books list
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            ViewData["Username"] = username;
            ViewData["AllGenres"] = allGenres;
            ViewData["sortOrder"] = sortOrder;

            return View(books);
        }

        [HttpPost]
        public IActionResult BuyBook(int bookId)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                // Check if the book exists and if there are available copies
                string checkBookQuery = "SELECT CopiesAvailable FROM Books WHERE ID = @BookId";
                using var checkBookCommand = new NpgsqlCommand(checkBookQuery, connection);
                checkBookCommand.Parameters.AddWithValue("@BookId", bookId);
                int availableCopies = (int)checkBookCommand.ExecuteScalar();

                if (availableCopies > 0)
                {
                    // Check if the user has already bought this book
                    string checkUserPurchasedQuery = "SELECT COUNT(*) FROM PurchasedBooks WHERE BookId = @BookId AND Username = @Username";
                    using var checkUserPurchasedCommand = new NpgsqlCommand(checkUserPurchasedQuery, connection);
                    checkUserPurchasedCommand.Parameters.AddWithValue("@BookId", bookId);
                    checkUserPurchasedCommand.Parameters.AddWithValue("@Username", HttpContext.Session.GetString("Username"));
                    long booksAlreadyperchased = (long)checkUserPurchasedCommand.ExecuteScalar();

                    if (booksAlreadyperchased > 0)
                    {
                        TempData["Error"] = "You have already purchased this book.";
                    }
                    else
                    {
                        // Update the book to reduce the available copies
                        string updateBookQuery = "UPDATE Books SET CopiesAvailable = CopiesAvailable - 1 WHERE ID = @BookId";
                        using var updateBookCommand = new NpgsqlCommand(updateBookQuery, connection);
                        updateBookCommand.Parameters.AddWithValue("@BookId", bookId);
                        updateBookCommand.ExecuteNonQuery();

                        // Insert into BorrowedBooks
                        string insertPurchasedQuery = @"
                INSERT INTO Purchasedbooks (BookId, Username, PurchaseDate) 
                VALUES (@BookId, @Username, @purchaseDate)";
                        using var insertBorrowCommand = new NpgsqlCommand(insertPurchasedQuery, connection);
                        insertBorrowCommand.Parameters.AddWithValue("@BookId", bookId);
                        insertBorrowCommand.Parameters.AddWithValue("@Username", HttpContext.Session.GetString("Username"));
                        insertBorrowCommand.Parameters.AddWithValue("@purchaseDate", DateTime.Now);
                        insertBorrowCommand.ExecuteNonQuery();
                        //inserting to cart to keep them in sync 
                        string insertToCartQuery = @"INSERT INTO ShoppingCart (Username, BookId, Quantity, ActionType, CreatedAt)
                        VALUES (@username, @bookId, '1', @ActionType, @createdAt);";
                        using var AddToCartCommand = new NpgsqlCommand(insertToCartQuery, connection);
                        AddToCartCommand.Parameters.AddWithValue("@Username", HttpContext.Session.GetString("Username"));
                        AddToCartCommand.Parameters.AddWithValue("@BookId", bookId);
                        AddToCartCommand.Parameters.AddWithValue("@Quantity", 1);
                        AddToCartCommand.Parameters.AddWithValue("@ActionType", "Buy"); 
                        AddToCartCommand.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                        AddToCartCommand.ExecuteNonQuery();
                        TempData["Message"] = "Book successfully purchased!";
                        
                        }
                    }
                else
                {
                    TempData["Error"] = "No copies available for buying.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                TempData["Error"] = "An error occurred while buying the book.";
            }

            return RedirectToAction("Index");
        }
        // Borrowing books 
        [HttpPost]
        public IActionResult BorrowBook(int bookId)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                // Check if the book exists and if there are available copies
                string checkBookQuery = "SELECT CopiesAvailable FROM Books WHERE ID = @BookId";
                using var checkBookCommand = new NpgsqlCommand(checkBookQuery, connection);
                checkBookCommand.Parameters.AddWithValue("@BookId", bookId);
                int availableCopies = (int)checkBookCommand.ExecuteScalar();

                if (availableCopies > 0)
                {
                    // Check if the user has already borrowed this book
                    string checkUserBorrowedQuery = "SELECT COUNT(*) FROM BorrowedBooks WHERE BookId = @BookId AND Username = @Username";
                    using var checkUserBorrowedCommand = new NpgsqlCommand(checkUserBorrowedQuery, connection);
                    checkUserBorrowedCommand.Parameters.AddWithValue("@BookId", bookId);
                    checkUserBorrowedCommand.Parameters.AddWithValue("@Username", HttpContext.Session.GetString("Username"));
                    long booksAlreadyBorrowed = (long)checkUserBorrowedCommand.ExecuteScalar();

                    if (booksAlreadyBorrowed > 0)
                    {
                        TempData["Error"] = "You have already borrowed this book.";
                    }
                    else
                    {
                        // Check if the user has already borrowed 3 books
                        string checkUserBooksCountQuery = "SELECT COUNT(*) FROM BorrowedBooks WHERE Username = @Username";
                        using var checkUserBooksCountCommand = new NpgsqlCommand(checkUserBooksCountQuery, connection);
                        checkUserBooksCountCommand.Parameters.AddWithValue("@Username", HttpContext.Session.GetString("Username"));
                        long borrowedBooksCount = (long)checkUserBooksCountCommand.ExecuteScalar();

                        if (borrowedBooksCount >= 3)
                        {
                            TempData["Error"] = "You can only borrow a maximum of 3 books at a time.";
                        }
                        else
                        {
                            // Update the book to reduce the available copies
                            string updateBookQuery = "UPDATE Books SET CopiesAvailable = CopiesAvailable - 1 WHERE ID = @BookId";
                            using var updateBookCommand = new NpgsqlCommand(updateBookQuery, connection);
                            updateBookCommand.Parameters.AddWithValue("@BookId", bookId);
                            updateBookCommand.ExecuteNonQuery();

                            // Insert into BorrowedBooks
                            string insertBorrowQuery = @"
                    INSERT INTO BorrowedBooks (BookId, Username, BorrowDate, ReturnDate) 
                    VALUES (@BookId, @Username, @BorrowDate, @ReturnDate)";
                            using var insertBorrowCommand = new NpgsqlCommand(insertBorrowQuery, connection);
                            insertBorrowCommand.Parameters.AddWithValue("@BookId", bookId);
                            insertBorrowCommand.Parameters.AddWithValue("@Username", HttpContext.Session.GetString("Username"));
                            insertBorrowCommand.Parameters.AddWithValue("@BorrowDate", DateTime.Now);
                            insertBorrowCommand.Parameters.AddWithValue("@ReturnDate", DateTime.Now.AddDays(30));
                            insertBorrowCommand.ExecuteNonQuery();
                            //inserting to cart to keep them in sync 
                            string insertToCartQuery = @"INSERT INTO ShoppingCart (Username, BookId, Quantity, ActionType, CreatedAt)
                        VALUES (@username, @bookId, '1', @ActionType, @createdAt);";
                            using var AddToCartCommand = new NpgsqlCommand(insertToCartQuery, connection);
                            AddToCartCommand.Parameters.AddWithValue("@Username", HttpContext.Session.GetString("Username"));
                            AddToCartCommand.Parameters.AddWithValue("@BookId", bookId);
                            AddToCartCommand.Parameters.AddWithValue("@Quantity", 1);
                            AddToCartCommand.Parameters.AddWithValue("@ActionType", "Borrow"); 
                            AddToCartCommand.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                            AddToCartCommand.ExecuteNonQuery();
                            TempData["Message"] = "Book successfully borrowed!";
                        }
                    }
                }
                else
                {
                    TempData["Error"] = "No copies available for borrowing.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                TempData["Error"] = "An error occurred while borrowing the book.";
            }

            return RedirectToAction("Index");
        }


    }
}