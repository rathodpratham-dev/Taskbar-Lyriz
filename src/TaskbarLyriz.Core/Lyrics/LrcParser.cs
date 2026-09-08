using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using TaskbarLyriz.Core.Abstractions;

namespace TaskbarLyriz.Core.Lyrics;

public sealed partial class LrcParser : ILrcParser
{
    public LrcDocument Parse(string? lrcText)
    {
        if (string.IsNullOrWhiteSpace(lrcText))
        {
            return LrcDocument.Empty;
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var offset = TimeSpan.Zero;
        var rawLines = lrcText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        foreach (var rawLine in rawLines)
        {
            var metadataMatch = MetadataPattern().Match(rawLine.Trim());
            if (!metadataMatch.Success)
            {
                continue;
            }

            var key = metadataMatch.Groups["key"].Value.Trim();
            var value = metadataMatch.Groups["value"].Value.Trim();
            metadata[key] = value;
            if (key.Equals("offset", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetMilliseconds))
            {
                offset = TimeSpan.FromMilliseconds(offsetMilliseconds);
            }
        }

        var parsed = new List<LyricsLine>();
        foreach (var rawLine in rawLines)
        {
            var matches = TimestampPattern().Matches(rawLine);
            if (matches.Count == 0)
            {
                continue;
            }

            var text = TimestampPattern().Replace(rawLine, string.Empty).Trim();
            foreach (Match match in matches)
            {
                if (!TryParseTimestamp(match, out var timestamp))
                {
                    continue;
                }

                timestamp += offset;
                parsed.Add(new LyricsLine(timestamp < TimeSpan.Zero ? TimeSpan.Zero : timestamp, text));
            }
        }

        var ordered = parsed.OrderBy(line => line.Timestamp).ToArray();
        var lines = new LyricsLine[ordered.Length];
        for (var index = 0; index < ordered.Length; index++)
        {
            var duration = index + 1 < ordered.Length
                ? ordered[index + 1].Timestamp - ordered[index].Timestamp
                : (TimeSpan?)null;
            lines[index] = ordered[index] with
            {
                Duration = duration.HasValue && duration.Value > TimeSpan.Zero ? duration : null,
            };
        }

        return new LrcDocument(
            Array.AsReadOnly(lines),
            new ReadOnlyDictionary<string, string>(metadata),
            offset);
    }

    private static bool TryParseTimestamp(Match match, out TimeSpan timestamp)
    {
        timestamp = TimeSpan.Zero;
        if (!int.TryParse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture, out var minutes) ||
            !int.TryParse(match.Groups["seconds"].Value, CultureInfo.InvariantCulture, out var seconds) ||
            seconds is < 0 or >= 60)
        {
            return false;
        }

        var fractionText = match.Groups["fraction"].Value;
        var milliseconds = 0;
        if (fractionText.Length > 0)
        {
            var normalizedFraction = fractionText.PadRight(3, '0')[..3];
            if (!int.TryParse(normalizedFraction, CultureInfo.InvariantCulture, out milliseconds))
            {
                return false;
            }
        }

        timestamp = TimeSpan.FromMinutes(minutes) +
            TimeSpan.FromSeconds(seconds) +
            TimeSpan.FromMilliseconds(milliseconds);
        return true;
    }

    [GeneratedRegex(
        @"\[(?<minutes>\d{1,3}):(?<seconds>\d{2})(?:\.(?<fraction>\d{1,3}))?\]",
        RegexOptions.CultureInvariant)]
    private static partial Regex TimestampPattern();

    [GeneratedRegex(
        @"^\[(?<key>[A-Za-z][A-Za-z0-9_-]*):(?<value>.*)\]$",
        RegexOptions.CultureInvariant)]
    private static partial Regex MetadataPattern();
}
