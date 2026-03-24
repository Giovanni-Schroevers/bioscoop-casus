using BioscoopCasus.API.Data;
using BioscoopCasus.API.Entities;
using BioscoopCasus.API.Services;
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

        var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

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
    
    // POST /api/reservations/{showtimeId:int}/reserve
    [HttpPost("{showtimeId:int}/reserve")]
    public async Task<ActionResult<ReservationConfirmResponseDto>> ConfirmReservation(
        int showtimeId,
        [FromBody] ReservationConfirmRequestDto request)
    {
        switch (request.SeatIds.Count)
        {
            case 0:
                return BadRequest("No seats selected");
            case > 20:
                return BadRequest("Maximum 20 seats per reservation");
        }

        var showtime = await context.Showtimes
            .Include(s => s.Room)
            .ThenInclude(r => r.Rows)
            .FirstOrDefaultAsync(s => s.Id == showtimeId);

        if (showtime == null)
            return NotFound("Showtime not found");

        var seats = await context.Seats
            .Where(s => request.SeatIds.Contains(s.Id) && s.RoomId == showtime.RoomId)
            .ToListAsync();

        if (seats.Count != request.SeatIds.Count)
            return BadRequest("One or more selected seats do not exist or belong to a different room");

        var existingReservations = await context.ShowtimeSeats
            .Where(ss => ss.ShowtimeId == showtimeId && request.SeatIds.Contains(ss.SeatId) && ss.ReservationId.HasValue)
            .ToListAsync();

        if (existingReservations.Any())
            return BadRequest("One or more selected seats are already reserved");

        var reservation = new Reservation
        {
            ShowtimeId = showtimeId
        };

        if (request.PopcornOrders is { Count: > 0 })
        {
            foreach (var popcorn in request.PopcornOrders)
            {
                reservation.PopcornOrders.Add(new PopcornOrder
                {
                    Size = popcorn.Size,
                    Flavor = popcorn.Flavor,
                    AddDrink = popcorn.AddDrink,
                    AddRefill = popcorn.AddRefill
                });
            }
        }

        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();
        
        var existingShowtimeSeats = await context.ShowtimeSeats
            .Where(ss => ss.ShowtimeId == showtimeId && request.SeatIds.Contains(ss.SeatId))
            .ToListAsync();

        foreach (var showtimeSeat in existingShowtimeSeats)
        {
            showtimeSeat.ReservationId = reservation.Id;
        }

        await context.SaveChangesAsync();

        var reservedSeatInfos = seats.Select(seat => new SeatInfoDto(
            seat.Id,
            seat.Row,
            seat.SeatNumber,
            false,
            SeatSelectionService.CalculateSeatScore(seat.Row, seat.SeatNumber,
                showtime.Room.Rows.Count, showtime.Room.Rows.Max(r => r.SeatCount))
        )).ToList();

        var response = new ReservationConfirmResponseDto(
            reservation.Id,
            reservedSeatInfos,
            $"Successfully reserved {request.SeatIds.Count} seat(s) for showtime {showtimeId}"
        );

        return Ok(response);
    }
}