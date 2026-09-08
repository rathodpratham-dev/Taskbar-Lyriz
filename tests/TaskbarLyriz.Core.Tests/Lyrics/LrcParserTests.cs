using TaskbarLyriz.Core.Lyrics;

namespace TaskbarLyriz.Core.Tests.Lyrics;

[TestClass]
public sealed class LrcParserTests
{
    private readonly LrcParser _parser = new();

    [TestMethod]
    public void Parse_TimestampsMetadataAndOffset_ReturnsOrderedLines()
    {
        const string input = """
            [ar:Example Artist]
            [offset:-500]
            [00:03.050][00:04.5]Shared line
            [00:01]First line
            [00:02.25]Second line
            """;

        var result = _parser.Parse(input);

        Assert.HasCount(4, result.Lines);
        Assert.AreEqual(TimeSpan.FromMilliseconds(-500), result.Offset);
        Assert.AreEqual("Example Artist", result.Metadata["ar"]);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), result.Lines[0].Timestamp);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1750), result.Lines[1].Timestamp);
        Assert.AreEqual(TimeSpan.FromMilliseconds(2550), result.Lines[2].Timestamp);
        Assert.AreEqual(TimeSpan.FromSeconds(4), result.Lines[3].Timestamp);
        Assert.AreEqual("Shared line", result.Lines[2].Text);
        Assert.AreEqual("Shared line", result.Lines[3].Text);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1250), result.Lines[0].Duration);
    }

    [TestMethod]
    public void Parse_EmptyTimedLine_PreservesTimingMarker()
    {
        var result = _parser.Parse("[00:01.00]\n[00:02.000]Words");

        Assert.HasCount(2, result.Lines);
        Assert.AreEqual(string.Empty, result.Lines[0].Text);
        Assert.AreEqual(TimeSpan.FromSeconds(1), result.Lines[0].Duration);
    }

    [TestMethod]
    public void Parse_NegativeOffset_ClampsTimestampToZero()
    {
        var result = _parser.Parse("[offset:-2000]\n[00:01.00]Early");

        Assert.HasCount(1, result.Lines);
        Assert.AreEqual(TimeSpan.Zero, result.Lines[0].Timestamp);
    }

    [TestMethod]
    public void Parse_NullOrMalformed_ReturnsNoLines()
    {
        Assert.IsEmpty(_parser.Parse(null).Lines);
        Assert.IsEmpty(_parser.Parse("[not a timestamp] lyrics").Lines);
        Assert.IsEmpty(_parser.Parse("[00:99.00] invalid seconds").Lines);
    }
}
