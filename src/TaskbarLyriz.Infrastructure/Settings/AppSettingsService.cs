using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Core.Configuration;

namespace TaskbarLyriz.Infrastructure.Settings;

public sealed class AppSettingsService : IAppSettingsService, IDisposable
{
    private readonly ISettingsStore _store;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AppSettingsService(ISettingsStore store, IAppLogger logger)
    {
        _store = store;
        _logger = logger;
    }

    public AppSettings Current { get; private set; } = new();

    public bool IsInitialized { get; private set; }

    public event EventHandler<AppSettings>? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsInitialized)
            {
                return;
            }

            Current = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            IsInitialized = true;
            _logger.Information("settings.initialized", "Application settings were loaded.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(
        Func<AppSettings, AppSettings> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        AppSettings updated;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            updated = AppSettingsValidator.Normalize(update(Current));
            await _store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
            Current = updated;
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, updated);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
