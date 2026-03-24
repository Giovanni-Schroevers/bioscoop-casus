using System.Net.Http.Json;
using BioscoopCasus.Models.DTOs;

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

    public async Task<bool> SendReservationEmail(string email, int reservationId)
    {
        TicketMailSendDto dto = new(email, reservationId);

        var response = await _httpClient.PostAsJsonAsync("api/mailing/ticket", dto);
        return response.IsSuccessStatusCode;
    }
}