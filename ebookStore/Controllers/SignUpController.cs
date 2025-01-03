using ebookStore.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace ebookStore.Controllers
{
    public class SignUpController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<SignUpController> _logger;
        private const string InsertUserQuery = 
            "INSERT INTO users (username, firstname, lastname, email, password, role) " +
            "VALUES (@username, @firstname, @lastname, @email, @password, @role);";
        

        public SignUpController(IConfiguration configuration, ILogger<SignUpController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnectionString");
            _logger = logger;
        }
        
        public IActionResult SignUp()
        {
            Console.WriteLine("Sign Up.cs");
            return View(); // return the SignUp View
        }
        [HttpPost]
        public void SaveUserToDatabase(User user)
        {
            try
            {
                string hashedPassword = user.HashPassword(user.Password);
                ExecuteNonQuery(InsertUserQuery, command =>
                {
                    command.Parameters.AddWithValue("@username", user.Username);
                    command.Parameters.AddWithValue("@firstname", user.FirstName);
                    command.Parameters.AddWithValue("@lastname", user.LastName);
                    command.Parameters.AddWithValue("@email", user.Email);
                    command.Parameters.AddWithValue("@password", hashedPassword);
                    command.Parameters.AddWithValue("@role", user.Role);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving user to database");
                throw;
            }
        }

        private void ExecuteNonQuery(string query, Action<NpgsqlCommand> parameterizeCommand)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var command = new NpgsqlCommand(query, connection);
            parameterizeCommand(command);
            command.ExecuteNonQuery();
        }
        
        [HttpPost]
        [Route("Account/SignUp")]
        public IActionResult SignUp(User user)
        {
            /// have to add function to check username 
            if (IsEmailAlreadyExist(_connectionString,user.Email))
            {
                ModelState.AddModelError("Email", "This email is already registered.");
                return View(user);
            }
            if (ModelState.IsValid)
            {
                SaveUserToDatabase(user);
                return RedirectToAction("Index", "Home"); // Redirect to success page
            }
            return View(user); // Return the view with validation errors if any
        }
        public  bool IsEmailAlreadyExist(string connectionString,string email)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            string query = "SELECT COUNT(*) FROM Users WHERE Email = @Email";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@Email", email); //Parameter bending to safely bind the email to the query, avoiding SQL injection.

            var count = (long)command.ExecuteScalar();
            return count > 0; // Returns true if the email exists, false otherwise

        }
    }
}