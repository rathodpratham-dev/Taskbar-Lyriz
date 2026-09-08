using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Lyrics;
using TaskbarLyriz.Infrastructure.Storage;

namespace TaskbarLyriz.Infrastructure.Lyrics;

public sealed class SqliteLyricsCache : ILyricsCache
{
    private const long MaximumLegacyFileBytes = 2 * 1024 * 1024;
    private const string LegacyImportKey = "legacy_json_import_v1";
    private readonly AppPaths _paths;
    private readonly ILrcParser _parser;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private bool _initialized;

    public SqliteLyricsCache(AppPaths paths, ILrcParser parser, IAppLogger logger)
    {
        _paths = paths;
        _parser = parser;
        _logger = logger;
    }

    public async Task<LyricsDocument?> GetAsync(
        LyricsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT track_title, artist, album, plain_lyrics, synced_lyrics,
                   source, source_id, duration_ms, fetched_at_utc
            FROM lyrics_cache
            WHERE cache_key = $cacheKey;
            """;
        command.Parameters.AddWithValue("$cacheKey", query.CacheKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var syncedLyrics = GetNullableString(reader, 4);
        var durationMilliseconds = reader.IsDBNull(7) ? (long?)null : reader.GetInt64(7);
        var fetchedAt = DateTimeOffset.TryParse(
            reader.GetString(8),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedFetchedAt)
            ? parsedFetchedAt
            : DateTimeOffset.UtcNow;

        return new LyricsDocument
        {
            TrackTitle = reader.GetString(0),
            Artist = reader.GetString(1),
            Album = GetNullableString(reader, 2),
            PlainLyrics = GetNullableString(reader, 3),
            SyncedLyrics = syncedLyrics,
            Source = reader.GetString(5),
            SourceId = GetNullableString(reader, 6),
            Duration = durationMilliseconds is > 0
                ? TimeSpan.FromMilliseconds(durationMilliseconds.Value)
                : null,
            Lines = string.IsNullOrWhiteSpace(syncedLyrics)
                ? Array.Empty<LyricsLine>()
                : _parser.Parse(syncedLyrics).Lines,
            FetchedAtUtc = fetchedAt,
        };
    }

    public async Task StoreAsync(
        LyricsQuery query,
        LyricsDocument lyrics,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(lyrics);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, query, lyrics, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LyricsCacheStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM lyrics_cache;";
        var count = Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
        return new LyricsCacheStatistics(count, GetDatabaseSize(), _paths.LyricsDatabaseFile);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM lyrics_cache;";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var checkpoint = connection.CreateCommand())
        {
            checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await checkpoint.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var vacuum = connection.CreateCommand())
        {
            vacuum.CommandText = "VACUUM;";
            await vacuum.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger.Information("lyrics.cache_cleared", "The lyrics cache was cleared.");
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            Directory.CreateDirectory(_paths.DatabaseDirectory);
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    PRAGMA journal_mode=WAL;
                    PRAGMA busy_timeout=5000;
                    CREATE TABLE IF NOT EXISTS lyrics_cache (
                        cache_key TEXT PRIMARY KEY NOT NULL,
                        query_title TEXT NOT NULL,
                        query_artist TEXT NOT NULL,
                        track_title TEXT NOT NULL,
                        artist TEXT NOT NULL,
                        album TEXT NULL,
                        plain_lyrics TEXT NULL,
                        synced_lyrics TEXT NULL,
                        source TEXT NOT NULL,
                        source_id TEXT NULL,
                        duration_ms INTEGER NULL,
                        fetched_at_utc TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS ix_lyrics_cache_fetched_at
                        ON lyrics_cache(fetched_at_utc);
                    CREATE TABLE IF NOT EXISTS cache_metadata (
                        key TEXT PRIMARY KEY NOT NULL,
                        value TEXT NOT NULL
                    );
                    """;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await ImportLegacyCacheAsync(connection, cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private async Task ImportLegacyCacheAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using (var marker = connection.CreateCommand())
        {
            marker.CommandText = "SELECT value FROM cache_metadata WHERE key = $key;";
            marker.Parameters.AddWithValue("$key", LegacyImportKey);
            if (await marker.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
            {
                return;
            }
        }

        var imported = 0;
        var skipped = 0;
        if (Directory.Exists(_paths.CacheDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(_paths.CacheDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var fileInfo = new FileInfo(path);
                    if (fileInfo.Length is <= 0 or > MaximumLegacyFileBytes)
                    {
                        skipped++;
                        continue;
                    }

                    var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                    using var json = JsonDocument.Parse(bytes);
                    if (!TryReadLegacyDocument(json.RootElement, fileInfo.LastWriteTimeUtc, out var query, out var lyrics))
                    {
                        skipped++;
                        continue;
                    }

                    await UpsertAsync(connection, query, lyrics, cancellationToken).ConfigureAwait(false);
                    imported++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    skipped++;
                }
            }
        }

        await using (var setMarker = connection.CreateCommand())
        {
            setMarker.CommandText = """
                INSERT INTO cache_metadata(key, value) VALUES($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            setMarker.Parameters.AddWithValue("$key", LegacyImportKey);
            setMarker.Parameters.AddWithValue("$value", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            await setMarker.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger.Write(
            AppLogLevel.Information,
            "lyrics.legacy_cache_imported",
            "The legacy lyrics cache import completed.",
            properties: new Dictionary<string, object?>
            {
                ["imported"] = imported,
                ["skipped"] = skipped,
            });
    }

    private bool TryReadLegacyDocument(
        JsonElement root,
        DateTime lastWriteTimeUtc,
        out LyricsQuery query,
        out LyricsDocument lyrics)
    {
        query = new LyricsQuery(string.Empty, string.Empty);
        lyrics = null!;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("song", out var song) ||
            song.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var title = GetJsonString(song, "title")?.Trim();
        var artist = GetJsonString(song, "artist")?.Trim();
        var album = GetJsonString(song, "album")?.Trim();
        var rawLyrics = GetJsonString(root, "lyrics")?.Trim();
        if (string.IsNullOrWhiteSpace(title) ||
            string.IsNullOrWhiteSpace(artist) ||
            string.IsNullOrWhiteSpace(rawLyrics))
        {
            return false;
        }

        var isLrc = root.TryGetProperty("is_lrc", out var lrcElement) &&
            lrcElement.ValueKind is JsonValueKind.True;
        var source = GetJsonString(root, "source")?.Trim();
        query = new LyricsQuery(title, artist, album);
        var parsed = isLrc ? _parser.Parse(rawLyrics) : LrcDocument.Empty;
        lyrics = new LyricsDocument
        {
            TrackTitle = title,
            Artist = artist,
            Album = string.IsNullOrWhiteSpace(album) ? null : album,
            PlainLyrics = isLrc ? null : rawLyrics,
            SyncedLyrics = isLrc ? rawLyrics : null,
            Source = string.IsNullOrWhiteSpace(source) ? "legacy" : source,
            Lines = parsed.Lines,
            FetchedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(lastWriteTimeUtc, DateTimeKind.Utc)),
        };
        return !isLrc || parsed.Lines.Count > 0;
    }

    private static async Task UpsertAsync(
        SqliteConnection connection,
        LyricsQuery query,
        LyricsDocument lyrics,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO lyrics_cache(
                cache_key, query_title, query_artist, track_title, artist, album,
                plain_lyrics, synced_lyrics, source, source_id, duration_ms, fetched_at_utc)
            VALUES(
                $cacheKey, $queryTitle, $queryArtist, $trackTitle, $artist, $album,
                $plainLyrics, $syncedLyrics, $source, $sourceId, $durationMs, $fetchedAtUtc)
            ON CONFLICT(cache_key) DO UPDATE SET
                query_title = excluded.query_title,
                query_artist = excluded.query_artist,
                track_title = excluded.track_title,
                artist = excluded.artist,
                album = excluded.album,
                plain_lyrics = excluded.plain_lyrics,
                synced_lyrics = excluded.synced_lyrics,
                source = excluded.source,
                source_id = excluded.source_id,
                duration_ms = excluded.duration_ms,
                fetched_at_utc = excluded.fetched_at_utc;
            """;
        command.Parameters.AddWithValue("$cacheKey", query.CacheKey);
        command.Parameters.AddWithValue("$queryTitle", query.Title);
        command.Parameters.AddWithValue("$queryArtist", query.Artist);
        command.Parameters.AddWithValue("$trackTitle", lyrics.TrackTitle);
        command.Parameters.AddWithValue("$artist", lyrics.Artist);
        command.Parameters.AddWithValue("$album", DbValue(lyrics.Album));
        command.Parameters.AddWithValue("$plainLyrics", DbValue(lyrics.PlainLyrics));
        command.Parameters.AddWithValue("$syncedLyrics", DbValue(lyrics.SyncedLyrics));
        command.Parameters.AddWithValue("$source", lyrics.Source);
        command.Parameters.AddWithValue("$sourceId", DbValue(lyrics.SourceId));
        command.Parameters.AddWithValue(
            "$durationMs",
            lyrics.Duration is { } duration ? (object)(long)duration.TotalMilliseconds : DBNull.Value);
        command.Parameters.AddWithValue(
            "$fetchedAtUtc",
            lyrics.FetchedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _paths.LyricsDatabaseFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            DefaultTimeout = 5,
        };
        var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private long GetDatabaseSize()
    {
        var size = GetFileSize(_paths.LyricsDatabaseFile);
        size += GetFileSize($"{_paths.LyricsDatabaseFile}-wal");
        size += GetFileSize($"{_paths.LyricsDatabaseFile}-shm");
        return size;
    }

    private static long GetFileSize(string path) =>
        File.Exists(path) ? new FileInfo(path).Length : 0;

    private static object DbValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    private static string? GetNullableString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static string? GetJsonString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
