using BioscoopCasus.API.Data;
using Microsoft.EntityFrameworkCore;

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
        var reservations = await _dbContext.Reservations
            .Include(r => r.Showtime)
                .ThenInclude(s => s.Movie)
            .Include(r => r.Showtime)
                .ThenInclude(s => s.Room)
            .Where(r => r.Showtime.StartTime >= startDate && r.Showtime.StartTime <= endDate)
            .ToListAsync();

        if (reservations.Count == 0)
        {
            return new RevenueAnalyticsSummary();
        }

        var totalRevenue = reservations.Sum(r => r.TotalPrice);

        var topMovie = reservations
            .GroupBy(r => r.Showtime.Movie.Title)
            .OrderByDescending(g => g.Sum(r => r.TotalPrice))
            .FirstOrDefault();

        var days = reservations
            .GroupBy(r => r.Showtime.StartTime.Date)
            .Count();

        var items = scope switch
        {
            "movie" => reservations
                .GroupBy(r => r.Showtime.Movie.Title)
                .Select(g => new RevenueItem
                {
                    Label = g.Key,
                    Revenue = g.Sum(r => r.TotalPrice)
                })
                .OrderByDescending(i => i.Revenue)
                .ToList(),

            _ => reservations
                .GroupBy(r => r.Showtime.StartTime.Date)
                .OrderBy(g => g.Key)
                .Select(g => new RevenueItem
                {
                    Label = g.Key.ToString("dd MMM yyyy"),
                    Revenue = g.Sum(r => r.TotalPrice)
                })
                .ToList()
        };

        var topMovies = reservations
            .GroupBy(r => r.Showtime.Movie.Title)
            .Select(g => new RevenueItem
            {
                Label = g.Key,
                Revenue = g.Sum(r => r.TotalPrice)
            })
            .OrderByDescending(i => i.Revenue)
            .Take(5)
            .ToList();

        var revenuePerRoom = reservations
            .GroupBy(r => $"Zaal {r.Showtime.Room.Number}")
            .Select(g => new RevenueItem
            {
                Label = g.Key,
                Revenue = g.Sum(r => r.TotalPrice)
            })
            .OrderBy(i => i.Label)
            .ToList();

        return new RevenueAnalyticsSummary
        {
            TotalRevenue = totalRevenue,
            TopMovieTitle = topMovie?.Key,
            AverageRevenuePerDay = days > 0 ? totalRevenue / days : 0m,
            Items = items,
            TopMovies = topMovies,
            RevenuePerRoom = revenuePerRoom
        };
    }
}

public class RevenueAnalyticsSummary
{
    public decimal TotalRevenue { get; set; }
    public string? TopMovieTitle { get; set; }
    public decimal AverageRevenuePerDay { get; set; }
    public List<RevenueItem> Items { get; set; } = new();
    public List<RevenueItem> TopMovies { get; set; } = new();
    public List<RevenueItem> RevenuePerRoom { get; set; } = new();
}

public class RevenueItem
{
    public string Label { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}
