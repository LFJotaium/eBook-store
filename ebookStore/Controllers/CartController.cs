using ebookStore.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Net.Mail;
using System.Net;

public class CartController : Controller
{
    private readonly string _connectionString;
    private readonly string _paypalClientId;

    public CartController(EbookContext context, IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnectionString");
        _paypalClientId = configuration["PaypalSettings:ClientId"];
    }

    [HttpGet]
    [Route("/cart/Checkout")]
    public IActionResult Checkout()
    {
        ViewBag.PaypalClientId = _paypalClientId;
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            return RedirectToAction("Login", "Account");
        }

        decimal totalAmount = 0;
        string action = "";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            connection.Open();
            string query = @"
                SELECT SUM(
                    CASE WHEN sc.ActionType = 'Buy' THEN b.PriceBuy 
                         ELSE b.PriceBorrowing 
                    END * sc.Quantity
                ) AS TotalAmount,
                sc.ActionType
                FROM ShoppingCart sc
                JOIN Books b ON sc.BookId = b.ID
                WHERE sc.Username = @username
                GROUP BY sc.ActionType";

            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@username", username);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        totalAmount = reader.IsDBNull(reader.GetOrdinal("TotalAmount")) ? 0 : reader.GetDecimal(reader.GetOrdinal("TotalAmount"));
                        action = reader.IsDBNull(reader.GetOrdinal("ActionType")) ? "buy" : reader.GetString(reader.GetOrdinal("ActionType"));
                    }
                }
            }
        }

        ViewBag.TotalAmount = totalAmount;
        ViewBag.Action = action;

        return View();
    }
    /*
    public IActionResult AddToCart(int bookId)
    {
        // Retrieve the user ID from the session or authentication context
        var userId = GetUserId();

        if (userId == null)
        {
            return RedirectToAction("Login", "Account");
        }

        string insertUserCartQuery = @"INSERT INTO ShoppingCart (Username, BookId, Quantity, ActionType, CreatedAt)
                                           VALUES (@Username, @BookId, 1, @ActionType, @CreatedAt)";
        using var insertUserCartCommand = new NpgsqlCommand(insertUserCartQuery, connection, transaction);
        insertUserCartCommand.Parameters.AddWithValue("@Username", username);
        insertUserCartCommand.Parameters.AddWithValue("@BookId", bookId);
        insertUserCartCommand.Parameters.AddWithValue("@ActionType", "Borrow");
        insertUserCartCommand.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
        await insertUserCartCommand.ExecuteNonQueryAsync();

        // Optionally, display a success message
        TempData["Message"] = $"{book.Title} has been added to your cart.";

        // Redirect back to the eBook gallery or the current page
        return RedirectToAction("Gallery", "Books");
    }*/
    [HttpGet]
    [Route("cart/ViewCart")]
    public IActionResult ViewCart()
    {
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            return RedirectToAction("Login", "Account");
        }

        var cartItems = new List<CartItemViewModel>();

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            connection.Open();
            string query = @"
                SELECT sc.BookId, b.Title, b.AuthorName, sc.Quantity, 
                       CASE WHEN sc.ActionType = 'Buy' THEN p.currentpricebuy 
                            ELSE p.currentpriceborrow 
                       END AS Price, 
                       sc.ActionType
                FROM ShoppingCart sc
                JOIN Books b ON sc.BookId = b.ID
                JOIN Prices p ON b.ID = p.bookid
                WHERE sc.Username = @username";

            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@username", username);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cartItems.Add(new CartItemViewModel
                        {
                            BookId = reader.GetInt32(0),
                            Title = reader.GetString(1),
                            AuthorName = reader.GetString(2),
                            Quantity = reader.GetInt32(3),
                            Price = reader.GetDecimal(4),
                            ActionType = reader.GetString(5)
                        });
                    }
                }
            }
        }

        return View(cartItems);
    }

    [HttpPost]
    [Route("Cart/Remove")]
    public async Task<IActionResult> RemoveFromCartAsync(int bookId, string actionType)
    {
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            ViewBag.Error = "Username not found in session. Please log in.";
            return RedirectToAction("Login", "Account");
        }

        try
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                // Step 1: Delete from PurchasedBooks or BorrowedBooks
                string deleteQuery = actionType == "Buy"
                    ? "DELETE FROM PurchasedBooks WHERE BookId = @bookId AND Username = @username"
                    : "DELETE FROM BorrowedBooks WHERE BookId = @bookId AND Username = @username";

                using (var command = new NpgsqlCommand(deleteQuery, connection))
                {
                    command.Parameters.AddWithValue("@bookId", bookId);
                    command.Parameters.AddWithValue("@username", username);
                    command.ExecuteNonQuery();
                }

                // Step 2: Delete from ShoppingCart
                string deleteFromCartQuery = "DELETE FROM ShoppingCart WHERE BookId = @bookId AND Username = @username";
                using (var command = new NpgsqlCommand(deleteFromCartQuery, connection))
                {
                    command.Parameters.AddWithValue("@bookId", bookId);
                    command.Parameters.AddWithValue("@username", username);
                    command.ExecuteNonQuery();
                }

                // Step 3: Handle specific logic for "Borrow"
                if (actionType == "Borrow")
                {
                    string incrementCopiesQuery = "UPDATE Books SET CopiesAvailable = CopiesAvailable + 1 WHERE ID = @bookId";
                    using (var command = new NpgsqlCommand(incrementCopiesQuery, connection))
                    {
                        command.Parameters.AddWithValue("@bookId", bookId);
                        command.ExecuteNonQuery();
                    }

                    // Step 4: Notify the first user in the waiting list
                    string getFirstWaitingUserQuery = "SELECT Email, Username FROM WaitingList WHERE BookId = @bookId ORDER BY CreatedAt ASC LIMIT 1";
                    string firstWaitingUserEmail = null;
                    string firstWaitingUserUsername = null;

                    using (var command = new NpgsqlCommand(getFirstWaitingUserQuery, connection))
                    {
                        command.Parameters.AddWithValue("@bookId", bookId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                firstWaitingUserEmail = reader.GetString(0);
                                firstWaitingUserUsername = reader.GetString(1);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(firstWaitingUserEmail))
                    {
                        await SendEmailForAvailableBook(firstWaitingUserEmail, bookId);

                        string addToCartQuery = "INSERT INTO ShoppingCart (BookId, Username, ActionType) VALUES (@bookId, @username, @actionType)";
                        using (var command = new NpgsqlCommand(addToCartQuery, connection))
                        {
                            command.Parameters.AddWithValue("@bookId", bookId);
                            command.Parameters.AddWithValue("@username", firstWaitingUserUsername);
                            command.Parameters.AddWithValue("@actionType", "Borrow");
                            command.ExecuteNonQuery();
                        }

                        string removeFromWaitingListQuery = "DELETE FROM WaitingList WHERE BookId = @bookId AND Username = @username";
                        using (var command = new NpgsqlCommand(removeFromWaitingListQuery, connection))
                        {
                            command.Parameters.AddWithValue("@bookId", bookId);
                            command.Parameters.AddWithValue("@username", firstWaitingUserUsername);
                            command.ExecuteNonQuery();
                        }

                        string decreaseCopiesQuery = "UPDATE Books SET CopiesAvailable = CopiesAvailable - 1 WHERE ID = @bookId";
                        using (var command = new NpgsqlCommand(decreaseCopiesQuery, connection))
                        {
                            command.Parameters.AddWithValue("@bookId", bookId);
                            command.ExecuteNonQuery();
                        }

                        ViewBag.Message = $"Book successfully removed from cart. The next user in the waiting list ({firstWaitingUserUsername}) has been notified.";
                    }
                    else
                    {
                        ViewBag.Message = "Book successfully removed from cart. No users were waiting for the book.";
                    }
                }
                else
                {
                    ViewBag.Message = "Book removed from cart successfully.";
                }
            }
        }
        catch (Exception ex)
        {
            ViewBag.Error = "An error occurred while removing the book from the cart.";
        }

        return RedirectToAction("ViewCart");
    }

    public async Task SendEmailForAvailableBook(string username, int bookId)
    {
        string fromMail = "malikabushah@gmail.com";
        string fromPassword = "eaqa ixie haib nkjw";

        string body = "<html><body><h2>Book Availability Notification</h2><p>Dear " + username + ",</p><p>The book you were waiting for is now available for borrowing and it has been added to your cart. " +
                      "You are the first to be notified. Please log in to your account and borrow the book as soon as possible., you have 30 days to pay for the borrow, otherwise we would have to take the book from your cart.</p>" +
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
            ViewBag.Error = "Error sending email notification.";
        }
    }

    [HttpPost]
    [Route("cart/CompletePayment")]
    public async Task<IActionResult> CompletePayment(string paypalOrderId, decimal totalAmount)
    {
        string username = HttpContext.Session.GetString("Username");
        string userEmail = HttpContext.Session.GetString("Email");
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(userEmail))
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = await connection.BeginTransactionAsync())
                {
                    try
                    {
                        // Retrieve cart items
                        string selectCartItemsQuery = @"
                            SELECT sc.BookId, sc.Quantity, 
                                   CASE WHEN sc.ActionType = 'Buy' THEN b.PriceBuy ELSE b.PriceBorrowing END AS Price,
                                   sc.ActionType
                            FROM ShoppingCart sc
                            JOIN Books b ON sc.BookId = b.ID
                            WHERE sc.Username = @Username";

                        List<(int bookId, int quantity, decimal price, string actionType)> cartItems = new List<(int, int, decimal, string)>();

                        using (var selectCartCommand = new NpgsqlCommand(selectCartItemsQuery, connection, transaction))
                        {
                            selectCartCommand.Parameters.AddWithValue("@Username", username);
                            using (var reader = await selectCartCommand.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    int bookId = reader.GetInt32(0);
                                    int quantity = reader.GetInt32(1);
                                    decimal price = reader.GetDecimal(2);
                                    string actionType = reader.GetString(3);
                                    cartItems.Add((bookId, quantity, price, actionType));
                                }
                            }
                        }

                        await SendEmail(userEmail, cartItems, "purchase");

                        foreach (var item in cartItems)
                        {
                            if (item.actionType == "Buy")
                            {
                                await InsertPurchaseRecord(item, connection, transaction, username, paypalOrderId);
                            }
                            else if (item.actionType == "Borrow")
                            {
                                await InsertBorrowRecord(item, connection, transaction, username, paypalOrderId);
                            }

                            // Remove from ShoppingCart
                            await DeleteFromCart(item.bookId, username, connection, transaction);
                        }

                        // Commit transaction
                        await transaction.CommitAsync();

                        ViewBag.Message = "Payment completed successfully. Thank you for your purchase!";
                        return RedirectToAction("Index", "Home");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        ViewBag.Error = "An error occurred during the payment process.";
                        return RedirectToAction("ViewCart");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ViewBag.Error = "An unexpected error occurred during the payment process.";
            return RedirectToAction("ViewCart");
        }
    }


    [HttpPost]
    public async Task<IActionResult> CompleteDirectPayment(int bookId, string paypalOrderId, decimal totalAmount)
    {
        string username = HttpContext.Session.GetString("Username");
        string userEmail = HttpContext.Session.GetString("Email");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(userEmail))
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = await connection.BeginTransactionAsync())
                {
                    try
                    {
                        // Check if the user has already bought the book
                        string checkUserBoughtQuery = @"
                        SELECT COUNT(*) 
                        FROM PurchasedBooks 
                        WHERE BookId = @BookId AND Username = @Username";

                        using (var checkUserBoughtCommand = new NpgsqlCommand(checkUserBoughtQuery, connection, transaction))
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

                        // Insert the purchase record
                        await InsertPurchaseRecord((bookId, 1, totalAmount, "Buy"), connection, transaction, username, paypalOrderId);

                        // Send email confirmation
                        await SendEmail(userEmail, new List<(int, int, decimal, string)> { (bookId, 1, totalAmount, "Buy") }, "purchase");

                        // Commit transaction
                        await transaction.CommitAsync();

                        TempData["Message"] = "Book purchased successfully. Thank you for your purchase!";
                        return RedirectToAction("Index", "Home");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        TempData["Error"] = "An error occurred during the payment process.";
                        return RedirectToAction("Index", "Home");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = "An unexpected error occurred during the payment process.";
            return RedirectToAction("Index", "Home");
        }
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

    private async Task DeleteFromCart(int bookId, string username, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        string deleteFromCartQuery = @"
            DELETE FROM ShoppingCart WHERE Username = @Username AND BookId = @BookId";

        using (var command = new NpgsqlCommand(deleteFromCartQuery, connection, transaction))
        {
            command.Parameters.AddWithValue("@Username", username);
            command.Parameters.AddWithValue("@BookId", bookId);
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

    [HttpGet]
    [Route("cart/PaymentSuccess")]
    public async Task<IActionResult> PaymentSuccessAsync()
    {
        ViewBag.Message = "Payment completed successfully. Thank you for your purchase!";
        return View();
    }
}