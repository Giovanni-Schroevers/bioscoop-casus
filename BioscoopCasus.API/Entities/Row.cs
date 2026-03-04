namespace BioscoopCasus.API.Entities;

public class Row
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    public int RowNumber { get; set; }

    public int SeatCount { get; set; }

    // Navigation property
    public Room Room { get; set; }
}
