# Codex Usage Widget for Windows

A small Windows desktop widget that displays remaining Codex usage and reset times. Built with .NET 8 and WPF; requires Windows.

## Features

- Weekly usage meter and an optional five-hour meter, shown only when Codex reports that limit.
- Right-aligned yellow reset dates with a crisp black outline for readability over desktop backgrounds.
- Drag to move, resize, and pin above other windows.
- Close to hide; restore with the tray icon's **Show widget** menu or a double-click.
- Right-click the widget or tray icon for **Refresh Immediately** and **Exit**.
- **Start with Windows** toggle in the widget menu.
- Automatic refresh every minute, with the last successful data retained and marked stale when a refresh fails.

## Requirements and build

Windows, the .NET 8 SDK, and an installed Codex executable signed into your account. The widget reads usage through the local Codex app-server.

```powershell
dotnet build CodexUsageWidget.sln -c Release
.\src\CodexUsageWidget\bin\Release\net8.0-windows\CodexUsageWidget.exe
```

Enable **Start with Windows** after launching the build you want to use. Keep its executable and accompanying files in that location.

## Tests

```powershell
dotnet test CodexUsageWidget.sln -c Release --filter "Category!=Live"
```

To include the live usage and tray interaction checks, run without the filter while signed into Codex on Windows. The tray check opens and closes a temporary widget window.

Settings are stored under `%LOCALAPPDATA%\CodexUsageWidget`. The original icon source is `src/CodexUsageWidget/Assets/widget.svg`; the app and tray use its multi-size ICO conversion.

## Codex discovery and authentication

The widget looks for `codex.exe` on PATH, in the current user's `.codex/.sandbox-bin` directory, and in the installed Windows Codex package. It checks candidates with `--version`. A packaged executable may be copied into `%LOCALAPPDATA%\CodexUsageWidget\CodexCli` to run outside the package directory.

The widget starts `codex app-server` and communicates over local standard input/output, requesting `account/rateLimits/read`. Codex uses its existing authentication and contacts its service to retrieve usage. The widget does not read or persist passwords, API keys, or access tokens. Its settings file stores window position, monitor, dimensions, pin state, and startup preference. Windows startup uses an entry in the current user's Run registry key.

## License

MIT. See [LICENSE](LICENSE).
