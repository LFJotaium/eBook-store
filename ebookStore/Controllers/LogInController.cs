using ebookStore.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

public class LogInController : Controller
{
    private readonly string? _connectionString;
    private readonly ILogger<LogInController> _logger;

    public LogInController(IConfiguration configuration, ILogger<LogInController> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnectionString");
        _logger = logger;
    }




    // Post login data
    [HttpPost]
    public IActionResult LogIn(LogInModel userInfo)
    {
        if (HttpContext.Session.GetString("LoggedIn") == "in")
        {
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid)
        {
            return View("~/Views/Account/LogIn.cshtml");
        }

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            _logger.LogInformation("Attempting login for Email: {Email}", userInfo.Email);

            string query = "SELECT Password, Username, FirstName, LastName, Email, Role FROM Users WHERE Email = @Email";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@Email", userInfo.Email);

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    var storedPasswordHash = reader["Password"].ToString();
                    var storedUsername = reader["Username"].ToString();
                    var firstName = reader["FirstName"].ToString();
                    var lastName = reader["LastName"].ToString();
                    var role = reader["Role"].ToString();

                    if (storedPasswordHash != null && VerifyPassword(userInfo.Password, storedPasswordHash))
                    {
                        HttpContext.Session.SetString("Username", storedUsername);
                        HttpContext.Session.SetString("FirstName", firstName);
                        HttpContext.Session.SetString("LastName", lastName);
                        HttpContext.Session.SetString("Email", userInfo.Email);
                        HttpContext.Session.SetString("Role", role);
                        HttpContext.Session.SetString("LoggedIn", "in");

                        return RedirectToAction("Index", "Home");
                    }
                }
            }

            _logger.LogWarning("Login failed for Email: {Email}", userInfo.Email);

            ViewBag.ErrorMessage = "Invalid email or password.";
            return View("~/Views/Account/LogIn.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for Email: {Email}", userInfo.Email);

            ViewBag.ErrorMessage = "An error occurred while processing your request.";
            return View("~/Views/Account/LogIn.cshtml");
        }
    }



    private bool VerifyPassword(string enteredPassword, string storedPasswordHash)
    {
        var parts = storedPasswordHash.Split(":");
        if (parts.Length != 2)
            return false;

        var storedHash = parts[0];
        var storedSalt = parts[1];

        using (var sha256 = SHA256.Create())
        {
            var combinedPassword = Encoding.UTF8.GetBytes(enteredPassword + storedSalt); 
            var hash = sha256.ComputeHash(combinedPassword);
            var hashString = Convert.ToBase64String(hash);

            return hashString == storedHash;
        }
    }

}
