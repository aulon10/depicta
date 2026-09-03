using System.Text.RegularExpressions;

namespace Depicta.Api.Vision;

/// <summary>
/// Enforces the alt-text contract on whatever the model returns, so a misbehaving model can
/// never push non-compliant text downstream. The prompt is the first line of defence; this
/// is the guarantee.
/// </summary>
public static partial class AltTextSanitizer
{
    public const int MaxLength = 125;

    [GeneratedRegex(@"^\s*(an?\s+)?(image|picture|photo(graph)?|graphic|illustration|icon)\s+(of|showing|depicting)\s+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingFillerRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    /// <summary>Clean + cap the model's alt text. Empty in → empty out (decorative image).</summary>
    public static string Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var s = WhitespaceRegex().Replace(raw.Trim(), " ");
        s = LeadingFillerRegex().Replace(s, string.Empty);

        // Capitalize first letter after stripping filler.
        if (s.Length > 0) s = char.ToUpperInvariant(s[0]) + s[1..];

        // Drop a single trailing period (alt text is a phrase, not a sentence).
        if (s.EndsWith('.') && !s.EndsWith("..")) s = s[..^1];

        if (s.Length > MaxLength)
        {
            // Trim to the last word boundary within the limit.
            var cut = s[..MaxLength];
            var lastSpace = cut.LastIndexOf(' ');
            s = (lastSpace > 40 ? cut[..lastSpace] : cut).TrimEnd();
        }
        return s;
    }

    /// <summary>True when the text satisfies the contract as-is.</summary>
    public static bool IsCompliant(string? alt) =>
        alt is null || (alt.Length <= MaxLength && !LeadingFillerRegex().IsMatch(alt));
}
