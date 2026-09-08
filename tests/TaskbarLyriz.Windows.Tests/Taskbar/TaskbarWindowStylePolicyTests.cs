using TaskbarLyriz.Windows.Taskbar;

namespace TaskbarLyriz.Windows.Tests.Taskbar;

[TestClass]
public sealed class TaskbarWindowStylePolicyTests
{
    [TestMethod]
    public void Apply_AddsNoActivateAndToolWindowAndRemovesAppWindow()
    {
        const long unrelatedStyle = 0x20;
        var current = unrelatedStyle | TaskbarWindowStylePolicy.AppWindow;

        var result = TaskbarWindowStylePolicy.Apply(current);

        Assert.AreNotEqual(0, result & TaskbarWindowStylePolicy.NoActivate);
        Assert.AreNotEqual(0, result & TaskbarWindowStylePolicy.ToolWindow);
        Assert.AreNotEqual(0, result & TaskbarWindowStylePolicy.Transparent);
        Assert.AreEqual(0, result & TaskbarWindowStylePolicy.AppWindow);
        Assert.AreNotEqual(0, result & unrelatedStyle);
    }
}
