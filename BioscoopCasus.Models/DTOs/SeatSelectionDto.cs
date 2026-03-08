using System.Collections.Generic;

namespace BioscoopCasus.Models.DTOs;
public record SeatInfoDto(
    int Id,
    int Row,
    int SeatNumber,
    bool IsAvailable,
    int QualityScore
);

public record SeatSelectionRequestDto(
    int GroupSize
);
public record SeatSelectionResponseDto(
    List<SeatInfoDto> SuggestedSeats,
    string Message,
    bool IsGroupedTogether,
    List<List<SeatInfoDto>> GroupedSeats,
    double TotalScore
);

public record ReservationConfirmRequestDto(
    List<int> SeatIds
);

public record ReservationConfirmResponseDto(
    int ReservationId,
    List<SeatInfoDto> ReservedSeats,
    string Message
);
