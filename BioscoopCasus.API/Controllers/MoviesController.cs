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
        var movies = await _context.Movies
            .OrderByDescending(m => m.ReleaseDate)
            .Select(m => new MovieResponseDto(
                m.Id,
                m.Title,
                m.Description,
                m.PosterUrl,
                m.Actors,
                m.TrailerUrl,
                m.Genres,
                m.AgeRating,
                m.DurationMinutes,
                m.ReleaseDate,
                null // Don't include showtimes in the list view to keep payload small
            ))
            .ToListAsync();

        return Ok(movies);
    }

    // GET /api/movies/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<MovieResponseDto>> GetMovie(int id)
    {
        var movie = await _context.Movies
            .Include(m => m.Showtimes)
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

        var response = new MovieResponseDto(
            movie.Id,
            movie.Title,
            movie.Description,
            movie.PosterUrl,
            movie.Actors,
            movie.TrailerUrl,
            movie.Genres,
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
            Description = dto.Description,
            PosterUrl = dto.PosterUrl,
            Actors = dto.Actors,
            TrailerUrl = dto.TrailerUrl,
            Genres = dto.Genres,
            AgeRating = dto.AgeRating,
            DurationMinutes = dto.DurationMinutes,
            ReleaseDate = dto.ReleaseDate
        };

        _context.Movies.Add(movie);
        await _context.SaveChangesAsync();

        var response = new MovieResponseDto(
            movie.Id,
            movie.Title,
            movie.Description,
            movie.PosterUrl,
            movie.Actors,
            movie.TrailerUrl,
            movie.Genres,
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
        var movie = await _context.Movies.FindAsync(id);

        if (movie == null)
            return NotFound();

        movie.Title = dto.Title;
        movie.Description = dto.Description;
        movie.PosterUrl = dto.PosterUrl;
        movie.Actors = dto.Actors;
        movie.TrailerUrl = dto.TrailerUrl;
        movie.Genres = dto.Genres;
        movie.AgeRating = dto.AgeRating;
        movie.DurationMinutes = dto.DurationMinutes;
        movie.ReleaseDate = dto.ReleaseDate;

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
