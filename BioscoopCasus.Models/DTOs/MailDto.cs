namespace BioscoopCasus.Models.DTOs;

public record TicketMailSendDto(
    string Email,
    int ReservationId = 0);

public record TicketMailResponseDto(
    bool Success);