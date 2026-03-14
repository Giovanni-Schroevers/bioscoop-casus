using System.Globalization;
using System.Resources;

namespace BioscoopCasus.Web.Resources;

public static class SharedResourceDataAnnotations
{
    private static readonly ResourceManager ResourceManager =
        new("BioscoopCasus.Web.Resources.SharedResource", typeof(SharedResourceDataAnnotations).Assembly);

    public static string Required =>
        ResourceManager.GetString(nameof(Required), CultureInfo.CurrentCulture)
        ?? nameof(Required);

    public static string MinLength =>
        ResourceManager.GetString(nameof(MinLength), CultureInfo.CurrentCulture)
        ?? nameof(MinLength);
}
