using BioscoopCasus.API.Data;

namespace BioscoopCasus.API.Services;

public class OccupancyAnalyticsService
{
    private readonly BioscoopDbContext _dbContext;

    public OccupancyAnalyticsService(BioscoopDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OccupancyAnalyticsSummary> GetOccupancyAsync(DateTime startDate, DateTime endDate, string scope)
    {
        // Placeholder implementation - kun je veranderen
        return await Task.FromResult(new OccupancyAnalyticsSummary
        {
            AverageOccupancyPercentage = 0.0,
            TopRoomName = null,
            TopMovieTitle = null,
            Items = new List<OccupancyItem>()
        });
    }
}

public class OccupancyAnalyticsSummary
{
    public double AverageOccupancyPercentage { get; set; }
    public string? TopRoomName { get; set; }
    public string? TopMovieTitle { get; set; }
    public List<OccupancyItem> Items { get; set; } = new();
}

public class OccupancyItem
{
    public string Label { get; set; } = string.Empty;
    public double OccupancyPercentage { get; set; }
}
