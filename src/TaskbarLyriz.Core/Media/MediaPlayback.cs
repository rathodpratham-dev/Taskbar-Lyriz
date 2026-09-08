namespace TaskbarLyriz.Core.Media;

public sealed record MediaPlayback
{
    public static MediaPlayback Empty { get; } = new();

    public MediaPlaybackStatus Status { get; init; } = MediaPlaybackStatus.Closed;

    public TimeSpan Position { get; init; }

    public TimeSpan Duration { get; init; }

    public DateTimeOffset PositionUpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public double PlaybackRate { get; init; } = 1.0;

    public MediaPlaybackCapabilities Capabilities { get; init; } = new();

    public TimeSpan EstimatePosition(DateTimeOffset nowUtc)
    {
        var position = Position;
        if (Status == MediaPlaybackStatus.Playing && PlaybackRate > 0)
        {
            var elapsed = nowUtc - PositionUpdatedAtUtc;
            if (elapsed > TimeSpan.Zero)
            {
                position += TimeSpan.FromTicks((long)(elapsed.Ticks * PlaybackRate));
            }
        }

        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return Duration > TimeSpan.Zero && position > Duration ? Duration : position;
    }
}

public sealed record MediaPlaybackCapabilities
{
    public bool CanPlay { get; init; }

    public bool CanPause { get; init; }

    public bool CanStop { get; init; }

    public bool CanSeek { get; init; }

    public bool CanSkipNext { get; init; }

    public bool CanSkipPrevious { get; init; }
}

public enum MediaPlaybackStatus
{
    Closed,
    Opened,
    Changing,
    Stopped,
    Playing,
    Paused,
}
