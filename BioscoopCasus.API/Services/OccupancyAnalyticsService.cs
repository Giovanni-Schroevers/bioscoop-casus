using BioscoopCasus.API.Data;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Services;

public class OccupancyAnalyticsService
{
    private readonly BioscoopDbContext _dbContext;

    public OccupancyAnalyticsService(BioscoopDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OccupancyAnalyticsSummary> GetOccupancyAsync(DateTime startDate, DateTime endDate, string scope, List<int>? roomIds = null)
    {
        if (startDate > endDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        var selectedRoomId = roomIds?.FirstOrDefault();

        var allSeatSnapshots = await _dbContext.ShowtimeSeats
            .AsNoTracking()
            .Where(ss => ss.Showtime.StartTime >= startDate && ss.Showtime.StartTime <= endDate)
            .Select(ss => new SeatSnapshot
            {
                Date = ss.Showtime.StartTime.Date,
                RoomId = ss.Showtime.RoomId,
                RoomNumber = ss.Showtime.Room.Number,
                SeatRow = ss.Seat.Row,
                SeatNumber = ss.Seat.SeatNumber,
                IsSold = ss.ReservationId != null
            })
            .ToListAsync();

        var filteredSeatSnapshots = roomIds is { Count: > 0 }
            ? allSeatSnapshots.Where(s => roomIds.Contains(s.RoomId)).ToList()
            : allSeatSnapshots;

        var soldSeats = filteredSeatSnapshots.Count(s => s.IsSold);
        var totalSeats = filteredSeatSnapshots.Count;
        var unsoldSeats = totalSeats - soldSeats;

        var items = BuildTimeOrRoomItems(filteredSeatSnapshots, scope);
        var occupancyPerRoom = BuildRoomItems(allSeatSnapshots);
        var seatOccupancy = BuildSeatItems(filteredSeatSnapshots, selectedRoomId);

        var todayStart = DateTime.Today;
        var todayEnd = DateTime.Today.AddDays(1).AddTicks(-1);
        var weekStart = DateTime.Today.AddDays(-6);
        var weekEnd = todayEnd;
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var monthEnd = todayEnd;

        var bestRoomToday = await GetBestRoomAsync(todayStart, todayEnd);
        var bestRoomWeek = await GetBestRoomAsync(weekStart, weekEnd);
        var bestRoomMonth = await GetBestRoomAsync(monthStart, monthEnd);

        return new OccupancyAnalyticsSummary
        {
            AverageOccupancyPercentage = totalSeats > 0 ? soldSeats * 100.0 / totalSeats : 0,
            TotalSoldSeats = soldSeats,
            TotalUnsoldSeats = unsoldSeats,
            BestRoomTodayName = bestRoomToday?.Label,
            BestRoomTodayPercentage = bestRoomToday?.OccupancyPercentage ?? 0,
            BestRoomWeekName = bestRoomWeek?.Label,
            BestRoomWeekPercentage = bestRoomWeek?.OccupancyPercentage ?? 0,
            BestRoomMonthName = bestRoomMonth?.Label,
            BestRoomMonthPercentage = bestRoomMonth?.OccupancyPercentage ?? 0,
            SelectedRoomName = selectedRoomId.HasValue
                ? allSeatSnapshots.Where(s => s.RoomId == selectedRoomId.Value).Select(s => $"Zaal {s.RoomNumber}").FirstOrDefault()
                : null,
            BestSeatLabel = seatOccupancy.FirstOrDefault()?.Label,
            BestSeatPercentage = seatOccupancy.FirstOrDefault()?.OccupancyPercentage ?? 0,
            Items = items,
            OccupancyPerRoom = occupancyPerRoom,
            SeatOccupancy = seatOccupancy
        };
    }

    private static List<OccupancyItem> BuildTimeOrRoomItems(List<SeatSnapshot> seatSnapshots, string scope)
    {
        if (scope == "room")
        {
            return seatSnapshots
                .GroupBy(s => s.RoomNumber)
                .OrderBy(g => g.Key)
                .Select(g => CreateItem($"Zaal {g.Key}", g))
                .ToList();
        }

        return seatSnapshots
            .GroupBy(s => s.Date)
            .OrderBy(g => g.Key)
            .Select(g => CreateItem(g.Key.ToString("dd MMM"), g))
            .ToList();
    }

    private static List<OccupancyRoomItem> BuildRoomItems(List<SeatSnapshot> seatSnapshots)
    {
        return seatSnapshots
            .GroupBy(s => s.RoomNumber)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var sold = g.Count(s => s.IsSold);
                var total = g.Count();
                return new OccupancyRoomItem
                {
                    Label = $"Zaal {g.Key}",
                    SoldSeats = sold,
                    UnsoldSeats = total - sold,
                    OccupancyPercentage = total > 0 ? sold * 100.0 / total : 0
                };
            })
            .ToList();
    }

    private static List<OccupancySeatItem> BuildSeatItems(List<SeatSnapshot> seatSnapshots, int? selectedRoomId)
    {
        if (!selectedRoomId.HasValue)
        {
            return new List<OccupancySeatItem>();
        }

        return seatSnapshots
            .Where(s => s.RoomId == selectedRoomId.Value)
            .GroupBy(s => new { s.SeatRow, s.SeatNumber })
            .Select(g =>
            {
                var sold = g.Count(s => s.IsSold);
                var total = g.Count();
                return new OccupancySeatItem
                {
                    Label = $"Rij {g.Key.SeatRow} - Stoel {g.Key.SeatNumber}",
                    SoldSeats = sold,
                    UnsoldSeats = total - sold,
                    OccupancyPercentage = total > 0 ? sold * 100.0 / total : 0
                };
            })
            .OrderByDescending(s => s.OccupancyPercentage)
            .ThenByDescending(s => s.SoldSeats)
            .Take(10)
            .ToList();
    }

    private async Task<BestRoomResult?> GetBestRoomAsync(DateTime startDate, DateTime endDate)
    {
        var query = _dbContext.ShowtimeSeats
            .AsNoTracking()
            .Where(ss => ss.Showtime.StartTime >= startDate && ss.Showtime.StartTime <= endDate);

        return await query
            .GroupBy(ss => ss.Showtime.Room.Number)
            .Select(g => new BestRoomResult
            {
                Label = $"Zaal {g.Key}",
                OccupancyPercentage = g.Count(ss => ss.ReservationId != null) * 100.0 / g.Count()
            })
            .OrderByDescending(r => r.OccupancyPercentage)
            .FirstOrDefaultAsync();
    }

    private static OccupancyItem CreateItem(string label, IEnumerable<SeatSnapshot> seats)
    {
        var sold = seats.Count(s => s.IsSold);
        var total = seats.Count();

        return new OccupancyItem
        {
            Label = label,
            SoldSeats = sold,
            UnsoldSeats = total - sold,
            OccupancyPercentage = total > 0 ? sold * 100.0 / total : 0
        };
    }

    private sealed class SeatSnapshot
    {
        public DateTime Date { get; init; }
        public int RoomId { get; init; }
        public int RoomNumber { get; init; }
        public int SeatRow { get; init; }
        public int SeatNumber { get; init; }
        public bool IsSold { get; init; }
    }
}

public class OccupancyAnalyticsSummary
{
    public double AverageOccupancyPercentage { get; set; }
    public int TotalSoldSeats { get; set; }
    public int TotalUnsoldSeats { get; set; }
    public string? BestRoomTodayName { get; set; }
    public double BestRoomTodayPercentage { get; set; }
    public string? BestRoomWeekName { get; set; }
    public double BestRoomWeekPercentage { get; set; }
    public string? BestRoomMonthName { get; set; }
    public double BestRoomMonthPercentage { get; set; }
    public string? SelectedRoomName { get; set; }
    public string? BestSeatLabel { get; set; }
    public double BestSeatPercentage { get; set; }
    public List<OccupancyItem> Items { get; set; } = new();
    public List<OccupancyRoomItem> OccupancyPerRoom { get; set; } = new();
    public List<OccupancySeatItem> SeatOccupancy { get; set; } = new();
}

public class OccupancyItem
{
    public string Label { get; set; } = string.Empty;
    public int SoldSeats { get; set; }
    public int UnsoldSeats { get; set; }
    public double OccupancyPercentage { get; set; }
}

public class OccupancyRoomItem
{
    public string Label { get; set; } = string.Empty;
    public int SoldSeats { get; set; }
    public int UnsoldSeats { get; set; }
    public double OccupancyPercentage { get; set; }
}

public class OccupancySeatItem
{
    public string Label { get; set; } = string.Empty;
    public int SoldSeats { get; set; }
    public int UnsoldSeats { get; set; }
    public double OccupancyPercentage { get; set; }
}

public class BestRoomResult
{
    public string Label { get; set; } = string.Empty;
    public double OccupancyPercentage { get; set; }
}
