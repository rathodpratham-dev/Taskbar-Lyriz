using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Media;
using Windows.Media.Control;

namespace TaskbarLyriz.Windows.MediaSession;

public sealed class GsmTcMediaSessionService : IMediaSessionService
{
    private const ulong MaximumArtworkBytes = 8 * 1024 * 1024;

    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly object _stateGate = new();
    private readonly Dictionary<GlobalSystemMediaTransportControlsSession, TrackedSession> _tracked =
        new(ReferenceEqualityComparer.Instance);
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private CancellationTokenSource? _lifetimeCancellation;
    private IReadOnlyList<MediaSessionSnapshot> _sessions = Array.Empty<MediaSessionSnapshot>();
    private MediaSessionSnapshot? _currentSession;
    private MediaSessionServiceState _state = MediaSessionServiceState.Stopped;
    private string? _statusMessage;
    private int _sessionSequence;
    private bool _disposed;

    public GsmTcMediaSessionService(IAppLogger logger)
    {
        _logger = logger;
    }

    public MediaSessionServiceState State
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public string? StatusMessage
    {
        get
        {
            lock (_stateGate)
            {
                return _statusMessage;
            }
        }
    }

    public IReadOnlyList<MediaSessionSnapshot> Sessions
    {
        get
        {
            lock (_stateGate)
            {
                return _sessions;
            }
        }
    }

    public MediaSessionSnapshot? CurrentSession
    {
        get
        {
            lock (_stateGate)
            {
                return _currentSession;
            }
        }
    }

    public event EventHandler? StateChanged;

    public event EventHandler<CurrentMediaSessionChangedEventArgs>? CurrentSessionChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State is MediaSessionServiceState.Starting or MediaSessionServiceState.Monitoring)
        {
            return;
        }

        SetState(MediaSessionServiceState.Starting, "Connecting to Windows media sessions...");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            cancellationToken.ThrowIfCancellationRequested();

            _lifetimeCancellation = new CancellationTokenSource();
            _manager = manager;
            manager.CurrentSessionChanged += OnCurrentSessionChanged;
            manager.SessionsChanged += OnSessionsChanged;

            await RefreshAsync(
                changedSession: null,
                refreshMediaProperties: true,
                reconcileSessions: true,
                cancellationToken).ConfigureAwait(false);

            SetState(MediaSessionServiceState.Monitoring, "Listening for Windows media-session events.");
            _logger.Information(
                "media.monitoring_started",
                "Windows media-session monitoring started.");
        }
        catch (OperationCanceledException)
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
            SetState(
                MediaSessionServiceState.Unavailable,
                "Windows did not grant access to system media sessions.");
            _logger.Warning(
                "media.access_denied",
                "Windows denied access to system media sessions.",
                exception);
        }
        catch (Exception exception)
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
            SetState(
                MediaSessionServiceState.Faulted,
                "Media-session monitoring could not be started.");
            _logger.Error(
                "media.monitoring_start_failed",
                "Windows media-session monitoring could not be started.",
                exception);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_manager is null && State == MediaSessionServiceState.Stopped)
        {
            return;
        }

        _lifetimeCancellation?.Cancel();
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_manager is not null)
            {
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
                _manager.SessionsChanged -= OnSessionsChanged;
            }

            foreach (var tracked in _tracked.Values)
            {
                Unsubscribe(tracked.Session);
            }

            _tracked.Clear();
            _manager = null;
            _lifetimeCancellation?.Dispose();
            _lifetimeCancellation = null;
        }
        finally
        {
            _refreshGate.Release();
        }

        PublishSessions(Array.Empty<MediaSessionSnapshot>(), null);
        SetState(MediaSessionServiceState.Stopped, "Media-session monitoring is stopped.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _refreshGate.Dispose();
    }

    private void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args) => QueueRefresh(null, refreshMediaProperties: true, reconcileSessions: true);

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) => QueueRefresh(null, refreshMediaProperties: false, reconcileSessions: false);

    private void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args) => QueueRefresh(sender, refreshMediaProperties: true, reconcileSessions: false);

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) => QueueRefresh(sender, refreshMediaProperties: false, reconcileSessions: false);

    private void OnTimelinePropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        TimelinePropertiesChangedEventArgs args) => QueueRefresh(sender, refreshMediaProperties: false, reconcileSessions: false);

    private void QueueRefresh(
        GlobalSystemMediaTransportControlsSession? changedSession,
        bool refreshMediaProperties,
        bool reconcileSessions)
    {
        var cancellationToken = _lifetimeCancellation?.Token ?? CancellationToken.None;
        _ = RefreshFromEventAsync(
            changedSession,
            refreshMediaProperties,
            reconcileSessions,
            cancellationToken);
    }

    private async Task RefreshFromEventAsync(
        GlobalSystemMediaTransportControlsSession? changedSession,
        bool refreshMediaProperties,
        bool reconcileSessions,
        CancellationToken cancellationToken)
    {
        try
        {
            await RefreshAsync(
                changedSession,
                refreshMediaProperties,
                reconcileSessions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal during application shutdown.
        }
        catch (Exception exception)
        {
            _logger.Warning(
                "media.session_refresh_failed",
                "A Windows media-session event could not be processed.",
                exception);
        }
    }

    private async Task RefreshAsync(
        GlobalSystemMediaTransportControlsSession? changedSession,
        bool refreshMediaProperties,
        bool reconcileSessions,
        CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<MediaSessionSnapshot> snapshots;
        MediaSessionSnapshot? current;
        try
        {
            var manager = _manager;
            if (manager is null)
            {
                return;
            }

            if (reconcileSessions)
            {
                ReconcileSessions(manager.GetSessions());
            }

            if (changedSession is not null && !_tracked.ContainsKey(changedSession))
            {
                return;
            }

            var mediaTargets = refreshMediaProperties
                ? changedSession is null
                    ? _tracked.Values.Where(tracked => !tracked.HasLoadedMediaProperties).ToArray()
                    : [_tracked[changedSession]]
                : Array.Empty<TrackedSession>();

            foreach (var tracked in mediaTargets)
            {
                await RefreshMediaPropertiesAsync(tracked, cancellationToken).ConfigureAwait(false);
            }

            IEnumerable<TrackedSession> playbackTargets = changedSession is null
                ? _tracked.Values
                : [_tracked[changedSession]];
            foreach (var tracked in playbackTargets)
            {
                RefreshPlayback(tracked);
            }

            snapshots = _tracked.Values
                .Select(tracked => tracked.CreateSnapshot())
                .OrderBy(snapshot => snapshot.SourceApplicationName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            string? preferredSessionId = null;
            var preferredSession = manager.GetCurrentSession();
            if (preferredSession is not null && _tracked.TryGetValue(preferredSession, out var preferred))
            {
                preferredSessionId = preferred.SessionId;
            }

            current = MediaSessionSelector.SelectCurrent(snapshots, preferredSessionId);
        }
        finally
        {
            _refreshGate.Release();
        }

        PublishSessions(snapshots, current);
    }

    private void ReconcileSessions(IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions)
    {
        var available = new HashSet<GlobalSystemMediaTransportControlsSession>(
            sessions,
            ReferenceEqualityComparer.Instance);

        foreach (var removed in _tracked.Keys.Where(session => !available.Contains(session)).ToArray())
        {
            Unsubscribe(removed);
            _tracked.Remove(removed);
        }

        foreach (var session in sessions)
        {
            if (_tracked.ContainsKey(session))
            {
                continue;
            }

            var sequence = Interlocked.Increment(ref _sessionSequence);
            var sourceId = session.SourceAppUserModelId?.Trim() ?? string.Empty;
            _tracked.Add(
                session,
                new TrackedSession(
                    session,
                    $"media-session-{sequence}",
                    sourceId,
                    SourceApplicationNameResolver.Resolve(sourceId)));
            Subscribe(session);
        }
    }

    private async Task RefreshMediaPropertiesAsync(
        TrackedSession tracked,
        CancellationToken cancellationToken)
    {
        try
        {
            var properties = await tracked.Session.TryGetMediaPropertiesAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var artwork = await ReadArtworkAsync(properties.Thumbnail, cancellationToken).ConfigureAwait(false);
            tracked.Track = GsmTcMediaMapper.MapTrack(properties, artwork);
            tracked.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.Warning(
                "media.properties_unavailable",
                "Media properties were unavailable for a Windows media session.",
                exception);
        }
        finally
        {
            tracked.HasLoadedMediaProperties = true;
        }
    }

    private static async Task<MediaArtwork?> ReadArtworkAsync(
        global::Windows.Storage.Streams.IRandomAccessStreamReference? artworkReference,
        CancellationToken cancellationToken)
    {
        if (artworkReference is null)
        {
            return null;
        }

        using var randomAccessStream = await artworkReference.OpenReadAsync();
        cancellationToken.ThrowIfCancellationRequested();
        if (randomAccessStream.Size == 0 || randomAccessStream.Size > MaximumArtworkBytes)
        {
            return null;
        }

        using var source = randomAccessStream.AsStreamForRead();
        using var destination = new MemoryStream((int)randomAccessStream.Size);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        return new MediaArtwork(destination.ToArray(), randomAccessStream.ContentType);
    }

    private static void RefreshPlayback(TrackedSession tracked)
    {
        try
        {
            tracked.Playback = GsmTcMediaMapper.MapPlayback(
                tracked.Session.GetPlaybackInfo(),
                tracked.Session.GetTimelineProperties());
            tracked.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        catch
        {
            tracked.Playback = MediaPlayback.Empty;
        }
    }

    private void PublishSessions(
        IReadOnlyList<MediaSessionSnapshot> sessions,
        MediaSessionSnapshot? current)
    {
        MediaSessionSnapshot? previous;
        lock (_stateGate)
        {
            previous = _currentSession;
            _sessions = sessions;
            _currentSession = current;
        }

        if (Equals(previous, current))
        {
            return;
        }

        var sourceChanged = !string.Equals(
            previous?.SessionId,
            current?.SessionId,
            StringComparison.Ordinal);
        var mediaChanged = HasMediaIdentityChanged(previous?.Track, current?.Track);
        if (sourceChanged || mediaChanged)
        {
            var eventName = sourceChanged
                ? "media.current_session_changed"
                : "media.current_item_changed";
            var message = current is null
                ? "There is no active Windows media session."
                : sourceChanged
                    ? $"The active media source is {current.SourceApplicationName}."
                    : $"The active media item changed in {current.SourceApplicationName}.";
            _logger.Information(eventName, message);
        }

        CurrentSessionChanged?.Invoke(this, new CurrentMediaSessionChangedEventArgs(current));
    }

    private static bool HasMediaIdentityChanged(MediaTrack? previous, MediaTrack? current) =>
        !string.Equals(previous?.Title, current?.Title, StringComparison.Ordinal) ||
        !string.Equals(previous?.Artist, current?.Artist, StringComparison.Ordinal) ||
        !string.Equals(previous?.Album, current?.Album, StringComparison.Ordinal);

    private void SetState(MediaSessionServiceState state, string message)
    {
        bool changed;
        lock (_stateGate)
        {
            changed = _state != state || !string.Equals(_statusMessage, message, StringComparison.Ordinal);
            _state = state;
            _statusMessage = message;
        }

        if (changed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Subscribe(GlobalSystemMediaTransportControlsSession session)
    {
        session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
    }

    private void Unsubscribe(GlobalSystemMediaTransportControlsSession session)
    {
        session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
    }

    private sealed class TrackedSession
    {
        internal TrackedSession(
            GlobalSystemMediaTransportControlsSession session,
            string sessionId,
            string sourceApplicationId,
            string sourceApplicationName)
        {
            Session = session;
            SessionId = sessionId;
            SourceApplicationId = sourceApplicationId;
            SourceApplicationName = sourceApplicationName;
        }

        internal GlobalSystemMediaTransportControlsSession Session { get; }

        internal string SessionId { get; }

        internal string SourceApplicationId { get; }

        internal string SourceApplicationName { get; }

        internal MediaTrack Track { get; set; } = MediaTrack.Empty;

        internal MediaPlayback Playback { get; set; } = MediaPlayback.Empty;

        internal bool HasLoadedMediaProperties { get; set; }

        internal DateTimeOffset LastUpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        internal MediaSessionSnapshot CreateSnapshot() => new()
        {
            SessionId = SessionId,
            SourceApplicationId = SourceApplicationId,
            SourceApplicationName = SourceApplicationName,
            Track = Track,
            Playback = Playback,
            LastUpdatedAtUtc = LastUpdatedAtUtc,
        };
    }
}
