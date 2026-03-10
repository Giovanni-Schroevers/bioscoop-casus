using System.Net.Http.Json;

namespace BioscoopCasus.Web.Services;

public class PaymentService
{
    private readonly HttpClient _httpClient;
    
    public PaymentService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> ValidatePinAsync(string pinCode)
    {
        var response = await _httpClient.PostAsJsonAsync("api/payment/pin", pinCode);
        return response.IsSuccessStatusCode;
    }
}