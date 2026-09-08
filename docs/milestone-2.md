# Milestone 2 — Media Session

## Acceptance coverage

| Requirement | Implementation |
| --- | --- |
| GSMTC detection | Event-driven `GsmTcMediaSessionService` |
| Track metadata | Title, artist, album, album artist, track number, and bounded artwork |
| Playback state | Closed, opened, changing, stopped, playing, and paused mapping |
| Position | GSMTC timeline plus playback-rate-aware display interpolation |
| Duration | Normalized from timeline start/end and clamped in Core |
| Application/source | Raw AUMID/executable identifier plus generic readable-name normalization |
| Automatic track changes | `MediaPropertiesChanged` refresh and immutable snapshot publication |
| Debug screen | Playback page showing player, track, state, timeline, source ID, session ID, and count |

## Session selection

The system-preferred GSMTC session wins when available. The fallback order is playing, paused, changing, opened, stopped, and closed. Equal states prefer the most recently updated session. This policy is unit tested and contains no player-specific allowlist.

## Failure behavior

- Access denial or manager startup failure is surfaced as a diagnostic state rather than crashing the app.
- Individual metadata and artwork failures leave the session available with safe fallback text.
- A disappearing media session is unsubscribed and removed during reconciliation.
- Shutdown cancels queued refreshes and removes all WinRT event handlers.
- Logs record service state/source transitions but do not record track titles or artists.

## Manual smoke test

```powershell
.\scripts\run-dev.ps1
```

Start playback, then check Home and Playback. Confirm play/pause, seek, and track changes update automatically. Stop the player and confirm TaskbarLyriz returns to an idle state without exiting.
