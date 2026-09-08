using System.Text.Json;
using System.Text.Json.Serialization;
using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Configuration;
using TaskbarLyriz.Infrastructure.Storage;

namespace TaskbarLyriz.Infrastructure.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly AppPaths _paths;
    private readonly IAppLogger _logger;

    public JsonSettingsStore(AppPaths paths, IAppLogger logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_paths.SettingsFile))
        {
            var defaults = AppSettingsValidator.Normalize(null);
            await SaveAsync(defaults, cancellationToken).ConfigureAwait(false);
            return defaults;
        }

        try
        {
            AppSettings? settings;
            await using (var stream = File.OpenRead(_paths.SettingsFile))
            {
                settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                    stream,
                    SerializerOptions,
                    cancellationToken).ConfigureAwait(false);
            }

            var normalized = AppSettingsValidator.Normalize(settings);
            if (settings?.SchemaVersion != normalized.SchemaVersion)
            {
                await SaveAsync(normalized, cancellationToken).ConfigureAwait(false);
            }

            return normalized;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.Warning(
                "settings.load_failed",
                "The settings file could not be read. Defaults will be used.",
                exception);
            return AppSettingsValidator.Normalize(null);
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = AppSettingsValidator.Normalize(settings);
        Directory.CreateDirectory(_paths.ConfigurationDirectory);

        var temporaryPath = Path.Combine(
            _paths.ConfigurationDirectory,
            $"settings.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                16_384,
                FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    normalized,
                    SerializerOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _paths.SettingsFile, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
