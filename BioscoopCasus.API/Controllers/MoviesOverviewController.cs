using BioscoopCasus.API.Data;
using BioscoopCasus.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Controllers;

[ApiController]
[Route("api/movies-overview")]
public class MoviesOverviewController : ControllerBase
{
    private readonly BioscoopDbContext _context;
    public MoviesOverviewController(BioscoopDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<MoviesOverviewDto>>> GetMoviesOverview([FromQuery] DateTime? date)
    {
        var now = DateTime.Now;
        var endOfWeek = DateTime.Today.AddDays(7 - (int)DateTime.Today.DayOfWeek).AddDays(1);
        var filterDate = date?.Date ?? now.Date;
        
        var movies = await _context.Movies
            .Include(m => m.Showtimes)
            .Where(m => m.Showtimes.Any(s =>
                s.StartTime.Date == filterDate &&
                s.StartTime >= now && 
                s.StartTime < endOfWeek))
            .OrderBy(m => m.Showtimes.Where(s => s.StartTime.Date == filterDate && s.StartTime >= now && s.StartTime < endOfWeek).Min(s => s.StartTime))
            .ThenBy(m => m.Title)
            .ToListAsync();
        
    var result = movies.Select(m => new MoviesOverviewDto(
            m.Id,
            m.Title,
            m.Genres,
            m.DurationMinutes,
            m.Showtimes
                .Where(s =>
                    s.StartTime.Date == filterDate &&
                    s.StartTime >= now &&
                    s.StartTime < endOfWeek)
                .OrderBy(s => s.StartTime)
                .Select(s => new MoviesOverviewShowtimeDto(s.Id, s.StartTime))
                .ToList()
        )).ToList();

        return Ok(result);
}
}