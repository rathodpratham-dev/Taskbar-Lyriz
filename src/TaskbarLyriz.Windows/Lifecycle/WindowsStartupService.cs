using TaskbarLyriz.Core.Abstractions;
using Windows.ApplicationModel;

namespace TaskbarLyriz.Windows.Lifecycle;

public sealed class WindowsStartupService : IStartupService
{
    private readonly IStartupService _implementation;

    public WindowsStartupService(IAppLogger logger)
    {
        _implementation = HasPackageIdentity()
            ? new PackagedStartupService(logger)
            : new RegistryStartupService(logger);
    }

    public Task<StartupStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        _implementation.GetStatusAsync(cancellationToken);

    public Task<StartupChangeResult> SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default) =>
        _implementation.SetEnabledAsync(enabled, cancellationToken);

    internal static bool HasPackageIdentity()
    {
        try
        {
            _ = Package.Current.Id.FullName;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
