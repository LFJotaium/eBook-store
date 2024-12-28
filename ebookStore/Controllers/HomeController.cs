using ebookStore.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ebookStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly string _connectionString;

        public HomeController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnectionString");
        }

        public IActionResult Index()
        {
            List<Book> books = new List<Book>();
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                string query = "SELECT * FROM Books";
                using var command = new NpgsqlCommand(query, connection);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    books.Add(new Book
                    {
                        ID = reader.GetInt32(reader.GetOrdinal("ID")),
                        Title = reader.GetString(reader.GetOrdinal("Title")),
                        AuthorName = reader.GetString(reader.GetOrdinal("AuthorName")),
                        Publisher = reader.GetString(reader.GetOrdinal("Publisher")),
                        PriceBuy = reader.GetDecimal(reader.GetOrdinal("PriceBuy")),
                        PriceBorrowing = reader.GetDecimal(reader.GetOrdinal("PriceBorrowing")),
                        YearOfPublish = reader.GetInt32(reader.GetOrdinal("YearOfPublish")),
                        Genre = reader.IsDBNull(reader.GetOrdinal("Genre")) ? null : reader.GetString(reader.GetOrdinal("Genre")),
                        CoverImagePath = reader.IsDBNull(reader.GetOrdinal("CoverImagePath")) ? null : reader.GetString(reader.GetOrdinal("CoverImagePath")),
                    });
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions
            }

            return View(books);
        }
    }
}
