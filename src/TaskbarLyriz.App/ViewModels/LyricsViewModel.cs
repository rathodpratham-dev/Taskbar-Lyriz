using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Configuration;
using TaskbarLyriz.Core.Lyrics;
using TaskbarLyriz.Core.Media;

namespace TaskbarLyriz.App.ViewModels;

public sealed class LyricsViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan SessionLossGracePeriod = TimeSpan.FromSeconds(8);

    private readonly IMediaSessionService _mediaSessions;
    private readonly ILyricsService _lyricsService;
    private readonly ILyricsSynchronizationService _synchronizer;
    private readonly IAppSettingsService _settings;
    private readonly IAppLogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _synchronizationTimer;
    private readonly DispatcherQueueTimer _sessionLossTimer;
    private CancellationTokenSource? _lookupCancellation;
    private MediaSessionSnapshot? _session;
    private LyricsQuery? _currentQuery;
    private string? _currentKey;
    private int _lookupRevision;
    private int _currentLineIndex = -1;
    private int _timingOffsetMilliseconds;
    private string _title = "Nothing playing";
    private string _artist = "Start music in a supported player to load lyrics.";
    private string _status = "Waiting for a Windows media session.";
    private string _source = "No provider used";
    private string _lyricsKind = "\u2014";
    private string _previousLine = string.Empty;
    private string _currentLine = "Waiting for synchronized lyrics";
    private string _nextLine = string.Empty;
    private string _playbackPosition = "00:00.0";
    private string _playbackStatus = "Idle";
    private MediaPlaybackStatus _playbackState = MediaPlaybackStatus.Closed;
    private string _synchronizationStatus = "Synchronization inactive";
    private string _timingOffsetText = "0 ms";
    private double _lineProgress;
    private bool _followCurrentLine = true;
    private bool _isBusy;
    private Visibility _synchronizationVisibility = Visibility.Collapsed;
    private LyricAnimation _animation = LyricAnimation.Fade;
    private FontFamily _lyricFontFamily = new(AppearanceSettings.DefaultFontFamily);
    private double _lyricFontSize = 15;
    private double _currentLyricFontSize = 28;
    private bool _isTrackTransitionPending;
    private bool _isSessionTransitionDeferred;
    private bool _disposed;

    public LyricsViewModel(
        IMediaSessionService mediaSessions,
        ILyricsService lyricsService,
        ILyricsSynchronizationService synchronizer,
        IAppSettingsService settings,
        IAppLogger logger,
        TimeProvider timeProvider)
    {
        _mediaSessions = mediaSessions;
        _lyricsService = lyricsService;
        _synchronizer = synchronizer;
        _settings = settings;
        _logger = logger;
        _timeProvider = timeProvider;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("The lyrics view model must be created on the UI thread.");
        _synchronizationTimer = _dispatcherQueue.CreateTimer();
        _synchronizationTimer.Interval = TimeSpan.FromMilliseconds(100);
        _synchronizationTimer.IsRepeating = true;
        _synchronizationTimer.Tick += OnSynchronizationTimerTick;
        _sessionLossTimer = _dispatcherQueue.CreateTimer();
        _sessionLossTimer.Interval = SessionLossGracePeriod;
        _sessionLossTimer.IsRepeating = false;
        _sessionLossTimer.Tick += OnSessionLossTimerTick;

        Lines = new ObservableCollection<LyricDisplayLine>();
        ApplySettings(_settings.Current);
        _settings.Changed += OnSettingsChanged;
        _mediaSessions.CurrentSessionChanged += OnCurrentSessionChanged;
        ApplySession(_mediaSessions.CurrentSession);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<CurrentLyricLineChangedEventArgs>? CurrentLineChanged;

    public ObservableCollection<LyricDisplayLine> Lines { get; }

    public string Title
    {
        get => _title;
        private set
        {
            SetField(ref _title, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TaskbarText)));
        }
    }

    public string Artist
    {
        get => _artist;
        private set
        {
            SetField(ref _artist, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TaskbarText)));
        }
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public string Source
    {
        get => _source;
        private set => SetField(ref _source, value);
    }

    public string LyricsKind
    {
        get => _lyricsKind;
        private set => SetField(ref _lyricsKind, value);
    }

    public string PreviousLine
    {
        get => _previousLine;
        private set => SetField(ref _previousLine, value);
    }

    public string CurrentLine
    {
        get => _currentLine;
        private set
        {
            SetField(ref _currentLine, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TaskbarText)));
        }
    }

    public string NextLine
    {
        get => _nextLine;
        private set => SetField(ref _nextLine, value);
    }

    public string PlaybackPosition
    {
        get => _playbackPosition;
        private set => SetField(ref _playbackPosition, value);
    }

    public string PlaybackStatus
    {
        get => _playbackStatus;
        private set => SetField(ref _playbackStatus, value);
    }

    public MediaPlaybackStatus PlaybackState
    {
        get => _playbackState;
        private set => SetField(ref _playbackState, value);
    }

    public bool HasSynchronizedLyrics => SynchronizationVisibility == Visibility.Visible;

    public string TaskbarText => HasSynchronizedLyrics
        ? CurrentLine
        : string.IsNullOrWhiteSpace(Artist)
            ? Title
            : $"{Title} \u2014 {Artist}";

    public string SynchronizationStatus
    {
        get => _synchronizationStatus;
        private set => SetField(ref _synchronizationStatus, value);
    }

    public string TimingOffsetText
    {
        get => _timingOffsetText;
        private set => SetField(ref _timingOffsetText, value);
    }

    public double LineProgress
    {
        get => _lineProgress;
        private set => SetField(ref _lineProgress, value);
    }

    public bool FollowCurrentLine
    {
        get => _followCurrentLine;
        private set => SetField(ref _followCurrentLine, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public Visibility SynchronizationVisibility
    {
        get => _synchronizationVisibility;
        private set => SetField(ref _synchronizationVisibility, value);
    }

    public LyricAnimation Animation
    {
        get => _animation;
        private set => SetField(ref _animation, value);
    }

    public FontFamily LyricFontFamily
    {
        get => _lyricFontFamily;
        private set => SetField(ref _lyricFontFamily, value);
    }

    public double LyricFontSize
    {
        get => _lyricFontSize;
        private set => SetField(ref _lyricFontSize, value);
    }

    public double CurrentLyricFontSize
    {
        get => _currentLyricFontSize;
        private set => SetField(ref _currentLyricFontSize, value);
    }

    public int CurrentLineIndex => _currentLineIndex;

    public Task RefreshAsync()
    {
        if (_currentQuery is null)
        {
            Status = "Start a track with title and artist metadata before refreshing.";
            return Task.CompletedTask;
        }

        return BeginLookupAsync(_currentQuery, forceRefresh: true);
    }

    public Task AdjustTimingOffsetAsync(int deltaMilliseconds) =>
        SetTimingOffsetAsync(_timingOffsetMilliseconds + deltaMilliseconds);

    public async Task SetTimingOffsetAsync(int milliseconds)
    {
        milliseconds = Math.Clamp(milliseconds, -10_000, 10_000);
        if (milliseconds == _timingOffsetMilliseconds)
        {
            return;
        }

        try
        {
            await _settings.UpdateAsync(current => current with
            {
                Lyrics = current.Lyrics with { TimingOffsetMilliseconds = milliseconds },
            });
        }
        catch (Exception exception)
        {
            Status = "The timing offset could not be saved.";
            _logger.Error("lyrics.offset_save_failed", "The lyrics timing offset could not be saved.", exception);
        }
    }

    public async Task SetFollowCurrentLineAsync(bool enabled)
    {
        if (enabled == FollowCurrentLine)
        {
            return;
        }

        try
        {
            await _settings.UpdateAsync(current => current with
            {
                Lyrics = current.Lyrics with { FollowCurrentLine = enabled },
            });
        }
        catch (Exception exception)
        {
            Status = "The follow-current-line setting could not be saved.";
            _logger.Error("lyrics.follow_save_failed", "The follow-current-line setting could not be saved.", exception);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _synchronizationTimer.Stop();
        _synchronizationTimer.Tick -= OnSynchronizationTimerTick;
        _sessionLossTimer.Stop();
        _sessionLossTimer.Tick -= OnSessionLossTimerTick;
        _settings.Changed -= OnSettingsChanged;
        _mediaSessions.CurrentSessionChanged -= OnCurrentSessionChanged;
        CancelLookup();
    }

    private void OnCurrentSessionChanged(object? sender, CurrentMediaSessionChangedEventArgs args) =>
        Enqueue(() =>
        {
            if (MediaSessionContinuityPolicy.ShouldDeferUnstableCandidate(
                    HasSynchronizedLyrics,
                    args.Session))
            {
                if (!_isSessionTransitionDeferred)
                {
                    _isSessionTransitionDeferred = true;
                    _sessionLossTimer.Start();
                    _logger.Information(
                        "lyrics.session_transition_deferred",
                        "The current lyric was preserved while Windows reported an unstable media session.");
                }

                return;
            }

            _sessionLossTimer.Stop();
            if (_isSessionTransitionDeferred)
            {
                _isSessionTransitionDeferred = false;
                _logger.Information(
                    "lyrics.session_transition_recovered",
                    "The Windows media session recovered before the lyric continuity timeout.");
            }

            ApplySession(args.Session);
        });

    private void OnSettingsChanged(object? sender, AppSettings settings) =>
        Enqueue(() => ApplySettings(settings));

    private void OnSynchronizationTimerTick(DispatcherQueueTimer sender, object args) =>
        UpdateSynchronization();

    private void OnSessionLossTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        if (!_isSessionTransitionDeferred)
        {
            return;
        }

        _isSessionTransitionDeferred = false;
        _logger.Information(
            "lyrics.session_transition_expired",
            "The lyric continuity timeout expired because the media session did not recover.");
        ApplySession(_mediaSessions.CurrentSession);
    }

    private void ApplySession(MediaSessionSnapshot? session)
    {
        if (_disposed)
        {
            return;
        }

        _session = session;
        if (session is null)
        {
            Reset(
                "Nothing playing",
                "Start music in a supported player to load lyrics.",
                "Waiting for a Windows media session.");
            return;
        }

        var track = session.Track;
        var artist = string.IsNullOrWhiteSpace(track.Artist) ? track.AlbumArtist : track.Artist;
        PlaybackState = session.Playback.Status;
        PlaybackStatus = session.Playback.Status.ToString();
        Title = string.IsNullOrWhiteSpace(track.Title) ? "Unknown track" : track.Title.Trim();
        Artist = string.IsNullOrWhiteSpace(artist) ? "Artist metadata is unavailable." : artist.Trim();
        if (string.IsNullOrWhiteSpace(track.Title) || string.IsNullOrWhiteSpace(artist))
        {
            _isTrackTransitionPending = false;
            CancelLookup();
            _currentQuery = null;
            _currentKey = null;
            Lines.Clear();
            ClearSynchronization("Lyrics need both title and artist metadata.");
            Source = "No provider used";
            LyricsKind = "\u2014";
            Status = "Lyrics need both title and artist metadata.";
            IsBusy = false;
            return;
        }

        var query = new LyricsQuery(
            track.Title.Trim(),
            artist.Trim(),
            string.IsNullOrWhiteSpace(track.Album) ? null : track.Album.Trim(),
            session.Playback.Duration > TimeSpan.Zero ? session.Playback.Duration : null);
        if (string.Equals(_currentKey, query.CacheKey, StringComparison.Ordinal))
        {
            UpdateTimerState();
            if (!_isTrackTransitionPending)
            {
                UpdateSynchronization();
            }

            return;
        }

        var preserveCurrentLyrics = HasSynchronizedLyrics;
        _currentKey = query.CacheKey;
        _currentQuery = query;
        _isTrackTransitionPending = preserveCurrentLyrics;
        if (preserveCurrentLyrics)
        {
            _synchronizationTimer.Stop();
            SynchronizationStatus = "Loading lyrics for the next track";
        }
        else
        {
            Lines.Clear();
            ClearSynchronization("Waiting for synchronized lyrics");
        }

        Source = "Checking cache";
        LyricsKind = "\u2014";
        Status = "Looking for lyrics...";
        _ = BeginLookupAsync(query, forceRefresh: false);
    }

    private async Task BeginLookupAsync(LyricsQuery query, bool forceRefresh)
    {
        CancelLookup();
        var cancellation = new CancellationTokenSource();
        Interlocked.Exchange(ref _lookupCancellation, cancellation);
        var revision = Interlocked.Increment(ref _lookupRevision);
        IsBusy = true;
        Status = forceRefresh ? "Refreshing lyrics from providers..." : "Looking for lyrics...";

        try
        {
            var result = await _lyricsService.FindAsync(
                query,
                forceRefresh,
                cancellation.Token).ConfigureAwait(false);
            Enqueue(() => ApplyResult(query, result, revision));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer track or refresh superseded this request.
        }
        catch (Exception exception)
        {
            _logger.Error("lyrics.lookup_failed", "The lyrics lookup failed unexpectedly.", exception);
            Enqueue(() =>
            {
                if (revision != _lookupRevision)
                {
                    return;
                }

                var wasTransitioning = _isTrackTransitionPending;
                _isTrackTransitionPending = false;
                IsBusy = false;
                Status = "Lyrics could not be loaded. Try Refresh.";
                Source = "Provider unavailable";
                if (wasTransitioning)
                {
                    Lines.Clear();
                    ClearSynchronization("Lyrics could not be loaded");
                }
            });
        }
        finally
        {
            if (ReferenceEquals(
                Interlocked.CompareExchange(ref _lookupCancellation, null, cancellation),
                cancellation))
            {
                cancellation.Dispose();
            }
        }
    }

    private void ApplyResult(LyricsQuery query, LyricsLookupResult result, int revision)
    {
        if (_disposed || revision != _lookupRevision ||
            !string.Equals(_currentKey, query.CacheKey, StringComparison.Ordinal))
        {
            return;
        }

        _isTrackTransitionPending = false;
        IsBusy = false;
        Lines.Clear();
        if (result.Status != LyricsLookupStatus.Found || result.Lyrics is null)
        {
            ClearSynchronization("No synchronized lyrics available");
            Source = "No match";
            LyricsKind = "\u2014";
            Status = result.Message ?? "No matching lyrics were found.";
            return;
        }

        var lyrics = result.Lyrics;
        if (lyrics.IsSynced)
        {
            var synchronizedLines = lyrics.Lines
                .OrderBy(line => line.Timestamp)
                .ToArray();
            foreach (var line in synchronizedLines)
            {
                Lines.Add(new LyricDisplayLine(
                    FormatTimestamp(line.Timestamp),
                    line.Text,
                    LyricFontSize));
            }

            _synchronizer.SetLines(synchronizedLines);
            SynchronizationVisibility = Visibility.Visible;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSynchronizedLyrics)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TaskbarText)));
            UpdateTimerState();
            UpdateSynchronization(forceLineChange: true);
        }
        else
        {
            ClearSynchronization("Plain lyrics do not contain playback timestamps");
            foreach (var line in SplitPlainLyrics(lyrics.PlainLyrics))
            {
                Lines.Add(new LyricDisplayLine(string.Empty, line, LyricFontSize));
            }
        }

        Source = result.FromCache ? $"{lyrics.Source} \u00B7 local cache" : lyrics.Source;
        LyricsKind = lyrics.IsSynced ? "Synchronized LRC" : "Plain lyrics";
        Status = Lines.Count == 0
            ? "The provider returned no displayable lyric lines."
            : lyrics.IsSynced
                ? $"Following playback across {Lines.Count} synchronized lines."
                : $"{Lines.Count} plain lyric lines loaded.";
    }

    private void ApplySettings(AppSettings settings)
    {
        var previousOffset = _timingOffsetMilliseconds;
        var wasFollowing = FollowCurrentLine;
        _timingOffsetMilliseconds = settings.Lyrics.TimingOffsetMilliseconds;
        TimingOffsetText = $"{_timingOffsetMilliseconds:+#;-#;0} ms";
        FollowCurrentLine = settings.Lyrics.FollowCurrentLine;
        Animation = settings.Appearance.Animation;
        LyricFontFamily = new FontFamily(settings.Appearance.FontFamily);
        LyricFontSize = settings.Appearance.FontSize;
        CurrentLyricFontSize = Math.Clamp(settings.Appearance.FontSize + 13, 20, 44);
        foreach (var line in Lines)
        {
            line.ApplyTypography(settings.Appearance.FontSize);
        }

        if (previousOffset != _timingOffsetMilliseconds)
        {
            UpdateSynchronization(forceLineChange: true);
        }
        else if (!wasFollowing && FollowCurrentLine && _currentLineIndex >= 0)
        {
            CurrentLineChanged?.Invoke(
                this,
                new CurrentLyricLineChangedEventArgs(_currentLineIndex));
        }
    }

    private void UpdateTimerState()
    {
        var shouldRun = !_isTrackTransitionPending &&
            SynchronizationVisibility == Visibility.Visible &&
            _session?.Playback.Status == MediaPlaybackStatus.Playing;
        if (shouldRun && !_synchronizationTimer.IsRunning)
        {
            _synchronizationTimer.Start();
        }
        else if (!shouldRun && _synchronizationTimer.IsRunning)
        {
            _synchronizationTimer.Stop();
        }
    }

    private void UpdateSynchronization(bool forceLineChange = false)
    {
        if (_session is null || SynchronizationVisibility != Visibility.Visible)
        {
            return;
        }

        var position = _session.Playback.EstimatePosition(_timeProvider.GetUtcNow());
        var state = _synchronizer.Synchronize(
            position,
            TimeSpan.FromMilliseconds(_timingOffsetMilliseconds));
        PlaybackPosition = FormatPlaybackPosition(position);
        PlaybackStatus = _session.Playback.Status.ToString();
        SynchronizationStatus = _session.Playback.Status switch
        {
            MediaPlaybackStatus.Playing => "Following playback",
            MediaPlaybackStatus.Paused => "Paused",
            MediaPlaybackStatus.Stopped => "Stopped",
            _ => "Waiting for playback",
        };
        LineProgress = state.LineProgress;

        var nextIndex = state.CurrentIndex ?? -1;
        if (!forceLineChange && nextIndex == _currentLineIndex)
        {
            return;
        }

        _currentLineIndex = nextIndex;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLineIndex)));
        PreviousLine = DisplayText(state.PreviousLine?.Text);
        if (state.CurrentLine is not null)
        {
            CurrentLine = DisplayText(state.CurrentLine.Text, "Instrumental break");
            NextLine = DisplayText(state.NextLine?.Text);
        }
        else
        {
            CurrentLine = state.NextLine is null
                ? "Waiting for synchronized lyrics"
                : $"Lyrics begin at {FormatTimestamp(state.NextLine.Timestamp)}";
            NextLine = DisplayText(state.NextLine?.Text);
        }

        UpdateLineEmphasis(state);
        CurrentLineChanged?.Invoke(this, new CurrentLyricLineChangedEventArgs(
            state.CurrentIndex ?? state.NextIndex));
    }

    private void UpdateLineEmphasis(LyricsSynchronizationState state)
    {
        for (var index = 0; index < Lines.Count; index++)
        {
            Lines[index].SetPosition(
                isCurrent: state.CurrentIndex == index,
                isAdjacent: state.PreviousIndex == index || state.NextIndex == index);
        }
    }

    private void ClearSynchronization(string currentLine)
    {
        _synchronizationTimer.Stop();
        _synchronizer.SetLines(null);
        _currentLineIndex = -1;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLineIndex)));
        SynchronizationVisibility = Visibility.Collapsed;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSynchronizedLyrics)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TaskbarText)));
        PreviousLine = string.Empty;
        CurrentLine = currentLine;
        NextLine = string.Empty;
        LineProgress = 0;
        PlaybackPosition = "00:00.0";
        SynchronizationStatus = "Synchronization inactive";
    }

    private void Reset(string title, string artist, string status)
    {
        CancelLookup();
        _isTrackTransitionPending = false;
        _isSessionTransitionDeferred = false;
        _sessionLossTimer.Stop();
        _session = null;
        _currentQuery = null;
        _currentKey = null;
        Title = title;
        Artist = artist;
        Status = status;
        Source = "No provider used";
        LyricsKind = "\u2014";
        PlaybackStatus = "Idle";
        PlaybackState = MediaPlaybackStatus.Closed;
        IsBusy = false;
        Lines.Clear();
        ClearSynchronization("Waiting for synchronized lyrics");
    }

    private void CancelLookup()
    {
        var cancellation = Interlocked.Exchange(ref _lookupCancellation, null);
        if (cancellation is not null)
        {
            try
            {
                cancellation.Cancel();
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        Interlocked.Increment(ref _lookupRevision);
    }

    private void Enqueue(Action action)
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
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

    private static IEnumerable<string> SplitPlainLyrics(string? lyrics) =>
        string.IsNullOrWhiteSpace(lyrics)
            ? []
            : lyrics.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0);

    private static string DisplayText(string? text, string fallback = "") =>
        string.IsNullOrWhiteSpace(text) ? fallback : text;

    private static string FormatTimestamp(TimeSpan timestamp) =>
        timestamp.TotalHours >= 1
            ? $"{(int)timestamp.TotalHours}:{timestamp.Minutes:00}:{timestamp.Seconds:00}"
            : $"{(int)timestamp.TotalMinutes:00}:{timestamp.Seconds:00}";

    private static string FormatPlaybackPosition(TimeSpan position) =>
        position.TotalHours >= 1
            ? $"{(int)position.TotalHours}:{position.Minutes:00}:{position.Seconds:00}.{position.Milliseconds / 100}"
            : $"{(int)position.TotalMinutes:00}:{position.Seconds:00}.{position.Milliseconds / 100}";

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

public sealed class CurrentLyricLineChangedEventArgs(int? index) : EventArgs
{
    public int? Index { get; } = index;
}
