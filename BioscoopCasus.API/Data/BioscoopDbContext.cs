using BioscoopCasus.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Data;

public class BioscoopDbContext : DbContext
{
    public BioscoopDbContext(DbContextOptions<BioscoopDbContext> options)
        : base(options)
    {
    }

    public DbSet<Movie> Movies { get; set; }
    public DbSet<Room> Rooms { get; set; }
    public DbSet<Row> Rows { get; set; }
    public DbSet<Seat> Seats { get; set; }
    public DbSet<Showtime> Showtimes { get; set; }
    public DbSet<ShowtimeSeat> ShowtimeSeats { get; set; }
    public DbSet<Reservation> Reservations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Room.Number must be unique
        modelBuilder.Entity<Room>()
            .HasIndex(r => r.Number)
            .IsUnique();

        // Row belongs to Room
        modelBuilder.Entity<Row>()
            .HasOne(r => r.Room)
            .WithMany(room => room.Rows)
            .HasForeignKey(r => r.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seat belongs to Room
        modelBuilder.Entity<Seat>()
            .HasOne(s => s.Room)
            .WithMany(r => r.Seats)
            .HasForeignKey(s => s.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seat must be unique per room (RoomId, Row, SeatNumber)
        modelBuilder.Entity<Seat>()
            .HasIndex(s => new { s.RoomId, s.Row, s.SeatNumber })
            .IsUnique();

        // Map SeatNumber property to "Seat" column name
        modelBuilder.Entity<Seat>()
            .Property(s => s.SeatNumber)
            .HasColumnName("Seat");

        // Showtime belongs to Movie
        modelBuilder.Entity<Showtime>()
            .HasOne(s => s.Movie)
            .WithMany(m => m.Showtimes)
            .HasForeignKey(s => s.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        // Showtime belongs to Room
        modelBuilder.Entity<Showtime>()
            .HasOne(s => s.Room)
            .WithMany(r => r.Showtimes)
            .HasForeignKey(s => s.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // ShowtimeSeat configuration
        modelBuilder.Entity<ShowtimeSeat>()
            .HasKey(ss => new { ss.ShowtimeId, ss.SeatId });

        modelBuilder.Entity<ShowtimeSeat>()
            .HasOne(ss => ss.Showtime)
            .WithMany(s => s.ShowtimeSeats)
            .HasForeignKey(ss => ss.ShowtimeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ShowtimeSeat>()
            .HasOne(ss => ss.Seat)
            .WithMany(s => s.ShowtimeSeats)
            .HasForeignKey(ss => ss.SeatId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ShowtimeSeat>()
            .HasOne(ss => ss.Reservation)
            .WithMany(r => r.ShowtimeSeats)
            .HasForeignKey(ss => ss.ReservationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ShowtimeSeat>()
            .HasIndex(ss => ss.ReservationId);

        // Reservation belongs to Showtime
        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Showtime)
            .WithMany()
            .HasForeignKey(r => r.ShowtimeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
