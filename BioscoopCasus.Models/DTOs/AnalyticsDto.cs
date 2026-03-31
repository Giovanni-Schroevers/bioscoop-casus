namespace BioscoopCasus.Models.DTOs;

public record AnalyticsPeriodRequestDto(
    DateTime StartDate,
    DateTime EndDate,
    string Scope,
    List<int>? RoomIds = null
);

public record OccupancyAnalyticsResponseDto(
    double AverageOccupancyPercentage,
    int TotalSoldSeats,
    int TotalUnsoldSeats,
    string? BestRoomTodayName,
    double BestRoomTodayPercentage,
    string? BestRoomWeekName,
    double BestRoomWeekPercentage,
    string? BestRoomMonthName,
    double BestRoomMonthPercentage,
    string? SelectedRoomName,
    string? BestSeatLabel,
    double BestSeatPercentage,
    List<OccupancyAnalyticsItemDto>? Items = null,
    List<OccupancyRoomAnalyticsItemDto>? OccupancyPerRoom = null,
    List<OccupancySeatAnalyticsItemDto>? SeatOccupancy = null
);

public record OccupancyAnalyticsItemDto(
    string Label,
    int SoldSeats,
    int UnsoldSeats,
    double OccupancyPercentage
);

public record OccupancyRoomAnalyticsItemDto(
    string Label,
    int SoldSeats,
    int UnsoldSeats,
    double OccupancyPercentage
);

public record OccupancySeatAnalyticsItemDto(
    string Label,
    int SoldSeats,
    int UnsoldSeats,
    double OccupancyPercentage
);

public record RevenueAnalyticsResponseDto(
    decimal TotalRevenue,
    string? TopMovieTitle,
    decimal AverageRevenuePerDay,
    List<RevenueAnalyticsItemDto>? Items = null,
    List<RevenueAnalyticsItemDto>? TopMovies = null,
    List<RevenueAnalyticsItemDto>? RevenuePerRoom = null,
    decimal TicketRevenue = 0m,
    decimal PopcornRevenue = 0m
);

public record RevenueAnalyticsItemDto(
    string Label,
    decimal Revenue
);
