using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ebookStore.Controllers
{
    public class CheckoutController : Controller
    {
        public string TotalAmount { get; set; } = null;
        private string PaypalClientId { get; set; } = "";
        private string PaypalSecret { get; set; } = "";
        private string PaypalUrl { get; set; } = "";
        private readonly string _connectionString;

        public CheckoutController(IConfiguration configuration)
        {
            PaypalClientId = configuration["PaypalSettings:ClientId"]!;
            PaypalSecret = configuration["PaypalSettings:Secret"]!;
            PaypalUrl = configuration["PaypalSettings:Url"]!;
            _connectionString = configuration.GetConnectionString("DefaultConnectionString");
        }

        [HttpPost]
        private async Task<string> GetPaypalAccessToken()
        {
            string accessToken = "";
            string url = PaypalUrl + "/v1/oauth2/token";
            using (var client = new HttpClient())
            {
                try
                {
                    string credentials64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(PaypalClientId + ":" + PaypalSecret));
                    client.DefaultRequestHeaders.Add("Authorization", "Basic " + credentials64);
                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                    requestMessage.Content = new StringContent("grant_type=client_credentials", null, "application/x-www-form-urlencoded");

                    var httpResponse = await client.SendAsync(requestMessage);

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var strResponse = await httpResponse.Content.ReadAsStringAsync();
                        var jsonResponse = JsonNode.Parse(strResponse);

                        if (jsonResponse != null)
                        {
                            accessToken = jsonResponse["access_token"]?.ToString() ?? "";
                            if (string.IsNullOrEmpty(accessToken))
                            {
                                Console.WriteLine("Error: access_token is null or empty.");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Error: {httpResponse.StatusCode}, Response: {await httpResponse.Content.ReadAsStringAsync()}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while fetching access token: {ex.Message}");
                }
            }
            return accessToken;
        }

        [HttpPost]
        [Route("Checkout/CreateOrder")]
        public async Task<IActionResult> CreateOrder([FromBody] JsonNode data)
        {
            var totalAmount = data?["amount"]?.ToString();
            if (totalAmount == null)
            {
                return new JsonResult(new { Id = "" });
            }

            JsonObject createOrderRequest = new JsonObject();
            createOrderRequest.Add("intent", "CAPTURE");
            JsonObject amount = new JsonObject();
            amount.Add("currency_code", "USD");
            amount.Add("value", totalAmount);
            JsonObject purchaseUnit1 = new JsonObject();
            purchaseUnit1.Add("amount", amount);
            JsonArray purchaseUnits = new JsonArray();
            purchaseUnits.Add(purchaseUnit1);
            createOrderRequest.Add("purchase_units", purchaseUnits);

            string accessToken = await GetPaypalAccessToken();

            if (string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine("Error: Access token is empty, cannot proceed.");
                return Json(new { Id = "" });
            }

            string url = PaypalUrl + "/v2/checkout/orders";
            using (var client = new HttpClient())
            {
                try
                {

                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                    requestMessage.Content = new StringContent(createOrderRequest.ToString(), null, "application/json");

                    var httpResponse = await client.SendAsync(requestMessage);

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var strResponse = await httpResponse.Content.ReadAsStringAsync();
                        var jsonResponse = JsonNode.Parse(strResponse);

                        if (jsonResponse != null)
                        {
                            string paypalOrderId = jsonResponse["id"]?.ToString() ?? "";
                            return new JsonResult(new { id = paypalOrderId });
                        }
                        else
                        {
                            Console.WriteLine("Error: PayPal response is null or invalid.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Error: {httpResponse.StatusCode}, Response: {await httpResponse.Content.ReadAsStringAsync()}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during order creation: {ex.Message}");
                }
            }

            return Json(new { Id = "" });
        }

        [HttpPost]
        [Route("Checkout/CompletePurchase")]
        public async Task<IActionResult> CompletePurchase([FromBody] JsonNode data)
        {

            Console.WriteLine("I AM IN COMPLETE PURCHASE");
            var paypalOrderId = data?["orderId"]?.ToString();
            var bookId = data?["bookId"]?.ToString();

            if (string.IsNullOrEmpty(paypalOrderId) || string.IsNullOrEmpty(bookId))
            {
                return Json(new { success = false, message = "Invalid request." });
            }

            try
            {
                Console.WriteLine($"Processing PayPal Order ID: {paypalOrderId} for Book ID: {bookId}");

                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                using var transaction = connection.BeginTransaction();

                string insertPurchasedQuery = @"
                INSERT INTO PurchasedBooks (BookId, Username, PurchaseDate, PaypalOrderId)
                VALUES (@BookId, @Username, @PurchaseDate, @PaypalOrderId)";
                using var insertPurchasedCommand = new NpgsqlCommand(insertPurchasedQuery, connection);
                insertPurchasedCommand.Parameters.AddWithValue("@BookId", bookId);
                insertPurchasedCommand.Parameters.AddWithValue("@Username", HttpContext.Session.GetString("Username"));
                insertPurchasedCommand.Parameters.AddWithValue("@PurchaseDate", DateTime.Now);
                insertPurchasedCommand.Parameters.AddWithValue("@PaypalOrderId", paypalOrderId);
                await insertPurchasedCommand.ExecuteNonQueryAsync();

                string updateBookQuery = "UPDATE Books SET CopiesAvailable = CopiesAvailable - 1 WHERE ID = @BookId";
                using var updateBookCommand = new NpgsqlCommand(updateBookQuery, connection);
                updateBookCommand.Parameters.AddWithValue("@BookId", bookId);
                await updateBookCommand.ExecuteNonQueryAsync();

                string deleteFromCartQuery = "DELETE FROM ShoppingCart WHERE Username = @Username AND BookId = @BookId";
                using var deleteFromCartCommand = new NpgsqlCommand(deleteFromCartQuery, connection);
                deleteFromCartCommand.Parameters.AddWithValue("@Username", HttpContext.Session.GetString("Username"));
                deleteFromCartCommand.Parameters.AddWithValue("@BookId", bookId);
                await deleteFromCartCommand.ExecuteNonQueryAsync();

                transaction.Commit();

                return Redirect("https://localhost:7050/");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                TempData["Error"] = "An error occurred while processing the payment.";
                return Redirect("https://localhost:7050/");
            }
        }
    }
}
