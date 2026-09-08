using Microsoft.Win32;
using TaskbarLyriz.Core.Abstractions;

namespace TaskbarLyriz.Windows.Lifecycle;

public sealed class RegistryStartupService : IStartupService
{
    internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName = "TaskbarLyriz";

    private readonly IAppLogger _logger;
    private readonly string _executablePath;
    private readonly IUserStartupRegistry _registry;

    public RegistryStartupService(IAppLogger logger)
        : this(
            logger,
            Environment.ProcessPath ?? throw new InvalidOperationException(
                "The TaskbarLyriz executable path is unavailable."),
            new CurrentUserStartupRegistry())
    {
    }

    internal RegistryStartupService(
        IAppLogger logger,
        string executablePath,
        IUserStartupRegistry registry)
    {
        _logger = logger;
        _executablePath = Path.GetFullPath(executablePath);
        _registry = registry;
    }

    public Task<StartupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var registeredCommand = _registry.Read(RunKeyPath, ValueName);
            var status = string.Equals(
                registeredCommand,
                BuildCommand(_executablePath),
                StringComparison.OrdinalIgnoreCase)
                ? StartupStatus.Enabled
                : StartupStatus.Disabled;
            return Task.FromResult(status);
        }
        catch (Exception exception)
        {
            _logger.Warning(
                "startup.registry_status_failed",
                "The per-user Windows startup registration could not be read.",
                exception);
            return Task.FromResult(StartupStatus.Unsupported);
        }
    }

    public Task<StartupChangeResult> SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (enabled)
            {
                _registry.Write(RunKeyPath, ValueName, BuildCommand(_executablePath));
                return Task.FromResult(new StartupChangeResult(true, StartupStatus.Enabled));
            }

            _registry.Delete(RunKeyPath, ValueName);
            return Task.FromResult(new StartupChangeResult(true, StartupStatus.Disabled));
        }
        catch (Exception exception)
        {
            _logger.Warning(
                "startup.registry_change_failed",
                "The per-user Windows startup registration could not be changed.",
                exception);
            return Task.FromResult(new StartupChangeResult(
                false,
                StartupStatus.Unsupported,
                "Windows could not change the per-user startup registration."));
        }
    }

    internal static string BuildCommand(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"\"{Path.GetFullPath(executablePath)}\" --startup";
    }
}

internal interface IUserStartupRegistry
{
    string? Read(string keyPath, string valueName);

    void Write(string keyPath, string valueName, string value);

    void Delete(string keyPath, string valueName);
}

internal sealed class CurrentUserStartupRegistry : IUserStartupRegistry
{
    public string? Read(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: false);
        return key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    public void Write(string keyPath, string valueName, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true)
            ?? throw new InvalidOperationException("The Windows startup registry key is unavailable.");
        key.SetValue(valueName, value, RegistryValueKind.String);
    }

    public void Delete(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
