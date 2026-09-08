using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Windows.Lifecycle;

namespace TaskbarLyriz.Windows.Tests.Lifecycle;

[TestClass]
public sealed class RegistryStartupServiceTests
{
    private const string ExecutablePath = @"C:\Program Files\TaskbarLyriz\TaskbarLyriz.App.exe";

    [TestMethod]
    public void BuildCommand_QuotesExecutableAndAddsStartupArgument()
    {
        Assert.AreEqual(
            $"\"{ExecutablePath}\" --startup",
            RegistryStartupService.BuildCommand(ExecutablePath));
    }

    [TestMethod]
    public async Task GetStatusAsync_MatchingRegistration_ReturnsEnabled()
    {
        var registry = new MemoryStartupRegistry
        {
            Value = RegistryStartupService.BuildCommand(ExecutablePath),
        };
        var service = CreateService(registry);

        var result = await service.GetStatusAsync();

        Assert.AreEqual(StartupStatus.Enabled, result);
    }

    [TestMethod]
    public async Task GetStatusAsync_MissingOrStaleRegistration_ReturnsDisabled()
    {
        var registry = new MemoryStartupRegistry { Value = @"""C:\Old\TaskbarLyriz.App.exe"" --startup" };
        var service = CreateService(registry);

        var result = await service.GetStatusAsync();

        Assert.AreEqual(StartupStatus.Disabled, result);
    }

    [TestMethod]
    public async Task SetEnabledAsync_WritesCurrentExecutableCommand()
    {
        var registry = new MemoryStartupRegistry();
        var service = CreateService(registry);

        var result = await service.SetEnabledAsync(true);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(StartupStatus.Enabled, result.Status);
        Assert.AreEqual(RegistryStartupService.BuildCommand(ExecutablePath), registry.Value);
    }

    [TestMethod]
    public async Task SetEnabledAsync_Disabled_DeletesRegistration()
    {
        var registry = new MemoryStartupRegistry { Value = "existing" };
        var service = CreateService(registry);

        var result = await service.SetEnabledAsync(false);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(StartupStatus.Disabled, result.Status);
        Assert.IsNull(registry.Value);
    }

    private static RegistryStartupService CreateService(MemoryStartupRegistry registry) =>
        new(new NullLogger(), ExecutablePath, registry);

    private sealed class MemoryStartupRegistry : IUserStartupRegistry
    {
        public string? Value { get; set; }

        public string? Read(string keyPath, string valueName) => Value;

        public void Write(string keyPath, string valueName, string value) => Value = value;

        public void Delete(string keyPath, string valueName) => Value = null;
    }

    private sealed class NullLogger : IAppLogger
    {
        public void Write(
            AppLogLevel level,
            string eventName,
            string message,
            Exception? exception = null,
            IReadOnlyDictionary<string, object?>? properties = null)
        {
        }

        public void Dispose()
        {
        }
    }
}
