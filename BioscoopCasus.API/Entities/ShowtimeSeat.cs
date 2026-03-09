namespace BioscoopCasus.API.Entities;

public class ShowtimeSeat
{
    public int ShowtimeId { get; set; }

    public int SeatId { get; set; }

    public int? ReservationId { get; set; }

    // Navigation properties
    public Showtime Showtime { get; set; }
    public Seat Seat { get; set; }
    public Reservation Reservation { get; set; }
}
