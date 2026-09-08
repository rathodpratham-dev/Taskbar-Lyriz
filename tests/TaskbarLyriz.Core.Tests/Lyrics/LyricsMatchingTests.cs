using TaskbarLyriz.Core.Lyrics;

namespace TaskbarLyriz.Core.Tests.Lyrics;

[TestClass]
public sealed class LyricsMatchingTests
{
    [TestMethod]
    public void Normalize_RemovesQualifiersDiacriticsAndPunctuation()
    {
        var result = LyricsText.Normalize("Beyonc\u00E9 & JAY-Z (Live) [Remastered]");

        Assert.AreEqual("beyonce and jay z", result);
    }

    [TestMethod]
    public void Score_ExactSynchronizedCandidate_IsPerfectMatch()
    {
        var query = new LyricsQuery("Track (Radio Edit)", "The Artist", Duration: TimeSpan.FromSeconds(200));
        var candidate = new LyricsCandidate(
            "Track",
            "The Artist",
            null,
            TimeSpan.FromSeconds(200),
            HasSyncedLyrics: true);

        var result = LyricsMatchScorer.Score(query, candidate);

        Assert.AreEqual(1, result, 0.0001);
    }

    [TestMethod]
    public void Score_UnrelatedCandidate_IsRejectedByProviderThreshold()
    {
        var result = LyricsMatchScorer.Score(
            new LyricsQuery("A Song", "An Artist"),
            new LyricsCandidate("Entirely Different", "Someone Else", null, null, false));

        Assert.IsLessThan(0.55, result);
    }

    [TestMethod]
    public void CacheKey_NormalizesValuesAndDistinguishesAlbums()
    {
        var first = new LyricsQuery("Track (Live)", "Artist", "First Album");
        var equivalent = new LyricsQuery("track", "ARTIST", "first album");
        var otherAlbum = new LyricsQuery("Track", "Artist", "Second Album");

        Assert.AreEqual(first.CacheKey, equivalent.CacheKey);
        Assert.AreNotEqual(first.CacheKey, otherAlbum.CacheKey);
    }
}
