using BioscoopCasus.Models.DTOs;
using System.Net.Http.Json;

namespace BioscoopCasus.Web.Services;

public class OccupancyAnalyticsService(HttpClient http)
{
    private readonly HttpClient _http = http;

    public async Task<OccupancyAnalyticsResponseDto?> GetOccupancyAsync(AnalyticsPeriodRequestDto filter)
    {
        var url = $"api/analytics/occupancy?startDate={filter.StartDate:yyyy-MM-dd}&endDate={filter.EndDate:yyyy-MM-dd}&scope={filter.Scope}";
        return await _http.GetFromJsonAsync<OccupancyAnalyticsResponseDto>(url);
    }
}
