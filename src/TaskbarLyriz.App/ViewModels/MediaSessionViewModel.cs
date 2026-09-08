using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Media;
using Windows.Storage.Streams;

namespace TaskbarLyriz.App.ViewModels;

public sealed class MediaSessionViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IMediaSessionService _mediaSessions;
    private readonly IAppLogger _logger;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _positionTimer;
    private MediaSessionSnapshot? _session;
    private string _serviceStatus = "Starting media-session monitor...";
    private string _player = "No active player";
    private string _title = "Nothing playing";
    private string _artist = "Start music in any Windows media-session player.";
    private string _album = "—";
    private string _playbackStatus = "Idle";
    private string _position = "00:00";
    private string _duration = "00:00";
    private string _sourceApplicationId = "—";
    private string _sessionId = "—";
    private string _sessionCount = "0 sessions detected";
    private double _progressMaximum = 1;
    private double _progressValue;
    private ImageSource? _artwork;
    private int _artworkRevision;
    private bool _disposed;

    public MediaSessionViewModel(IMediaSessionService mediaSessions, IAppLogger logger)
    {
        _mediaSessions = mediaSessions;
        _logger = logger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("The media view model must be created on the UI thread.");

        _positionTimer = _dispatcherQueue.CreateTimer();
        _positionTimer.Interval = TimeSpan.FromSeconds(1);
        _positionTimer.IsRepeating = true;
        _positionTimer.Tick += OnPositionTimerTick;
        _positionTimer.Start();

        _mediaSessions.StateChanged += OnServiceStateChanged;
        _mediaSessions.CurrentSessionChanged += OnCurrentSessionChanged;
        ApplyServiceState();
        ApplySession(_mediaSessions.CurrentSession);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ServiceStatus
    {
        get => _serviceStatus;
        private set => SetField(ref _serviceStatus, value);
    }

    public string Player
    {
        get => _player;
        private set => SetField(ref _player, value);
    }

    public string Title
    {
        get => _title;
        private set => SetField(ref _title, value);
    }

    public string Artist
    {
        get => _artist;
        private set => SetField(ref _artist, value);
    }

    public string Album
    {
        get => _album;
        private set => SetField(ref _album, value);
    }

    public string PlaybackStatus
    {
        get => _playbackStatus;
        private set => SetField(ref _playbackStatus, value);
    }

    public string Position
    {
        get => _position;
        private set => SetField(ref _position, value);
    }

    public string Duration
    {
        get => _duration;
        private set => SetField(ref _duration, value);
    }

    public string SourceApplicationId
    {
        get => _sourceApplicationId;
        private set => SetField(ref _sourceApplicationId, value);
    }

    public string SessionId
    {
        get => _sessionId;
        private set => SetField(ref _sessionId, value);
    }

    public string SessionCount
    {
        get => _sessionCount;
        private set => SetField(ref _sessionCount, value);
    }

    public double ProgressMaximum
    {
        get => _progressMaximum;
        private set => SetField(ref _progressMaximum, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetField(ref _progressValue, value);
    }

    public ImageSource? Artwork
    {
        get => _artwork;
        private set => SetField(ref _artwork, value);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _positionTimer.Stop();
        _positionTimer.Tick -= OnPositionTimerTick;
        _mediaSessions.StateChanged -= OnServiceStateChanged;
        _mediaSessions.CurrentSessionChanged -= OnCurrentSessionChanged;
    }

    private void OnServiceStateChanged(object? sender, EventArgs args) =>
        Enqueue(ApplyServiceState);

    private void OnCurrentSessionChanged(
        object? sender,
        CurrentMediaSessionChangedEventArgs args) =>
        Enqueue(() => ApplySession(args.Session));

    private void ApplyServiceState()
    {
        ServiceStatus = _mediaSessions.State switch
        {
            MediaSessionServiceState.Starting => "Connecting to Windows media sessions...",
            MediaSessionServiceState.Monitoring when _mediaSessions.Sessions.Count == 0 =>
                "Listening — start playback in Spotify, a browser, VLC, or another supported player.",
            MediaSessionServiceState.Monitoring => "Live GSMTC updates are connected.",
            MediaSessionServiceState.Unavailable =>
                _mediaSessions.StatusMessage ?? "Windows media sessions are unavailable.",
            MediaSessionServiceState.Faulted =>
                _mediaSessions.StatusMessage ?? "The media-session monitor encountered an error.",
            _ => "Media-session monitoring is stopped.",
        };

        var count = _mediaSessions.Sessions.Count;
        SessionCount = count == 1 ? "1 session detected" : $"{count} sessions detected";
    }

    private void ApplySession(MediaSessionSnapshot? session)
    {
        var previousArtwork = _session?.Track.Artwork;
        _session = session;
        ApplyServiceState();

        if (session is null)
        {
            Interlocked.Increment(ref _artworkRevision);
            Player = "No active player";
            Title = "Nothing playing";
            Artist = "Start music in any Windows media-session player.";
            Album = "—";
            PlaybackStatus = "Idle";
            SourceApplicationId = "—";
            SessionId = "—";
            ProgressMaximum = 1;
            ProgressValue = 0;
            Position = "00:00";
            Duration = "00:00";
            if (previousArtwork is not null)
            {
                Artwork = null;
            }

            return;
        }

        Player = session.SourceApplicationName;
        Title = session.DisplayTitle;
        Artist = string.IsNullOrWhiteSpace(session.Track.Artist) ? "Unknown artist" : session.Track.Artist;
        Album = string.IsNullOrWhiteSpace(session.Track.Album) ? "Unknown album" : session.Track.Album;
        PlaybackStatus = session.Playback.Status.ToString();
        SourceApplicationId = string.IsNullOrWhiteSpace(session.SourceApplicationId)
            ? "Unavailable"
            : session.SourceApplicationId;
        SessionId = session.SessionId;
        UpdatePosition();

        if (!ReferenceEquals(previousArtwork, session.Track.Artwork))
        {
            var artworkRevision = Interlocked.Increment(ref _artworkRevision);
            _ = ApplyArtworkAsync(session.Track.Artwork, artworkRevision);
        }
    }

    private async Task ApplyArtworkAsync(MediaArtwork? artwork, int revision)
    {
        try
        {
            if (artwork is null || artwork.IsEmpty)
            {
                if (revision == _artworkRevision)
                {
                    Artwork = null;
                }

                return;
            }

            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(artwork.Data);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            if (revision == _artworkRevision)
            {
                Artwork = bitmap;
            }
        }
        catch (Exception exception)
        {
            _logger.Warning(
                "media.artwork_decode_failed",
                "Album artwork could not be decoded for display.",
                exception);
            if (revision == _artworkRevision)
            {
                Artwork = null;
            }
        }
    }

    private void OnPositionTimerTick(DispatcherQueueTimer sender, object args) => UpdatePosition();

    private void UpdatePosition()
    {
        if (_session is null)
        {
            return;
        }

        var position = _session.Playback.EstimatePosition(DateTimeOffset.UtcNow);
        var duration = _session.Playback.Duration;
        Position = FormatTime(position);
        Duration = FormatTime(duration);
        ProgressMaximum = Math.Max(1, duration.TotalSeconds);
        ProgressValue = Math.Clamp(position.TotalSeconds, 0, ProgressMaximum);
    }

    private void Enqueue(Action action)
    {
        if (_disposed)
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (!_disposed)
            {
                action();
            }
        });
    }

    private static string FormatTime(TimeSpan value) => value.TotalHours >= 1
        ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
        : $"{(int)value.TotalMinutes:00}:{value.Seconds:00}";

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
