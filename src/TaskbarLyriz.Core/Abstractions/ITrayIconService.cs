using TaskbarLyriz.Core.Models;

namespace TaskbarLyriz.Core.Abstractions;

public interface ITrayIconService : IDisposable
{
    event EventHandler? ShowRequested;

    event EventHandler? SettingsRequested;

    event EventHandler? TaskbarLyricsToggleRequested;

    event EventHandler? ExitRequested;

    void Initialize();

    void Update(TrayIconState state);
}
