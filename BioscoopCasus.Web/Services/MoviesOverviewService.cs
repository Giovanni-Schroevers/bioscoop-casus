using System.Net.Http.Json;
using BioscoopCasus.Models.DTOs;
using BioscoopCasus.Models.DTOs;

namespace BioscoopCasus.Web.Services;

public class MoviesOverviewService
{
    private readonly HttpClient _httpClient;
    
    public MoviesOverviewService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<MoviesOverviewDto>> GetMoviesAsync(DateTime date)
    {
        var result = await _httpClient.GetFromJsonAsync<List<MoviesOverviewDto>>(
            $"api/movies-overview?date={date:yyyy-MM-dd}");
            return result ?? [];
    }
}