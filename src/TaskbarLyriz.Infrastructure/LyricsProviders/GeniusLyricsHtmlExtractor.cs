using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace TaskbarLyriz.Infrastructure.LyricsProviders;

internal static partial class GeniusLyricsHtmlExtractor
{
    internal static string? Extract(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var output = new StringBuilder();
        var searchIndex = 0;
        while (searchIndex < html.Length)
        {
            var marker = LyricsContainerPattern().Match(html, searchIndex);
            if (!marker.Success)
            {
                break;
            }

            var openingTagStart = html.LastIndexOf('<', marker.Index);
            var contentStart = html.IndexOf('>', marker.Index + marker.Length);
            if (openingTagStart < 0 || contentStart < 0 ||
                !html.AsSpan(openingTagStart + 1).StartsWith("div", StringComparison.OrdinalIgnoreCase))
            {
                searchIndex = marker.Index + marker.Length;
                continue;
            }

            contentStart++;
            var depth = 1;
            var cursor = contentStart;
            while (cursor < html.Length && depth > 0)
            {
                var tagStart = html.IndexOf('<', cursor);
                if (tagStart < 0)
                {
                    break;
                }

                if (tagStart > cursor)
                {
                    output.Append(html, cursor, tagStart - cursor);
                }

                var tagEnd = html.IndexOf('>', tagStart + 1);
                if (tagEnd < 0)
                {
                    break;
                }

                var tag = html.AsSpan(tagStart + 1, tagEnd - tagStart - 1).Trim();
                if (tag.StartsWith("/div", StringComparison.OrdinalIgnoreCase))
                {
                    depth--;
                    if (depth == 0)
                    {
                        output.AppendLine();
                        searchIndex = tagEnd + 1;
                        break;
                    }
                }
                else if (tag.StartsWith("div", StringComparison.OrdinalIgnoreCase))
                {
                    depth++;
                }
                else if (tag.StartsWith("br", StringComparison.OrdinalIgnoreCase))
                {
                    output.AppendLine();
                }

                cursor = tagEnd + 1;
            }

            if (depth > 0)
            {
                break;
            }
        }

        var decoded = WebUtility.HtmlDecode(output.ToString()).Replace('\u00A0', ' ');
        var lines = new List<string>();
        foreach (var rawLine in decoded.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = HorizontalWhitespacePattern().Replace(rawLine, " ").Trim();
            if (line.Equals("Embed", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("You might also like", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            line = EmbedSuffixPattern().Replace(line, string.Empty).Trim();
            if (line.Length == 0)
            {
                if (lines.Count > 0 && lines[^1].Length > 0)
                {
                    lines.Add(string.Empty);
                }

                continue;
            }

            lines.Add(line);
        }

        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var result = string.Join(Environment.NewLine, lines).Trim();
        return result.Length == 0 ? null : result;
    }

    [GeneratedRegex(
        "data-lyrics-container\\s*=\\s*[\\\"']true[\\\"']",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LyricsContainerPattern();

    [GeneratedRegex(@"[\t\f\v ]+", RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalWhitespacePattern();

    [GeneratedRegex(@"\d+\s*Embed$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmbedSuffixPattern();
}
