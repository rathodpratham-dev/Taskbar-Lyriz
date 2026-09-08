using TaskbarLyriz.Core.Abstractions;
using Windows.ApplicationModel;

namespace TaskbarLyriz.Windows.Lifecycle;

public sealed class PackagedStartupService : IStartupService
{
    public const string TaskId = "TaskbarLyrizStartup";

    private readonly IAppLogger _logger;

    public PackagedStartupService(IAppLogger logger)
    {
        _logger = logger;
    }

    public async Task<StartupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var startupTask = await StartupTask.GetAsync(TaskId);
            cancellationToken.ThrowIfCancellationRequested();
            return Map(startupTask.State);
        }
        catch (Exception exception)
        {
            _logger.Warning(
                "startup.status_unavailable",
                "Windows startup status is unavailable for the current deployment.",
                exception);
            return StartupStatus.Unsupported;
        }
    }

    public async Task<StartupChangeResult> SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var startupTask = await StartupTask.GetAsync(TaskId);
            if (enabled)
            {
                var state = await startupTask.RequestEnableAsync();
                var status = Map(state);
                var succeeded = status is StartupStatus.Enabled or StartupStatus.EnabledByPolicy;
                return new StartupChangeResult(
                    succeeded,
                    status,
                    succeeded ? null : "Windows did not enable startup for TaskbarLyriz.");
            }

            startupTask.Disable();
            return new StartupChangeResult(true, StartupStatus.Disabled);
        }
        catch (Exception exception)
        {
            _logger.Warning(
                "startup.change_failed",
                "Windows could not change the startup setting.",
                exception);
            return new StartupChangeResult(
                false,
                StartupStatus.Unsupported,
                "Startup can only be changed from an installed TaskbarLyriz package.");
        }
    }

    internal static StartupStatus Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled => StartupStatus.Enabled,
        StartupTaskState.EnabledByPolicy => StartupStatus.EnabledByPolicy,
        StartupTaskState.Disabled => StartupStatus.Disabled,
        StartupTaskState.DisabledByUser => StartupStatus.DisabledByUser,
        StartupTaskState.DisabledByPolicy => StartupStatus.DisabledByPolicy,
        _ => StartupStatus.Unsupported,
    };
}
