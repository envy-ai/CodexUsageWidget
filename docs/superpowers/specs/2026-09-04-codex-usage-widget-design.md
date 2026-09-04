# Codex Usage Widget — Design Specification

**Date:** 2026-09-04

**Target:** Windows 10, x64

**Implementation:** .NET 8 WPF

## Goal

Build a compact desktop-level HUD that displays the remaining allowance and reset timing for the current Codex five-hour and weekly usage windows. The widget must use the user's existing Codex authentication without copying or exposing credentials.

## Scope

The first release includes:

- Five-hour and weekly remaining percentages.
- A horizontal meter for each window.
- A countdown and local reset date/time for each available window.
- Graceful display of missing windows.
- Manual refresh, optional launch at sign-in, and exit controls.
- Remembered position across launches and monitors.

The first release does not include historical charts, token counts, credit-reset redemption, Spark/model-specific buckets, remote-machine usage, installer UI, or an always-on-top mode.

## Data source

The widget communicates with the locally installed Codex CLI through the official Codex app-server JSONL protocol. It starts `codex app-server`, performs the required `initialize` handshake, and sends `account/rateLimits/read` once per refresh interval.

Executable discovery checks, in order:

1. `codex.exe` available through `PATH`.
2. `%USERPROFILE%\.codex\.sandbox-bin\codex.exe`.

If neither path resolves to a runnable Codex CLI, the widget enters its unavailable state and reports that Codex could not be found; it does not search arbitrary executables or modify Codex configuration.

The app-server interface is documented by OpenAI but currently marked experimental. The protocol is therefore isolated behind a small client interface so response handling or transport can be replaced without changing the UI.

## Architecture

### `CodexAppServerClient`

- Owns one app-server child process.
- Completes the initialization handshake once per process.
- Sends rate-limit requests with correlated request IDs.
- Continuously drains stdout notifications and stderr so the child cannot block.
- Applies a request timeout and cancels cleanly during shutdown.
- Restarts after process failure on the next refresh attempt.
- Does not persist raw responses or authentication data.

### `UsageSnapshotParser`

- Reads the general `codex` bucket from `rateLimitsByLimitId` when present and falls back to the backward-compatible `rateLimits` object.
- Identifies windows by `windowDurationMins`, not by `primary` or `secondary` position.
- Maps 300 minutes to the five-hour meter and 10,080 minutes to the weekly meter.
- Converts `usedPercent` to remaining percentage with `clamp(100 - usedPercent, 0, 100)`.
- Treats an absent window, duration, percentage, or reset timestamp as unavailable rather than inventing a value.

### `UsageViewModel`

- Refreshes usage every 60 seconds.
- Updates countdown text locally between server refreshes.
- Retains the last successful snapshot during temporary failures.
- Exposes current, stale, connecting, and unavailable states to the view.

### `SettingsStore`

Stores a small JSON document under `%LOCALAPPDATA%\CodexUsageWidget\settings.json` containing:

- Last window position and monitor identity.
- Whether launch at sign-in is enabled.

Malformed settings fall back to safe defaults. Saving is atomic through a temporary file and replacement.

### `StartupManager`

Manages an explicit current-user value under:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

No administrator privileges are required. The menu check state is read back from the registry rather than assumed from the last write.

## Visual design

The widget is approximately 320 × 78 device-independent pixels and uses a transparent, borderless WPF window containing a dark translucent rounded panel.

Each meter row contains:

1. A fixed-width `5 HOUR` or `WEEK` label.
2. A horizontal remaining-capacity track.
3. A right-aligned remaining percentage.
4. A secondary line containing both countdown and local reset time, such as `2h 14m · resets Fri 3:18 PM`.

Unavailable values display `—`; the five-hour row remains visible even when no five-hour cap is reported.

Remaining-capacity colors are:

- Green above 50%.
- Amber from 20% through 50%.
- Red below 20%.
- Muted gray when unavailable.

The window is not topmost. Normal application windows cover it. It is excluded from the taskbar and Alt-Tab.

## Interaction

- Dragging anywhere on the panel repositions the widget.
- The final position is saved after a drag completes.
- Hovering over the panel reveals a small close button in the upper-right corner.
- Right-clicking opens a context menu with:
  - **Refresh now**
  - **Start with Windows**
  - **Exit**
- Start with Windows is disabled by default.
- When a saved monitor no longer exists, the widget is moved into the nearest visible work area.

## Failure behavior

- Initial state: muted meters with `Connecting…`.
- Temporary request or process failure after a successful reading: retain values and show `STALE` with the last successful update time.
- Failure before any successful reading: show `—` for both meters and `Codex unavailable`.
- Missing five-hour data: show `5 HOUR —`; this is not treated as an error.
- App-server crash: dispose of the failed process and reconnect on the next refresh or manual refresh.
- Widget shutdown: stop timers, cancel pending requests, and terminate only the child process created by the widget.

## Security and privacy

- Reuse Codex's existing authentication through app-server.
- Never read or copy Codex token files directly.
- Never log raw app-server responses, account identifiers, tokens, prompts, or conversation data.
- Bind no listening network port; use the default stdio transport.
- Launch only the discovered local Codex executable.

## Testing

Focused automated tests cover:

- Five-hour and weekly windows in either primary/secondary order.
- The multi-bucket response and backward-compatible fallback response.
- Missing five-hour data.
- Used-to-remaining conversion and boundary clamping.
- Reset countdown and local-time formatting.
- Malformed and incomplete response payloads.
- Stale-state transitions after a successful snapshot.
- Settings fallback and visible-screen position correction.

Manual and integration verification covers:

- Successful release build.
- App-server handshake and live `account/rateLimits/read` response.
- Launch on the current Windows 10 desktop.
- Correct rendering of the user's live weekly remaining percentage.
- `5 HOUR —` when the general Codex bucket omits that window.
- Dragging and position restoration.
- Hover close control and context menu behavior.
- Startup toggle registry write and read-back, with the registry entry removed after verification unless the user enables it.
- Clean shutdown without an orphaned app-server process.

## Packaging

Produce:

- A normal release build for development and diagnostics.
- A self-contained Windows x64 publish directory that does not require a separately installed .NET runtime.
- A desktop shortcut for convenient first launch.

The build remains source-available in this project directory. No installer or system-wide files are required.

## Acceptance criteria

The work is complete when:

1. The published widget launches on this Windows 10 machine without a console window.
2. It obtains a live Codex rate-limit snapshot without requesting credentials.
3. The weekly meter shows the correct remaining percentage and both reset representations.
4. The five-hour meter shows a correct value when present and `—` when absent.
5. Normal applications cover the widget, and it does not appear in the taskbar or Alt-Tab.
6. Drag position persists and remains visible after relaunch.
7. Hover close, refresh, startup toggle, and exit controls work.
8. Automated tests and the release build pass.
9. No Codex authentication material or account identifier is written by the widget.
