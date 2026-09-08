namespace TaskbarLyriz.Core.Media;

public sealed record MediaSessionSnapshot
{
    public required string SessionId { get; init; }

    public required string SourceApplicationId { get; init; }

    public required string SourceApplicationName { get; init; }

    public MediaTrack Track { get; init; } = MediaTrack.Empty;

    public MediaPlayback Playback { get; init; } = MediaPlayback.Empty;

    public DateTimeOffset LastUpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string DisplayTitle => string.IsNullOrWhiteSpace(Track.Title) ? "Unknown track" : Track.Title;
}
