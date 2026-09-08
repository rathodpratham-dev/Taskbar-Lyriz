using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Configuration;
using TaskbarLyriz.Infrastructure.Settings;
using TaskbarLyriz.Infrastructure.Storage;

namespace TaskbarLyriz.Infrastructure.Tests.Settings;

[TestClass]
public sealed class JsonSettingsStoreTests
{
    private string _testRoot = null!;
    private RecordingLogger _logger = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "TaskbarLyriz.Tests", Guid.NewGuid().ToString("N"));
        _logger = new RecordingLogger();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadAsync_WhenMissing_CreatesDefaultsFile()
    {
        var paths = new AppPaths(_testRoot);
        var store = new JsonSettingsStore(paths, _logger);

        var result = await store.LoadAsync();

        Assert.IsTrue(File.Exists(paths.SettingsFile));
        Assert.IsTrue(result.General.MinimizeToTray);
        Assert.IsEmpty(_logger.Entries);
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_RoundTripsAndNormalizesSettings()
    {
        var paths = new AppPaths(_testRoot);
        var store = new JsonSettingsStore(paths, _logger);
        var settings = new AppSettings
        {
            General = new GeneralSettings { StartMinimized = true },
            TaskbarLyrics = new TaskbarLyricsSettings { Width = 80 },
            Appearance = new AppearanceSettings
            {
                Theme = AppTheme.Dark,
                FontFamily = "Consolas",
                FontSize = 18,
            },
            Lyrics = new LyricsSettings
            {
                TimingOffsetMilliseconds = 750,
                FollowCurrentLine = false,
            },
        };

        await store.SaveAsync(settings);
        var result = await store.LoadAsync();

        Assert.IsTrue(result.General.StartMinimized);
        Assert.AreEqual(160, result.TaskbarLyrics.Width);
        Assert.AreEqual(AppTheme.Dark, result.Appearance.Theme);
        Assert.AreEqual("Consolas", result.Appearance.FontFamily);
        Assert.AreEqual(18, result.Appearance.FontSize);
        Assert.AreEqual(750, result.Lyrics.TimingOffsetMilliseconds);
        Assert.IsFalse(result.Lyrics.FollowCurrentLine);
    }

    [TestMethod]
    public async Task LoadAsync_WhenJsonIsInvalid_ReturnsDefaultsAndLogsWarning()
    {
        var paths = new AppPaths(_testRoot);
        Directory.CreateDirectory(paths.ConfigurationDirectory);
        await File.WriteAllTextAsync(paths.SettingsFile, "{ definitely-not-json }");
        var store = new JsonSettingsStore(paths, _logger);

        var result = await store.LoadAsync();

        Assert.AreEqual(AppSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.HasCount(1, _logger.Entries);
        Assert.AreEqual("settings.load_failed", _logger.Entries[0]);
    }

    [TestMethod]
    public async Task LoadAsync_MilestoneThreeSettings_AddsSynchronizationDefaults()
    {
        var paths = new AppPaths(_testRoot);
        Directory.CreateDirectory(paths.ConfigurationDirectory);
        await File.WriteAllTextAsync(paths.SettingsFile, """
            {
              "schemaVersion": 1,
              "general": { "minimizeToTray": true },
              "taskbarLyrics": { "enabled": true },
              "appearance": { "theme": "System" }
            }
            """);
        var store = new JsonSettingsStore(paths, _logger);

        var result = await store.LoadAsync();

        Assert.AreEqual(AppSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.AreEqual(0, result.Lyrics.TimingOffsetMilliseconds);
        Assert.IsTrue(result.Lyrics.FollowCurrentLine);
    }

    [TestMethod]
    public async Task LoadAsync_PreMilestoneFiveSettings_MovesSurfaceToLeftTaskbarGapAndPersistsMigration()
    {
        var paths = new AppPaths(_testRoot);
        Directory.CreateDirectory(paths.ConfigurationDirectory);
        await File.WriteAllTextAsync(paths.SettingsFile, """
            {
              "schemaVersion": 2,
              "taskbarLyrics": { "enabled": true, "position": "Right", "width": 400 }
            }
            """);
        var store = new JsonSettingsStore(paths, _logger);

        var result = await store.LoadAsync();
        var persisted = await File.ReadAllTextAsync(paths.SettingsFile);

        Assert.AreEqual(AppSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.AreEqual(TaskbarPosition.Left, result.TaskbarLyrics.Position);
        StringAssert.Contains(persisted, $"\"SchemaVersion\": {AppSettings.CurrentSchemaVersion}");
        StringAssert.Contains(persisted, "\"Position\": \"Left\"");
    }

    private sealed class RecordingLogger : IAppLogger
    {
        public List<string> Entries { get; } = [];

        public void Write(
            AppLogLevel level,
            string eventName,
            string message,
            Exception? exception = null,
            IReadOnlyDictionary<string, object?>? properties = null) => Entries.Add(eventName);

        public void Dispose()
        {
        }
    }
}
