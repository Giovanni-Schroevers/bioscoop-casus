namespace BioscoopCasus.API.Entities;

public class Reservation
{
    public int Id { get; set; }
    public int ShowtimeId { get; set; }

    // Navigation properties
    public Showtime Showtime { get; set; }
    public ICollection<ShowtimeSeat> ShowtimeSeats { get; set; } = new List<ShowtimeSeat>();
}