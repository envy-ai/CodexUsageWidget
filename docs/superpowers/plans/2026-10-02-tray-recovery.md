# Tray Recovery Implementation Plan

> **For agentic workers:** Use executing-plans to implement task by task in the existing implementation worktree. Icon artwork is delegated to Astra/medium at the user's request.

**Goal:** Recover a hidden widget from the tray, expose refresh/exit menus, hide absent five-hour limits, and launch this build at login.

**Architecture:** MainWindow owns hide/restore/exit behavior. App owns the NotifyIcon, dispatches menu actions to the WPF thread and disposes resources when the window actually closes. MainViewModel exposes five-hour availability; XAML collapses the associated meter and countdown.

**Tech Stack:** .NET 8 WPF, Windows Forms NotifyIcon, xUnit, SVG and ICO.

### Task 1: Optional five-hour window
- [x] Add transition test: present window at 0%, failed refresh, then absent window. Expect visible, visible/stale, then hidden and empty reset text.
- [x] Run `dotnet test --no-restore --filter Category!=Live` to confirm failure before adding visibility.
- [x] Add `public bool HasFiveHourLimit => FiveHourRemaining.HasValue;` and notify when FiveHourRemaining changes.
- [x] Bind meter and countdown Visibility through BooleanToVisibilityConverter and collapse their unused spacing.

### Task 2: Recovery lifecycle and menus
- [x] Add STA integration test that constructs a real window, shows it, closes it, restores it, then explicitly exits. Assert hidden after Close, visible after ShowWidget, Closed only after ExitWidget. Test that the widget menu contains Exit and Refresh Immediately.
- [x] Implement `ShowWidget()` using Show and Activate. Implement `ExitWidget()` by setting an explicit exit flag then Close. Cancel ordinary Closing and Hide after saving settings.
- [x] Create NotifyIcon with Show widget, Refresh Immediately and Exit. Marshal callbacks through Dispatcher. Keep client alive while hidden; dispose NotifyIcon and MainViewModel only after actual Closed.
- [x] Add SVG/ICO artwork and executable/window icon resource.

### Task 3: Verify and restart
- [x] Run all non-live tests and Release build, checking warnings/errors.
- [x] Use a user-session script to verify the live app-server response, close/restore behavior and tray menu handlers.
- [x] Stop only old CodexUsageWidget processes from this project, start the new Release executable, and update HKCU Run/CodexUsageWidget to its quoted full path.
- [x] Verify one running widget matches the startup path and reports current weekly data with the five-hour row absent.


## Verification results

October 2, 2026: Release build succeeded without warnings or errors. Full test suite passed (28 tests), including live account read and application-level tray Show, double-click, Refresh Immediately, Exit, and absent five-hour row checks. Running executable and HKCU startup entry both use bin/Release/net8.0-windows/CodexUsageWidget.exe. SVG artwork supplied by Astra/medium; ICO includes eight sizes. Existing reset text changes preserved. Windows Computer Use does not expose this tool window; live UI behavior was verified through the application integration test.

