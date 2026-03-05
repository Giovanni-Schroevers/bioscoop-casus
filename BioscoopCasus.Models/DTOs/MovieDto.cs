using System;
using System.Collections.Generic;

namespace BioscoopCasus.Models.DTOs;

public record MovieResponseDto(
    int Id,
    string Title,
    string Description,
    string PosterUrl,
    string Actors,
    string TrailerUrl,
    string Genres,
    int AgeRating,
    int DurationMinutes,
    DateTime ReleaseDate,
    List<ShowtimeResponseDto>? Showtimes = null
);

public record MovieCreateDto(
    string Title,
    string Description,
    string PosterUrl,
    string Actors,
    string TrailerUrl,
    string Genres,
    int AgeRating,
    int DurationMinutes,
    DateTime ReleaseDate
);

public record MovieUpdateDto(
    string Title,
    string Description,
    string PosterUrl,
    string Actors,
    string TrailerUrl,
    string Genres,
    int AgeRating,
    int DurationMinutes,
    DateTime ReleaseDate
);
