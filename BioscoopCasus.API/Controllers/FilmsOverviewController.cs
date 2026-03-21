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
        var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        var showtimes = await _context.Showtimes
            .Include(s => s.Movie)
                .ThenInclude(m => m.Translations)
            .Where(s => s.StartTime >= now)
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        var result = showtimes.Select(s => {
            var t = s.Movie.Translations.FirstOrDefault(tr => tr.LanguageCode == culture) 
                 ?? s.Movie.Translations.FirstOrDefault(tr => tr.LanguageCode == "en") 
                 ?? new BioscoopCasus.API.Entities.MovieTranslation { Genres = "" };

            return new FilmsOverviewDto(
                s.Id,
                s.MovieId,
                s.Movie.Title,
                t.Genres,
                s.Movie.DurationMinutes,
                s.StartTime,
                s.Movie.PosterUrl
            );
        }).ToList();

        return Ok(result);
    }
}
