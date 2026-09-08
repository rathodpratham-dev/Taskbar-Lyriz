using TaskbarLyriz.Core.Lyrics;
using TaskbarLyriz.Infrastructure.LyricsProviders;
using TaskbarLyriz.Infrastructure.Tests.TestSupport;

namespace TaskbarLyriz.Infrastructure.Tests.LyricsProviders;

[TestClass]
public sealed class GeniusLyricsProviderTests
{
    [TestMethod]
    public async Task FindAsync_MatchingResult_ExtractsNestedLyricsContainers()
    {
        var handler = new RoutingHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.StartsWith("/api/search", StringComparison.Ordinal))
            {
                return RoutingHttpMessageHandler.Json("""
                    {
                      "response": {
                        "sections": [{
                          "hits": [{
                            "result": {
                              "title": "Example Song",
                              "primary_artist": { "name": "Example Artist" },
                              "url": "https://genius.com/example-lyrics"
                            }
                          }]
                        }]
                      }
                    }
                    """);
            }

            return RoutingHttpMessageHandler.Html("""
                <html><div data-lyrics-container="true">[Verse]<br>First &amp; second<div>Nested<br>line</div></div></html>
                """);
        });
        using var client = new HttpClient(handler);
        var provider = new GeniusLyricsProvider(client);

        var result = await provider.FindAsync(new LyricsQuery("Example Song", "Example Artist"));

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.PlainLyrics);
        StringAssert.Contains(result.PlainLyrics, "First & second");
        StringAssert.Contains(result.PlainLyrics, "Nested");
        Assert.HasCount(2, handler.Requests);
    }

    [TestMethod]
    public async Task FindAsync_NonGeniusResult_DoesNotFetchPage()
    {
        var handler = new RoutingHttpMessageHandler(_ => RoutingHttpMessageHandler.Json("""
            {
              "response": {
                "sections": [{
                  "hits": [{
                    "result": {
                      "title": "Example Song",
                      "primary_artist": { "name": "Example Artist" },
                      "url": "https://example.com/not-allowed"
                    }
                  }]
                }]
              }
            }
            """));
        using var client = new HttpClient(handler);
        var provider = new GeniusLyricsProvider(client);

        var result = await provider.FindAsync(new LyricsQuery("Example Song", "Example Artist"));

        Assert.IsNull(result);
        Assert.HasCount(1, handler.Requests);
    }
}
