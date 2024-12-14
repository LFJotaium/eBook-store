using ebookStore.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace ebookStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly string connectionString;

        public HomeController(IConfiguration configuration)
        {
            connectionString = configuration.GetConnectionString("DefaultConnectionString");
        }

        public IActionResult AddUser()
        {
            try
            {
                var user = new User
                {
                    Username = "johndoe",
                    FirstName = "John",
                    LastName = "Doe",
                    Email = "john.doe@example.com",
                    Password = HashPassword("Passw0rd123"),
                    Role = "Admin"
                };

                SaveUser(user);

                return Ok("User added successfully");
            }
            catch (Exception ex)
            {
                // Log error here for monitoring/debugging purposes
                return BadRequest("An error occurred while adding the user.");
            }
        }

        public void SaveUser(User user)
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    string query =
                        "INSERT INTO users (username, firstname, lastname, email, password, role) VALUES (@username, @firstname, @lastname, @email, @password, @role);";
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@username", user.Username);
                        command.Parameters.AddWithValue("@firstname", user.FirstName);
                        command.Parameters.AddWithValue("@lastname", user.LastName);
                        command.Parameters.AddWithValue("@email", user.Email);
                        command.Parameters.AddWithValue("@password", user.Password);
                        command.Parameters.AddWithValue("@role", user.Role);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log and rethrow the exception
                Console.WriteLine($"Error while saving user: {ex.Message}");
                throw;
            }
        }

        private static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }
    }
}
