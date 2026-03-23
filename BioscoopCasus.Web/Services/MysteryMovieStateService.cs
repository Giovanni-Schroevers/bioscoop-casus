namespace BioscoopCasus.Web.Services;

public class MysteryMovieStateService
{
    public const decimal MysteryDiscount = 2.50m;

    public bool IsMystery { get; private set; }

    public void SetMystery() => IsMystery = true;
    public void Clear()      => IsMystery = false;
}