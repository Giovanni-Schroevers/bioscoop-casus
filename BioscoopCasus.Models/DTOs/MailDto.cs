namespace BioscoopCasus.Models.DTOs;

public record TicketMailSendDto(
    string Email,
    string TicketCode);

public record TicketMailResponseDto(
    bool Success);