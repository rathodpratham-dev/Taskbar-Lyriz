using TaskbarLyriz.Core.Lyrics;

namespace TaskbarLyriz.Core.Abstractions;

public interface ILrcParser
{
    LrcDocument Parse(string? lrcText);
}

public interface ILyricsProvider
{
    string Id { get; }

    string DisplayName { get; }

    int Priority { get; }

    Task<LyricsProviderResponse?> FindAsync(
        LyricsQuery query,
        CancellationToken cancellationToken = default);
}

public interface ILyricsCache
{
    Task<LyricsDocument?> GetAsync(
        LyricsQuery query,
        CancellationToken cancellationToken = default);

    Task StoreAsync(
        LyricsQuery query,
        LyricsDocument lyrics,
        CancellationToken cancellationToken = default);

    Task<LyricsCacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface ILyricsService
{
    Task<LyricsLookupResult> FindAsync(
        LyricsQuery query,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}

public interface ILyricsSynchronizationService
{
    void SetLines(IEnumerable<LyricsLine>? lines);

    LyricsSynchronizationState Synchronize(
        TimeSpan playbackPosition,
        TimeSpan timingOffset = default);
}
