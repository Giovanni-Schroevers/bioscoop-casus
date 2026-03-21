using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BioscoopCasus.API.Data;
using BioscoopCasus.Models.DTOs;
using BioscoopCasus.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MoviesController : ControllerBase
{
    private readonly BioscoopDbContext _context;

    public MoviesController(BioscoopDbContext context)
    {
        _context = context;
    }

    // GET /api/movies
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MovieResponseDto>>> GetMovies()
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        
        var movies = await _context.Movies
            .Include(m => m.Translations)
            .OrderByDescending(m => m.ReleaseDate)
            .ToListAsync();

        var result = movies.Select(m => 
        {
            var t = m.Translations.FirstOrDefault(tr => tr.LanguageCode == culture) 
                 ?? m.Translations.FirstOrDefault(tr => tr.LanguageCode == "en") 
                 ?? new MovieTranslation { Description = "", Genres = "" };

            return new MovieResponseDto(
                m.Id,
                m.Title,
                t.Description,
                m.PosterUrl,
                m.Actors,
                m.TrailerUrl,
                t.Genres,
                m.AgeRating,
                m.DurationMinutes,
                m.ReleaseDate,
                null
            );
        }).ToList();

        return Ok(result);
    }

    // GET /api/movies/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<MovieResponseDto>> GetMovie(int id)
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        var movie = await _context.Movies
            .Include(m => m.Showtimes)
            .Include(m => m.Translations)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
            return NotFound();

        var showtimeDtos = movie.Showtimes.Select(s => new ShowtimeResponseDto(
            s.Id,
            s.MovieId,
            s.RoomId,
            s.StartTime,
            0 // Ticket price is out of scope for now
        )).OrderBy(s => s.StartTime).ToList();

        var t = movie.Translations.FirstOrDefault(tr => tr.LanguageCode == culture) 
             ?? movie.Translations.FirstOrDefault(tr => tr.LanguageCode == "en") 
             ?? new MovieTranslation { Description = "", Genres = "" };

        var response = new MovieResponseDto(
            movie.Id,
            movie.Title,
            t.Description,
            movie.PosterUrl,
            movie.Actors,
            movie.TrailerUrl,
            t.Genres,
            movie.AgeRating,
            movie.DurationMinutes,
            movie.ReleaseDate,
            showtimeDtos
        );

        return Ok(response);
    }

    // POST /api/movies
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<MovieResponseDto>> CreateMovie(MovieCreateDto dto)
    {
        var movie = new Movie
        {
            Title = dto.Title,
            PosterUrl = dto.PosterUrl,
            Actors = dto.Actors,
            TrailerUrl = dto.TrailerUrl,
            AgeRating = dto.AgeRating,
            DurationMinutes = dto.DurationMinutes,
            ReleaseDate = dto.ReleaseDate,
            Translations = new List<MovieTranslation>
            {
                new MovieTranslation { LanguageCode = "en", Description = dto.Description, Genres = dto.Genres },
                new MovieTranslation { LanguageCode = "nl", Description = dto.Description, Genres = dto.Genres } // Automatically copy English to Dutch for now
            }
        };

        _context.Movies.Add(movie);
        await _context.SaveChangesAsync();

        var response = new MovieResponseDto(
            movie.Id,
            dto.Title,
            dto.Description,
            movie.PosterUrl,
            movie.Actors,
            movie.TrailerUrl,
            dto.Genres,
            movie.AgeRating,
            movie.DurationMinutes,
            movie.ReleaseDate,
            new List<ShowtimeResponseDto>()
        );

        return CreatedAtAction(nameof(GetMovie), new { id = movie.Id }, response);
    }

    // PUT /api/movies/{id}
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateMovie(int id, MovieUpdateDto dto)
    {
        var movie = await _context.Movies
            .Include(m => m.Translations)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
            return NotFound();

        movie.Title = dto.Title;
        movie.PosterUrl = dto.PosterUrl;
        movie.Actors = dto.Actors;
        movie.TrailerUrl = dto.TrailerUrl;
        movie.AgeRating = dto.AgeRating;
        movie.DurationMinutes = dto.DurationMinutes;
        movie.ReleaseDate = dto.ReleaseDate;

        var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var t = movie.Translations.FirstOrDefault(tr => tr.LanguageCode == culture);
        if (t != null)
        {
            t.Description = dto.Description;
            t.Genres = dto.Genres;
        }
        else
        {
            movie.Translations.Add(new MovieTranslation { LanguageCode = culture, Description = dto.Description, Genres = dto.Genres });
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE /api/movies/{id}
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteMovie(int id)
    {
        var movie = await _context.Movies.FindAsync(id);
        if (movie == null)
            return NotFound();

        _context.Movies.Remove(movie);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
