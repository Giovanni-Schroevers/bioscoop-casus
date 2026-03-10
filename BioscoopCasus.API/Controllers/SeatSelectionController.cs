using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BioscoopCasus.API.Data;
using BioscoopCasus.API.Entities;
using BioscoopCasus.API.Services;
using BioscoopCasus.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Controllers;

[ApiController]
[Route("api/seat-selection")]
public class SeatSelectionController : ControllerBase
{
    private readonly BioscoopDbContext _context;

    public SeatSelectionController(BioscoopDbContext context)
    {
        _context = context;
    }

    [HttpGet("{showtimeId}/available")]
    public async Task<ActionResult<IEnumerable<SeatInfoDto>>> GetAvailableSeats(int showtimeId)
    {
        var showtime = await _context.Showtimes
            .Include(s => s.Room)
            .ThenInclude(r => r.Rows)
            .FirstOrDefaultAsync(s => s.Id == showtimeId);

        if (showtime == null)
            return NotFound("Showtime not found");

        await EnsureSeatsInitializedForShowtime(showtime);

        var occupiedSeatIds = await _context.ShowtimeSeats
            .Where(ss => ss.ShowtimeId == showtimeId && ss.ReservationId.HasValue)
            .Select(ss => ss.SeatId)
            .ToHashSetAsync();

        var seats = await _context.Seats
            .Where(s => s.RoomId == showtime.RoomId)
            .OrderBy(s => s.Row)
            .ThenBy(s => s.SeatNumber)
            .ToListAsync();

        int totalRows = showtime.Room.Rows.Count;
        int maxSeatsPerRow = showtime.Room.Rows.Max(r => r.SeatCount);

        var seatInfos = seats.Select(seat => new SeatInfoDto(
            seat.Id,
            seat.Row,
            seat.SeatNumber,
            !occupiedSeatIds.Contains(seat.Id),
            SeatSelectionService.CalculateSeatScore(seat.Row, seat.SeatNumber, totalRows, maxSeatsPerRow)
        )).ToList();

        return Ok(seatInfos);
    }
    
    [HttpPost("{showtimeId}/suggest")]
    public async Task<ActionResult<SeatSelectionResponseDto>> SuggestSeats(
        int showtimeId,
        [FromBody] SeatSelectionRequestDto request)
    {
        if (request.GroupSize <= 0 || request.GroupSize > 20)
            return BadRequest("Group size must be between 1 and 20");

        var showtime = await _context.Showtimes
            .Include(s => s.Room)
            .ThenInclude(r => r.Rows)
            .Include(s => s.Room)
            .ThenInclude(r => r.Seats)
            .FirstOrDefaultAsync(s => s.Id == showtimeId);

        if (showtime == null)
            return NotFound("Showtime not found");

        await EnsureSeatsInitializedForShowtime(showtime);

        var showtimeSeatsQuery = _context.ShowtimeSeats.Where(ss => ss.ShowtimeId == showtimeId);

        var result = SeatSelectionService.SelectBestSeats(showtime, request.GroupSize, showtimeSeatsQuery);

        var suggestedSeatInfos = result.SelectedSeats.Select(seat => new SeatInfoDto(
            seat.Id,
            seat.Row,
            seat.SeatNumber,
            true,
            SeatSelectionService.CalculateSeatScore(seat.Row, seat.SeatNumber,
                showtime.Room.Rows.Count, showtime.Room.Rows.Max(r => r.SeatCount))
        )).ToList();

        var groupedSeatInfos = result.GroupedSeats.Select(group =>
            group.Select(seat => new SeatInfoDto(
                seat.Id,
                seat.Row,
                seat.SeatNumber,
                true,
                SeatSelectionService.CalculateSeatScore(seat.Row, seat.SeatNumber,
                    showtime.Room.Rows.Count, showtime.Room.Rows.Max(r => r.SeatCount))
            )).ToList()
        ).ToList();

        var response = new SeatSelectionResponseDto(
            suggestedSeatInfos,
            result.Message,
            result.IsGroupedTogether,
            groupedSeatInfos,
            result.TotalScore
        );

        return Ok(response);
    }

    [HttpPost("{showtimeId}/reserve")]
    public async Task<ActionResult<ReservationConfirmResponseDto>> ConfirmReservation(
        int showtimeId,
        [FromBody] ReservationConfirmRequestDto request)
    {
        if (request.SeatIds == null || !request.SeatIds.Any())
            return BadRequest("No seats selected");

        if (request.SeatIds.Count > 20)
            return BadRequest("Maximum 20 seats per reservation");

        var showtime = await _context.Showtimes
            .Include(s => s.Room)
            .ThenInclude(r => r.Rows)
            .FirstOrDefaultAsync(s => s.Id == showtimeId);

        if (showtime == null)
            return NotFound("Showtime not found");

        await EnsureSeatsInitializedForShowtime(showtime);

        var seats = await _context.Seats
            .Where(s => request.SeatIds.Contains(s.Id) && s.RoomId == showtime.RoomId)
            .ToListAsync();

        if (seats.Count != request.SeatIds.Count)
            return BadRequest("One or more selected seats do not exist or belong to a different room");

        var existingReservations = await _context.ShowtimeSeats
            .Where(ss => ss.ShowtimeId == showtimeId && request.SeatIds.Contains(ss.SeatId) && ss.ReservationId.HasValue)
            .ToListAsync();

        if (existingReservations.Any())
            return BadRequest("One or more selected seats are already reserved");

        var reservation = new Reservation
        {
            ShowtimeId = showtimeId
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();
        
        var existingShowtimeSeats = await _context.ShowtimeSeats
            .Where(ss => ss.ShowtimeId == showtimeId && request.SeatIds.Contains(ss.SeatId))
            .ToListAsync();

        foreach (var showtimeSeat in existingShowtimeSeats)
        {
            showtimeSeat.ReservationId = reservation.Id;
        }

        await _context.SaveChangesAsync();

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

    [HttpPost("{showtimeId}/initialize-seats")]
    public async Task<ActionResult<string>> InitializeSeatsForShowtime(int showtimeId)
    {
        var showtime = await _context.Showtimes
            .Include(s => s.Room)
            .ThenInclude(r => r.Seats)
            .FirstOrDefaultAsync(s => s.Id == showtimeId);

        if (showtime == null)
            return NotFound("Showtime not found");

        var existingShowtimeSeatsCount = await _context.ShowtimeSeats
            .Where(ss => ss.ShowtimeId == showtimeId)
            .CountAsync();

        if (existingShowtimeSeatsCount > 0)
            return Ok($"Seats already initialized for showtime {showtimeId} ({existingShowtimeSeatsCount} seats)");

        var roomSeats = showtime.Room.Seats.ToList();

        if (!roomSeats.Any())
            return BadRequest($"No seats found for room {showtime.RoomId}");

        var showtimeSeats = roomSeats.Select(seat => new ShowtimeSeat
        {
            ShowtimeId = showtimeId,
            SeatId = seat.Id,
            ReservationId = null
        }).ToList();

        _context.ShowtimeSeats.AddRange(showtimeSeats);
        await _context.SaveChangesAsync();

        return Ok($"Successfully initialized {showtimeSeats.Count} seats for showtime {showtimeId} in room {showtime.Room.Name}");
    }

    [HttpGet("debug/{showtimeId}")]
    public async Task<ActionResult<object>> DebugShowtime(int showtimeId)
    {
        var showtime = await _context.Showtimes
            .Include(s => s.Room)
            .ThenInclude(r => r.Seats)
            .FirstOrDefaultAsync(s => s.Id == showtimeId);

        if (showtime == null)
            return NotFound("Showtime not found");

        var showtimeSeats = await _context.ShowtimeSeats
            .Where(ss => ss.ShowtimeId == showtimeId)
            .Include(ss => ss.Seat)
            .ToListAsync();

        var reservations = await _context.Reservations
            .Where(r => r.ShowtimeId == showtimeId)
            .ToListAsync();

        return Ok(new
        {
            Showtime = new { showtime.Id, showtime.StartTime, RoomName = showtime.Room.Name },
            TotalSeatsInRoom = showtime.Room.Seats.Count,
            ShowtimeSeatsCount = showtimeSeats.Count,
            OccupiedSeatsCount = showtimeSeats.Count(ss => ss.ReservationId.HasValue),
            ReservationsCount = reservations.Count,
            OccupiedSeats = showtimeSeats
                .Where(ss => ss.ReservationId.HasValue)
                .Select(ss => new { ss.Seat.Row, ss.Seat.SeatNumber, ss.ReservationId })
                .ToList()
        });
    }

    private async Task EnsureSeatsInitializedForShowtime(Showtime showtime)
    {
        var existingShowtimeSeatsCount = await _context.ShowtimeSeats
            .Where(ss => ss.ShowtimeId == showtime.Id)
            .CountAsync();

        if (existingShowtimeSeatsCount > 0)
            return;

        var roomSeats = showtime.Room.Seats.ToList();

        if (!roomSeats.Any())
            throw new InvalidOperationException($"No seats found for room {showtime.RoomId}");

        var showtimeSeats = roomSeats.Select(seat => new ShowtimeSeat
        {
            ShowtimeId = showtime.Id,
            SeatId = seat.Id,
            ReservationId = null
        }).ToList();

        _context.ShowtimeSeats.AddRange(showtimeSeats);
        await _context.SaveChangesAsync();
    }
}

