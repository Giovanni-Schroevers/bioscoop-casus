namespace BioscoopCasus.Models.DTOs;

public record AnalyticsPeriodRequestDto(
    DateTime StartDate,
    DateTime EndDate,
    string Scope
);

public record OccupancyAnalyticsResponseDto(
    double AverageOccupancyPercentage,
    string? TopRoomName,
    string? TopMovieTitle,
    List<OccupancyAnalyticsItemDto>? Items = null
);

public record OccupancyAnalyticsItemDto(
    string Label,
    double OccupancyPercentage
);

public record RevenueAnalyticsResponseDto(
    decimal TotalRevenue,
    string? TopMovieTitle,
    decimal AverageRevenuePerDay,
    List<RevenueAnalyticsItemDto>? Items = null
);

public record RevenueAnalyticsItemDto(
    string Label,
    decimal Revenue
);


