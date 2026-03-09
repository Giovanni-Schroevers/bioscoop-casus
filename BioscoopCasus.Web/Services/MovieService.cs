using BioscoopCasus.Models.DTOs;
using System.Net.Http.Json;

namespace BioscoopCasus.Web.Services;

public class MovieService(HttpClient http)
{
    private readonly HttpClient _http = http;

    public async Task<IEnumerable<MovieResponseDto>> GetMoviesAsync()
    {
        var result = await _http.GetFromJsonAsync<IEnumerable<MovieResponseDto>>("api/movies");
        return result ?? Enumerable.Empty<MovieResponseDto>();
    }

    public async Task<MovieResponseDto?> GetMovieAsync(int id)
    {
        return await _http.GetFromJsonAsync<MovieResponseDto>($"api/movies/{id}");
    }

    public async Task<bool> CreateMovieAsync(MovieCreateDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/movies", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateMovieAsync(int id, MovieUpdateDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/movies/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteMovieAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/movies/{id}");
        return response.IsSuccessStatusCode;
    }
}
