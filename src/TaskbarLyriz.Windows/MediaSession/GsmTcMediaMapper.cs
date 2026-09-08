using TaskbarLyriz.Core.Media;
using Windows.Media.Control;

namespace TaskbarLyriz.Windows.MediaSession;

internal static class GsmTcMediaMapper
{
    internal static MediaTrack MapTrack(
        GlobalSystemMediaTransportControlsSessionMediaProperties properties,
        MediaArtwork? artwork) => new()
        {
            Title = properties.Title?.Trim() ?? string.Empty,
            Artist = properties.Artist?.Trim() ?? string.Empty,
            Album = properties.AlbumTitle?.Trim() ?? string.Empty,
            AlbumArtist = properties.AlbumArtist?.Trim() ?? string.Empty,
            TrackNumber = (uint)Math.Max(0, properties.TrackNumber),
            Artwork = artwork,
        };

    internal static MediaPlayback MapPlayback(
        GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo,
        GlobalSystemMediaTransportControlsSessionTimelineProperties timeline)
    {
        var start = timeline.StartTime;
        var end = timeline.EndTime;
        var duration = end > start ? end - start : TimeSpan.Zero;
        var position = timeline.Position >= start ? timeline.Position - start : timeline.Position;
        position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        position = duration > TimeSpan.Zero && position > duration ? duration : position;

        var controls = playbackInfo.Controls;
        return new MediaPlayback
        {
            Status = MapStatus(playbackInfo.PlaybackStatus),
            Position = position,
            Duration = duration,
            PositionUpdatedAtUtc = timeline.LastUpdatedTime,
            PlaybackRate = playbackInfo.PlaybackRate is > 0 ? playbackInfo.PlaybackRate.Value : 1.0,
            Capabilities = new MediaPlaybackCapabilities
            {
                CanPlay = controls.IsPlayEnabled || controls.IsPlayPauseToggleEnabled,
                CanPause = controls.IsPauseEnabled || controls.IsPlayPauseToggleEnabled,
                CanStop = controls.IsStopEnabled,
                CanSeek = controls.IsPlaybackPositionEnabled,
                CanSkipNext = controls.IsNextEnabled,
                CanSkipPrevious = controls.IsPreviousEnabled,
            },
        };
    }

    internal static MediaPlaybackStatus MapStatus(
        GlobalSystemMediaTransportControlsSessionPlaybackStatus status) => status switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened => MediaPlaybackStatus.Opened,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => MediaPlaybackStatus.Changing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => MediaPlaybackStatus.Stopped,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MediaPlaybackStatus.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MediaPlaybackStatus.Paused,
            _ => MediaPlaybackStatus.Closed,
        };
}
