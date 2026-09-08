namespace TaskbarLyriz.Core.Configuration;

public static class AppSettingsValidator
{
    public static AppSettings Normalize(AppSettings? settings)
    {
        settings ??= new AppSettings();

        var general = settings.General ?? new GeneralSettings();
        var taskbar = settings.TaskbarLyrics ?? new TaskbarLyricsSettings();
        var appearance = settings.Appearance ?? new AppearanceSettings();
        var lyrics = settings.Lyrics ?? new LyricsSettings();

        var width = Math.Clamp(taskbar.Width, 160, 1_200);
        var maximumWidth = Math.Clamp(taskbar.MaximumWidth, width, 2_400);
        var position = settings.SchemaVersion < 3
            ? TaskbarPosition.Left
            : taskbar.Position is TaskbarPosition.Left or TaskbarPosition.Right
                ? taskbar.Position
                : TaskbarPosition.Left;

        return settings with
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            General = general,
            TaskbarLyrics = taskbar with
            {
                Width = width,
                MaximumWidth = maximumWidth,
                LineCount = Math.Clamp(taskbar.LineCount, 1, 3),
                Position = position,
            },
            Appearance = appearance with
            {
                FontFamily = NormalizeFontFamily(appearance.FontFamily),
                FontSize = Math.Clamp(appearance.FontSize, 10, 36),
                Opacity = Math.Clamp(appearance.Opacity, 0.4, 1.0),
                CornerRadius = Math.Clamp(appearance.CornerRadius, 0, 32),
            },
            Lyrics = lyrics with
            {
                TimingOffsetMilliseconds = Math.Clamp(
                    lyrics.TimingOffsetMilliseconds,
                    -10_000,
                    10_000),
            },
        };
    }

    private static string NormalizeFontFamily(string? fontFamily)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            return AppearanceSettings.DefaultFontFamily;
        }

        var normalized = fontFamily.Trim();
        return normalized.Length <= 100
            ? normalized
            : normalized[..100];
    }
}
