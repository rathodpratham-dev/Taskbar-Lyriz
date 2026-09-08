using TaskbarLyriz.Windows.MediaSession;

namespace TaskbarLyriz.Windows.Tests.MediaSession;

[TestClass]
public sealed class SourceApplicationNameResolverTests
{
    [TestMethod]
    [DataRow("Spotify.exe", "Spotify")]
    [DataRow("chrome.exe", "Chrome")]
    [DataRow("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "Spotify")]
    [DataRow("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "Zune Music")]
    [DataRow("VideoLAN.VLC", "VLC")]
    [DataRow(null, "Unknown player")]
    public void Resolve_ReturnsReadablePlayerName(string? sourceId, string expected)
    {
        Assert.AreEqual(expected, SourceApplicationNameResolver.Resolve(sourceId));
    }
}
