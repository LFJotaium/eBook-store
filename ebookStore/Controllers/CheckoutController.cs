using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;


namespace ebookStore.Controllers;

public class CheckoutController : Controller
{
    private string PaypalClientId { get; set; } = "";
    private string PaypalSecret { get; set; } = "";
    private string PaypalUrl { get; set; } = "";

    public CheckoutController(IConfiguration configuration)
    {
        PaypalClientId = configuration["PaypalSettings:ClientId"]!;
        PaypalSecret = configuration["PaypalSettings:Secret"]!;
        PaypalUrl = configuration["PaypalSettings:Url"]!;
 
    }
    [HttpGet]
    [Route("checkout")]
    public IActionResult Checkout()
    {
        ViewBag.PaypalClientId = PaypalClientId;
        Console.WriteLine("Checkout Page Loaded");
        return View();
    }
    

    [HttpPost]
    private async Task<string> GetPaypalAccessToken()
    {
        string accessToken = "";
        string url = PaypalUrl + "/v1/oauth2/token";
        using (var client = new HttpClient())
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
                }
            }
        }
        //Console.WriteLine("Access Token in method: " + accessToken);
        return accessToken;
    }
    
    [HttpPost]
    [Route("Checkout/CreateOrder")]
    public async Task<IActionResult> CreateOrder([FromBody] JsonNode data)
    {
       var totalAmount = data?["amount"]?.ToString();
        if (totalAmount == null)
        {
            return new JsonResult(new {  Id = ""});
        }
        
        // create the request body 
        JsonObject createOrderRequest = new JsonObject();
        createOrderRequest.Add("intent","CAPTURE");
        JsonObject amount = new JsonObject();
        amount.Add("currency_code", "USD");
        amount.Add("value", totalAmount);
        JsonObject purchaseUnit1 = new JsonObject();
        purchaseUnit1.Add("amount", amount);
        JsonArray purchaseUnits = new JsonArray();
        purchaseUnits.Add(purchaseUnit1);
        createOrderRequest.Add("purchase_units", purchaseUnits);
        //get access token 
        string accessToken = await GetPaypalAccessToken();
        //Console.WriteLine($"Access Token: {accessToken}");
        //send request
        string url = PaypalUrl + "/v2/checkout/orders";
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Content = new StringContent(createOrderRequest.ToString(), null, "application/json");
            var httpResponse = await client.SendAsync(requestMessage);
            //Console.WriteLine("Response Content: "+ httpResponse.Content.ReadAsStringAsync().Result);
            if (httpResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("connecting success");
                var strResponse = await httpResponse.Content.ReadAsStringAsync();
                var jsonResponse = JsonNode.Parse(strResponse);
                if (jsonResponse != null)
                {
                    string paypalOrderId = jsonResponse["id"]?.ToString() ?? "";
                    return new JsonResult(new { id = paypalOrderId });
                }
                else
                {
                    Console.WriteLine("Error: " + httpResponse.Content.ReadAsStringAsync().Result);
                }
            }
        }
        
        return Json(new { Id = ""});
    }
    
}
