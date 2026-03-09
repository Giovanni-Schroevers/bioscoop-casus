using System.Security.Cryptography;
using System.Text;
using BioscoopCasus.Models.DTOs;
using QRCoder;

namespace BioscoopCasus.Models.Helpers;

public class QrCodeHelper
{
    public string GetQrCodeString(ReservationResponseDto reservation)
    {
        var reservationId = reservation.Id;
        var showtimeId = reservation.Showtime.Id;
        var roomId = reservation.Showtime.RoomId;
        var seatIds = string.Join(",", reservation.Seats.Select(s => s.SeatId));
        var checksum = CalculateChecksum(reservationId, showtimeId, roomId, reservation.Seats);

        return $"{reservationId}-{showtimeId}-{roomId}-{seatIds}-{checksum}";
    }

    public string GetQrCodeImage(string qrCodeText)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(qrCodeText, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(20);
        return Convert.ToBase64String(qrCodeBytes);
    }

    public bool VerifyChecksum(string qrCodeString)
    {
        if (string.IsNullOrWhiteSpace(qrCodeString))
            return false;

        var parts = qrCodeString.Split('-');
        if (parts.Length < 5)
            return false;

        if (!int.TryParse(parts[0], out var reservationId) ||
            !int.TryParse(parts[1], out var showtimeId) ||
            !int.TryParse(parts[2], out var roomId))
            return false;

        var seatIdsString = parts[3];
        var providedChecksum = parts[4];

        var seatIds = seatIdsString.Split(',')
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        if (seatIds.Count is 0)
            return false;

        var seats = seatIds.Select(id => new SeatDto(id, 0, 0)).ToList();
        var calculatedChecksum = CalculateChecksum(reservationId, showtimeId, roomId, seats);

        return string.Equals(calculatedChecksum, providedChecksum, StringComparison.OrdinalIgnoreCase);
    }

    public int? ExtractReservationId(string qrCodeString)
    {
        if (string.IsNullOrWhiteSpace(qrCodeString))
            return null;

        var parts = qrCodeString.Split('-');
        if (parts.Length < 1)
            return null;

        if (int.TryParse(parts[0], out var reservationId))
            return reservationId;

        return null;
    }

    private string CalculateChecksum(int reservationId, int showtimeId, int roomId, List<SeatDto> seats)
    {
        var seatIdsSum = seats.Sum(s => s.SeatId);
        var combined = $"{reservationId}-{showtimeId}-{roomId}-{seatIdsSum}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        var hexString = Convert.ToHexString(hash);
        return hexString[..4];
    }
}
