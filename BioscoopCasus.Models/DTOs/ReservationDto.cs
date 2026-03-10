namespace BioscoopCasus.Models.DTOs;

public record ReservationResponseDto(
    int Id,
    ShowtimeResponseDto Showtime,
    List<SeatDto> Seats,
    string MovieTitle,
    string RoomName);