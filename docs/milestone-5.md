# Milestone 5 - Taskbar experience

Milestone 5 projects the synchronized current line into the unused part of the primary bottom taskbar without injecting into or modifying Explorer.

## Delivered

- Compact single-line taskbar lyric surface with progress underline
- Primary-monitor, bottom-taskbar geometry from Windows app-bar APIs
- Default left placement in the free region between Weather/widgets and the centered app strip
- Optional right placement before the notification area
- Width clamping to the currently detected safe gap
- Automatic hiding when a safe minimum width is unavailable
- `WS_EX_NOACTIVATE` focus protection
- `WS_EX_TOOLWINDOW` removal from Alt+Tab
- Transparent hit testing so taskbar pointer input is not intercepted
- Borderless always-on-top presentation with lightweight lyric transitions
- Automatic show for synchronized Playing or Paused sessions
- Automatic hide for stopped/closed sessions, missing synchronized lyrics, or a disabled setting
- Taskbar page for enablement, side selection, and preferred width
- Appearance page with a live taskbar-sized preview
- App theme and taskbar-surface opacity controls
- Persistent lyric font family and font size shared with Home and Lyrics
- Configurable surface corner radius and line-change style
- Font-aware surface height clamped inside the taskbar band
- Eight-second grace for transient browser media-session disconnects
- Two-second placement heartbeat that survives minimize, app switching, and Explorer restacking
- Notification-area toggle synchronized with settings changes
- Schema 4 settings migration that preserves the taskbar gap and adds typography defaults

## Placement policy

TaskbarLyriz reads the live taskbar, app-strip, and notification-area bounds. The left mode reserves the Windows widgets/weather end and stops before the centered Start/application region. The right mode begins after the reserved centered region and stops before the system tray. The preferred width is DPI-scaled and clamped within that interval. No fixed screen resolution or absolute taskbar coordinate is used.

Appearance changes are persisted through the evented settings service. The taskbar surface, Home live-lyrics card, and full Lyrics screen consume the same font family and base size. Opacity and corner radius remain specific to the compact taskbar surface, while the selected theme applies to the native shell and lyric window.

Browser media sessions can briefly disappear while switching tabs or foreground applications. The lyric view model retains the last synchronized state for an eight-second grace period and cancels the reset when the session returns. Separately, the taskbar window reasserts its no-activate placement every two seconds while it should be visible, preventing Explorer z-order changes from leaving it obscured.

Milestone 5 intentionally supports the primary monitor with a bottom taskbar. Other edges, display changes, all-monitor surfaces, custom coordinates, hover controls, and expanded interaction belong to Milestone 6.

## Verification scope

Automated tests cover left and right safe-gap placement, width clamping, missing-space behavior, negative monitor coordinates, show/hide playback policy, settings migration, and native no-activate/tool-window/transparent styles. Live checks inspect the taskbar control rectangles, window bounds and styles, foreground ownership, hit-test result, toggle lifecycle, and synchronized lyric accessibility.
