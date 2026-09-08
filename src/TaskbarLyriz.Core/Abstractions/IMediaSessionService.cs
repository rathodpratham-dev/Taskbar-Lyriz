using TaskbarLyriz.Core.Media;

namespace TaskbarLyriz.Core.Abstractions;

public interface IMediaSessionService : IAsyncDisposable
{
    MediaSessionServiceState State { get; }

    string? StatusMessage { get; }

    IReadOnlyList<MediaSessionSnapshot> Sessions { get; }

    MediaSessionSnapshot? CurrentSession { get; }

    event EventHandler? StateChanged;

    event EventHandler<CurrentMediaSessionChangedEventArgs>? CurrentSessionChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

public enum MediaSessionServiceState
{
    Stopped,
    Starting,
    Monitoring,
    Unavailable,
    Faulted,
}

public sealed class CurrentMediaSessionChangedEventArgs : EventArgs
{
    public CurrentMediaSessionChangedEventArgs(MediaSessionSnapshot? session)
    {
        Session = session;
    }

    public MediaSessionSnapshot? Session { get; }
}
