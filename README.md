# TaskbarLyriz

TaskbarLyriz is a native Windows companion for synchronized lyrics. The project is being migrated from its Python reference implementation to C#, .NET, WinUI 3, the Windows App SDK, and documented Win32/WinRT APIs.

Milestone 5 places the synchronized line inside a safe unused region of the primary Windows taskbar.

## Implemented features

- Packaged WinUI 3 application with a Mica-backed navigation shell
- Home, Settings, About, and future-feature placeholder screens
- Windows notification-area icon with Show, Settings, taskbar-lyrics toggle, and Exit commands
- Close-to-tray behavior
- Single-instance activation that redirects later launches to the main window
- JSON settings stored under `%LOCALAPPDATA%\TaskbarLyriz\config`
- Structured JSON Lines logs under `%LOCALAPPDATA%\TaskbarLyriz\logs`
- Per-user Start with Windows support for packaged and unpackaged builds
- Light, dark, and system theme modes
- Event-driven Global System Media Transport Controls (GSMTC) monitoring
- Automatic current-player and track-change detection
- Title, artist, album, artwork, playback state, position, duration, and source application
- Live Home screen, Playback diagnostics, and tray current-song text
- Position interpolation between Windows timeline events without aggressive API polling
- Automatic lyrics lookup when the active track changes
- LRCLIB synchronized/plain lyrics with Genius plain-lyrics fallback
- Standards-aware LRC parsing for timestamps, repeated timestamps, metadata, empty lines, and offsets
- SQLite lyrics cache with one-time, non-destructive import of legacy JSON cache files
- Lyrics, provider status, and cache-management screens
- Cancellation-aware, bounded network requests with a 15-second timeout
- Playback-driven previous, current, and next lyric selection
- Smooth per-line progress driven by a low-cost 100 ms timer while playing
- Immediate synchronization after seeks, pauses, resumes, and track changes
- Persistent timing adjustment from -10,000 ms to +10,000 ms
- Automatic current-line emphasis and optional list following
- Live synchronized lyrics on both Home and Lyrics screens
- Compact single-line lyric surface inside the bottom taskbar
- Automatic left-gap placement beside Weather, with an optional right-gap mode
- Live clamping between the taskbar app strip and notification area
- Non-activating, Alt+Tab-hidden, click-through Win32 window behavior
- Automatic show for playing/paused synchronized tracks and hide when inactive
- Live Appearance page with theme, lyric font, font size, opacity, corner radius, and transition controls
- Persistent typography shared by the taskbar, Home, and Lyrics displays

The app does not inject into Explorer, modify the taskbar process, install global hooks, or require administrator privileges.

## Requirements

- Windows 10 version 1809 or newer, or Windows 11
- .NET SDK 10.0.400 or newer in the .NET 10 feature band
- Visual Studio 2026 with WinUI application-development components for IDE workflows (optional)

The project uses Windows App SDK 2.4 and restores its Windows SDK build tooling through NuGet.

## Build and test

From the repository root:

```powershell
dotnet restore TaskbarLyriz.sln
dotnet build TaskbarLyriz.sln -c Debug -p:Platform=x64
dotnet test TaskbarLyriz.sln -c Debug -p:Platform=x64 --no-build
```

For the quickest local launch, use the self-contained development script:

```powershell
.\scripts\run-dev.ps1
```

This mode works without enabling Windows Developer Mode. **Start with Windows** uses the current user's Windows Run registration in this unpackaged mode and does not require administrator rights.

To launch with package identity, first enable **Settings > System > For developers > Developer Mode**, then run:

```powershell
dotnet run --project src\TaskbarLyriz.App\TaskbarLyriz.App.csproj -c Debug -p:Platform=x64
```

Package identity is required for the supported Windows `StartupTask` API.

Installed packages use the Windows `StartupTask` API. Unpackaged development builds use `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; both are controlled by the same **Settings > Start with Windows** toggle.

## Verify media detection

1. Start TaskbarLyriz with `.\scripts\run-dev.ps1`.
2. Play media in Spotify, Chrome, Edge, VLC, Apple Music, or another app that publishes a Windows media session.
3. Open **Playback** to inspect the selected player, metadata, playback state, timeline, raw source identifier, and number of available sessions.

Windows chooses a preferred session. If it does not provide one, TaskbarLyriz deterministically prefers a playing session, then a paused session, then the most recently updated remaining session.

## Verify lyrics

1. Start TaskbarLyriz with `.\scripts\run-dev.ps1`.
2. Play a track whose media session supplies both a title and artist.
3. Open **Lyrics**. TaskbarLyriz first checks the local SQLite cache, then LRCLIB, then Genius.
4. Use **Refresh** to bypass a recent no-result delay and query the providers again.
5. Open **Cache** to inspect the local entry count and on-disk size or clear downloaded lyrics.

Positive timing offsets delay lyric changes; negative values advance them. The synchronization controls are available directly on the **Lyrics** screen and under **Settings > Lyrics synchronization**.

## Verify taskbar lyrics

1. Start TaskbarLyriz with `.\scripts\run-dev.ps1` and play a track with synchronized lyrics.
2. The current line appears inside the free taskbar gap between Weather and the centered Start/app buttons.
3. Open **Taskbar** to switch between the left and right free regions or change the preferred width.
4. Disable **Show taskbar lyrics** on that page, under **Settings**, or from the notification-area menu to hide it immediately.

The surface is a separate click-through window positioned from current Windows geometry. It does not inject UI into Explorer. If occupied taskbar controls leave too little room, TaskbarLyriz hides the surface instead of covering them.

Brief GSMTC disconnects from browser players are held for eight seconds so switching apps or tabs does not erase the current synchronized line. A low-frequency placement heartbeat also restores the surface if Explorer restacks the taskbar after minimizing or app switching.

## Adjust lyric appearance

Open **Appearance** from the navigation pane. Font family and font size update the taskbar line, Home live-lyrics card, and Lyrics screen immediately. Theme, taskbar-surface opacity, corner radius, and line-change style are also saved automatically. Larger taskbar fonts increase the lyric window height only within the existing taskbar band.

## Solution layout

```text
src/
  TaskbarLyriz.App             WinUI composition root, views, and view models
  TaskbarLyriz.Core            Domain settings and service contracts
  TaskbarLyriz.Infrastructure  File storage and structured logging
  TaskbarLyriz.Windows         WinRT/Win32 lifecycle and shell integration
tests/
  TaskbarLyriz.Core.Tests
  TaskbarLyriz.Infrastructure.Tests
  TaskbarLyriz.Windows.Tests
docs/
  architecture.md
```

See [docs/architecture.md](docs/architecture.md) for design boundaries and the next milestone.
