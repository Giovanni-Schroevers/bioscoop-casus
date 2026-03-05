using System;
using System.Collections.Generic;

namespace BioscoopCasus.Models.DTOs;

public record ShowtimeResponseDto(
    int Id,
    int MovieId,
    int RoomId,
    DateTime StartTime,
    double TicketPrice
);

public record ShowtimeCreateDto(
    int MovieId,
    int RoomId,
    DateTime StartTime
);

public record ShowtimeBulkCreateDto(
    List<ShowtimeCreateDto> Showtimes
);
