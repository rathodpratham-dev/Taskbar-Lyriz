using System.Globalization;
using System.Text.Json;
using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Lyrics;
using TaskbarLyriz.Infrastructure.Http;

namespace TaskbarLyriz.Infrastructure.LyricsProviders;

public sealed class LrcLibLyricsProvider : ILyricsProvider
{
    private const int MaximumResponseBytes = 4 * 1024 * 1024;
    private const double MinimumMatchScore = 0.55;
    private readonly HttpClient _httpClient;

    public LrcLibLyricsProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Id => "lrclib";

    public string DisplayName => "LRCLIB";

    public int Priority => 10;

    public async Task<LyricsProviderResponse?> FindAsync(
        LyricsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!query.IsValid)
        {
            return null;
        }

        var exact = await GetExactAsync(query, cancellationToken).ConfigureAwait(false);
        if (exact is { SyncedLyrics.Length: > 0 })
        {
            return exact;
        }

        IReadOnlyList<ScoredResponse> candidates;
        try
        {
            candidates = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch when (exact is not null)
        {
            return exact;
        }
        var bestSynced = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Response.SyncedLyrics))
            .OrderByDescending(candidate => candidate.Score)
            .FirstOrDefault();
        if (bestSynced is not null && bestSynced.Score >= MinimumMatchScore)
        {
            return bestSynced.Response;
        }

        if (exact is not null)
        {
            return exact;
        }

        var best = candidates.OrderByDescending(candidate => candidate.Score).FirstOrDefault();
        return best is not null && best.Score >= MinimumMatchScore ? best.Response : null;
    }

    private async Task<LyricsProviderResponse?> GetExactAsync(
        LyricsQuery query,
        CancellationToken cancellationToken)
    {
        var parameters = new List<KeyValuePair<string, string?>>
        {
            new("track_name", query.Title),
            new("artist_name", query.Artist),
        };
        if (!string.IsNullOrWhiteSpace(query.Album))
        {
            parameters.Add(new("album_name", query.Album));
        }

        if (query.Duration is { } duration && duration > TimeSpan.Zero)
        {
            parameters.Add(new(
                "duration",
                Math.Round(duration.TotalSeconds).ToString(CultureInfo.InvariantCulture)));
        }

        using var response = await _httpClient.GetAsync(
            BuildUri("https://lrclib.net/api/get", parameters),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var bytes = await BoundedHttpContent.ReadBytesAsync(
            response.Content,
            MaximumResponseBytes,
            cancellationToken).ConfigureAwait(false);
        using var json = JsonDocument.Parse(bytes);
        if (json.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var result = ParseRecord(json.RootElement);
        if (result is null)
        {
            return null;
        }

        var score = LyricsMatchScorer.Score(query, ToCandidate(result));
        return score >= MinimumMatchScore ? result : null;
    }

    private async Task<IReadOnlyList<ScoredResponse>> SearchAsync(
        LyricsQuery query,
        CancellationToken cancellationToken)
    {
        var uri = BuildUri(
            "https://lrclib.net/api/search",
            [new("q", $"{query.Artist} {query.Title}")]);
        using var response = await _httpClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await BoundedHttpContent.ReadBytesAsync(
            response.Content,
            MaximumResponseBytes,
            cancellationToken).ConfigureAwait(false);
        using var json = JsonDocument.Parse(bytes);
        if (json.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ScoredResponse>();
        }

        var results = new List<ScoredResponse>();
        foreach (var record in json.RootElement.EnumerateArray().Take(50))
        {
            if (record.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var parsed = ParseRecord(record);
            if (parsed is null)
            {
                continue;
            }

            results.Add(new ScoredResponse(
                parsed,
                LyricsMatchScorer.Score(query, ToCandidate(parsed))));
        }

        return results;
    }

    private static LyricsProviderResponse? ParseRecord(JsonElement record)
    {
        var synced = GetString(record, "syncedLyrics")?.Trim();
        var plain = GetString(record, "plainLyrics")?.Trim();
        if (string.IsNullOrWhiteSpace(synced) && string.IsNullOrWhiteSpace(plain))
        {
            return null;
        }

        var durationSeconds = GetDouble(record, "duration");
        return new LyricsProviderResponse
        {
            TrackTitle = GetString(record, "trackName")?.Trim() ?? string.Empty,
            Artist = GetString(record, "artistName")?.Trim() ?? string.Empty,
            Album = GetString(record, "albumName")?.Trim(),
            PlainLyrics = plain,
            SyncedLyrics = synced,
            Duration = durationSeconds is > 0 ? TimeSpan.FromSeconds(durationSeconds.Value) : null,
            SourceId = GetId(record),
        };
    }

    private static LyricsCandidate ToCandidate(LyricsProviderResponse response) => new(
        response.TrackTitle,
        response.Artist,
        response.Album,
        response.Duration,
        !string.IsNullOrWhiteSpace(response.SyncedLyrics));

    private static string? GetString(JsonElement record, string propertyName) =>
        record.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? GetDouble(JsonElement record, string propertyName) =>
        record.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var number)
            ? number
            : null;

    private static string? GetId(JsonElement record, string propertyName = "id")
    {
        if (!record.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static Uri BuildUri(
        string baseUri,
        IEnumerable<KeyValuePair<string, string?>> parameters)
    {
        var query = string.Join(
            "&",
            parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                .Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!)}"));
        return new Uri($"{baseUri}?{query}", UriKind.Absolute);
    }

    private sealed record ScoredResponse(LyricsProviderResponse Response, double Score);
}
