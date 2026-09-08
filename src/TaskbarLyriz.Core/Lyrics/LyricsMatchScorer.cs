namespace TaskbarLyriz.Core.Lyrics;

public static class LyricsMatchScorer
{
    public static double Score(LyricsQuery query, LyricsCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidate);

        var title = Similarity(query.Title, candidate.TrackTitle);
        var artist = Similarity(query.Artist, candidate.Artist);
        var syncedBonus = candidate.HasSyncedLyrics ? 0.05 : 0;

        if (query.Duration is { } queryDuration &&
            candidate.Duration is { } candidateDuration &&
            queryDuration > TimeSpan.Zero &&
            candidateDuration > TimeSpan.Zero)
        {
            var difference = Math.Abs((queryDuration - candidateDuration).TotalSeconds);
            var duration = Math.Max(0, 1 - (difference / Math.Max(10, queryDuration.TotalSeconds)));
            return Math.Clamp((title * 0.60) + (artist * 0.25) + (duration * 0.10) + syncedBonus, 0, 1);
        }

        return Math.Clamp((title * 0.68) + (artist * 0.27) + syncedBonus, 0, 1);
    }

    public static double Similarity(string? left, string? right)
    {
        var normalizedLeft = LyricsText.Normalize(left);
        var normalizedRight = LyricsText.Normalize(right);
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return 0;
        }

        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
        {
            return 1;
        }

        var distance = LevenshteinDistance(normalizedLeft, normalizedRight);
        return 1 - ((double)distance / Math.Max(normalizedLeft.Length, normalizedRight.Length));
    }

    private static int LevenshteinDistance(string left, string right)
    {
        if (left.Length > right.Length)
        {
            (left, right) = (right, left);
        }

        var previous = new int[left.Length + 1];
        var current = new int[left.Length + 1];
        for (var index = 0; index <= left.Length; index++)
        {
            previous[index] = index;
        }

        for (var row = 1; row <= right.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= left.Length; column++)
            {
                var substitutionCost = left[column - 1] == right[row - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[left.Length];
    }
}
