using TaskbarLyriz.Core.Abstractions;
using TaskbarLyriz.Windows.Lifecycle;
using Windows.ApplicationModel;

namespace TaskbarLyriz.Windows.Tests.Lifecycle;

[TestClass]
public sealed class PackagedStartupServiceTests
{
    [TestMethod]
    [DataRow(StartupTaskState.Enabled, StartupStatus.Enabled)]
    [DataRow(StartupTaskState.EnabledByPolicy, StartupStatus.EnabledByPolicy)]
    [DataRow(StartupTaskState.Disabled, StartupStatus.Disabled)]
    [DataRow(StartupTaskState.DisabledByUser, StartupStatus.DisabledByUser)]
    [DataRow(StartupTaskState.DisabledByPolicy, StartupStatus.DisabledByPolicy)]
    public void Map_ReturnsDomainStatus(StartupTaskState input, StartupStatus expected)
    {
        Assert.AreEqual(expected, PackagedStartupService.Map(input));
    }
}
