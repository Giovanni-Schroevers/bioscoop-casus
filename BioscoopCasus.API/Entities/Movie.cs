using System.ComponentModel.DataAnnotations;

namespace BioscoopCasus.API.Entities;

public class Movie
{
    public int Id { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public string PosterUrl { get; set; }

    public string Actors { get; set; }

    public string TrailerUrl { get; set; }

    public string Genres { get; set; }

    public int AgeRating { get; set; }

    public int DurationMinutes { get; set; }

    public DateTime ReleaseDate { get; set; }

    // Navigation property
    public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
