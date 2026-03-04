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
    public DbSet<Showtime> Showtimes { get; set; }

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
    }
}
