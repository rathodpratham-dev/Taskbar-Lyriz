namespace TaskbarLyriz.Core.Lyrics;

public sealed record LyricsSynchronizationState
{
    public TimeSpan PlaybackPosition { get; init; }

    public TimeSpan EffectivePosition { get; init; }

    public TimeSpan TimingOffset { get; init; }

    public int? PreviousIndex { get; init; }

    public int? CurrentIndex { get; init; }

    public int? NextIndex { get; init; }

    public LyricsLine? PreviousLine { get; init; }

    public LyricsLine? CurrentLine { get; init; }

    public LyricsLine? NextLine { get; init; }

    public double LineProgress { get; init; }

    public bool IsBeforeFirstLine => CurrentIndex is null && NextIndex is not null;
}
