namespace BioscoopCasus.Web.Services;

public class MysteryShowtimeService
{
    private readonly HashSet<int> _mysteryShowtimeIds = [];

    public bool IsMystery(int showtimeId)
        => _mysteryShowtimeIds.Contains(showtimeId);

    public void Toggle(int showtimeId)
    {
        if (!_mysteryShowtimeIds.Remove(showtimeId))
            _mysteryShowtimeIds.Add(showtimeId);
    }

    public IReadOnlySet<int> GetAll() => _mysteryShowtimeIds;
}