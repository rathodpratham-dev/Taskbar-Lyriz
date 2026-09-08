using TaskbarLyriz.Core.Media;
using TaskbarLyriz.Windows.MediaSession;
using Windows.Media.Control;

namespace TaskbarLyriz.Windows.Tests.MediaSession;

[TestClass]
public sealed class GsmTcMediaMapperTests
{
    [TestMethod]
    [DataRow(GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed, MediaPlaybackStatus.Closed)]
    [DataRow(GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened, MediaPlaybackStatus.Opened)]
    [DataRow(GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing, MediaPlaybackStatus.Changing)]
    [DataRow(GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped, MediaPlaybackStatus.Stopped)]
    [DataRow(GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing, MediaPlaybackStatus.Playing)]
    [DataRow(GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused, MediaPlaybackStatus.Paused)]
    public void MapStatus_MapsEveryWindowsPlaybackState(
        GlobalSystemMediaTransportControlsSessionPlaybackStatus input,
        MediaPlaybackStatus expected)
    {
        Assert.AreEqual(expected, GsmTcMediaMapper.MapStatus(input));
    }
}
