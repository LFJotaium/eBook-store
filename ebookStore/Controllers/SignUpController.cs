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
            return View("~/Views/Account/SignUp.cshtml");
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
        public IActionResult SignUp(UserRegistrationViewModel userViewModel)
        {
            if (IsEmailAlreadyExist(_connectionString, userViewModel.Email))
            {
                ModelState.AddModelError("Email", "This email is already registered.");
                return View("~/Views/Account/SignUp.cshtml", userViewModel);
            }

            if (!ModelState.IsValid)
            {
                foreach (var modelError in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning($"Validation Error: {modelError.ErrorMessage}");
                }
                return View("~/Views/Account/SignUp.cshtml", userViewModel);
            }

            var user = new User
            {
                FirstName = userViewModel.FirstName,
                LastName = userViewModel.LastName,
                Username = userViewModel.Username,
                Email = userViewModel.Email,
                Password = userViewModel.Password,
                Role = userViewModel.Role
            };

            try
            {
                SaveUserToDatabase(user);

                HttpContext.Session.SetString("Username", user.Username);
                HttpContext.Session.SetString("FirstName", user.FirstName);
                HttpContext.Session.SetString("LastName", user.LastName);
                HttpContext.Session.SetString("Email", user.Email);
                HttpContext.Session.SetString("Role", user.Role);

                return RedirectToAction("Index", "Home"); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving user to database");
                ModelState.AddModelError("", "An error occurred while processing your request.");
                return View("~/Views/Account/SignUp.cshtml", userViewModel);
            }
        }

        public bool IsEmailAlreadyExist(string connectionString,string email)

        {
            Console.WriteLine("HERE3");

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            string query = "SELECT COUNT(*) FROM Users WHERE Email = @Email";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@Email", email); 

            var count = (long)command.ExecuteScalar();
            return count > 0; 

        }
    }
}