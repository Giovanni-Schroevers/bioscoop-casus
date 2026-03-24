using BioscoopCasus.Models.DTOs;

namespace BioscoopCasus.Web.Services;

public class CheckoutStateService
{
    public int? ShowtimeId { get; private set; }
    public List<int> SelectedSeatIds { get; private set; } = new();
    public string? MovieTitle { get; private set; }
    public string? ShowtimeDisplay { get; private set; }
    public List<PopcornOrderDto>? PopcornOrders { get; private set; }
    public int TotalPriceCents { get; private set; }

    public bool HasActiveCheckout =>
        ShowtimeId.HasValue &&
        SelectedSeatIds.Count > 0;

    public void SetCheckout(
        int showtimeId,
        List<int> selectedSeatIds,
        string? movieTitle,
        string? showtimeDisplay,
        List<PopcornOrderDto>? popcornOrders,
        int totalPriceCents)
    {
        ShowtimeId = showtimeId;
        SelectedSeatIds = selectedSeatIds;
        MovieTitle = movieTitle;
        ShowtimeDisplay = showtimeDisplay;
        PopcornOrders = popcornOrders;
        TotalPriceCents = totalPriceCents;
    }

    public void Clear()
    {
        ShowtimeId = null;
        SelectedSeatIds = new List<int>();
        MovieTitle = null;
        ShowtimeDisplay = null;
        PopcornOrders = null;
        TotalPriceCents = 0;
    }
}