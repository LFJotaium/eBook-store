using ebookStore.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ebookStore.Controllers
{
    public class LogInController : Controller
    {
        private readonly string? _connectionString;
        private readonly ILogger<LogInController> _logger;

        public LogInController(IConfiguration configuration, ILogger<LogInController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnectionString");
            _logger = logger;
        }

        public IActionResult LogIn()
        {
            return View();
        }

        [HttpPost]
        [Route("Account/LogIn")]
        public IActionResult LogIn(LogInModel userInfo)
        {
            if (!ModelState.IsValid)
            {
                return View(userInfo);
            }

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                _logger.LogInformation("Attempting login for Email: {Email}", userInfo.Email);
                string hashedPassword = userInfo.HashPassword(userInfo.Password);
                string query = "SELECT COUNT(*) FROM Users WHERE user.Email = @Email AND hashedpassword = @Password";
                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@Email", userInfo.Email);
                command.Parameters.AddWithValue("@Password", hashedPassword);
                long count = (long)command.ExecuteScalar();

                if (count > 0)
                {
                    TempData["Message"] = "Login successful!";
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    _logger.LogWarning("Login failed for Email: {Email}", userInfo.Email);
                    ModelState.AddModelError("", "Invalid email or password.");
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for Email: {Email}", userInfo.Email);
                ModelState.AddModelError("", "An error occurred while processing your request.");
            }

            return View(userInfo);
        }

    }
}


    