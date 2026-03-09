using BioscoopCasus.API.Data;
using BioscoopCasus.Models.DTOs;
using BioscoopCasus.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShowtimesController : ControllerBase
{
    private readonly BioscoopDbContext _context;

    public ShowtimesController(BioscoopDbContext context)
    {
        _context = context;
    }

    // GET /api/showtimes
    // Optionally pass ?movieId=... or ?date=...
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShowtimeResponseDto>>> GetShowtimes(
        [FromQuery] int? movieId,
        [FromQuery] DateTime? date)
    {
        var query = _context.Showtimes.AsQueryable();

        if (movieId.HasValue)
        {
            query = query.Where(s => s.MovieId == movieId.Value);
        }

        if (date.HasValue)
        {
            var targetDate = date.Value.Date;
            query = query.Where(s => s.StartTime.Date == targetDate);
        }

        var showtimes = await query
            .OrderBy(s => s.StartTime)
            .Select(s => new ShowtimeResponseDto(
                s.Id,
                s.MovieId,
                s.RoomId,
                s.StartTime,
                0 // Ticket price is out of scope
            ))
            .ToListAsync();

        return Ok(showtimes);
    }

    // POST /api/showtimes
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ShowtimeResponseDto>> CreateShowtime(ShowtimeCreateDto dto)
    {
        if (!await _context.Movies.AnyAsync(m => m.Id == dto.MovieId))
            return BadRequest("Invalid MovieId");

        var movie = await _context.Movies.FindAsync(dto.MovieId);
        if (movie == null)
            return BadRequest("Invalid MovieId");

        if (!await _context.Rooms.AnyAsync(r => r.Id == dto.RoomId))
            return BadRequest("Invalid RoomId");

        if (dto.StartTime < DateTime.Now)
            return BadRequest("Showtime cannot be in the past");

        // Calculate end time (duration + 30 min cleaning buffer)
        var newEndTime = dto.StartTime.AddMinutes(movie.DurationMinutes + 30);

        // Check for double bookings in the requested room
        var hasConflict = await _context.Showtimes
            .Include(s => s.Movie)
            .AnyAsync(s => s.RoomId == dto.RoomId &&
                           ((dto.StartTime >= s.StartTime && dto.StartTime < s.StartTime.AddMinutes(s.Movie.DurationMinutes + 30)) ||
                            (newEndTime > s.StartTime && newEndTime <= s.StartTime.AddMinutes(s.Movie.DurationMinutes + 30)) ||
                            (dto.StartTime <= s.StartTime && newEndTime >= s.StartTime.AddMinutes(s.Movie.DurationMinutes + 30))));

        if (hasConflict)
        {
            return BadRequest($"Room is already booked during this time slot (includes 30m cleaning buffer).");
        }

        var showtime = new Showtime
        {
            MovieId = dto.MovieId,
            RoomId = dto.RoomId,
            StartTime = dto.StartTime
        };

        _context.Showtimes.Add(showtime);
        await _context.SaveChangesAsync();

        var response = new ShowtimeResponseDto(
            showtime.Id,
            showtime.MovieId,
            showtime.RoomId,
            showtime.StartTime,
            0
        );

        return CreatedAtAction(nameof(GetShowtimes), new { id = showtime.Id }, response);
    }

    // POST /api/showtimes/bulk
    [HttpPost("bulk")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<ShowtimeResponseDto>>> BulkCreateShowtimes(ShowtimeBulkCreateDto bulkDto)
    {
        if (bulkDto.Showtimes == null || !bulkDto.Showtimes.Any())
            return BadRequest("No showtimes provided.");

        var showtimeEntities = new List<Showtime>();
        var responseDtos = new List<ShowtimeResponseDto>();

        // Pre-fetch all movies for duration checks to avoid N+1 queries
        var movieIds = bulkDto.Showtimes.Select(s => s.MovieId).Distinct().ToList();
        var movies = await _context.Movies.Where(m => movieIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);

        // Get the date range for existing showtimes
        var minTime = bulkDto.Showtimes.Min(s => s.StartTime);
        var maxTime = bulkDto.Showtimes.Max(s => s.StartTime.AddMinutes(240)); // rough max duration buffer
        var roomIds = bulkDto.Showtimes.Select(s => s.RoomId).Distinct().ToList();

        // Fetch existing showtimes in that timeframe
        var existingShowtimes = await _context.Showtimes
            .Include(s => s.Movie)
            .Where(s => roomIds.Contains(s.RoomId) && s.StartTime >= minTime.AddDays(-1) && s.StartTime <= maxTime.AddDays(1))
            .ToListAsync();

        foreach (var dto in bulkDto.Showtimes)
        {
            if (!movies.TryGetValue(dto.MovieId, out var movie))
                return BadRequest($"Invalid MovieId: {dto.MovieId}");

            var newEndTime = dto.StartTime.AddMinutes(movie.DurationMinutes + 30); // 30 min cleaning

            // Check against existing database showtimes
            var dbConflict = existingShowtimes.Any(s =>
                s.RoomId == dto.RoomId &&
                ((dto.StartTime >= s.StartTime && dto.StartTime < s.StartTime.AddMinutes(s.Movie.DurationMinutes + 30)) ||
                 (newEndTime > s.StartTime && newEndTime <= s.StartTime.AddMinutes(s.Movie.DurationMinutes + 30)) ||
                 (dto.StartTime <= s.StartTime && newEndTime >= s.StartTime.AddMinutes(s.Movie.DurationMinutes + 30))));

            // Check against other showtimes currently being processed in this bulk request
            var inMemoryConflict = showtimeEntities.Any(s =>
            {
                var processingMovie = movies[s.MovieId];
                var processingEndTime = s.StartTime.AddMinutes(processingMovie.DurationMinutes + 30);
                return s.RoomId == dto.RoomId &&
                       ((dto.StartTime >= s.StartTime && dto.StartTime < processingEndTime) ||
                        (newEndTime > s.StartTime && newEndTime <= processingEndTime) ||
                        (dto.StartTime <= s.StartTime && newEndTime >= processingEndTime));
            });

            if (dto.StartTime < DateTime.Now)
            {
                return BadRequest($"StartTime for RoomId {dto.RoomId} cannot be in the past.");
            }

            if (dbConflict || inMemoryConflict)
            {
                return BadRequest($"Scheduling conflict detected for RoomId {dto.RoomId} at {dto.StartTime}.");
            }

            var showtime = new Showtime
            {
                MovieId = dto.MovieId,
                RoomId = dto.RoomId,
                StartTime = dto.StartTime
            };

            showtimeEntities.Add(showtime);

            responseDtos.Add(new ShowtimeResponseDto(
                showtime.Id,
                showtime.MovieId,
                showtime.RoomId,
                showtime.StartTime,
                0
            ));
        }

        await _context.Showtimes.AddRangeAsync(showtimeEntities);
        await _context.SaveChangesAsync();

        return Ok(responseDtos);
    }

    // PUT /api/showtimes/{id}
    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<ShowtimeResponseDto>> UpdateShowtime(int id, ShowtimeCreateDto dto)
    {
        var showtime = await _context.Showtimes.FindAsync(id);
        if (showtime == null)
        {
            return NotFound("Show not found");
        }
        
        var movie = await _context.Movies.FindAsync(dto.MovieId);
        if (movie == null)
            return BadRequest("Invalid MovieId");

        if (!await _context.Rooms.AnyAsync(r => r.Id == dto.RoomId))
            return BadRequest("Invalid RoomId");

        if (dto.StartTime < DateTime.Now)
            return BadRequest("Showtime cannot be in the past");

        // Calculate end time (duration + 30 min cleaning buffer)
        var newEndTime = dto.StartTime.AddMinutes(movie.DurationMinutes + 30);

        // Check for double bookings excluding the current showtime
        var hasConflict = await _context.Showtimes
            .Include(s => s.Movie)
            .AnyAsync(s => s.Id != id && s.RoomId == dto.RoomId &&
                           ((dto.StartTime >= s.StartTime && dto.StartTime < s.StartTime.AddMinutes(s.Movie.DurationMinutes + 30)) ||
                            (newEndTime > s.StartTime && newEndTime <= s.StartTime.AddMinutes(s.Movie.DurationMinutes + 30)) ||
                            (dto.StartTime <= s.StartTime && newEndTime >= s.StartTime.AddMinutes(s.Movie.DurationMinutes + 30))));

        if (hasConflict)
        {
            return BadRequest($"Room is already booked during this time slot.");
        }

        showtime.MovieId = dto.MovieId;
        showtime.RoomId = dto.RoomId;
        showtime.StartTime = dto.StartTime;

        await _context.SaveChangesAsync();

        return Ok(new ShowtimeResponseDto(
            showtime.Id,
            showtime.MovieId,
            showtime.RoomId,
            showtime.StartTime,
            0
        ));
    }

    // DELETE /api/showtimes/{id}
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteShowtime(int id)
    {
        var showtime = await _context.Showtimes.FindAsync(id);
        if (showtime == null)
            return NotFound();

        _context.Showtimes.Remove(showtime);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
