using TaskbarLyriz.Core.Lyrics;
using TaskbarLyriz.Infrastructure.Lyrics;
using TaskbarLyriz.Infrastructure.Tests.TestSupport;

namespace TaskbarLyriz.Infrastructure.Tests.Lyrics;

[TestClass]
public sealed class LyricsServiceTests
{
    [TestMethod]
    public async Task FindAsync_CacheHit_DoesNotCallProviders()
    {
        var cached = new LyricsDocument
        {
            TrackTitle = "Song",
            Artist = "Artist",
            PlainLyrics = "Cached words",
            Source = "LRCLIB",
        };
        var cache = new MemoryLyricsCache { Value = cached };
        var provider = Provider("unused", 1, _ => Response("Network words"));
        var service = CreateService([provider], cache);

        var result = await service.FindAsync(new LyricsQuery("Song", "Artist"));

        Assert.AreEqual(LyricsLookupStatus.Found, result.Status);
        Assert.IsTrue(result.FromCache);
        Assert.AreSame(cached, result.Lyrics);
        Assert.AreEqual(0, provider.Calls);
    }

    [TestMethod]
    public async Task FindAsync_OrdersProvidersAndStoresParsedResult()
    {
        var cache = new MemoryLyricsCache();
        var fallback = Provider("fallback", 20, _ => Response("Plain fallback"));
        var primary = new StubLyricsProvider(
            "primary",
            10,
            (_, _) => Task.FromResult<LyricsProviderResponse?>(new LyricsProviderResponse
            {
                TrackTitle = "Song",
                Artist = "Artist",
                SyncedLyrics = "[00:01.00]First\n[00:02.00]Second",
            }));
        var service = CreateService([fallback, primary], cache);

        var result = await service.FindAsync(new LyricsQuery("Song", "Artist"));

        Assert.AreEqual(LyricsLookupStatus.Found, result.Status);
        Assert.IsNotNull(result.Lyrics);
        Assert.IsTrue(result.Lyrics.IsSynced);
        Assert.HasCount(2, result.Lyrics.Lines);
        Assert.AreEqual("primary", result.Lyrics.Source);
        Assert.AreEqual(1, primary.Calls);
        Assert.AreEqual(0, fallback.Calls);
        Assert.AreEqual(1, cache.StoreCalls);
    }

    [TestMethod]
    public async Task FindAsync_WhenPrimaryThrows_TriesFallback()
    {
        var cache = new MemoryLyricsCache();
        var primary = new StubLyricsProvider(
            "primary",
            10,
            (_, _) => throw new HttpRequestException("Offline"));
        var fallback = Provider("fallback", 20, _ => Response("Fallback words"));
        var logger = new RecordingLogger();
        var service = CreateService([primary, fallback], cache, logger);

        var result = await service.FindAsync(new LyricsQuery("Song", "Artist"));

        Assert.AreEqual(LyricsLookupStatus.Found, result.Status);
        Assert.AreEqual("fallback", result.Lyrics!.Source);
        CollectionAssert.Contains(logger.Entries, "lyrics.provider_failed");
    }

    [TestMethod]
    public async Task FindAsync_RecentMiss_SkipsRepeatedProviderRequests()
    {
        var cache = new MemoryLyricsCache();
        var provider = new StubLyricsProvider(
            "empty",
            10,
            (_, _) => Task.FromResult<LyricsProviderResponse?>(null));
        var service = CreateService([provider], cache);
        var query = new LyricsQuery("Missing", "Artist");

        var first = await service.FindAsync(query);
        var second = await service.FindAsync(query);
        var forced = await service.FindAsync(query, forceRefresh: true);

        Assert.AreEqual(LyricsLookupStatus.NotFound, first.Status);
        Assert.AreEqual(LyricsLookupStatus.NotFound, second.Status);
        Assert.AreEqual(LyricsLookupStatus.NotFound, forced.Status);
        Assert.AreEqual(2, provider.Calls);
    }

    [TestMethod]
    public async Task FindAsync_InvalidQuery_DoesNotReadCache()
    {
        var cache = new MemoryLyricsCache();
        var service = CreateService([], cache);

        var result = await service.FindAsync(new LyricsQuery("Song", string.Empty));

        Assert.AreEqual(LyricsLookupStatus.InvalidQuery, result.Status);
        Assert.AreEqual(0, cache.GetCalls);
    }

    private static LyricsService CreateService(
        IEnumerable<StubLyricsProvider> providers,
        MemoryLyricsCache cache,
        RecordingLogger? logger = null) =>
        new(providers, cache, new LrcParser(), logger ?? new RecordingLogger(), TimeProvider.System);

    private static StubLyricsProvider Provider(
        string id,
        int priority,
        Func<LyricsQuery, LyricsProviderResponse?> response) =>
        new(id, priority, (query, _) => Task.FromResult(response(query)));

    private static LyricsProviderResponse Response(string plainLyrics) => new()
    {
        TrackTitle = "Song",
        Artist = "Artist",
        PlainLyrics = plainLyrics,
    };
}
