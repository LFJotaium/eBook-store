using ebookStore.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Net.Mail;
using System.Net;
using System.Text.Json.Nodes;

namespace ebookStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly string _connectionString;

        public HomeController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnectionString");
        }

        private bool IsUserLoggedIn()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("Username"));
        }
        private int? SafeGetInt32(NpgsqlDataReader reader, int columnIndex)
        {
            return reader.IsDBNull(columnIndex) ? (int?)null : reader.GetInt32(columnIndex);
        }
        public IActionResult Index(
            string searchQuery,
            string titleFilter, 
            string genreFilter,
            string authorFilter,
            decimal? minPrice,
            decimal? maxPrice,
            string sortOrder,
            bool? showDiscounted) 
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account"); 
            }

            Dictionary<int, decimal> bookRatings = new Dictionary<int, decimal>(); var username = HttpContext.Session.GetString("Username") ?? "Guest";
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

                string genreQuery = "SELECT DISTINCT Genre FROM Books WHERE Genre IS NOT NULL";
                using (var genreCommand = new NpgsqlCommand(genreQuery, connection))
                {
                    using var genreReader = genreCommand.ExecuteReader();
                    while (genreReader.Read())
                    {
                        allGenres.Add(genreReader.GetString(0));
                    }
                }

                string query = @"
SELECT 
    b.ID, b.Title, b.AuthorName, b.Publisher, b.CopiesAvailable, b.SoldCopies, b.AgeLimit,
    p.CurrentPriceBuy, p.CurrentPriceBorrow, 
    p.OriginalPriceBuy, p.OriginalPriceBorrow, 
    p.IsDiscounted, p.DiscountEndDate, 
    b.YearOfPublish, b.Genre, b.CoverImagePath,
    b.IsPopular, b.IsBuyOnly, -- Include these columns
    COALESCE(AVG(bf.Rating), 0) AS AverageRating
FROM Books b
LEFT JOIN Prices p ON b.ID = p.BookID
LEFT JOIN BookFeedback bf ON b.ID = bf.BookId
WHERE 1=1"; 

                if (!string.IsNullOrEmpty(titleFilter)) query += " AND b.Title ILIKE @TitleFilter";
                if (!string.IsNullOrEmpty(authorFilter)) query += " AND b.AuthorName ILIKE @AuthorFilter";
                if (!string.IsNullOrEmpty(genreFilter)) query += " AND b.Genre = @Genre";
                if (minPrice.HasValue) query += " AND p.CurrentPriceBuy >= @MinPrice";
                if (maxPrice.HasValue) query += " AND p.CurrentPriceBuy <= @MaxPrice";

                if (showDiscounted == true)
                {
                    query += " AND p.IsDiscounted = TRUE AND p.DiscountEndDate >= CURRENT_DATE";
                }

                query += @"
GROUP BY b.ID, b.Title, b.AuthorName, b.Publisher, b.CopiesAvailable, b.SoldCopies, b.AgeLimit,
         p.CurrentPriceBuy, p.CurrentPriceBorrow, p.OriginalPriceBuy, p.OriginalPriceBorrow, 
         p.IsDiscounted, p.DiscountEndDate, b.YearOfPublish, b.Genre, b.CoverImagePath";

                query += sortOrder switch
                {
                    "price_asc" => " ORDER BY p.CurrentPriceBuy ASC",
                    "price_desc" => " ORDER BY p.CurrentPriceBuy DESC",
                    "year_asc" => " ORDER BY b.YearOfPublish ASC",
                    "year_desc" => " ORDER BY b.YearOfPublish DESC",
                    "popularity" => " ORDER BY b.SoldCopies DESC",
                    _ => " ORDER BY b.Title ASC" 
                };
                using var command = new NpgsqlCommand(query, connection);

                if (!string.IsNullOrEmpty(titleFilter)) command.Parameters.AddWithValue("@TitleFilter", $"%{titleFilter}%");
                if (!string.IsNullOrEmpty(authorFilter)) command.Parameters.AddWithValue("@AuthorFilter", $"%{authorFilter}%");
                if (!string.IsNullOrEmpty(genreFilter)) command.Parameters.AddWithValue("@Genre", genreFilter);
                if (minPrice.HasValue) command.Parameters.AddWithValue("@MinPrice", minPrice.Value);
                if (maxPrice.HasValue) command.Parameters.AddWithValue("@MaxPrice", maxPrice.Value);

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    var book = new Book
                    {
                        ID = reader.GetInt32(0),
                        Title = reader.GetString(1),
                        AuthorName = reader.GetString(2),
                        Publisher = reader.GetString(3),
                        CopiesAvailable = SafeGetInt32(reader, 4),
                        SoldCopies = (int)SafeGetInt32(reader, 5),
                        AgeLimit = SafeGetInt32(reader, 6),
                        Price = new Price
                        {
                            CurrentPriceBuy = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                            CurrentPriceBorrow = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                            OriginalPriceBuy = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9),
                            OriginalPriceBorrow = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10),
                            IsDiscounted = reader.IsDBNull(11) ? false : reader.GetBoolean(11),
                            DiscountEndDate = reader.IsDBNull(12) ? (DateTime?)null : reader.GetDateTime(12)
                        },
                        YearOfPublish = reader.GetInt32(13),
                        Genre = reader.GetString(14),
                        CoverImagePath = reader.GetString(15),
                        IsPopular = reader.GetBoolean(16), 
                        IsBuyOnly = reader.GetBoolean(17)  
                    };
                    books.Add(book);

                    decimal averageRating = reader.GetDecimal(18); 
                    bookRatings[book.ID] = averageRating;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }






			var waitingListCounts = new Dictionary<int, int>();
			var nextAvailableDates = new Dictionary<int, DateTime?>();
			var daysUntilAvailable = new Dictionary<int, int?>();

			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				connection.Open();

				Console.WriteLine("Database connection opened successfully.");

				foreach (var book in books)
				{
					Console.WriteLine($"Processing book ID: {book.ID}, Title: {book.Title}");

					string waitingListQuery = "SELECT COUNT(*) FROM WaitingList WHERE BookId = @BookId";
					using var waitingListCommand = new NpgsqlCommand(waitingListQuery, connection);
					waitingListCommand.Parameters.AddWithValue("@BookId", book.ID);
					int waitingListCount = Convert.ToInt32(waitingListCommand.ExecuteScalar());
					waitingListCounts[book.ID] = waitingListCount;

					Console.WriteLine($"Book ID: {book.ID}, Waiting List Count: {waitingListCount}");

					string nextAvailableQuery = @"
            SELECT MIN(ReturnDate) 
            FROM BorrowedBooks 
            WHERE BookId = @BookId AND ReturnDate > CURRENT_DATE";
					using var nextAvailableCommand = new NpgsqlCommand(nextAvailableQuery, connection);
					nextAvailableCommand.Parameters.AddWithValue("@BookId", book.ID);
					var nextAvailableDate = nextAvailableCommand.ExecuteScalar() as DateTime?;

					if (nextAvailableDate.HasValue)
					{
						nextAvailableDates[book.ID] = nextAvailableDate.Value;
						daysUntilAvailable[book.ID] = (int)(nextAvailableDate.Value - DateTime.Now).TotalDays;

						Console.WriteLine($"Book ID: {book.ID}, Next Available Date: {nextAvailableDate.Value}, Days Until Available: {daysUntilAvailable[book.ID]}");
					}
					else
					{
						nextAvailableDates[book.ID] = null;
						daysUntilAvailable[book.ID] = null;

						Console.WriteLine($"Book ID: {book.ID}, No next available date found.");
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching waiting list or next available date: {ex.Message}");
				TempData["Error"] = $"Error fetching waiting list or next available date: {ex.Message}";
			}

			ViewBag.WaitingListCounts = waitingListCounts;
			ViewBag.NextAvailableDates = nextAvailableDates;
			ViewBag.DaysUntilAvailable = daysUntilAvailable;

			Console.WriteLine("Waiting list and next available dates processed successfully.");




			List<(string Username, int Rating, string Comment)> feedbackList = new List<(string, int, string)>();

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                string feedbackQuery = @"
            SELECT Username, Rating, Comment
            FROM Feedback
            ORDER BY RANDOM() -- This will randomly order the feedbacks
            LIMIT 5";

                using var feedbackCommand = new NpgsqlCommand(feedbackQuery, connection);
                using var feedbackReader = feedbackCommand.ExecuteReader();

                while (feedbackReader.Read())
                {
                    feedbackList.Add((
                        feedbackReader.GetString(0), 
                        feedbackReader.GetInt32(1), 
                        feedbackReader.IsDBNull(2) ? "" : feedbackReader.GetString(2) 
                    ));
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }
            ViewBag.BookRatings = bookRatings;
            ViewData["FeedbackList"] = feedbackList;
            ViewData["Username"] = username;
            ViewData["AllGenres"] = allGenres;
            ViewData["sortOrder"] = sortOrder;
            ViewData["showDiscounted"] = showDiscounted;

            return View(books);
        }

        [HttpPost]
        public async Task<IActionResult> BuyBookDirectly(int bookId)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            string username = HttpContext.Session.GetString("Username");

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string checkUserBoughtQuery = @"
                SELECT COUNT(*) 
                FROM PurchasedBooks 
                WHERE BookId = @BookId AND Username = @Username";
                    using (var checkUserBoughtCommand = new NpgsqlCommand(checkUserBoughtQuery, connection))
                    {
                        checkUserBoughtCommand.Parameters.AddWithValue("@BookId", bookId);
                        checkUserBoughtCommand.Parameters.AddWithValue("@Username", username);
                        long booksAlreadyBought = (long)await checkUserBoughtCommand.ExecuteScalarAsync();

                        if (booksAlreadyBought > 0)
                        {
                            TempData["Error"] = "You have already bought this book.";
                            return RedirectToAction("Index", "Home");
                        }
                    }

                    string getPriceQuery = @"
                SELECT currentpricebuy 
                FROM Prices 
                WHERE bookid = @BookId";
                    using (var getPriceCommand = new NpgsqlCommand(getPriceQuery, connection))
                    {
                        getPriceCommand.Parameters.AddWithValue("@BookId", bookId);
                        decimal price = (decimal)await getPriceCommand.ExecuteScalarAsync();

                        ViewBag.PaypalClientId = "AVUpkuHdTvwNd9BBm_r3O1e1REZX1O0AsngCwreFyRjKpohV-i__JKzmiAch1nvR4VWDckZ4xlInNZR3";
                        ViewBag.TotalAmount = price;
                        ViewBag.BookId = bookId;

                        return View("~/Views/Cart/DirectCheckout.cshtml");
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while fetching the book details.";
                return RedirectToAction("Index", "Home");
            }
        }
        [HttpPost]
        public async Task<IActionResult> AddToBorrowCart(int bookId)
        {
            Console.WriteLine($"AddToBorrowCart called for BookId: {bookId}");

            if (!IsUserLoggedIn())
            {
                Console.WriteLine("User is not logged in. Redirecting to Login page.");
                TempData["Error"] = "You need to log in to borrow a book.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                Console.WriteLine("Database connection opened successfully.");

                using var transaction = await connection.BeginTransactionAsync();
                Console.WriteLine("Transaction started.");

                string username = HttpContext.Session.GetString("Username");
                string email = HttpContext.Session.GetString("Email");
                Console.WriteLine($"Username: {username}, Email: {email}");

                string checkBorrowedBookQuery = "SELECT COUNT(*) FROM BorrowedBooks WHERE BookId = @BookId AND Username = @Username";
                using var checkBorrowedBookCommand = new NpgsqlCommand(checkBorrowedBookQuery, connection, transaction);
                checkBorrowedBookCommand.Parameters.AddWithValue("@BookId", bookId);
                checkBorrowedBookCommand.Parameters.AddWithValue("@Username", username);
                long alreadyBorrowedCount = (long)await checkBorrowedBookCommand.ExecuteScalarAsync();
                Console.WriteLine($"Number of times the user has already borrowed this book: {alreadyBorrowedCount}");

                if (alreadyBorrowedCount > 0)
                {
                    Console.WriteLine("User has already borrowed this book. Rolling back transaction.");
                    TempData["Error"] = "You have already borrowed this book.";
                    await transaction.RollbackAsync();
                    return RedirectToAction("Index");
                }

                string checkTotalBorrowedBooksQuery = @"
SELECT 
    (SELECT COUNT(*) FROM BorrowedBooks WHERE Username = @Username) +
    (SELECT COUNT(*) FROM ShoppingCart WHERE Username = @Username AND ActionType = 'Borrow') AS TotalBorrowedBooks";
                using var checkTotalBorrowedBooksCommand = new NpgsqlCommand(checkTotalBorrowedBooksQuery, connection, transaction);
                checkTotalBorrowedBooksCommand.Parameters.AddWithValue("@Username", username);
                long totalBorrowedBooks = (long)await checkTotalBorrowedBooksCommand.ExecuteScalarAsync();
                Console.WriteLine($"Total borrowed books (including cart): {totalBorrowedBooks}");

                if (totalBorrowedBooks >= 3)
                {
                    Console.WriteLine("User has reached the borrowing limit. Rolling back transaction.");
                    TempData["Error"] = "You can only borrow up to 3 books at a time (including books in your cart).";
                    await transaction.RollbackAsync();
                    return RedirectToAction("Index");
                }

                string getBookQuery = "SELECT CopiesAvailable FROM Books WHERE ID = @BookId FOR UPDATE";
                using var getBookCommand = new NpgsqlCommand(getBookQuery, connection, transaction);
                getBookCommand.Parameters.AddWithValue("@BookId", bookId);
                using var reader = await getBookCommand.ExecuteReaderAsync();
                Console.WriteLine("Fetching book details...");

                if (!await reader.ReadAsync())
                {
                    Console.WriteLine("Book not found. Rolling back transaction.");
                    TempData["Error"] = "Book not found.";
                    return RedirectToAction("Index");
                }

                int copiesAvailable = reader.GetInt32(0);
                await reader.CloseAsync();
                Console.WriteLine($"Copies available: {copiesAvailable}");

                if (copiesAvailable <= 0)
                {
                    Console.WriteLine("Book is out of stock. Checking waiting list...");
                    string checkWaitingListUserQuery = "SELECT COUNT(*) FROM WaitingList WHERE BookId = @BookId AND Username = @Username";
                    using var checkWaitingListUserCommand = new NpgsqlCommand(checkWaitingListUserQuery, connection, transaction);
                    checkWaitingListUserCommand.Parameters.AddWithValue("@BookId", bookId);
                    checkWaitingListUserCommand.Parameters.AddWithValue("@Username", username);
                    long userInWaitingList = (long)await checkWaitingListUserCommand.ExecuteScalarAsync();
                    Console.WriteLine($"User in waiting list: {userInWaitingList}");

                    if (userInWaitingList == 0)
                    {
                        Console.WriteLine("Adding user to waiting list...");
                        string addToWaitingListQuery = @"INSERT INTO WaitingList (BookId, Username, Email, CreatedAt)
                                         VALUES (@BookId, @Username, @Email, @CreatedAt)";
                        using var addToWaitingListCommand = new NpgsqlCommand(addToWaitingListQuery, connection, transaction);
                        addToWaitingListCommand.Parameters.AddWithValue("@BookId", bookId);
                        addToWaitingListCommand.Parameters.AddWithValue("@Username", username);
                        addToWaitingListCommand.Parameters.AddWithValue("@Email", email);
                        addToWaitingListCommand.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                        await addToWaitingListCommand.ExecuteNonQueryAsync();

                        TempData["Message"] = "The book is out of stock. You have been added to the waiting list.";
                        Console.WriteLine("User added to waiting list.");
                    }
                    else
                    {
                        Console.WriteLine("User is already in the waiting list.");
                        TempData["Message"] = "You are already in the waiting list for this book.";
                    }

                    await transaction.CommitAsync();
                    Console.WriteLine("Transaction committed.");
                    return RedirectToAction("Index");
                }

                Console.WriteLine("Checking prioritized waiting list...");
                string getWaitingListQuery = @"SELECT Username, Email 
                               FROM WaitingList 
                               WHERE BookId = @BookId 
                               ORDER BY CreatedAt ASC 
                               LIMIT @CopiesAvailable";
                using var getWaitingListCommand = new NpgsqlCommand(getWaitingListQuery, connection, transaction);
                getWaitingListCommand.Parameters.AddWithValue("@BookId", bookId);
                getWaitingListCommand.Parameters.AddWithValue("@CopiesAvailable", copiesAvailable);
                using var waitingListReader = await getWaitingListCommand.ExecuteReaderAsync();

                var prioritizedUsers = new List<(string Username, string Email)>();
                while (await waitingListReader.ReadAsync())
                {
                    prioritizedUsers.Add((waitingListReader.GetString(0), waitingListReader.GetString(1)));
                }
                await waitingListReader.CloseAsync();
                Console.WriteLine($"Number of prioritized users: {prioritizedUsers.Count}");

                foreach (var user in prioritizedUsers)
                {
                    try
                    {
                        Console.WriteLine($"Notifying user {user.Username}...");
                        await SendEmailForAvailableBook(user.Email, bookId);

                        string removeFromWaitingListQuery = "DELETE FROM WaitingList WHERE BookId = @BookId AND Username = @Username";
                        using var removeFromWaitingListCommand = new NpgsqlCommand(removeFromWaitingListQuery, connection, transaction);
                        removeFromWaitingListCommand.Parameters.AddWithValue("@BookId", bookId);
                        removeFromWaitingListCommand.Parameters.AddWithValue("@Username", user.Username);
                        await removeFromWaitingListCommand.ExecuteNonQueryAsync();
                        Console.WriteLine($"User {user.Username} removed from waiting list.");

                        string insertToCartQuery = @"INSERT INTO ShoppingCart (Username, BookId, Quantity, ActionType, CreatedAt)
                                     VALUES (@Username, @BookId, 1, @ActionType, @CreatedAt)";
                        using var addToCartCommand = new NpgsqlCommand(insertToCartQuery, connection, transaction);
                        addToCartCommand.Parameters.AddWithValue("@Username", user.Username);
                        addToCartCommand.Parameters.AddWithValue("@BookId", bookId);
                        addToCartCommand.Parameters.AddWithValue("@ActionType", "Borrow");
                        addToCartCommand.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                        await addToCartCommand.ExecuteNonQueryAsync();
                        Console.WriteLine($"Book added to cart for user {user.Username}.");

                        copiesAvailable--;
                        string updateBookQuery = "UPDATE Books SET CopiesAvailable = @CopiesAvailable WHERE ID = @BookId";
                        using var updateBookCommand = new NpgsqlCommand(updateBookQuery, connection, transaction);
                        updateBookCommand.Parameters.AddWithValue("@CopiesAvailable", copiesAvailable);
                        updateBookCommand.Parameters.AddWithValue("@BookId", bookId);
                        await updateBookCommand.ExecuteNonQueryAsync();
                        Console.WriteLine($"Copies available updated to {copiesAvailable}.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error notifying user {user.Username}: {ex.Message}");
                        TempData["Error"] = $"Error notifying user '{user.Username}': {ex.Message}";
                    }
                }

                if (!prioritizedUsers.Any(u => u.Username == username))
                {
                    Console.WriteLine("Current user is not in the prioritized list. Adding book to cart...");
                    string checkBookInCartQuery = "SELECT COUNT(*) FROM ShoppingCart WHERE BookId = @BookId AND Username = @Username";
                    using var checkBookInCartCommand = new NpgsqlCommand(checkBookInCartQuery, connection, transaction);
                    checkBookInCartCommand.Parameters.AddWithValue("@BookId", bookId);
                    checkBookInCartCommand.Parameters.AddWithValue("@Username", username);
                    long bookInCart = (long)await checkBookInCartCommand.ExecuteScalarAsync();
                    Console.WriteLine($"Book in cart: {bookInCart}");

                    if (bookInCart > 0)
                    {
                        Console.WriteLine("Book is already in the user's cart. Rolling back transaction.");
                        TempData["Error"] = "This book is already in your cart.";
                        await transaction.RollbackAsync();
                        return RedirectToAction("Index");
                    }

                    string insertUserCartQuery = @"INSERT INTO ShoppingCart (Username, BookId, Quantity, ActionType, CreatedAt)
                                   VALUES (@Username, @BookId, 1, @ActionType, @CreatedAt)";
                    using var insertUserCartCommand = new NpgsqlCommand(insertUserCartQuery, connection, transaction);
                    insertUserCartCommand.Parameters.AddWithValue("@Username", username);
                    insertUserCartCommand.Parameters.AddWithValue("@BookId", bookId);
                    insertUserCartCommand.Parameters.AddWithValue("@ActionType", "Borrow");
                    insertUserCartCommand.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                    await insertUserCartCommand.ExecuteNonQueryAsync();
                    Console.WriteLine("Book added to user's cart.");

                    copiesAvailable--;
                    string updateUserBookQuery = "UPDATE Books SET CopiesAvailable = @CopiesAvailable WHERE ID = @BookId";
                    using var updateUserBookCommand = new NpgsqlCommand(updateUserBookQuery, connection, transaction);
                    updateUserBookCommand.Parameters.AddWithValue("@CopiesAvailable", copiesAvailable);
                    updateUserBookCommand.Parameters.AddWithValue("@BookId", bookId);
                    await updateUserBookCommand.ExecuteNonQueryAsync();
                    Console.WriteLine($"Copies available updated to {copiesAvailable}.");
                }

                await transaction.CommitAsync();
                Console.WriteLine("Transaction committed.");
                TempData["Message"] = "Book added to cart. Please proceed to checkout!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in AddToBorrowCart: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                TempData["Error"] = "An error occurred while borrowing the book.";
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
		public IActionResult AddToBuyCart(int bookId)
		{
			if (!IsUserLoggedIn())
			{
				return RedirectToAction("Login", "Account");
			}

			try
			{
				using var connection = new NpgsqlConnection(_connectionString);
				connection.Open();

				string checkBookInCartQuery = "SELECT COUNT(*) FROM ShoppingCart WHERE BookId = @BookId AND Username = @Username";
				using var checkBookInCartCommand = new NpgsqlCommand(checkBookInCartQuery, connection);
				checkBookInCartCommand.Parameters.AddWithValue("@BookId", bookId);
				checkBookInCartCommand.Parameters.AddWithValue("@Username", HttpContext.Session.GetString("Username"));
				long bookInCart = (long)checkBookInCartCommand.ExecuteScalar();

				if (bookInCart > 0)
				{
					TempData["Error"] = "This book is already in your cart.";
				}
				else
				{
					string checkUserBoughtQuery = "SELECT COUNT(*) FROM PurchasedBooks WHERE BookId = @BookId AND Username = @Username";
					using var checkUserBoughtCommand = new NpgsqlCommand(checkUserBoughtQuery, connection);
					checkUserBoughtCommand.Parameters.AddWithValue("@BookId", bookId);
					checkUserBoughtCommand.Parameters.AddWithValue("@Username", HttpContext.Session.GetString("Username"));
					long booksAlreadyBought = (long)checkUserBoughtCommand.ExecuteScalar();

					if (booksAlreadyBought > 0)
					{
						TempData["Error"] = "You have already bought this book.";
					}
					else
					{
						string insertToCartQuery = @"
                    INSERT INTO ShoppingCart (Username, BookId, Quantity, ActionType, CreatedAt)
                    VALUES (@Username, @BookId, '1', @ActionType, @CreatedAt);";
						using var addToCartCommand = new NpgsqlCommand(insertToCartQuery, connection);
						addToCartCommand.Parameters.AddWithValue("@Username", HttpContext.Session.GetString("Username"));
						addToCartCommand.Parameters.AddWithValue("@BookId", bookId);
						addToCartCommand.Parameters.AddWithValue("@ActionType", "Buy");
						addToCartCommand.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
						addToCartCommand.ExecuteNonQuery();

						TempData["Message"] = "Book added to cart. Please proceed to checkout!";
					}
				}
			}
			catch (Exception ex)
			{
				TempData["Error"] = "An error occurred while adding the book to the cart.";
			}

			return RedirectToAction("Index");
		}

        public IActionResult BookProfile(int id)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            Book book = null;
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                string query = @"
            SELECT b.ID, b.Title, b.AuthorName, b.Publisher, b.CopiesAvailable, b.SoldCopies, b.AgeLimit,
                   p.CurrentPriceBuy, p.CurrentPriceBorrow, 
                   p.OriginalPriceBuy, p.OriginalPriceBorrow, 
                   p.IsDiscounted, p.DiscountEndDate, 
                   b.YearOfPublish, b.Genre, b.CoverImagePath
            FROM Books b
            LEFT JOIN Prices p ON b.ID = p.BookID
            WHERE b.ID = @BookId";

                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@BookId", id);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    book = new Book
                    {
                        ID = reader.GetInt32(0),
                        Title = reader.GetString(1),
                        AuthorName = reader.GetString(2),
                        Publisher = reader.GetString(3),
                        CopiesAvailable = reader.GetInt32(4),
                        SoldCopies = reader.GetInt32(5),
                        AgeLimit = reader.GetInt32(6),
                        Price = new Price
                        {
                            CurrentPriceBuy = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                            CurrentPriceBorrow = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                            OriginalPriceBuy = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9),
                            OriginalPriceBorrow = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10),
                            IsDiscounted = reader.IsDBNull(11) ? false : reader.GetBoolean(11),
                            DiscountEndDate = reader.IsDBNull(12) ? (DateTime?)null : reader.GetDateTime(12)
                        },
                        YearOfPublish = reader.GetInt32(13),
                        Genre = reader.GetString(14),
                        CoverImagePath = reader.GetString(15)
                    };
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("Index");
            }

            return View(book);
        }

        [HttpPost]
        public IActionResult SubmitFeedback(int rating, string comment)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                string query = @"
            INSERT INTO Feedback (Username, Rating, Comment, CreatedAt)
            VALUES (@Username, @Rating, @Comment, @CreatedAt)";

                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@Username", HttpContext.Session.GetString("Username"));
                command.Parameters.AddWithValue("@Rating", rating);
                command.Parameters.AddWithValue("@Comment", comment ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                command.ExecuteNonQuery();
                TempData["Message"] = "Thank you for your feedback!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> SubmitBookFeedback(int bookId, int rating, string comment)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            string username = HttpContext.Session.GetString("Username");

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                bool hasPurchasedOrBorrowed = await CheckUserPurchaseOrBorrowedBookAsync(connection, username, bookId);

                if (!hasPurchasedOrBorrowed)
                {
                    TempData["Error"] = "You must have bought or borrowed the book before submitting a feedback.";
                    return RedirectToAction("BookProfile", new { id = bookId });
                }

                string query = @"
            INSERT INTO BookFeedback (Username, BookId, Rating, Comment, CreatedAt)
            VALUES (@Username, @BookId, @Rating, @Comment, @CreatedAt)";

                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@Username", username);
                command.Parameters.AddWithValue("@BookId", bookId);
                command.Parameters.AddWithValue("@Rating", rating);
                command.Parameters.AddWithValue("@Comment", comment ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                await command.ExecuteNonQueryAsync();
                TempData["Message"] = "Thank you for your feedback!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToAction("BookProfile", new { id = bookId });
        }

        [HttpGet]
        public async Task<IActionResult> GetBookFeedback(int bookId, int offset = 0, int limit = 5)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                string query = @"
        SELECT Username, Rating, Comment, CreatedAt
        FROM BookFeedback
        WHERE BookId = @BookId
        ORDER BY random() -- Fetch random feedback
        OFFSET @Offset LIMIT @Limit";

                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@BookId", bookId);
                command.Parameters.AddWithValue("@Offset", offset);
                command.Parameters.AddWithValue("@Limit", limit);

                var feedbackList = new List<object>();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    feedbackList.Add(new
                    {
                        Username = reader.GetString(0),
                        Rating = reader.GetInt32(1),
                        Comment = reader.IsDBNull(2) ? null : reader.GetString(2),
                        CreatedAt = reader.GetDateTime(3)
                    });
                }

                return Json(feedbackList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }





        private async Task<bool> CheckUserPurchaseOrBorrowedBookAsync(NpgsqlConnection connection, string username, int bookId)
        {
            string checkPurchasedQuery = @"
        SELECT COUNT(*) 
        FROM PurchasedBooks 
        WHERE Username = @Username AND BookId = @BookId";

            using var purchasedCommand = new NpgsqlCommand(checkPurchasedQuery, connection);
            purchasedCommand.Parameters.AddWithValue("@Username", username);
            purchasedCommand.Parameters.AddWithValue("@BookId", bookId);
            long purchasedCount = (long)await purchasedCommand.ExecuteScalarAsync();

            string checkBorrowedQuery = @"
        SELECT COUNT(*) 
        FROM BorrowedBooks 
        WHERE Username = @Username AND BookId = @BookId";

            using var borrowedCommand = new NpgsqlCommand(checkBorrowedQuery, connection);
            borrowedCommand.Parameters.AddWithValue("@Username", username);
            borrowedCommand.Parameters.AddWithValue("@BookId", bookId);
            long borrowedCount = (long)await borrowedCommand.ExecuteScalarAsync();

            return purchasedCount > 0 || borrowedCount > 0;
        }

        public async Task SendEmailForAvailableBook(string username, int bookId)
        {
            string fromMail = "malikabushah@gmail.com";
            string fromPassword = "eaqa ixie haib nkjw"; 

            // Create the email body for the book availability notification
            string body = "<html><body><h2>Book Availability Notification</h2><p>Dear " + username + ",</p><p>The book you were waiting for is now available for borrowing. " +
                          "You are among the first to be notified. Please log in to your account and borrow the book as soon as possible, you have only 2 days.</p>" +
                          "<p>Book ID: " + bookId + "</p>" +
                          "<p>If you have any questions, feel free to contact us.</p><p>Best regards,<br>Your Ebook Store Team</p></body></html>";

            MailMessage message = new MailMessage
            {
                From = new MailAddress(fromMail),
                Subject = "Book Available for Borrowing!",
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(username)); 

            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(fromMail, fromPassword),
                EnableSsl = true
            };

            try
            {
                await smtpClient.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error sending email: {ex.Message}";
            }
        }

        [HttpPost]
        public async Task<IActionResult> NotifyUsersForAvailableBook(int bookId)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                string waitingListQuery = @"
            SELECT Username
            FROM WaitingList
            WHERE BookId = @BookId
            ORDER BY Position
            LIMIT 3";

                using var command = new NpgsqlCommand(waitingListQuery, connection);
                command.Parameters.AddWithValue("@BookId", bookId);

                using var reader = command.ExecuteReader();
                List<string> notifiedUsers = new List<string>();
                while (reader.Read())
                {
                    string username = reader.GetString(0);

                    string userEmailQuery = "SELECT Email FROM Users WHERE Username = @Username";
                    using var emailCommand = new NpgsqlCommand(userEmailQuery, connection);
                    emailCommand.Parameters.AddWithValue("@Username", username);
                    var userEmail = emailCommand.ExecuteScalar()?.ToString();

                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        
                        await SendEmailForAvailableBook(userEmail, bookId);
                        notifiedUsers.Add(username);
                    }
                }

                if (notifiedUsers.Any())
                {
                    string removeNotifiedUsersQuery = @"
                DELETE FROM WaitingList
                WHERE BookId = @BookId AND Username = ANY(@Usernames)";

                    using var removeCommand = new NpgsqlCommand(removeNotifiedUsersQuery, connection);
                    removeCommand.Parameters.AddWithValue("@BookId", bookId);
                    removeCommand.Parameters.AddWithValue("@Usernames", notifiedUsers.ToArray());
                    await removeCommand.ExecuteNonQueryAsync();
                }

                TempData["Message"] = "Notification sent to users who were waiting for the book.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToAction("Index");
        }



        public async Task InsertPurchaseRecord((int bookId, int quantity, decimal price, string actionType) item, NpgsqlConnection connection, NpgsqlTransaction transaction, string username, string paypalOrderId)
        {
            string insertPurchasedQuery = @"
            INSERT INTO PurchasedBooks (BookId, Username, PurchaseDate, PaypalOrderId)
            VALUES (@BookId, @Username, @PurchaseDate, @PaypalOrderId)";

            using (var command = new NpgsqlCommand(insertPurchasedQuery, connection, transaction))
            {
                command.Parameters.AddWithValue("@BookId", item.bookId);
                command.Parameters.AddWithValue("@Username", username);
                command.Parameters.AddWithValue("@PurchaseDate", DateTime.Now);
                command.Parameters.AddWithValue("@PaypalOrderId", paypalOrderId);
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task InsertBorrowRecord((int bookId, int quantity, decimal price, string actionType) item, NpgsqlConnection connection, NpgsqlTransaction transaction, string username, string paypalOrderId)
        {
            string insertBorrowedQuery = @"
            INSERT INTO BorrowedBooks (BookId, Username, BorrowDate, ReturnDate, PaypalOrderId)
            VALUES (@BookId, @Username, @BorrowDate, @ReturnDate, @PaypalOrderId)";

            using (var command = new NpgsqlCommand(insertBorrowedQuery, connection, transaction))
            {
                command.Parameters.AddWithValue("@BookId", item.bookId);
                command.Parameters.AddWithValue("@Username", username);
                command.Parameters.AddWithValue("@BorrowDate", DateTime.Now);
                command.Parameters.AddWithValue("@ReturnDate", DateTime.Now.AddDays(30));
                command.Parameters.AddWithValue("@PaypalOrderId", paypalOrderId);
                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task SendEmail(string username, List<(int bookId, int quantity, decimal price, string actionType)> cartItems, string action)
        {
            string fromMail = "malikabushah@gmail.com";
            string fromPassword = "vsjm dvly keqg ymzl";

            string body = "<html><body><h2>Your " + action + " Confirmation</h2><p>Dear " + username + ",</p><p>Thank you for your " + action + " order. Here are the details:</p><ul>";

            foreach (var item in cartItems)
            {
                body += $"<li>{item.quantity} x {item.actionType} of Book ID: {item.bookId} at ${item.price} each.</li>";
            }

            body += "</ul><p>If you have any questions, feel free to contact us.</p><p>Best regards,<br>Ebook Store Team</p></body></html>";

            MailMessage message = new MailMessage
            {
                From = new MailAddress(fromMail),
                Subject = action + " Confirmation",
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(username));

            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(fromMail, fromPassword),
                EnableSsl = true
            };

            try
            {
                await smtpClient.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error sending confirmation email.";
            }
        }
    }
}