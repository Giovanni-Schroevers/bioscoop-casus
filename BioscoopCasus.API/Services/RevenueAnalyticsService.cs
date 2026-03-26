using BioscoopCasus.API.Data;

namespace BioscoopCasus.API.Services;

public class RevenueAnalyticsService
{
    private readonly BioscoopDbContext _dbContext;

    public RevenueAnalyticsService(BioscoopDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RevenueAnalyticsSummary> GetRevenueAsync(DateTime startDate, DateTime endDate, string scope)
    {
        // Placeholder implementation - kun je veranderen
        return await Task.FromResult(new RevenueAnalyticsSummary
        {
            TotalRevenue = 0m,
            TopMovieTitle = null,
            AverageRevenuePerDay = 0m,
            Items = new List<RevenueItem>()
        });
    }
}

public class RevenueAnalyticsSummary
{
    public decimal TotalRevenue { get; set; }
    public string? TopMovieTitle { get; set; }
    public decimal AverageRevenuePerDay { get; set; }
    public List<RevenueItem> Items { get; set; } = new();
}

public class RevenueItem
{
    public string Label { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}
