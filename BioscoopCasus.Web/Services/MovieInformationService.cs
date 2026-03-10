using System.Net.Http.Json;
using BioscoopCasus.Models.DTOs;

namespace BioscoopCasus.Web.Services;

public class MovieInformationService(HttpClient httpClient)
{
    public async Task<MovieResponseDto?> GetMovieInformationAsync(string movieId)
    {
        var result = await httpClient.GetFromJsonAsync<MovieResponseDto>(
            $"api/movies/{movieId}"); 
        return result;
    }
}