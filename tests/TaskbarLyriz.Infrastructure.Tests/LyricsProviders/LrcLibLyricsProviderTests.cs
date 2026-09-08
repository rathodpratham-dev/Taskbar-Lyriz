using System.Net;
using TaskbarLyriz.Core.Lyrics;
using TaskbarLyriz.Infrastructure.LyricsProviders;
using TaskbarLyriz.Infrastructure.Tests.TestSupport;

namespace TaskbarLyriz.Infrastructure.Tests.LyricsProviders;

[TestClass]
public sealed class LrcLibLyricsProviderTests
{
    [TestMethod]
    public async Task FindAsync_ExactSynchronizedMatch_DoesNotSearch()
    {
        var handler = new RoutingHttpMessageHandler(_ => RoutingHttpMessageHandler.Json("""
            {
              "id": 42,
              "trackName": "Example Song",
              "artistName": "Example Artist",
              "duration": 180,
              "plainLyrics": "Line one",
              "syncedLyrics": "[00:01.00]Line one"
            }
            """));
        using var client = new HttpClient(handler);
        var provider = new LrcLibLyricsProvider(client);

        var result = await provider.FindAsync(new LyricsQuery(
            "Example Song", "Example Artist", Duration: TimeSpan.FromSeconds(180)));

        Assert.IsNotNull(result);
        Assert.AreEqual("[00:01.00]Line one", result.SyncedLyrics);
        Assert.HasCount(1, handler.Requests);
        StringAssert.Contains(handler.Requests[0].AbsoluteUri, "/api/get?");
    }

    [TestMethod]
    public async Task FindAsync_NullDuration_ReturnsLyricsWithoutDuration()
    {
        var handler = new RoutingHttpMessageHandler(_ => RoutingHttpMessageHandler.Json("""
            {
              "id": 43,
              "trackName": "Example Song",
              "artistName": "Example Artist",
              "duration": null,
              "plainLyrics": "Line one",
              "syncedLyrics": "[00:01.00]Line one"
            }
            """));
        using var client = new HttpClient(handler);
        var provider = new LrcLibLyricsProvider(client);

        var result = await provider.FindAsync(new LyricsQuery("Example Song", "Example Artist"));

        Assert.IsNotNull(result);
        Assert.IsNull(result.Duration);
        Assert.AreEqual("[00:01.00]Line one", result.SyncedLyrics);
        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    public async Task FindAsync_ExactPlainMatch_PrefersSynchronizedSearchMatch()
    {
        var handler = new RoutingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/get", StringComparison.Ordinal))
            {
                return RoutingHttpMessageHandler.Json("""
                    {
                      "id": 1,
                      "trackName": "Example Song",
                      "artistName": "Example Artist",
                      "plainLyrics": "Plain only",
                      "syncedLyrics": null
                    }
                    """);
            }

            return RoutingHttpMessageHandler.Json("""
                [{
                  "id": 2,
                  "trackName": "Example Song",
                  "artistName": "Example Artist",
                  "plainLyrics": "Line one",
                  "syncedLyrics": "[00:01.00]Line one"
                }]
                """);
        });
        using var client = new HttpClient(handler);
        var provider = new LrcLibLyricsProvider(client);

        var result = await provider.FindAsync(new LyricsQuery("Example Song", "Example Artist"));

        Assert.IsNotNull(result);
        Assert.AreEqual("2", result.SourceId);
        Assert.HasCount(2, handler.Requests);
    }

    [TestMethod]
    public async Task FindAsync_NotFoundAndEmptySearch_ReturnsNull()
    {
        var handler = new RoutingHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/get", StringComparison.Ordinal)
                ? RoutingHttpMessageHandler.Json("{}", HttpStatusCode.NotFound)
                : RoutingHttpMessageHandler.Json("[]"));
        using var client = new HttpClient(handler);
        var provider = new LrcLibLyricsProvider(client);

        var result = await provider.FindAsync(new LyricsQuery("Missing Song", "Missing Artist"));

        Assert.IsNull(result);
        Assert.HasCount(2, handler.Requests);
    }
}
