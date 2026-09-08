using TaskbarLyriz.Core.Configuration;

namespace TaskbarLyriz.Core.Abstractions;

public interface IAppSettingsService
{
    AppSettings Current { get; }

    bool IsInitialized { get; }

    event EventHandler<AppSettings>? Changed;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Func<AppSettings, AppSettings> update,
        CancellationToken cancellationToken = default);
}
