namespace BioscoopCasus.Models.DTOs;

public record RowResponseDto(
    int Id,
    int RowNumber,
    int SeatCount
);

public record RowCreateDto(
    int RowNumber,
    int SeatCount
);

public record RoomResponseDto(
    int Id,
    int Number,
    string Name,
    bool Has3D,
    bool IsWheelchairAccessible,
    List<RowResponseDto> Rows
);

public record RoomCreateDto(
    int Number,
    string Name,
    bool Has3D,
    bool IsWheelchairAccessible,
    List<RowCreateDto> Rows
);

public record RoomUpdateDto(
    int Number,
    string Name,
    bool Has3D,
    bool IsWheelchairAccessible,
    List<RowCreateDto> Rows
);
