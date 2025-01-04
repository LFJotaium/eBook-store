using Microsoft.AspNetCore.Mvc;
using ebookStore.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using ebookStore.Models.ViewModels;
using Npgsql;

public class UserController : Controller
{
    private readonly EbookContext _context;
    private readonly string _connectionString;

    public UserController(EbookContext context, IConfiguration configuration)
    {
        _context = context;
        _connectionString = configuration.GetConnectionString("DefaultConnectionString");

    }

    public async Task<IActionResult> Profile(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return BadRequest("Username is required.");
        }

        // Fetch session data
        var sessionUsername = HttpContext.Session.GetString("Username") ?? "Guest";
        var firstName = HttpContext.Session.GetString("FirstName") ?? "";
        var lastName = HttpContext.Session.GetString("LastName") ?? "";
        var email = HttpContext.Session.GetString("Email") ?? "";
        var role = HttpContext.Session.GetString("Role") ?? "";

        User user = null;
        List<BorrowedBookViewModel> borrowedBooks = new List<BorrowedBookViewModel>();
        List<PurchasedBookViewModel> purchasedBooks = new List<PurchasedBookViewModel>();

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Fetch user details
            var userQuery = "SELECT FirstName, LastName, Email FROM Users WHERE Username = @Username";
            using (var userCommand = new NpgsqlCommand(userQuery, connection))
            {
                userCommand.Parameters.AddWithValue("@Username", username);
                using var reader = await userCommand.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    user = new User
                    {
                        FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                        LastName = reader.GetString(reader.GetOrdinal("LastName")),
                        Email = reader.GetString(reader.GetOrdinal("Email"))
                    };
                }
            }

            if (user == null)
            {
                return NotFound("User not found.");
            }


            // Fetch borrowed books
            var borrowedBooksQuery = @"
        SELECT b.ID, b.Title, b.AuthorName, bb.BorrowDate, bb.ReturnDate
        FROM BorrowedBooks bb
        INNER JOIN Books b ON bb.BookId = b.ID
        WHERE bb.Username = @Username";
            using (var borrowedBooksCommand = new NpgsqlCommand(borrowedBooksQuery, connection))
            {
                borrowedBooksCommand.Parameters.AddWithValue("@Username", username);
                using var reader = await borrowedBooksCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        Console.WriteLine($"Field {i}: {reader.GetName(i)}");
                    }

                    borrowedBooks.Add(new BorrowedBookViewModel
                    {
                        BookId = reader.GetInt32(reader.GetOrdinal("id")),
                        Title = reader.GetString(reader.GetOrdinal("Title")),
                        Author = reader.GetString(reader.GetOrdinal("AuthorName")),
                        BorrowDate = reader.GetDateTime(reader.GetOrdinal("BorrowDate")),
                        ReturnDate = (DateTime)(reader.IsDBNull(reader.GetOrdinal("ReturnDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ReturnDate")))
                    });
                }
            }

            // Fetch purchased books
            var purchasedBooksQuery = @"
        SELECT b.ID, b.Title, b.AuthorName, pb.PurchaseDate
        FROM PurchasedBooks pb
        INNER JOIN Books b ON pb.BookId = b.ID
        WHERE pb.Username = @Username";
            using (var purchasedBooksCommand = new NpgsqlCommand(purchasedBooksQuery, connection))
            {
                purchasedBooksCommand.Parameters.AddWithValue("@Username", username);
                using var reader = await purchasedBooksCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    purchasedBooks.Add(new PurchasedBookViewModel
                    {
                        BookId = reader.GetInt32(reader.GetOrdinal("id")),
                        Title = reader.GetString(reader.GetOrdinal("Title")),
                        Author = reader.GetString(reader.GetOrdinal("AuthorName")),
                        PurchaseDate = reader.GetDateTime(reader.GetOrdinal("PurchaseDate"))
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return StatusCode(500, "Internal server error.");
        }

        // Map data to the view model
        var profileViewModel = new ProfileViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            BorrowedBooks = borrowedBooks,
            PurchasedBooks = purchasedBooks
        };

        ViewData["Username"] = sessionUsername;
        ViewData["FirstName"] = firstName;
        ViewData["LastName"] = lastName;
        ViewData["Email"] = email;
        ViewData["Role"] = role;

        return View(profileViewModel);
    }


}