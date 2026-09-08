namespace TaskbarLyriz.Infrastructure.Storage;

public sealed class AppPaths
{
    public const string ProductName = "TaskbarLyriz";

    public AppPaths(string? dataRoot = null)
    {
        DataRoot = dataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductName);
    }

    public string DataRoot { get; }

    public string ConfigurationDirectory => Path.Combine(DataRoot, "config");

    public string SettingsFile => Path.Combine(ConfigurationDirectory, "settings.json");

    public string LogsDirectory => Path.Combine(DataRoot, "logs");

    public string CacheDirectory => Path.Combine(DataRoot, "cache");

    public string DatabaseDirectory => Path.Combine(DataRoot, "database");

    public string LyricsDatabaseFile => Path.Combine(DatabaseDirectory, "lyrics.db");
}
