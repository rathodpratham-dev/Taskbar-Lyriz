using TaskbarLyriz.Core.Lyrics;

namespace TaskbarLyriz.Core.Tests.Lyrics;

[TestClass]
public sealed class LyricsSynchronizationServiceTests
{
    private LyricsSynchronizationService _synchronizer = null!;

    [TestInitialize]
    public void Initialize()
    {
        _synchronizer = new LyricsSynchronizationService();
        _synchronizer.SetLines(
        [
            new LyricsLine(TimeSpan.FromSeconds(3), "First", TimeSpan.FromSeconds(7)),
            new LyricsLine(TimeSpan.FromSeconds(10), "Second", TimeSpan.FromSeconds(10)),
            new LyricsLine(TimeSpan.FromSeconds(20), "Third"),
        ]);
    }

    [TestMethod]
    public void Synchronize_BeforeFirstLine_ReturnsFirstAsNext()
    {
        var result = _synchronizer.Synchronize(TimeSpan.FromSeconds(1));

        Assert.IsNull(result.CurrentLine);
        Assert.AreEqual(0, result.NextIndex);
        Assert.AreEqual("First", result.NextLine?.Text);
        Assert.IsTrue(result.IsBeforeFirstLine);
    }

    [TestMethod]
    public void Synchronize_ExactBoundary_ReturnsCurrentPreviousAndNext()
    {
        var result = _synchronizer.Synchronize(TimeSpan.FromSeconds(10));

        Assert.AreEqual(1, result.CurrentIndex);
        Assert.AreEqual("First", result.PreviousLine?.Text);
        Assert.AreEqual("Second", result.CurrentLine?.Text);
        Assert.AreEqual("Third", result.NextLine?.Text);
        Assert.AreEqual(0, result.LineProgress);
    }

    [TestMethod]
    public void Synchronize_WithinLine_CalculatesClampedProgress()
    {
        var middle = _synchronizer.Synchronize(TimeSpan.FromSeconds(15));
        var afterLast = _synchronizer.Synchronize(TimeSpan.FromSeconds(30));

        Assert.AreEqual(0.5, middle.LineProgress, 0.0001);
        Assert.AreEqual(1, afterLast.LineProgress);
        Assert.IsNull(afterLast.NextLine);
    }

    [TestMethod]
    public void Synchronize_SeekingBackward_ReevaluatesWithoutStaleState()
    {
        Assert.AreEqual(2, _synchronizer.Synchronize(TimeSpan.FromSeconds(25)).CurrentIndex);

        var result = _synchronizer.Synchronize(TimeSpan.FromSeconds(4));

        Assert.AreEqual(0, result.CurrentIndex);
        Assert.AreEqual("First", result.CurrentLine?.Text);
    }

    [TestMethod]
    public void Synchronize_PositiveOffsetDelaysAndNegativeOffsetAdvancesLyrics()
    {
        var delayed = _synchronizer.Synchronize(
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(1));
        var advanced = _synchronizer.Synchronize(
            TimeSpan.FromSeconds(9),
            TimeSpan.FromSeconds(-1));

        Assert.AreEqual(0, delayed.CurrentIndex);
        Assert.AreEqual(TimeSpan.FromSeconds(9), delayed.EffectivePosition);
        Assert.AreEqual(1, advanced.CurrentIndex);
        Assert.AreEqual(TimeSpan.FromSeconds(10), advanced.EffectivePosition);
    }

    [TestMethod]
    public void SetLines_UnsortedWithDuplicateTimestamp_UsesStableRightmostLine()
    {
        _synchronizer.SetLines(
        [
            new LyricsLine(TimeSpan.FromSeconds(5), "Later"),
            new LyricsLine(TimeSpan.FromSeconds(2), "First at two"),
            new LyricsLine(TimeSpan.FromSeconds(2), "Second at two"),
        ]);

        var result = _synchronizer.Synchronize(TimeSpan.FromSeconds(2));

        Assert.AreEqual(1, result.CurrentIndex);
        Assert.AreEqual("Second at two", result.CurrentLine?.Text);
        Assert.AreEqual("First at two", result.PreviousLine?.Text);
        Assert.AreEqual("Later", result.NextLine?.Text);
    }

    [TestMethod]
    public void SetLines_Null_ClearsTimeline()
    {
        _synchronizer.SetLines(null);

        var result = _synchronizer.Synchronize(TimeSpan.FromSeconds(10));

        Assert.IsNull(result.CurrentIndex);
        Assert.IsNull(result.NextIndex);
        Assert.AreEqual(0, result.LineProgress);
    }

    [TestMethod]
    public void Synchronize_NegativePlayback_ClampsPlaybackButPreservesEffectiveOffset()
    {
        var result = _synchronizer.Synchronize(
            TimeSpan.FromSeconds(-5),
            TimeSpan.FromSeconds(1));

        Assert.AreEqual(TimeSpan.Zero, result.PlaybackPosition);
        Assert.AreEqual(TimeSpan.FromSeconds(-1), result.EffectivePosition);
        Assert.IsNull(result.CurrentIndex);
    }
}
