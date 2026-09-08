using System.ComponentModel;
using System.Runtime.CompilerServices;
using TaskbarLyriz.Core.Abstractions;

namespace TaskbarLyriz.App.ViewModels;

public sealed class CacheViewModel : INotifyPropertyChanged
{
    private readonly ILyricsCache _cache;
    private readonly IAppLogger _logger;
    private string _entryCount = "Loading...";
    private string _size = "Loading...";
    private string _databasePath = "Loading...";
    private string _status = "Reading the local lyrics cache.";
    private bool _isBusy;

    public CacheViewModel(ILyricsCache cache, IAppLogger logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string EntryCount
    {
        get => _entryCount;
        private set => SetField(ref _entryCount, value);
    }

    public string Size
    {
        get => _size;
        private set => SetField(ref _size, value);
    }

    public string DatabasePath
    {
        get => _databasePath;
        private set => SetField(ref _databasePath, value);
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var statistics = await _cache.GetStatisticsAsync();
            EntryCount = statistics.EntryCount == 1 ? "1 track" : $"{statistics.EntryCount:N0} tracks";
            Size = FormatBytes(statistics.SizeBytes);
            DatabasePath = statistics.DatabasePath;
            Status = "Cache statistics are up to date.";
        }
        catch (Exception exception)
        {
            Status = "The cache statistics could not be loaded.";
            _logger.Error("lyrics.cache_statistics_failed", "Cache statistics could not be loaded.", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ClearAsync()
    {
        IsBusy = true;
        Status = "Clearing downloaded lyrics...";
        try
        {
            await _cache.ClearAsync();
            await RefreshAsync();
            Status = "Downloaded lyrics were cleared. They can be downloaded again when needed.";
        }
        catch (Exception exception)
        {
            Status = "The lyrics cache could not be cleared.";
            _logger.Error("lyrics.cache_clear_failed", "The lyrics cache could not be cleared.", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
