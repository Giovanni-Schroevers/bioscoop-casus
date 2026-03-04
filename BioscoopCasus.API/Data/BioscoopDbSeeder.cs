using BioscoopCasus.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Data;

public static class BioscoopDbSeeder
{
    public static async Task SeedAsync(BioscoopDbContext context)
    {
        // Only seed if the database is empty
        if (await context.Movies.AnyAsync())
            return;

        var rooms = CreateRooms();
        var rows = CreateRows(rooms);
        var movies = CreateMovies();
        var showtimes = CreateShowtimes(movies, rooms);

        context.Rooms.AddRange(rooms);
        context.Rows.AddRange(rows);
        context.Movies.AddRange(movies);
        context.Showtimes.AddRange(showtimes);

        await context.SaveChangesAsync();
    }

    private static List<Room> CreateRooms()
    {
        return
        [
            new Room { Number = 1, Name = "Zaal 1", Has3D = true,  IsWheelchairAccessible = true },
            new Room { Number = 2, Name = "Zaal 2", Has3D = true,  IsWheelchairAccessible = true },
            new Room { Number = 3, Name = "Zaal 3", Has3D = false, IsWheelchairAccessible = true },
            new Room { Number = 4, Name = "Zaal 4", Has3D = false, IsWheelchairAccessible = true },
            new Room { Number = 5, Name = "Zaal 5", Has3D = false, IsWheelchairAccessible = false },
            new Room { Number = 6, Name = "Zaal 6", Has3D = false, IsWheelchairAccessible = false },
        ];
    }

    private static List<Row> CreateRows(List<Room> rooms)
    {
        var rows = new List<Row>();

        foreach (var room in rooms)
        {
            switch (room.Number)
            {
                // Rooms 1-3: 8 rows of 15 seats = 120 seats
                case 1 or 2 or 3:
                    for (int i = 1; i <= 8; i++)
                        rows.Add(new Row { Room = room, RowNumber = i, SeatCount = 15 });
                    break;

                // Room 4: 6 rows of 10 seats = 60 seats
                case 4:
                    for (int i = 1; i <= 6; i++)
                        rows.Add(new Row { Room = room, RowNumber = i, SeatCount = 10 });
                    break;

                // Rooms 5-6: front 2 rows of 10, back 2 rows of 15 = 50 seats
                case 5 or 6:
                    rows.Add(new Row { Room = room, RowNumber = 1, SeatCount = 10 });
                    rows.Add(new Row { Room = room, RowNumber = 2, SeatCount = 10 });
                    rows.Add(new Row { Room = room, RowNumber = 3, SeatCount = 15 });
                    rows.Add(new Row { Room = room, RowNumber = 4, SeatCount = 15 });
                    break;
            }
        }

        return rows;
    }

    private static List<Movie> CreateMovies()
    {
        return
        [
            new Movie
            {
                Title = "The Matrix Resurrections",
                Description = "Return to the world of two realities: one, everyday life; the other, what lies behind it.",
                PosterUrl = "https://example.com/matrix.jpg",
                Actors = "Keanu Reeves, Carrie-Anne Moss, Yahya Abdul-Mateen II",
                TrailerUrl = "https://youtube.com/watch?v=9ix7TUGVYIo",
                Genres = "Sci-Fi, Action",
                AgeRating = 16,
                DurationMinutes = 148,
                ReleaseDate = new DateTime(2025, 12, 22)
            },
            new Movie
            {
                Title = "Dune: Part Three",
                Description = "The epic conclusion of the Dune saga as Paul Atreides faces his ultimate destiny.",
                PosterUrl = "https://example.com/dune3.jpg",
                Actors = "Timothée Chalamet, Zendaya, Florence Pugh",
                TrailerUrl = "https://youtube.com/watch?v=WayToDune3",
                Genres = "Sci-Fi, Adventure",
                AgeRating = 12,
                DurationMinutes = 155,
                ReleaseDate = new DateTime(2026, 1, 15)
            },
            new Movie
            {
                Title = "Spider-Man: Beyond",
                Description = "Spider-Man faces threats from across the multiverse in this action-packed adventure.",
                PosterUrl = "https://example.com/spiderman.jpg",
                Actors = "Tom Holland, Zendaya, Jacob Batalon",
                TrailerUrl = "https://youtube.com/watch?v=SpiderManBeyond",
                Genres = "Action, Superhero",
                AgeRating = 12,
                DurationMinutes = 130,
                ReleaseDate = new DateTime(2026, 2, 1)
            },
            new Movie
            {
                Title = "Inside Out 3",
                Description = "Riley's emotions embark on yet another adventure as she navigates adulthood.",
                PosterUrl = "https://example.com/insideout3.jpg",
                Actors = "Amy Poehler, Phyllis Smith, Bill Hader",
                TrailerUrl = "https://youtube.com/watch?v=InsideOut3",
                Genres = "Animation, Family",
                AgeRating = 6,
                DurationMinutes = 105,
                ReleaseDate = new DateTime(2026, 2, 14)
            },
            new Movie
            {
                Title = "The Batman: Gotham Nights",
                Description = "The Dark Knight returns to protect Gotham from a new wave of crime.",
                PosterUrl = "https://example.com/batman.jpg",
                Actors = "Robert Pattinson, Zoë Kravitz, Jeffrey Wright",
                TrailerUrl = "https://youtube.com/watch?v=BatmanGothamNights",
                Genres = "Action, Crime",
                AgeRating = 16,
                DurationMinutes = 152,
                ReleaseDate = new DateTime(2025, 11, 20)
            },
            new Movie
            {
                Title = "Frozen III",
                Description = "Elsa and Anna discover new magical realms beyond Arendelle.",
                PosterUrl = "https://example.com/frozen3.jpg",
                Actors = "Idina Menzel, Kristen Bell, Josh Gad",
                TrailerUrl = "https://youtube.com/watch?v=Frozen3",
                Genres = "Animation, Musical",
                AgeRating = 6,
                DurationMinutes = 110,
                ReleaseDate = new DateTime(2025, 12, 6)
            },
        ];
    }

    private static List<Showtime> CreateShowtimes(List<Movie> movies, List<Room> rooms)
    {
        var showtimes = new List<Showtime>();

        // Calculate the Monday of the current week
        var today = DateTime.Today;
        int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7; // Monday = 0
        var currentMonday = today.AddDays(-daysSinceMonday);

        // Seed current week (Mon-Sun)
        AddWeekShowtimes(showtimes, movies, rooms, currentMonday);

        // If today is Thursday (3) or later in the week, also seed next week
        if (daysSinceMonday >= 3) // Thursday = 3 (Mon=0, Tue=1, Wed=2, Thu=3)
        {
            var nextMonday = currentMonday.AddDays(7);
            AddWeekShowtimes(showtimes, movies, rooms, nextMonday);
        }

        return showtimes;
    }

    private static void AddWeekShowtimes(
        List<Showtime> showtimes,
        List<Movie> movies,
        List<Room> rooms,
        DateTime monday)
    {
        // Time slots for showtimes
        var timeSlots = new[] { 14, 17, 20 }; // 14:00, 17:00, 20:00

        // For each day of the week (Mon=0 through Sun=6)
        for (int day = 0; day < 7; day++)
        {
            var date = monday.AddDays(day);

            // Spread movies across rooms and time slots
            int movieIndex = 0;
            foreach (var room in rooms)
            {
                foreach (var hour in timeSlots)
                {
                    var movie = movies[movieIndex % movies.Count];
                    showtimes.Add(new Showtime
                    {
                        Movie = movie,
                        Room = room,
                        StartTime = date.AddHours(hour),
                    });

                    movieIndex++;
                }
            }
        }
    }
}
