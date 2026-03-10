namespace BioscoopCasus.Models.DTOs;

public record FilmsOverviewDto(
    int ShowtimeId,
    int MovieId,
    string Title,
    string Genres,
    int DurationMinutes,
    DateTime StartTime,
    string PosterUrl
);
