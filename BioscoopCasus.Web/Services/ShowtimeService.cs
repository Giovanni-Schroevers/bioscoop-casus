using BioscoopCasus.Models.DTOs;
using System.Net.Http.Json;

namespace BioscoopCasus.Web.Services;

public class ShowtimeService(HttpClient http)
{
    private readonly HttpClient _http = http;

    public async Task<IEnumerable<ShowtimeResponseDto>> GetShowtimesAsync(DateTime? date = null, int? movieId = null)
    {
        var url = "api/showtimes";
        var queryParams = new List<string>();

        if (date.HasValue)
        {
            queryParams.Add($"date={date.Value:yyyy-MM-dd}");
        }

        if (movieId.HasValue)
        {
            queryParams.Add($"movieId={movieId.Value}");
        }

        if (queryParams.Any())
        {
            url += "?" + string.Join("&", queryParams);
        }

        var result = await _http.GetFromJsonAsync<IEnumerable<ShowtimeResponseDto>>(url);
        return result ?? Enumerable.Empty<ShowtimeResponseDto>();
    }

    public async Task<bool> CreateShowtimeAsync(ShowtimeCreateDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/showtimes", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateShowtimeAsync(int id, ShowtimeCreateDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/showtimes/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteShowtimeAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/showtimes/{id}");
        return response.IsSuccessStatusCode;
    }
}
