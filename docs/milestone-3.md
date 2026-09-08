# Milestone 3 - Lyrics

Milestone 3 connects the current Windows media session to a provider-neutral lyrics pipeline and displays the result in the main WinUI application.

## Delivered

- `ILyricsProvider`, `ILyricsCache`, `ILyricsService`, and `ILrcParser` boundaries
- LRCLIB exact and search lookups, preferring synchronized results
- Genius search and plain-lyrics fallback
- Candidate normalization and similarity scoring
- LRC parsing for `[mm:ss]`, `[mm:ss.xx]`, `[mm:ss.xxx]`, repeated timestamps, metadata, empty timed lines, and offsets
- SQLite cache keyed by normalized artist, title, and album
- One-time import of compatible legacy JSON cache entries without deleting them
- Automatic lookup on meaningful GSMTC track changes and cancellation of stale requests
- Fifteen-minute in-memory suppression of repeated misses, with manual refresh override
- Lyrics display with timestamps, provider/source information, loading and error states
- Provider information and cache statistics/cleanup screens

## Reliability and privacy

Network responses are streamed with explicit size limits. The shared `HttpClient` has a 15-second timeout and every provider accepts a `CancellationToken`. Cache and provider failures are isolated so media-session monitoring continues. Structured logs store operational outcomes and provider identifiers, but omit track titles, artists, and lyric text.

## Verification

The automated suite covers parsing, matching, provider choice and fallback, Genius HTML extraction through its public provider, SQLite round trips, cache clearing, legacy import, recent-miss suppression, configuration, media-session mapping, startup behavior, and tray message decoding.

Milestone 4 owns playback-position synchronization, current-line highlighting, seeking, pause/resume behavior, timing offsets controlled by the user, and smooth lyric transitions.
