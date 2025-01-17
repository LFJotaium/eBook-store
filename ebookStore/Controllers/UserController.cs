using Microsoft.AspNetCore.Mvc;
using ebookStore.Models;
using ebookStore.Models.ViewModels;
using Npgsql;
using System.Net.Mail;
using System.Net;

public class UserController : Controller
{
    private readonly string _connectionString;

    public UserController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnectionString");

    }


    private bool IsUserLoggedIn(string username,string currentUsername)
    {
        return !string.IsNullOrEmpty(HttpContext.Session.GetString("Username")) && currentUsername == username ;
    }
    public async Task<IActionResult> Profile(string username)
    {


        var currentUsername = HttpContext.Session.GetString("Username");

        if (!IsUserLoggedIn(username, currentUsername))
        {
            return RedirectToAction("Index", "Home"); 
        }

        if (string.IsNullOrEmpty(username))
        {
            return BadRequest("Username is required.");
        }


        var sessionUsername = HttpContext.Session.GetString("Username") ?? "Guest";
        var firstName = HttpContext.Session.GetString("FirstName") ?? "";
        var lastName = HttpContext.Session.GetString("LastName") ?? "";
        var email = HttpContext.Session.GetString("Email") ?? "";
        var role = HttpContext.Session.GetString("Role") ?? "";

        User user = null;
        List<BorrowedBookViewModel> borrowedBooks = new List<BorrowedBookViewModel>();
        List<PurchasedBookViewModel> purchasedBooks = new List<PurchasedBookViewModel>();
        List<dynamic> waitingList = new List<dynamic>();

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

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

            var waitingListQuery = @"
            SELECT wl.BookId, b.Title, b.AuthorName, wl.CreatedAt
            FROM WaitingList wl
            INNER JOIN Books b ON wl.BookId = b.ID
            WHERE wl.Username = @Username
            ORDER BY wl.CreatedAt ASC";
            using (var waitingListCommand = new NpgsqlCommand(waitingListQuery, connection))
            {
                waitingListCommand.Parameters.AddWithValue("@Username", username);
                using var reader = await waitingListCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    waitingList.Add(new
                    {
                        BookId = reader.GetInt32(reader.GetOrdinal("BookId")),
                        Author = reader.GetString(reader.GetOrdinal("AuthorName")),
                        AddedOn = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            TempData["Error"] = $"Error: {ex.Message}";
            return RedirectToAction("Index");
        }

        var profileViewModel = new ProfileViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            BorrowedBooks = borrowedBooks,
            PurchasedBooks = purchasedBooks
        };

        ViewData["WaitingList"] = waitingList;

        ViewData["Username"] = sessionUsername;
        ViewData["FirstName"] = firstName;
        ViewData["LastName"] = lastName;
        ViewData["Email"] = email;
        ViewData["Role"] = role;

        return View(profileViewModel);
    }


    [HttpPost]
    public async Task<IActionResult> UnborrowBook(int bookId)
    {
        var username = HttpContext.Session.GetString("Username");

        if (string.IsNullOrEmpty(username))
        {
            return BadRequest("User is not logged in.");
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var unborrowQuery = "DELETE FROM BorrowedBooks WHERE Username = @Username AND BookId = @BookId";
            using (var unborrowCommand = new NpgsqlCommand(unborrowQuery, connection))
            {
                unborrowCommand.Parameters.AddWithValue("@Username", username);
                unborrowCommand.Parameters.AddWithValue("@BookId", bookId);
                await unborrowCommand.ExecuteNonQueryAsync();
            }

            var incrementCopiesQuery = "UPDATE Books SET CopiesAvailable = CopiesAvailable + 1 WHERE ID = @BookId";
            using (var incrementCommand = new NpgsqlCommand(incrementCopiesQuery, connection))
            {
                incrementCommand.Parameters.AddWithValue("@BookId", bookId);
                await incrementCommand.ExecuteNonQueryAsync();
            }

            var getFirstWaitingUserQuery = "SELECT Email, Username FROM WaitingList WHERE BookId = @BookId ORDER BY CreatedAt ASC LIMIT 1";
            string firstWaitingUserEmail = null;
            string firstWaitingUserUsername = null;

            using (var waitingListCommand = new NpgsqlCommand(getFirstWaitingUserQuery, connection))
            {
                waitingListCommand.Parameters.AddWithValue("@BookId", bookId);
                using (var reader = await waitingListCommand.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        firstWaitingUserEmail = reader.GetString(0);
                        firstWaitingUserUsername = reader.GetString(1);
                    }
                }
            }

            if (!string.IsNullOrEmpty(firstWaitingUserEmail))
            {
                Console.WriteLine($"Sending email to: {firstWaitingUserEmail} for Book ID: {bookId}");
                try
                {
                    await SendEmailForAvailableBook(firstWaitingUserEmail, bookId);
                    Console.WriteLine($"Email successfully sent to: {firstWaitingUserEmail}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending email: {ex.Message}");
                }

                var addToShoppingCartQuery = "INSERT INTO ShoppingCart (BookId, Username, ActionType) VALUES (@BookId, @Username, @ActionType)";
                using (var addToCartCommand = new NpgsqlCommand(addToShoppingCartQuery, connection))
                {
                    addToCartCommand.Parameters.AddWithValue("@BookId", bookId);
                    addToCartCommand.Parameters.AddWithValue("@Username", firstWaitingUserUsername);
                    addToCartCommand.Parameters.AddWithValue("@ActionType", "Borrow");
                    await addToCartCommand.ExecuteNonQueryAsync();
                }

                var removeFromWaitingListQuery = "DELETE FROM WaitingList WHERE BookId = @BookId AND Username = @Username";
                using (var removeFromWaitingListCommand = new NpgsqlCommand(removeFromWaitingListQuery, connection))
                {
                    removeFromWaitingListCommand.Parameters.AddWithValue("@BookId", bookId);
                    removeFromWaitingListCommand.Parameters.AddWithValue("@Username", firstWaitingUserUsername);
                    await removeFromWaitingListCommand.ExecuteNonQueryAsync();
                }

                var decreaseCopiesQuery = "UPDATE Books SET CopiesAvailable = CopiesAvailable - 1 WHERE ID = @BookId";
                using (var decreaseCommand = new NpgsqlCommand(decreaseCopiesQuery, connection))
                {
                    decreaseCommand.Parameters.AddWithValue("@BookId", bookId);
                    await decreaseCommand.ExecuteNonQueryAsync();
                }

                TempData["Message"] = "Book returned successfully. The first person in the waiting list has been notified and the book has been added to their shopping cart.";
            }
            else
            {
                TempData["Message"] = "Book returned successfully.";
            }


            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            TempData["Error"] = $"Error: {ex.Message}";
            return Json(new { success = false, message = ex.Message });
        }
    }


    public IActionResult DownloadBook(int bookId)
    {


        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            string query = @"
            SELECT Files
            FROM Books
            WHERE ID = @BookId";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@BookId", bookId);

            var filesLink = command.ExecuteScalar() as string;

            if (string.IsNullOrEmpty(filesLink))
            {
                TempData["Error"] = "Download link not found.";
                return RedirectToAction("Index"); 
            }

            return Redirect(filesLink);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error: {ex.Message}";
            return RedirectToAction("Index");
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeletePurchasedBook(int bookId)
    {
        var username = HttpContext.Session.GetString("Username");

        if (string.IsNullOrEmpty(username))
        {
            return BadRequest("User is not logged in.");
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var deleteQuery = "DELETE FROM PurchasedBooks WHERE Username = @Username AND BookId = @BookId";
            using (var deleteCommand = new NpgsqlCommand(deleteQuery, connection))
            {
                deleteCommand.Parameters.AddWithValue("@Username", username);
                deleteCommand.Parameters.AddWithValue("@BookId", bookId);
                await deleteCommand.ExecuteNonQueryAsync();
            }

            TempData["Message"] = "Book deleted successfully.";
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            TempData["Error"] = $"Error: {ex.Message}";
            return Json(new { success = false, message = ex.Message });
        }
    }


    private async Task SendEmailForAvailableBook(string email, int bookId)
    {
        string fromMail = "malikabushah@gmail.com";
        string fromPassword = "vsjm dvly keqg ymzl"; 

        string body = $@"
<html>
<body>
    <h2>Your requested book (ID: {bookId}) is now available!</h2>
    <p>Dear user,</p>
    <p>The book you requested (ID: {bookId}) is now available to borrow. Please borrow it within the next 24 hours.</p>
</body>
</html>";

        MailMessage message = new MailMessage
        {
            From = new MailAddress(fromMail),
            Subject = "Book Available for Borrowing",
            Body = body,
            IsBodyHtml = true
        };

        message.To.Add(new MailAddress(email)); 

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


}