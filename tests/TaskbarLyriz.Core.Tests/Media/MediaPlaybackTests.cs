using TaskbarLyriz.Core.Media;

namespace TaskbarLyriz.Core.Tests.Media;

[TestClass]
public sealed class MediaPlaybackTests
{
    [TestMethod]
    public void EstimatePosition_WhenPlaying_AdvancesUsingPlaybackRate()
    {
        var updatedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var playback = new MediaPlayback
        {
            Status = MediaPlaybackStatus.Playing,
            Position = TimeSpan.FromSeconds(10),
            Duration = TimeSpan.FromMinutes(3),
            PositionUpdatedAtUtc = updatedAt,
            PlaybackRate = 1.5,
        };

        var result = playback.EstimatePosition(updatedAt.AddSeconds(5));

        Assert.AreEqual(TimeSpan.FromSeconds(17.5), result);
    }

    [TestMethod]
    public void EstimatePosition_WhenPaused_DoesNotAdvance()
    {
        var updatedAt = DateTimeOffset.UtcNow;
        var playback = new MediaPlayback
        {
            Status = MediaPlaybackStatus.Paused,
            Position = TimeSpan.FromSeconds(42),
            Duration = TimeSpan.FromMinutes(3),
            PositionUpdatedAtUtc = updatedAt,
        };

        var result = playback.EstimatePosition(updatedAt.AddMinutes(1));

        Assert.AreEqual(TimeSpan.FromSeconds(42), result);
    }

    [TestMethod]
    public void EstimatePosition_WhenEstimateExceedsDuration_ClampsToDuration()
    {
        var updatedAt = DateTimeOffset.UtcNow;
        var playback = new MediaPlayback
        {
            Status = MediaPlaybackStatus.Playing,
            Position = TimeSpan.FromSeconds(58),
            Duration = TimeSpan.FromMinutes(1),
            PositionUpdatedAtUtc = updatedAt,
        };

        var result = playback.EstimatePosition(updatedAt.AddSeconds(10));

        Assert.AreEqual(TimeSpan.FromMinutes(1), result);
    }
}
