using BioscoopCasus.API.Data;
using BioscoopCasus.API.Services;
using BioscoopCasus.Models.DTOs;
using BioscoopCasus.Models.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MailingController(BioscoopDbContext context, MailingService mailingService) : ControllerBase
{
    private readonly QrCodeHelper _qrCodeHelper = new();

    // POST /api/mailing/ticket
    [HttpPost("ticket")]
    public async Task<ActionResult<TicketMailResponseDto>> SendReservationMail([FromBody] TicketMailSendDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest("Email is required");

        if (string.IsNullOrWhiteSpace(dto.TicketCode))
            return BadRequest("Ticket code is required");

        var reservationId = _qrCodeHelper.ExtractReservationId(dto.TicketCode);
        if (reservationId is null)
            return BadRequest("Invalid ticket code format");

        if (!_qrCodeHelper.VerifyChecksum(dto.TicketCode))
            return BadRequest("Invalid ticket code checksum");

        var reservation = await context.Reservations
            .Include(r => r.Showtime)
                .ThenInclude(s => s.Movie)
            .Include(r => r.Showtime)
                .ThenInclude(s => s.Room)
            .Include(r => r.ShowtimeSeats)
                .ThenInclude(ss => ss.Seat)
            .FirstOrDefaultAsync(r => r.Id == reservationId.Value);

        if (reservation is null)
            return NotFound("Reservation not found");

        var seats = reservation.ShowtimeSeats
            .Select(ss => new SeatDto(
                ss.SeatId,
                ss.Seat.Row,
                ss.Seat.SeatNumber))
            .ToList();

        var reservationDto = new ReservationResponseDto(
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

        try
        {
            await mailingService.SendReservationEmailAsync(dto.Email, reservationDto);
            return Ok(new TicketMailResponseDto(true));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new TicketMailResponseDto(false));
        }
    }
}