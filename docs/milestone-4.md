# Milestone 4 - Synchronization

Milestone 4 maps the live Windows playback timeline to synchronized lyrics and keeps the main application current through playback, pauses, seeks, and track changes.

## Delivered

- UI-independent `ILyricsSynchronizationService`
- Stable timestamp sorting and binary-search line selection
- Previous, current, and next lyric state
- Clamped progress through each lyric line
- Correct forward and backward seeking without cursor state
- Playback-rate-aware position interpolation from GSMTC snapshots
- Immediate reevaluation on playback and timeline events
- 100 ms updates only while synchronized lyrics are playing
- Frozen lyric position and zero timer activity while paused
- Persistent timing offset between -10,000 ms and +10,000 ms
- Optional automatic following of the active line
- Active, adjacent, and inactive lyric emphasis
- Current-line fade/slide transitions according to the appearance setting
- Live lyric cards on Home and the full Lyrics screen

## Timing semantics

A positive timing offset delays lyric changes. For example, `+500 ms` causes a lyric timestamped at 10 seconds to become active at playback time 10.5 seconds. A negative offset advances lyric changes. Embedded LRC `[offset:]` tags are already included in parsed timestamps and remain independent from this user setting.

## Performance

Lines are sorted once when lyrics change. Each playback update uses O(log n) binary search. The UI timer runs at 10 Hz only during active synchronized playback and is stopped for paused, stopped, plain-lyric, empty, and disposed states. Property notifications are suppressed when values have not changed.

## Verification scope

Automated tests cover boundaries, previous/current/next selection, progress, seeking backward, positive and negative offsets, duplicate timestamps, unsorted input, empty timelines, playback interpolation, paused playback, and settings migration. Live verification covers the WinUI Home and Lyrics screens against a real GSMTC browser session with synchronized LRCLIB lyrics.

Milestone 5 reuses this service for the first safe lyric surface inside an unused taskbar gap.
