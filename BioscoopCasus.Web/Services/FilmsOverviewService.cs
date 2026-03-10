using System.Net.Http.Json;
using BioscoopCasus.Models.DTOs;

namespace BioscoopCasus.Web.Services;

public class FilmsOverviewService
{
    private readonly HttpClient _httpClient;

    public FilmsOverviewService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<FilmsOverviewDto>> GetFilmsAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<FilmsOverviewDto>>("api/films-overview");
        return result ?? [];
    }
}
