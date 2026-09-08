using System.Text;
using System.Text.Json;
using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Lyrics;
using TaskbarLyriz.Infrastructure.Http;

namespace TaskbarLyriz.Infrastructure.LyricsProviders;

public sealed class GeniusLyricsProvider : ILyricsProvider
{
    private const int MaximumJsonBytes = 4 * 1024 * 1024;
    private const int MaximumHtmlBytes = 6 * 1024 * 1024;
    private const double MinimumMatchScore = 0.55;
    private readonly HttpClient _httpClient;

    public GeniusLyricsProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Id => "genius";

    public string DisplayName => "Genius";

    public int Priority => 20;

    public async Task<LyricsProviderResponse?> FindAsync(
        LyricsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!query.IsValid)
        {
            return null;
        }

        var searchUri = new Uri(
            $"https://genius.com/api/search/multi?q={Uri.EscapeDataString($"{query.Artist} {query.Title}")}");
        using var searchResponse = await _httpClient.GetAsync(
            searchUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        searchResponse.EnsureSuccessStatusCode();
        var jsonBytes = await BoundedHttpContent.ReadBytesAsync(
            searchResponse.Content,
            MaximumJsonBytes,
            cancellationToken).ConfigureAwait(false);

        var candidate = FindBestCandidate(query, jsonBytes);
        if (candidate is null || candidate.Score < MinimumMatchScore || !IsAllowedGeniusUri(candidate.Uri))
        {
            return null;
        }

        using var lyricsResponse = await _httpClient.GetAsync(
            candidate.Uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        lyricsResponse.EnsureSuccessStatusCode();
        var htmlBytes = await BoundedHttpContent.ReadBytesAsync(
            lyricsResponse.Content,
            MaximumHtmlBytes,
            cancellationToken).ConfigureAwait(false);
        var html = Encoding.UTF8.GetString(htmlBytes);
        var lyrics = GeniusLyricsHtmlExtractor.Extract(html);
        if (string.IsNullOrWhiteSpace(lyrics))
        {
            return null;
        }

        return new LyricsProviderResponse
        {
            TrackTitle = candidate.TrackTitle,
            Artist = candidate.Artist,
            Album = query.Album,
            PlainLyrics = lyrics,
            SourceId = candidate.Uri.AbsoluteUri,
        };
    }

    private static Candidate? FindBestCandidate(LyricsQuery query, byte[] jsonBytes)
    {
        using var json = JsonDocument.Parse(jsonBytes);
        if (!json.RootElement.TryGetProperty("response", out var response) ||
            !response.TryGetProperty("sections", out var sections) ||
            sections.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        Candidate? best = null;
        foreach (var section in sections.EnumerateArray())
        {
            if (!section.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var hit in hits.EnumerateArray())
            {
                if (!hit.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var title = GetString(result, "title") ?? GetString(result, "full_title") ?? string.Empty;
                var artist = GetArtist(result);
                var url = GetString(result, "url");
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var path = GetString(result, "path");
                    if (string.IsNullOrWhiteSpace(path) ||
                        !Uri.TryCreate(new Uri("https://genius.com"), path, out uri))
                    {
                        continue;
                    }
                }

                var score = LyricsMatchScorer.Score(
                    query,
                    new LyricsCandidate(title, artist, null, null, HasSyncedLyrics: false));
                if (best is null || score > best.Score)
                {
                    best = new Candidate(title, artist, uri, score);
                }
            }
        }

        return best;
    }

    private static string GetArtist(JsonElement result)
    {
        if (result.TryGetProperty("primary_artist", out var primaryArtist) &&
            primaryArtist.ValueKind == JsonValueKind.Object)
        {
            return GetString(primaryArtist, "name") ?? string.Empty;
        }

        return GetString(result, "artist_names") ?? string.Empty;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool IsAllowedGeniusUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host.Equals("genius.com", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.EndsWith(".genius.com", StringComparison.OrdinalIgnoreCase));

    private sealed record Candidate(string TrackTitle, string Artist, Uri Uri, double Score);
}
