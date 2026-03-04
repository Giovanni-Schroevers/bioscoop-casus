namespace BioscoopCasus.API.Entities;

public class Room
{
    public int Id { get; set; }

    public int Number { get; set; }

    public string Name { get; set; }

    public bool Has3D { get; set; }

    public bool IsWheelchairAccessible { get; set; }

    // Navigation properties
    public ICollection<Row> Rows { get; set; } = new List<Row>();
    public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
