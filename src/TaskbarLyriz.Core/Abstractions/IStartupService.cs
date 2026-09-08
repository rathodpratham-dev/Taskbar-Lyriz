namespace TaskbarLyriz.Core.Abstractions;

public interface IStartupService
{
    Task<StartupStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<StartupChangeResult> SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default);
}

public enum StartupStatus
{
    Unsupported,
    Enabled,
    EnabledByPolicy,
    Disabled,
    DisabledByUser,
    DisabledByPolicy,
}

public sealed record StartupChangeResult(bool Succeeded, StartupStatus Status, string? Message = null);
