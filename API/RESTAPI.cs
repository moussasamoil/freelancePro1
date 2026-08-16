namespace lotus_blue.API;
using lotus_blue.Models.ViewModel;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

public class RESTAPI
{
    private readonly HttpClient _httpClient;

    public RESTAPI()
    {
        _httpClient = new HttpClient();

        // Set the default authorization header with your token
        string predefinedToken = "ZBLSJWA9235821DT"; // Replace with your actual token
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", predefinedToken);
    }

  
    public async Task<HttpResponseMessage> CreateOrderAsync(OrderPostApi orderData)
    {
        var response = await _httpClient.PostAsJsonAsync("https://sbskarg.com/Api/Order/Create", orderData);
        return response;
    }

    public async Task<HttpResponseMessage> UpdateOrderAsync(int externalOrderId, PostOrderToSbs orderData)
    {
        // Log externalOrderId and orderData to console for debugging
        Console.WriteLine($"External Order ID: {externalOrderId}");
        Console.WriteLine($"Order Data: {JsonConvert.SerializeObject(orderData, Formatting.Indented)}");

        // Construct the URL for the API endpoint, including the external order ID
        var url = $"https://sbskarg.com/Api/Order/edit/{externalOrderId}";

        // Serialize the order data to JSON
        var jsonContent = JsonConvert.SerializeObject(orderData);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Make the PUT request to the external API
        var response = await _httpClient.PutAsync(url, content);

        return response;
    }

    // update order status 
    public async Task<HttpResponseMessage> UpdateOrderStatusAsync(int externalOrderId, UpdateStatusRequest Status)
    {
        // Construct the URL for the API endpoint, including the external order ID
        var url = $"https://sbskarg.com/Api/Order/UpdateStatus/{externalOrderId}";
        Console.WriteLine(url);

        // Serialize the order data to JSON, including the reason and status
        var jsonContent = JsonConvert.SerializeObject(Status);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Make the POST request to the external API
        var response = await _httpClient.PostAsync(url, content);
        Console.WriteLine(response);

        return response;
    }





}


