namespace BioscoopCasus.Web.Services;

public class PaymentStateService
{
    public int TotalPriceCents { get; private set; }

    public void SetPrice(int cents) => TotalPriceCents = cents;
    public void Clear()              => TotalPriceCents = 0;
}