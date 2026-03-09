using System.Net.Http.Json;
using BioscoopCasus.Models.DTOs;

namespace BioscoopCasus.Web.Services;

public class ReservationService(HttpClient httpClient)
{
    public async Task<ReservationResponseDto?> GetReservationAsync(int id)
    {
        var response = await httpClient.GetAsync($"api/reservations/{id}");

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<ReservationResponseDto>();
    }

    public async Task<QrCodeValidationResponseDto?> ValidateQrCodeAsync(string qrCode)
    {
        var request = new QrCodeValidationRequestDto(qrCode);
        var response = await httpClient.PostAsJsonAsync("api/reservations/validate-qr", request);

        if (!response.IsSuccessStatusCode)
            return new QrCodeValidationResponseDto(false, null, "Er is een fout opgetreden bij het valideren van de QR code.");

        return await response.Content.ReadFromJsonAsync<QrCodeValidationResponseDto>();
    }
}
