using BioscoopCasus.Models.DTOs;
using System.Net.Http.Json;

namespace BioscoopCasus.Web.Services;

public class MovieService
{
    private readonly HttpClient _http;

    public MovieService(HttpClient http)
    {
        _http = http;
    }

    public async Task<IEnumerable<MovieResponseDto>> GetMoviesAsync()
    {
        var result = await _http.GetFromJsonAsync<IEnumerable<MovieResponseDto>>("api/movies");
        return result ?? Enumerable.Empty<MovieResponseDto>();
    }

    public async Task<MovieResponseDto?> GetMovieAsync(int id)
    {
        return await _http.GetFromJsonAsync<MovieResponseDto>($"api/movies/{id}");
    }
}
