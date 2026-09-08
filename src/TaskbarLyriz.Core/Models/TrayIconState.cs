namespace TaskbarLyriz.Core.Models;

public sealed record TrayIconState(
    bool TaskbarLyricsEnabled,
    string CurrentSong = "Nothing playing");
