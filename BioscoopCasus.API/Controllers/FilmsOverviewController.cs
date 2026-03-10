using BioscoopCasus.API.Data;
using BioscoopCasus.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Controllers;

[ApiController]
[Route("api/films-overview")]
public class FilmsOverviewController : ControllerBase
{
    private readonly BioscoopDbContext _context;

    public FilmsOverviewController(BioscoopDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<FilmsOverviewDto>>> GetFilmsOverview()
    {
        var now = DateTime.Now;

        var showtimes = await _context.Showtimes
            .Include(s => s.Movie)
            .Where(s => s.StartTime >= now)
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.Movie.Title)
            .ToListAsync();

        var result = showtimes.Select(s => new FilmsOverviewDto(
            s.Id,
            s.MovieId,
            s.Movie.Title,
            s.Movie.Genres,
            s.Movie.DurationMinutes,
            s.StartTime,
            s.Movie.PosterUrl
        )).ToList();

        return Ok(result);
    }
}
