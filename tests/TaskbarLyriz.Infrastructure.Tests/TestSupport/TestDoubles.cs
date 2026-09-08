using System.Net;
using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Lyrics;

namespace TaskbarLyriz.Infrastructure.Tests.TestSupport;

internal sealed class RecordingLogger : IAppLogger
{
    public List<string> Entries { get; } = [];

    public void Write(
        AppLogLevel level,
        string eventName,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? properties = null) => Entries.Add(eventName);

    public void Dispose()
    {
    }
}

internal sealed class RoutingHttpMessageHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request.RequestUri!);
        return Task.FromResult(responseFactory(request));
    }

    internal static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    internal static HttpResponseMessage Html(string html) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html"),
        };
}

internal sealed class MemoryLyricsCache : ILyricsCache
{
    public LyricsDocument? Value { get; set; }

    public int GetCalls { get; private set; }

    public int StoreCalls { get; private set; }

    public Task<LyricsDocument?> GetAsync(LyricsQuery query, CancellationToken cancellationToken = default)
    {
        GetCalls++;
        return Task.FromResult(Value);
    }

    public Task StoreAsync(
        LyricsQuery query,
        LyricsDocument lyrics,
        CancellationToken cancellationToken = default)
    {
        StoreCalls++;
        Value = lyrics;
        return Task.CompletedTask;
    }

    public Task<LyricsCacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new LyricsCacheStatistics(Value is null ? 0 : 1, 0, "memory"));

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Value = null;
        return Task.CompletedTask;
    }
}

internal sealed class StubLyricsProvider(
    string id,
    int priority,
    Func<LyricsQuery, CancellationToken, Task<LyricsProviderResponse?>> find) : ILyricsProvider
{
    public string Id => id;

    public string DisplayName => id;

    public int Priority => priority;

    public int Calls { get; private set; }

    public Task<LyricsProviderResponse?> FindAsync(
        LyricsQuery query,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        return find(query, cancellationToken);
    }
}
