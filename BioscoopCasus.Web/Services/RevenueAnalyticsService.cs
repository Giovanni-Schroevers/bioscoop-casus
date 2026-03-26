using BioscoopCasus.Models.DTOs;
using System.Net.Http.Json;

namespace BioscoopCasus.Web.Services;

public class RevenueAnalyticsService(HttpClient http)
{
    private readonly HttpClient _http = http;

    public async Task<RevenueAnalyticsResponseDto?> GetRevenueAsync(AnalyticsPeriodRequestDto filter)
    {
        var url = $"api/analytics/revenue?startDate={filter.StartDate:yyyy-MM-dd}&endDate={filter.EndDate:yyyy-MM-dd}&scope={filter.Scope}";
        return await _http.GetFromJsonAsync<RevenueAnalyticsResponseDto>(url);
    }
}
