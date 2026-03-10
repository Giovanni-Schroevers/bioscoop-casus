namespace BioscoopCasus.Models.DTOs;

public record QrCodeValidationRequestDto(string QrCode);

public record QrCodeValidationResponseDto(
    bool IsValid,
    int? ReservationId,
    string? ErrorMessage);