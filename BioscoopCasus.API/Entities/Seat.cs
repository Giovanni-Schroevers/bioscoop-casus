namespace BioscoopCasus.API.Entities;

public class Seat
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    public int Row { get; set; }

    public int SeatNumber { get; set; }

    // Navigation properties
    public Room Room { get; set; }
    public ICollection<ShowtimeSeat> ShowtimeSeats { get; set; } = new List<ShowtimeSeat>();
}
