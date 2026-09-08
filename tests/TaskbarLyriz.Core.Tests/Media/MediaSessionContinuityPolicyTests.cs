using TaskbarLyriz.Core.Media;

namespace TaskbarLyriz.Core.Tests.Media;

[TestClass]
public sealed class MediaSessionContinuityPolicyTests
{
    [TestMethod]
    public void ShouldDeferUnstableCandidate_WithoutDisplayedLyrics_ReturnsFalse()
    {
        Assert.IsFalse(MediaSessionContinuityPolicy.ShouldDeferUnstableCandidate(
            hasSynchronizedLyrics: false,
            candidate: null));
    }

    [TestMethod]
    public void ShouldDeferUnstableCandidate_MissingSession_ReturnsTrue()
    {
        Assert.IsTrue(MediaSessionContinuityPolicy.ShouldDeferUnstableCandidate(
            hasSynchronizedLyrics: true,
            candidate: null));
    }

    [TestMethod]
    [DataRow(MediaPlaybackStatus.Closed)]
    [DataRow(MediaPlaybackStatus.Opened)]
    [DataRow(MediaPlaybackStatus.Changing)]
    [DataRow(MediaPlaybackStatus.Stopped)]
    public void ShouldDeferUnstableCandidate_NonPresentingState_ReturnsTrue(
        MediaPlaybackStatus status)
    {
        Assert.IsTrue(MediaSessionContinuityPolicy.ShouldDeferUnstableCandidate(
            hasSynchronizedLyrics: true,
            Create(status, "Track", "Artist")));
    }

    [TestMethod]
    [DataRow(MediaPlaybackStatus.Playing)]
    [DataRow(MediaPlaybackStatus.Paused)]
    public void ShouldDeferUnstableCandidate_StableSession_ReturnsFalse(
        MediaPlaybackStatus status)
    {
        Assert.IsFalse(MediaSessionContinuityPolicy.ShouldDeferUnstableCandidate(
            hasSynchronizedLyrics: true,
            Create(status, "Track", "Artist")));
    }

    [TestMethod]
    public void ShouldDeferUnstableCandidate_MissingMetadata_ReturnsTrue()
    {
        Assert.IsTrue(MediaSessionContinuityPolicy.ShouldDeferUnstableCandidate(
            hasSynchronizedLyrics: true,
            Create(MediaPlaybackStatus.Playing, string.Empty, string.Empty)));
    }

    private static MediaSessionSnapshot Create(
        MediaPlaybackStatus status,
        string title,
        string artist) => new()
        {
            SessionId = "session",
            SourceApplicationId = "player.exe",
            SourceApplicationName = "Player",
            Track = new MediaTrack { Title = title, Artist = artist },
            Playback = new MediaPlayback { Status = status },
        };
}
