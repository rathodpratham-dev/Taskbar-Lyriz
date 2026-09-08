using TaskbarLyriz.Core.Media;

namespace TaskbarLyriz.Core.Taskbar;

public static class TaskbarLyricsVisibilityPolicy
{
    public static bool ShouldShow(
        bool enabled,
        bool hasDisplayContent,
        MediaPlaybackStatus playbackStatus) =>
        enabled &&
        hasDisplayContent &&
        playbackStatus is MediaPlaybackStatus.Playing or MediaPlaybackStatus.Paused;
}
