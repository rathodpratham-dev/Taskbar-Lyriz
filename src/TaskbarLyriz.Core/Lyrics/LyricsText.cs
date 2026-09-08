using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TaskbarLyriz.Core.Lyrics;

public static partial class LyricsText
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutQualifiers = QualifierPattern().Replace(value, " ");
        withoutQualifiers = withoutQualifiers.Replace("&", " and ", StringComparison.Ordinal);
        var decomposed = withoutQualifiers.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return WhitespacePattern().Replace(builder.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"\([^)]*\)|\[[^\]]*\]", RegexOptions.CultureInvariant)]
    private static partial Regex QualifierPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}
