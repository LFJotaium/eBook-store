using System.Net;
using System.Net.Mail;
using ebookStore.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ebookStore.Controllers;

public class CartController: Controller
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

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            connection.Open();
            string query = @"
            SELECT SUM(
                CASE WHEN sc.ActionType = 'Buy' THEN b.PriceBuy 
                     ELSE b.PriceBorrowing 
                END * sc.Quantity
            ) AS TotalAmount
            FROM ShoppingCart sc
            JOIN Books b ON sc.BookId = b.ID
            WHERE sc.Username = @username";

            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@username", username);
                var result = command.ExecuteScalar();
                if (result != DBNull.Value && result != null)
                {
                    totalAmount = Convert.ToDecimal(result);
                }
            }
        }
        ViewBag.TotalAmount = totalAmount;
        Console.WriteLine("Checkout Page Loaded with Total Amount: $" + totalAmount);
        return View();
    }
    
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
           CASE 
               WHEN sc.ActionType = 'Buy' THEN p.currentpricebuy 
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
    public IActionResult RemoveFromCart(int bookId, string actionType)
    {
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            return RedirectToAction("Login", "Account");
        }

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            connection.Open();

            // delete from PurchasedBooks or BorrowedBooks table based on the actionType
            string deleteQuery = actionType == "Buy"
                ? "DELETE FROM PurchasedBooks WHERE BookId = @bookId AND Username = @username"
                : "DELETE FROM BorrowedBooks WHERE BookId = @bookId AND Username = @username";

            using (var command = new NpgsqlCommand(deleteQuery, connection))
            {
                command.Parameters.AddWithValue("@bookId", bookId);
                command.Parameters.AddWithValue("@username", username);
                command.ExecuteNonQuery();
            }

            // Delete from ShoppingCart table
            string deleteFromCartQuery = "DELETE FROM ShoppingCart WHERE BookId = @bookId AND Username = @username";

            using (var command = new NpgsqlCommand(deleteFromCartQuery, connection))
            {
                command.Parameters.AddWithValue("@bookId", bookId);
                command.Parameters.AddWithValue("@username", username);
                command.ExecuteNonQuery();
            }
        }

        return RedirectToAction("ViewCart");
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

                    await SendEmail(userEmail, cartItems, "purchase"); // You can change the action type as needed (buy/borrow)

                        // Process each cart item
                        foreach (var item in cartItems)
                        {
                            if (item.actionType == "Buy")
                            {
                                await InsertPurchaseRecord(item, connection, transaction, username, paypalOrderId);
                                await UpdateBookCopies(item, connection, transaction);
                            }
                            else if (item.actionType == "Borrow")
                            {
                                await InsertBorrowRecord(item, connection, transaction, username, paypalOrderId);
                                await UpdateBookCopies(item, connection, transaction);
                            }

                            // Remove from ShoppingCart
                            await DeleteFromCart(item.bookId, username, connection, transaction);
                        }

                        // Commit transaction
                        await transaction.CommitAsync();


                        // Redirect to success page
                        return RedirectToAction("Index", "Home");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = "An error occurred during the payment process." });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred during the payment process." });
        }
    }

    // Helper methods for inserting records and updating books
    private async Task InsertPurchaseRecord((int bookId, int quantity, decimal price, string actionType) item, NpgsqlConnection connection, NpgsqlTransaction transaction, string username, string paypalOrderId)
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

    private async Task InsertBorrowRecord((int bookId, int quantity, decimal price, string actionType) item, NpgsqlConnection connection, NpgsqlTransaction transaction, string username, string paypalOrderId)
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

    private async Task UpdateBookCopies((int bookId, int quantity, decimal price, string actionType) item, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        string updateBookQuery = @"
            UPDATE Books SET CopiesAvailable = CopiesAvailable - @Quantity WHERE ID = @BookId";

        using (var command = new NpgsqlCommand(updateBookQuery, connection, transaction))
        {
            command.Parameters.AddWithValue("@Quantity", item.quantity);
            command.Parameters.AddWithValue("@BookId", item.bookId);
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
        string fromPassword = "nsyx tbov ttyq khch"; // Use an app-specific password if 2FA is enabled

        // Create a dynamic email body based on the action
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

        message.To.Add(new MailAddress(username));  // Send email to the user's email address

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
            Console.WriteLine($"Error sending email: {ex.Message}");
        }
    }



    [HttpGet]
    [Route("cart/PaymentSuccess")]
    public async Task<IActionResult> PaymentSuccessAsync()
    {
        return View();
    }





}