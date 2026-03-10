using BioscoopCasus.API.Data;
using BioscoopCasus.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationsController(BioscoopDbContext context) : ControllerBase
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
}