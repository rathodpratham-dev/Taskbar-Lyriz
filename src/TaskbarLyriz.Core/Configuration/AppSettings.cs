namespace TaskbarLyriz.Core.Configuration;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 4;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public GeneralSettings General { get; init; } = new();

    public TaskbarLyricsSettings TaskbarLyrics { get; init; } = new();

    public AppearanceSettings Appearance { get; init; } = new();

    public LyricsSettings Lyrics { get; init; } = new();
}

public sealed record GeneralSettings
{
    public bool StartWithWindows { get; init; }

    public bool StartMinimized { get; init; }

    public bool MinimizeToTray { get; init; } = true;

    public bool NotificationsEnabled { get; init; } = true;
}

public sealed record TaskbarLyricsSettings
{
    public bool Enabled { get; init; } = true;

    public TaskbarPosition Position { get; init; } = TaskbarPosition.Left;

    public int Width { get; init; } = 420;

    public int MaximumWidth { get; init; } = 720;

    public int LineCount { get; init; } = 1;
}

public sealed record AppearanceSettings
{
    public const string DefaultFontFamily = "Segoe UI Variable Text";

    public AppTheme Theme { get; init; } = AppTheme.System;

    public string FontFamily { get; init; } = DefaultFontFamily;

    public int FontSize { get; init; } = 15;

    public double Opacity { get; init; } = 0.94;

    public int CornerRadius { get; init; } = 10;

    public LyricAnimation Animation { get; init; } = LyricAnimation.Fade;
}

public sealed record LyricsSettings
{
    public int TimingOffsetMilliseconds { get; init; }

    public bool FollowCurrentLine { get; init; } = true;
}

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public enum TaskbarPosition
{
    Left,
    Center,
    Right,
    Custom,
}

public enum LyricAnimation
{
    None,
    Fade,
    Slide,
    Karaoke,
}
