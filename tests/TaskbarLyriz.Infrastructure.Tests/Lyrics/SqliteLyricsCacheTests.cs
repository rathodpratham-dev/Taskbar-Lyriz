using TaskbarLyriz.Core.Lyrics;
using TaskbarLyriz.Infrastructure.Lyrics;
using TaskbarLyriz.Infrastructure.Storage;
using TaskbarLyriz.Infrastructure.Tests.TestSupport;

namespace TaskbarLyriz.Infrastructure.Tests.Lyrics;

[TestClass]
public sealed class SqliteLyricsCacheTests
{
    private string _testRoot = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "TaskbarLyriz.Tests", Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task StoreAndGetAsync_RoundTripsSynchronizedLyrics()
    {
        var paths = new AppPaths(_testRoot);
        var cache = CreateCache(paths);
        var query = new LyricsQuery("Example Song", "Example Artist", "Example Album");
        var lyrics = new LyricsDocument
        {
            TrackTitle = query.Title,
            Artist = query.Artist,
            Album = query.Album,
            PlainLyrics = "First line",
            SyncedLyrics = "[00:01.00]First line\n[00:03.50]Second line",
            Source = "LRCLIB",
            SourceId = "42",
            Duration = TimeSpan.FromSeconds(180),
            Lines = new LrcParser().Parse("[00:01.00]First line\n[00:03.50]Second line").Lines,
            FetchedAtUtc = DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
        };

        await cache.StoreAsync(query, lyrics);
        var result = await cache.GetAsync(new LyricsQuery("Example Song (Live)", "Example Artist", "Example Album"));
        var statistics = await cache.GetStatisticsAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual("LRCLIB", result.Source);
        Assert.AreEqual("42", result.SourceId);
        Assert.AreEqual(TimeSpan.FromSeconds(180), result.Duration);
        Assert.HasCount(2, result.Lines);
        Assert.AreEqual(TimeSpan.FromSeconds(3.5), result.Lines[1].Timestamp);
        Assert.AreEqual(1L, statistics.EntryCount);
        Assert.IsGreaterThan(0L, statistics.SizeBytes);
        Assert.IsTrue(File.Exists(paths.LyricsDatabaseFile));
    }

    [TestMethod]
    public async Task ClearAsync_RemovesLyricsButKeepsUsableDatabase()
    {
        var paths = new AppPaths(_testRoot);
        var cache = CreateCache(paths);
        var query = new LyricsQuery("Example Song", "Example Artist");
        await cache.StoreAsync(query, new LyricsDocument
        {
            TrackTitle = query.Title,
            Artist = query.Artist,
            PlainLyrics = "Words",
            Source = "Test",
        });

        await cache.ClearAsync();

        Assert.IsNull(await cache.GetAsync(query));
        Assert.AreEqual(0L, (await cache.GetStatisticsAsync()).EntryCount);
    }

    [TestMethod]
    public async Task Initialize_ImportsLegacyJsonOnceWithoutDeletingFile()
    {
        var paths = new AppPaths(_testRoot);
        Directory.CreateDirectory(paths.CacheDirectory);
        var legacyPath = Path.Combine(paths.CacheDirectory, "legacy.json");
        await File.WriteAllTextAsync(legacyPath, """
            {
              "song": {
                "title": "Legacy Song",
                "artist": "Legacy Artist",
                "album": "Legacy Album"
              },
              "lyrics": "[00:01.00]Old line",
              "is_lrc": true,
              "source": "lrclib"
            }
            """);
        var cache = CreateCache(paths);

        var result = await cache.GetAsync(new LyricsQuery("Legacy Song", "Legacy Artist", "Legacy Album"));

        Assert.IsNotNull(result);
        Assert.HasCount(1, result.Lines);
        Assert.IsTrue(File.Exists(legacyPath));
        Assert.AreEqual(1L, (await cache.GetStatisticsAsync()).EntryCount);
    }

    private static SqliteLyricsCache CreateCache(AppPaths paths) =>
        new(paths, new LrcParser(), new RecordingLogger());
}
