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

        var qrCodeContent = $"RE:{reservationId}|SH:{showtimeId}|RO:{roomId}|SE:{seatIds}|CH:{checksum}";
        var bytes = Encoding.UTF8.GetBytes(qrCodeContent);
        return Convert.ToBase64String(bytes);
    }

    public string GetQrCodeImage(string qrCodeText)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(qrCodeText, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(20);
        return Convert.ToBase64String(qrCodeBytes);
    }

    public QrCodeData? ParseQrCode(string qrCodeString)
    {
        if (string.IsNullOrWhiteSpace(qrCodeString))
            return null;

        string decodedContent;
        try
        {
            var bytes = Convert.FromBase64String(qrCodeString);
            decodedContent = Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }

        var parts = decodedContent.Split('|');
        if (parts.Length != 5)
            return null;

        var data = new QrCodeData();

        foreach (var part in parts)
        {
            if (part.StartsWith("RE:", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(part[3..], out var reservationId))
                    return null;
                data.ReservationId = reservationId;
            }
            else if (part.StartsWith("SH:", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(part[3..], out var showtimeId))
                    return null;
                data.ShowtimeId = showtimeId;
            }
            else if (part.StartsWith("RO:", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(part[3..], out var roomId))
                    return null;
                data.RoomId = roomId;
            }
            else if (part.StartsWith("SE:", StringComparison.OrdinalIgnoreCase))
            {
                var seatIdsString = part[3..];
                if (string.IsNullOrWhiteSpace(seatIdsString))
                    return null;

                var seatIds = seatIdsString.Split(',')
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList();

                if (seatIds.Count is 0)
                    return null;

                data.SeatIds = seatIds;
            }
            else if (part.StartsWith("CH:", StringComparison.OrdinalIgnoreCase))
                data.Checksum = part[3..];
        }

        if (data.ReservationId is null || data.ShowtimeId is null || data.RoomId is null || 
            data.SeatIds is null || data.Checksum is null)
            return null;

        return data;
    }
    
    private string CalculateChecksum(int reservationId, int showtimeId, int roomId, List<SeatDto> seats)
    {
        var seatIdsSum = seats.Sum(s => s.SeatId);
        var combined = $"{reservationId}-{showtimeId}-{roomId}-{seatIdsSum}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        var hexString = Convert.ToHexString(hash);
        return hexString[..4];
    }

    public bool VerifyChecksum(string qrCodeString)
    {
        var data = ParseQrCode(qrCodeString);
        if (data?.SeatIds is null || data.ReservationId is null ||
            data.ShowtimeId is null || data.RoomId is null)
            return false;

        var seats = data.SeatIds.Select(id => new SeatDto(id, 0, 0)).ToList();
        var calculatedChecksum = CalculateChecksum(data.ReservationId.Value, data.ShowtimeId.Value, data.RoomId.Value, seats);

        return string.Equals(calculatedChecksum, data.Checksum, StringComparison.OrdinalIgnoreCase);
    }

    public class QrCodeData
    {
        public int? ReservationId { get; set; }
        public int? ShowtimeId { get; set; }
        public int? RoomId { get; set; }
        public List<int>? SeatIds { get; set; }
        public string? Checksum { get; set; }
    }
}
