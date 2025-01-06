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
        //return View("Checkout"); // Ensure it points to the correct view
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
                   CASE WHEN sc.ActionType = 'Buy' THEN b.PriceBuy ELSE b.PriceBorrowing END AS Price, 
                   sc.ActionType
            FROM ShoppingCart sc
            JOIN Books b ON sc.BookId = b.ID
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





}