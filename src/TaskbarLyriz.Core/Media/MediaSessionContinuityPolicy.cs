namespace TaskbarLyriz.Core.Media;

public static class MediaSessionContinuityPolicy
{
    public static bool ShouldDeferUnstableCandidate(
        bool hasSynchronizedLyrics,
        MediaSessionSnapshot? candidate)
    {
        if (!hasSynchronizedLyrics)
        {
            return false;
        }

        if (candidate is null ||
            candidate.Playback.Status is not (MediaPlaybackStatus.Playing or MediaPlaybackStatus.Paused))
        {
            return true;
        }

        var track = candidate.Track;
        var artist = string.IsNullOrWhiteSpace(track.Artist)
            ? track.AlbumArtist
            : track.Artist;
        return string.IsNullOrWhiteSpace(track.Title) || string.IsNullOrWhiteSpace(artist);
    }
}
