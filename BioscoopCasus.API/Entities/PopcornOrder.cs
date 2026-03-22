namespace BioscoopCasus.API.Entities;

public class PopcornOrder
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public string Size { get; set; } = string.Empty;
    public string Flavor { get; set; } = string.Empty;
    public bool AddDrink { get; set; }
    public bool AddRefill { get; set; }

    // Navigation property
    public Reservation Reservation { get; set; }
}
