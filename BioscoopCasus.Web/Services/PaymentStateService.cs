using BioscoopCasus.Models.DTOs;

namespace BioscoopCasus.Web.Services;

public class PaymentStateService
{
    public int TotalPriceCents { get; private set; }
    public List<PopcornOrderDto> PopcornOrders { get; private set; } = new();

    public void SetPrice(int cents) => TotalPriceCents = cents;
    public void SetPopcornOrders(List<PopcornOrderDto> orders) => PopcornOrders = orders;
    public void Clear()
    {
        TotalPriceCents = 0;
        PopcornOrders = new();
    }
}