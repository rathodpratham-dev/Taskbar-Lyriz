namespace TaskbarLyriz.Core.Lyrics;

public sealed record LyricsQuery(
    string Title,
    string Artist,
    string? Album = null,
    TimeSpan? Duration = null)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Artist);

    public string CacheKey =>
        $"{LyricsText.Normalize(Artist)}::{LyricsText.Normalize(Title)}::{LyricsText.Normalize(Album)}";
}

public sealed record LyricsLine(
    TimeSpan Timestamp,
    string Text,
    TimeSpan? Duration = null);

public sealed record LrcDocument(
    IReadOnlyList<LyricsLine> Lines,
    IReadOnlyDictionary<string, string> Metadata,
    TimeSpan Offset)
{
    public static LrcDocument Empty { get; } = new(
        Array.Empty<LyricsLine>(),
        new Dictionary<string, string>(),
        TimeSpan.Zero);
}

public sealed record LyricsDocument
{
    public required string TrackTitle { get; init; }

    public required string Artist { get; init; }

    public string? Album { get; init; }

    public string? PlainLyrics { get; init; }

    public string? SyncedLyrics { get; init; }

    public required string Source { get; init; }

    public string? SourceId { get; init; }

    public TimeSpan? Duration { get; init; }

    public IReadOnlyList<LyricsLine> Lines { get; init; } = Array.Empty<LyricsLine>();

    public DateTimeOffset FetchedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool IsSynced => Lines.Count > 0 && !string.IsNullOrWhiteSpace(SyncedLyrics);
}

public sealed record LyricsProviderResponse
{
    public required string TrackTitle { get; init; }

    public required string Artist { get; init; }

    public string? Album { get; init; }

    public string? PlainLyrics { get; init; }

    public string? SyncedLyrics { get; init; }

    public string? SourceId { get; init; }

    public TimeSpan? Duration { get; init; }

    public bool HasLyrics =>
        !string.IsNullOrWhiteSpace(SyncedLyrics) ||
        !string.IsNullOrWhiteSpace(PlainLyrics);
}

public sealed record LyricsCandidate(
    string TrackTitle,
    string Artist,
    string? Album,
    TimeSpan? Duration,
    bool HasSyncedLyrics);

public sealed record LyricsLookupResult(
    LyricsLookupStatus Status,
    LyricsDocument? Lyrics = null,
    bool FromCache = false,
    string? Message = null)
{
    public static LyricsLookupResult Found(LyricsDocument lyrics, bool fromCache = false) =>
        new(LyricsLookupStatus.Found, lyrics, fromCache);

    public static LyricsLookupResult NotFound(string? message = null) =>
        new(LyricsLookupStatus.NotFound, Message: message);
}

public enum LyricsLookupStatus
{
    Found,
    NotFound,
    InvalidQuery,
    Unavailable,
}

public sealed record LyricsCacheStatistics(long EntryCount, long SizeBytes, string DatabasePath);
