using TaskbarLyriz.Core.Media;

namespace TaskbarLyriz.Core.Tests.Media;

[TestClass]
public sealed class MediaSessionSelectorTests
{
    [TestMethod]
    public void SelectCurrent_WhenPreferredIsPaused_PrefersPlayingSession()
    {
        var sessions = new[]
        {
            Create("playing", MediaPlaybackStatus.Playing, DateTimeOffset.UtcNow),
            Create("preferred", MediaPlaybackStatus.Paused, DateTimeOffset.UtcNow.AddMinutes(-1)),
        };

        var result = MediaSessionSelector.SelectCurrent(sessions, "preferred");

        Assert.IsNotNull(result);
        Assert.AreEqual("playing", result.SessionId);
    }

    [TestMethod]
    public void SelectCurrent_WhenPreferredIsPlaying_UsesWindowsPreferredSession()
    {
        var sessions = new[]
        {
            Create("other-playing", MediaPlaybackStatus.Playing, DateTimeOffset.UtcNow),
            Create("preferred", MediaPlaybackStatus.Playing, DateTimeOffset.UtcNow.AddMinutes(-1)),
        };

        var result = MediaSessionSelector.SelectCurrent(sessions, "preferred");

        Assert.IsNotNull(result);
        Assert.AreEqual("preferred", result.SessionId);
    }

    [TestMethod]
    public void SelectCurrent_WhenNothingIsPlaying_UsesWindowsPreferredSession()
    {
        var sessions = new[]
        {
            Create("recent-paused", MediaPlaybackStatus.Paused, DateTimeOffset.UtcNow),
            Create("preferred", MediaPlaybackStatus.Paused, DateTimeOffset.UtcNow.AddMinutes(-1)),
        };

        var result = MediaSessionSelector.SelectCurrent(sessions, "preferred");

        Assert.IsNotNull(result);
        Assert.AreEqual("preferred", result.SessionId);
    }

    [TestMethod]
    public void SelectCurrent_WithoutPreferred_UsesPlayingSession()
    {
        var sessions = new[]
        {
            Create("paused", MediaPlaybackStatus.Paused, DateTimeOffset.UtcNow),
            Create("playing", MediaPlaybackStatus.Playing, DateTimeOffset.UtcNow.AddMinutes(-1)),
        };

        var result = MediaSessionSelector.SelectCurrent(sessions, preferredSessionId: null);

        Assert.IsNotNull(result);
        Assert.AreEqual("playing", result.SessionId);
    }

    [TestMethod]
    public void SelectCurrent_ForEqualStatuses_UsesMostRecentlyUpdatedSession()
    {
        var now = DateTimeOffset.UtcNow;
        var sessions = new[]
        {
            Create("older", MediaPlaybackStatus.Paused, now.AddMinutes(-2)),
            Create("newer", MediaPlaybackStatus.Paused, now),
        };

        var result = MediaSessionSelector.SelectCurrent(sessions, preferredSessionId: null);

        Assert.IsNotNull(result);
        Assert.AreEqual("newer", result.SessionId);
    }

    [TestMethod]
    public void SelectCurrent_WhenNoSessions_ReturnsNull()
    {
        var result = MediaSessionSelector.SelectCurrent([], preferredSessionId: null);

        Assert.IsNull(result);
    }

    private static MediaSessionSnapshot Create(
        string id,
        MediaPlaybackStatus status,
        DateTimeOffset updatedAt) => new()
        {
            SessionId = id,
            SourceApplicationId = $"{id}.exe",
            SourceApplicationName = id,
            Playback = new MediaPlayback { Status = status },
            LastUpdatedAtUtc = updatedAt,
        };
}
