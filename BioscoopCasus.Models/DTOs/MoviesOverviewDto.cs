namespace BioscoopCasus.Models.DTOs;

public record MoviesOverviewShowtimeDto(int Id, DateTime StartTime);

public record MoviesOverviewDto(
    int Id,
    string Title,
    string Genres,
    int DurationMinutes,
    List<MoviesOverviewShowtimeDto> Showtimes
);