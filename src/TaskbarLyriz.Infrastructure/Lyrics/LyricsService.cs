using System.Collections.Concurrent;
using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Lyrics;

namespace TaskbarLyriz.Infrastructure.Lyrics;

public sealed class LyricsService : ILyricsService
{
    private static readonly TimeSpan NotFoundLifetime = TimeSpan.FromMinutes(15);
    private readonly IReadOnlyList<ILyricsProvider> _providers;
    private readonly ILyricsCache _cache;
    private readonly ILrcParser _parser;
    private readonly IAppLogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _notFound = new(StringComparer.Ordinal);

    public LyricsService(
        IEnumerable<ILyricsProvider> providers,
        ILyricsCache cache,
        ILrcParser parser,
        IAppLogger logger,
        TimeProvider timeProvider)
    {
        _providers = providers.OrderBy(provider => provider.Priority).ToArray();
        _cache = cache;
        _parser = parser;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<LyricsLookupResult> FindAsync(
        LyricsQuery query,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!query.IsValid)
        {
            return new LyricsLookupResult(
                LyricsLookupStatus.InvalidQuery,
                Message: "A track title and artist are required.");
        }

        if (!forceRefresh)
        {
            try
            {
                var cached = await _cache.GetAsync(query, cancellationToken).ConfigureAwait(false);
                if (cached is not null)
                {
                    _logger.Information("lyrics.cache_hit", "Lyrics were loaded from the local cache.");
                    return LyricsLookupResult.Found(cached, fromCache: true);
                }

                _logger.Information("lyrics.cache_miss", "No lyrics were found in the local cache.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.Warning(
                    "lyrics.cache_read_failed",
                    "The lyrics cache could not be read; providers will still be tried.",
                    exception);
            }

            if (_notFound.TryGetValue(query.CacheKey, out var missedAt) &&
                _timeProvider.GetUtcNow() - missedAt < NotFoundLifetime)
            {
                return LyricsLookupResult.NotFound("No lyrics were found recently. Use Refresh to try again.");
            }

            _notFound.TryRemove(query.CacheKey, out _);
        }

        var completedProviderRequest = false;
        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _logger.Write(
                    AppLogLevel.Information,
                    "lyrics.provider_requested",
                    "A lyrics provider request started.",
                    properties: new Dictionary<string, object?> { ["provider"] = provider.Id });
                var response = await provider.FindAsync(query, cancellationToken).ConfigureAwait(false);
                completedProviderRequest = true;
                var document = CreateDocument(query, provider, response);
                if (document is null)
                {
                    continue;
                }

                _notFound.TryRemove(query.CacheKey, out _);
                try
                {
                    await _cache.StoreAsync(query, document, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.Warning(
                        "lyrics.cache_write_failed",
                        "Downloaded lyrics could not be saved to the local cache.",
                        exception);
                }

                _logger.Write(
                    AppLogLevel.Information,
                    "lyrics.provider_succeeded",
                    "Lyrics were downloaded from a provider.",
                    properties: new Dictionary<string, object?>
                    {
                        ["provider"] = provider.Id,
                        ["synchronized"] = document.IsSynced,
                    });
                return LyricsLookupResult.Found(document);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.Write(
                    AppLogLevel.Warning,
                    "lyrics.provider_failed",
                    "A lyrics provider request failed; the next provider will be tried.",
                    exception,
                    new Dictionary<string, object?> { ["provider"] = provider.Id });
            }
        }

        RememberNotFound(query.CacheKey);
        _logger.Information("lyrics.lookup_not_found", "No matching lyrics were returned by the configured providers.");
        return completedProviderRequest
            ? LyricsLookupResult.NotFound("No matching lyrics were found.")
            : new LyricsLookupResult(
                LyricsLookupStatus.Unavailable,
                Message: "Lyrics providers are currently unavailable.");
    }

    private void RememberNotFound(string cacheKey)
    {
        var now = _timeProvider.GetUtcNow();
        _notFound[cacheKey] = now;
        if (_notFound.Count <= 512)
        {
            return;
        }

        foreach (var entry in _notFound.Where(entry => now - entry.Value >= NotFoundLifetime))
        {
            _notFound.TryRemove(entry.Key, out _);
        }

        if (_notFound.Count <= 512)
        {
            return;
        }

        foreach (var entry in _notFound.OrderBy(entry => entry.Value).Take(128))
        {
            _notFound.TryRemove(entry.Key, out _);
        }
    }

    private LyricsDocument? CreateDocument(
        LyricsQuery query,
        ILyricsProvider provider,
        LyricsProviderResponse? response)
    {
        if (response is null || !response.HasLyrics)
        {
            return null;
        }

        var synced = response.SyncedLyrics?.Trim();
        var plain = response.PlainLyrics?.Trim();
        var parsed = string.IsNullOrWhiteSpace(synced) ? LrcDocument.Empty : _parser.Parse(synced);
        if (parsed.Lines.Count == 0)
        {
            synced = null;
        }

        if (string.IsNullOrWhiteSpace(synced) && string.IsNullOrWhiteSpace(plain))
        {
            return null;
        }

        return new LyricsDocument
        {
            TrackTitle = string.IsNullOrWhiteSpace(response.TrackTitle)
                ? query.Title.Trim()
                : response.TrackTitle.Trim(),
            Artist = string.IsNullOrWhiteSpace(response.Artist)
                ? query.Artist.Trim()
                : response.Artist.Trim(),
            Album = string.IsNullOrWhiteSpace(response.Album) ? query.Album?.Trim() : response.Album.Trim(),
            PlainLyrics = string.IsNullOrWhiteSpace(plain) ? null : plain,
            SyncedLyrics = synced,
            Source = provider.DisplayName,
            SourceId = response.SourceId,
            Duration = response.Duration ?? query.Duration,
            Lines = parsed.Lines,
            FetchedAtUtc = _timeProvider.GetUtcNow(),
        };
    }
}
