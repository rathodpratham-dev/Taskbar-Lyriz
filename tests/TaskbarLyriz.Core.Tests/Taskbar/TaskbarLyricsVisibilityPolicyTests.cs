using TaskbarLyriz.Core.Media;
using TaskbarLyriz.Core.Taskbar;

namespace TaskbarLyriz.Core.Tests.Taskbar;

[TestClass]
public sealed class TaskbarLyricsVisibilityPolicyTests
{
    [TestMethod]
    [DataRow(MediaPlaybackStatus.Playing, true)]
    [DataRow(MediaPlaybackStatus.Paused, true)]
    [DataRow(MediaPlaybackStatus.Stopped, false)]
    [DataRow(MediaPlaybackStatus.Opened, false)]
    [DataRow(MediaPlaybackStatus.Closed, false)]
    public void ShouldShow_UsesPlaybackState(MediaPlaybackStatus status, bool expected)
    {
        Assert.AreEqual(expected, TaskbarLyricsVisibilityPolicy.ShouldShow(true, true, status));
    }

    [TestMethod]
    public void ShouldShow_Disabled_ReturnsFalse()
    {
        Assert.IsFalse(TaskbarLyricsVisibilityPolicy.ShouldShow(
            false,
            true,
            MediaPlaybackStatus.Playing));
    }

    [TestMethod]
    public void ShouldShow_MissingDisplayContent_ReturnsFalse()
    {
        Assert.IsFalse(TaskbarLyricsVisibilityPolicy.ShouldShow(
            true,
            false,
            MediaPlaybackStatus.Playing));
    }
}
