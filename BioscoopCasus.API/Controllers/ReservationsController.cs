using BioscoopCasus.API.Data;
using BioscoopCasus.Models.DTOs;
using BioscoopCasus.Models.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationsController(BioscoopDbContext context, QrCodeHelper qrCodeHelper) : ControllerBase
{
    // GET /api/reservations
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReservationResponseDto>>> Get()
    {
        var reservations = await context.Reservations
            .Include(r => r.Showtime)
                .ThenInclude(s => s.Movie)
            .Include(r => r.Showtime)
                .ThenInclude(s => s.Room)
            .Include(r => r.ShowtimeSeats)
                .ThenInclude(ss => ss.Seat)
            .ToListAsync();

        var response = reservations.Select(r =>
        {
            var seats = r.ShowtimeSeats
                .Select(ss => new SeatDto(
                    ss.SeatId,
                    ss.Seat.Row,
                    ss.Seat.SeatNumber))
                .ToList();

            return new ReservationResponseDto(
                r.Id,
                new ShowtimeResponseDto(
                    r.Showtime.Id,
                    r.Showtime.MovieId,
                    r.Showtime.RoomId,
                    r.Showtime.StartTime,
                    0
                ),
                seats,
                r.Showtime.Movie.Title,
                r.Showtime.Room.Name
            );
        }).ToList();

        return Ok(response);
    }

    // GET /api/reservations/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReservationResponseDto>> Get(int id)
    {
        var reservation = await context.Reservations
            .Include(r => r.Showtime)
                .ThenInclude(s => s.Movie)
            .Include(r => r.Showtime)
                .ThenInclude(s => s.Room)
            .Include(r => r.ShowtimeSeats)
                .ThenInclude(ss => ss.Seat)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation is null)
            return NotFound();

        var seats = reservation.ShowtimeSeats
            .Select(ss => new SeatDto(
                ss.SeatId,
                ss.Seat.Row,
                ss.Seat.SeatNumber))
            .ToList();

        var response = new ReservationResponseDto(
            reservation.Id,
            new ShowtimeResponseDto(
                reservation.Showtime.Id,
                reservation.Showtime.MovieId,
                reservation.Showtime.RoomId,
                reservation.Showtime.StartTime,
                0
            ),
            seats,
            reservation.Showtime.Movie.Title,
            reservation.Showtime.Room.Name
        );

        return Ok(response);
    }

    // POST /api/reservations/validate-qr
    [HttpPost("validate-qr")]
    public async Task<ActionResult<QrCodeValidationResponseDto>> ValidateQrCode([FromBody] QrCodeValidationRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.QrCode))
            return BadRequest(new QrCodeValidationResponseDto(false, null, "QR code is required"));
        
        var qrCodeData = qrCodeHelper.ParseQrCode(request.QrCode);

        if (qrCodeData is null)
            return Ok(new QrCodeValidationResponseDto(false, null, "Invalid QR code format"));

        if (!qrCodeHelper.VerifyChecksum(request.QrCode))
            return Ok(new QrCodeValidationResponseDto(false, null, "Invalid QR code checksum"));

        var reservation = await context.Reservations
            .Include(r => r.Showtime)
            .FirstOrDefaultAsync(r => qrCodeData.ReservationId != null && r.Id == qrCodeData.ReservationId.Value);

        if (reservation is null)
            return Ok(new QrCodeValidationResponseDto(false, null, "Reservation not found"));

        if (reservation.Showtime.StartTime <= DateTime.UtcNow)
            return Ok(new QrCodeValidationResponseDto(false, null, "This ticket can no longer be viewed because the movie has already started."));

        return Ok(new QrCodeValidationResponseDto(true, reservation.Id, null));
    }
}