using TaskbarLyriz.Core.Abstractions;

namespace TaskbarLyriz.Core.Lyrics;

public sealed class LyricsSynchronizationService : ILyricsSynchronizationService
{
    private static readonly TimeSpan DefaultFinalLineDuration = TimeSpan.FromSeconds(3.5);
    private static readonly TimeSpan MinimumProgressDuration = TimeSpan.FromMilliseconds(400);
    private readonly object _gate = new();
    private LyricsLine[] _lines = [];

    public void SetLines(IEnumerable<LyricsLine>? lines)
    {
        var ordered = lines?
            .Select((line, order) => (Line: line, Order: order))
            .OrderBy(item => item.Line.Timestamp)
            .ThenBy(item => item.Order)
            .Select(item => item.Line)
            .ToArray() ?? [];

        lock (_gate)
        {
            _lines = ordered;
        }
    }

    public LyricsSynchronizationState Synchronize(
        TimeSpan playbackPosition,
        TimeSpan timingOffset = default)
    {
        LyricsLine[] lines;
        lock (_gate)
        {
            lines = _lines;
        }

        if (playbackPosition < TimeSpan.Zero)
        {
            playbackPosition = TimeSpan.Zero;
        }

        var effectivePosition = playbackPosition - timingOffset;
        if (lines.Length == 0)
        {
            return new LyricsSynchronizationState
            {
                PlaybackPosition = playbackPosition,
                EffectivePosition = effectivePosition,
                TimingOffset = timingOffset,
            };
        }

        var currentIndex = FindCurrentIndex(lines, effectivePosition);
        if (currentIndex < 0)
        {
            return new LyricsSynchronizationState
            {
                PlaybackPosition = playbackPosition,
                EffectivePosition = effectivePosition,
                TimingOffset = timingOffset,
                NextIndex = 0,
                NextLine = lines[0],
            };
        }

        var previousIndex = currentIndex > 0 ? currentIndex - 1 : (int?)null;
        var nextIndex = currentIndex + 1 < lines.Length ? currentIndex + 1 : (int?)null;
        var currentLine = lines[currentIndex];
        var progressDuration = GetProgressDuration(lines, currentIndex, nextIndex);
        var elapsed = effectivePosition - currentLine.Timestamp;
        var progress = progressDuration <= TimeSpan.Zero
            ? 0
            : Math.Clamp(elapsed.TotalMilliseconds / progressDuration.TotalMilliseconds, 0, 1);

        return new LyricsSynchronizationState
        {
            PlaybackPosition = playbackPosition,
            EffectivePosition = effectivePosition,
            TimingOffset = timingOffset,
            PreviousIndex = previousIndex,
            CurrentIndex = currentIndex,
            NextIndex = nextIndex,
            PreviousLine = previousIndex is { } previous ? lines[previous] : null,
            CurrentLine = currentLine,
            NextLine = nextIndex is { } next ? lines[next] : null,
            LineProgress = progress,
        };
    }

    private static int FindCurrentIndex(LyricsLine[] lines, TimeSpan position)
    {
        var low = 0;
        var high = lines.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (lines[middle].Timestamp <= position)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low - 1;
    }

    private static TimeSpan GetProgressDuration(
        LyricsLine[] lines,
        int currentIndex,
        int? nextIndex)
    {
        var current = lines[currentIndex];
        var duration = current.Duration is { } lineDuration && lineDuration > TimeSpan.Zero
            ? lineDuration
            : nextIndex is { } next
                ? lines[next].Timestamp - current.Timestamp
                : DefaultFinalLineDuration;
        return duration < MinimumProgressDuration ? MinimumProgressDuration : duration;
    }
}
