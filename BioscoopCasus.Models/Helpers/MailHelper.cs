using System.Net;
using System.Text.RegularExpressions;

namespace BioscoopCasus.Models.Helpers;

public partial class MailHelper
{
    public class MailVariableDefinition
    {
        public MailVariableDefinition()
        {
        }

        public MailVariableDefinition(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
    
    public static string Render(string templateHtml, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(templateHtml))
        {
            return string.Empty;
        }

        return VariableRegex().Replace(templateHtml, match =>
        {
            var key = match.Groups["key"].Value;

            return values.TryGetValue(key, out var value)
                ? WebUtility.HtmlEncode(value)
                : match.Value;
        });
    }

    public static string BuildVariableTokenHtml(string key)
    {
        var encodedKey = WebUtility.HtmlEncode(key);
        return $"<span class=\"mail-merge-token\" contenteditable=\"false\" data-token=\"{encodedKey}\">{{{{{encodedKey}}}}}</span>";
    }

    public static string BuildImageHtml(string imageUrl, string? altText)
    {
        var encodedUrl = WebUtility.HtmlEncode(imageUrl);
        var encodedAltText = WebUtility.HtmlEncode(altText ?? string.Empty);

        return $"<img src=\"{encodedUrl}\" alt=\"{encodedAltText}\" class=\"mail-editor-image\" />";
    }

    public static string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var trimmedUrl = url.Trim();

        if (!trimmedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmedUrl = $"https://{trimmedUrl}";
        }

        return Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri)
            ? uri.ToString()
            : string.Empty;
    }

    [GeneratedRegex(@"\{\{(?<key>[A-Za-z0-9_]+)\}\}")]
    private static partial Regex VariableRegex();
}