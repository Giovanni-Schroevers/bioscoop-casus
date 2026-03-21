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
        var filterDate = date?.Date ?? DateTime.Today;

        var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        var movies = await _context.Movies
            .Include(m => m.Showtimes)
            .Include(m => m.Translations)
            .Where(m => m.Showtimes.Any(s => s.StartTime.Date == filterDate))
            .OrderBy(m => m.Showtimes.Where(s => s.StartTime.Date == filterDate).Min(s => s.StartTime))
            .ToListAsync();

        var result = movies.Select(m => {
            var t = m.Translations.FirstOrDefault(tr => tr.LanguageCode == culture) 
                 ?? m.Translations.FirstOrDefault(tr => tr.LanguageCode == "en") 
                 ?? new BioscoopCasus.API.Entities.MovieTranslation { Genres = "" };

            return new MoviesOverviewDto(
                m.Id,
                m.Title,
                t.Genres,
                m.DurationMinutes,
                m.Showtimes
                    .Where(s => s.StartTime.Date == filterDate)
                    .OrderBy(s => s.StartTime)
                    .Select(s => new MoviesOverviewShowtimeDto(s.Id, s.StartTime))
                    .ToList()
            );
        }).ToList();

        return Ok(result);
}
}