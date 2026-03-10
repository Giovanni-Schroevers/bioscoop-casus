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
}
