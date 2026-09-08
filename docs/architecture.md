# TaskbarLyriz architecture

## Milestone 1 baseline

TaskbarLyriz uses four production projects with dependencies pointing inward:

```text
TaskbarLyriz.App
  |-- TaskbarLyriz.Infrastructure --> TaskbarLyriz.Core
  |-- TaskbarLyriz.Windows --------> TaskbarLyriz.Core
  `-- TaskbarLyriz.Core
```

`TaskbarLyriz.Core` owns stable models and contracts. It has no dependency on WinUI, storage, or Windows APIs. `TaskbarLyriz.Infrastructure` supplies portable persistence concerns. `TaskbarLyriz.Windows` contains explicit WinRT and Win32 boundaries. `TaskbarLyriz.App` is the composition root and owns only presentation and lifecycle orchestration.

This keeps future media detection, lyric selection, parsing, synchronization, and taskbar positioning testable without constructing UI objects.

## Lifecycle

The process registers a stable key with Windows AppLifecycle before starting WinUI. A secondary launch redirects its activation to the primary process, where the existing window is shown and activated.

At primary startup, dependency injection creates the settings store, logger, startup-task adapter, tray service, and view models. Settings load before the window is shown. Closing the window hides it when `MinimizeToTray` is enabled; Exit from the tray performs explicit cleanup.

## Local data

All mutable data lives under `%LOCALAPPDATA%\TaskbarLyriz`:

```text
config/settings.json
logs/taskbar-lyriz-YYYYMMDD.jsonl
cache/
database/
```

Settings writes use a uniquely named temporary file followed by an atomic replace. Settings are validated at both load and save boundaries. Corrupt JSON falls back to safe defaults and produces a warning log.

Logs are structured JSON Lines, flushed after each entry, and rolled by date or at 5 MB. The schema includes timestamp, severity, event name, message, optional exception details, and optional structured properties.

## Windows integration

- Notification area: a message-only Win32 window plus `Shell_NotifyIcon` version 4. Explorer restart is handled through the registered `TaskbarCreated` message.
- Startup: packaged `windows.startupTask` manifest extension and `Windows.ApplicationModel.StartupTask`.
- App identity and activation: Windows App SDK AppLifecycle.
- Main UI: WinUI 3 with a custom title bar and Mica system backdrop.

No Explorer injection, unsupported taskbar mutation, global input hooks, or elevated registration is used.

## Media-session boundary (Milestone 2)

`TaskbarLyriz.Core` defines immutable track, playback, capability, artwork, and session snapshots behind `IMediaSessionService`. It also owns deterministic current-session selection and playback-position estimation, keeping both policies independent of WinRT and UI.

`TaskbarLyriz.Windows` implements the contract with Global System Media Transport Controls. It listens to `SessionsChanged`, `CurrentSessionChanged`, `MediaPropertiesChanged`, `PlaybackInfoChanged`, and `TimelinePropertiesChanged`. Event work is serialized to prevent stale concurrent snapshots. Album artwork is copied out of the WinRT stream with an 8 MB upper bound. No media API polling loop is used.

```text
Windows GSMTC events
  -> GsmTcMediaSessionService
  -> immutable Core snapshot
  |-> MediaSessionViewModel -> Home / Playback diagnostics
  `-> tray tooltip and current-song menu row
```

The view model advances the displayed position once per second from the last Windows timestamp and playback rate. It clamps the estimate to the reported duration and stops advancing when paused.

## Lyrics boundary (Milestone 3)

`TaskbarLyriz.Core` owns provider-neutral lyric queries, documents, timed lines, matching rules, service contracts, and the LRC parser. The parser supports two- and three-digit fractional timestamps, multiple timestamps per line, metadata, empty timed lines, and signed offset tags.

`TaskbarLyriz.Infrastructure` implements the provider pipeline and persistence. A lookup checks SQLite first, then LRCLIB exact/search results, then Genius as a plain-lyrics fallback. Provider requests are asynchronous, cancellation-aware, bounded by response size, and use an application-wide 15-second HTTP timeout. One failed provider does not stop later providers or GSMTC monitoring. Recent misses are held for 15 minutes to prevent repeated downloads; an explicit refresh bypasses both the cache and miss delay.

```text
GSMTC track change
  -> LyricsViewModel (deduplicate + cancel stale lookup)
  -> LyricsService
      |-> SQLite cache
      |-> LRCLIB (synchronized preferred, then plain)
      `-> Genius (plain fallback)
  -> LrcParser
  -> Lyrics page
```

The SQLite database lives at `%LOCALAPPDATA%\TaskbarLyriz\database\lyrics.db`. Cache keys normalize artist, title, and album. Existing JSON entries under `cache/` are imported once without removing the original files. Logs record provider/cache outcomes without song titles or artists.

## Synchronization boundary (Milestone 4)

`LyricsSynchronizationService` is UI-independent and owns an immutable, timestamp-sorted timeline. Each evaluation uses binary search to select the previous, current, and next line and calculates clamped progress through the current line. The service is stateless with respect to playback direction, so forward and backward seeks produce an immediate correct result without stale cursor state.

The Lyrics view model combines that timeline with `MediaPlayback.EstimatePosition`. It reevaluates immediately on every GSMTC playback/timeline event. While playing synchronized lyrics, a 100 ms dispatcher timer interpolates smooth progress between Windows events. The timer stops when playback pauses, stops, lyrics are unavailable, or the view model is disposed.

```text
GSMTC playback/timeline event
  -> immutable MediaPlayback snapshot
  -> position estimate + persistent timing offset
  -> LyricsSynchronizationService (binary search)
  -> previous / current / next + line progress
  |-> Home live-lyrics card
  `-> Lyrics active-line view + optional auto-follow
```

The persisted timing offset is clamped to +/-10 seconds. Positive values delay lyric changes by subtracting the offset from effective playback time; negative values advance them. LRC file offsets remain part of parsed timestamps, while the user offset is applied non-destructively at evaluation time.

## Taskbar experience boundary (Milestone 5)

The taskbar lyric surface is a dedicated borderless WinUI window. It is never parented to or injected into Explorer. `WS_EX_NOACTIVATE` prevents focus changes, `WS_EX_TOOLWINDOW` removes it from Alt+Tab, and transparent hit testing passes pointer input through rather than intercepting taskbar interactions.

The Windows boundary reads the documented primary taskbar app-bar rectangle plus the native task-list and notification-area child rectangles. The Core placement calculator reserves the weather/widgets end and the centered application strip, then clamps the requested lyric width to the selected free left or right gap. If the primary taskbar is not on the bottom edge or a minimum safe width is unavailable, the surface remains hidden.

```text
primary bottom taskbar geometry
  + task-list bounds
  + notification-area bounds
  -> free left/right gap calculator
  -> DPI-scaled, vertically centered window bounds
  -> no-activate + click-through lyric surface
```

Visibility is deterministic: enabled settings plus synchronized lyrics plus Playing/Paused means shown; all other states mean hidden. The surface consumes the existing 100 ms synchronization state and adds no media or provider polling.

Transient `null` GSMTC publications are debounced at the lyric presentation boundary for eight seconds. A replacement session cancels the pending reset, preserving cached synchronized lyrics across browser tab and app switches. A separate two-second UI-thread heartbeat reasserts taskbar geometry and z-order without activating the window.

Appearance settings use the same evented settings path as taskbar placement. Schema 4 adds a validated font family alongside theme, base lyric size, surface opacity, corner radius, and line-change animation. A settings change immediately updates the taskbar window and both in-app lyric views; no renderer maintains a separate typography preference.

## Next boundary: Milestone 6

Milestone 6 owns center/custom positioning, hover details and controls, multi-monitor targeting, live display-change repositioning, non-bottom taskbar edges, and broader Windows 10/11 layout adaptation.
