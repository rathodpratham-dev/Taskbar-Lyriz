namespace TaskbarLyriz.Core.Media;

public static class MediaSessionSelector
{
    public static MediaSessionSnapshot? SelectCurrent(
        IReadOnlyList<MediaSessionSnapshot> sessions,
        string? preferredSessionId)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        MediaSessionSnapshot? preferred = null;
        if (!string.IsNullOrWhiteSpace(preferredSessionId))
        {
            preferred = sessions.FirstOrDefault(
                session => string.Equals(
                    session.SessionId,
                    preferredSessionId,
                    StringComparison.Ordinal));
            if (preferred?.Playback.Status == MediaPlaybackStatus.Playing)
            {
                return preferred;
            }
        }

        var playing = sessions
            .Where(session => session.Playback.Status == MediaPlaybackStatus.Playing)
            .OrderByDescending(session => session.LastUpdatedAtUtc)
            .FirstOrDefault();
        if (playing is not null)
        {
            return playing;
        }

        if (preferred is not null)
        {
            return preferred;
        }

        return sessions
            .OrderBy(session => GetStatusPriority(session.Playback.Status))
            .ThenByDescending(session => session.LastUpdatedAtUtc)
            .FirstOrDefault();
    }

    private static int GetStatusPriority(MediaPlaybackStatus status) => status switch
    {
        MediaPlaybackStatus.Playing => 0,
        MediaPlaybackStatus.Paused => 1,
        MediaPlaybackStatus.Changing => 2,
        MediaPlaybackStatus.Opened => 3,
        MediaPlaybackStatus.Stopped => 4,
        _ => 5,
    };
}
