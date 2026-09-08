using TaskbarLyriz.Core.Configuration;

namespace TaskbarLyriz.Core.Tests.Configuration;

[TestClass]
public sealed class AppSettingsValidatorTests
{
    [TestMethod]
    public void Normalize_NullSettings_ReturnsProductDefaults()
    {
        var result = AppSettingsValidator.Normalize(null);

        Assert.AreEqual(AppSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.IsTrue(result.General.MinimizeToTray);
        Assert.IsTrue(result.TaskbarLyrics.Enabled);
        Assert.AreEqual(TaskbarPosition.Left, result.TaskbarLyrics.Position);
        Assert.AreEqual(AppTheme.System, result.Appearance.Theme);
        Assert.AreEqual(AppearanceSettings.DefaultFontFamily, result.Appearance.FontFamily);
        Assert.AreEqual(0, result.Lyrics.TimingOffsetMilliseconds);
        Assert.IsTrue(result.Lyrics.FollowCurrentLine);
    }

    [TestMethod]
    public void Normalize_OutOfRangeValues_ClampsToSupportedBounds()
    {
        var settings = new AppSettings
        {
            SchemaVersion = 99,
            TaskbarLyrics = new TaskbarLyricsSettings
            {
                Width = 20,
                MaximumWidth = 40,
                LineCount = 9,
            },
            Appearance = new AppearanceSettings
            {
                FontFamily = "   ",
                FontSize = 100,
                Opacity = 0.1,
                CornerRadius = -5,
            },
            Lyrics = new LyricsSettings { TimingOffsetMilliseconds = 50_000 },
        };

        var result = AppSettingsValidator.Normalize(settings);

        Assert.AreEqual(AppSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.AreEqual(160, result.TaskbarLyrics.Width);
        Assert.AreEqual(160, result.TaskbarLyrics.MaximumWidth);
        Assert.AreEqual(3, result.TaskbarLyrics.LineCount);
        Assert.AreEqual(AppearanceSettings.DefaultFontFamily, result.Appearance.FontFamily);
        Assert.AreEqual(36, result.Appearance.FontSize);
        Assert.AreEqual(0.4, result.Appearance.Opacity);
        Assert.AreEqual(0, result.Appearance.CornerRadius);
        Assert.AreEqual(10_000, result.Lyrics.TimingOffsetMilliseconds);
    }

    [TestMethod]
    public void Normalize_PreMilestoneFiveSettings_MigratesTaskbarPositionToLeftGap()
    {
        var result = AppSettingsValidator.Normalize(new AppSettings
        {
            SchemaVersion = 2,
            TaskbarLyrics = new TaskbarLyricsSettings { Position = TaskbarPosition.Right },
        });

        Assert.AreEqual(AppSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.AreEqual(TaskbarPosition.Left, result.TaskbarLyrics.Position);
    }

    [TestMethod]
    public void Normalize_CustomFontFamily_TrimsAndPreservesValue()
    {
        var result = AppSettingsValidator.Normalize(new AppSettings
        {
            Appearance = new AppearanceSettings { FontFamily = "  Consolas  " },
        });

        Assert.AreEqual("Consolas", result.Appearance.FontFamily);
    }
}
