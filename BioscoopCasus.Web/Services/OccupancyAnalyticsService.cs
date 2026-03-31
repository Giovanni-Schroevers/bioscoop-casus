using BioscoopCasus.Models.DTOs;
using System.Net.Http.Json;

namespace BioscoopCasus.Web.Services;

public class OccupancyAnalyticsService(HttpClient http)
{
    private readonly HttpClient _http = http;

    public async Task<OccupancyAnalyticsResponseDto?> GetOccupancyAsync(AnalyticsPeriodRequestDto filter)
    {
        var url = $"api/analytics/occupancy?startDate={filter.StartDate:yyyy-MM-dd}&endDate={filter.EndDate:yyyy-MM-dd}&scope={filter.Scope}";

        if (filter.RoomIds is { Count: > 0 })
            url += string.Concat(filter.RoomIds.Select(id => $"&roomIds={id}"));

        return await _http.GetFromJsonAsync<OccupancyAnalyticsResponseDto>(url);
    }
}
