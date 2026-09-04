# Codex Usage Widget Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and verify a compact Windows 10 desktop HUD that shows remaining Codex five-hour and weekly allowances with reset timing.

**Architecture:** A .NET 8 WPF presentation layer consumes a small domain model populated by a persistent stdio connection to `codex app-server`. Parsing, time formatting, settings, startup registration, and window placement remain separate units so the experimental protocol can change without rewriting the UI.

**Tech Stack:** C# 12, .NET 8, WPF, System.Text.Json, xUnit, Windows Registry, Codex app-server JSONL protocol

**Protocol reference:** [Official Codex app-server documentation](https://developers.openai.com/codex/app-server)

---

## File map

- `CodexUsageWidget.sln` — solution containing application and tests.
- `src/CodexUsageWidget/CodexUsageWidget.csproj` — WPF executable project.
- `src/CodexUsageWidget/App.xaml` — application resources and startup window.
- `src/CodexUsageWidget/App.xaml.cs` — composition root and shutdown cleanup.
- `src/CodexUsageWidget/MainWindow.xaml` — compact meter-bar view.
- `src/CodexUsageWidget/MainWindow.xaml.cs` — drag, hover-close, context menu, placement, and shutdown interactions.
- `src/CodexUsageWidget/Domain/UsageSnapshot.cs` — immutable five-hour/weekly usage records.
- `src/CodexUsageWidget/Services/UsageSnapshotParser.cs` — maps app-server JSON to the domain model.
- `src/CodexUsageWidget/Services/ResetTimeFormatter.cs` — countdown and local reset display.
- `src/CodexUsageWidget/Services/CodexExecutableLocator.cs` — deterministic CLI discovery.
- `src/CodexUsageWidget/Services/CodexAppServerClient.cs` — persistent JSONL process client.
- `src/CodexUsageWidget/Services/SettingsStore.cs` — atomic JSON settings persistence.
- `src/CodexUsageWidget/Services/StartupManager.cs` — current-user Run-key toggle.
- `src/CodexUsageWidget/Services/WindowPlacementService.cs` — restore and clamp the HUD to a visible monitor work area.
- `src/CodexUsageWidget/ViewModels/MainViewModel.cs` — timers, refresh state, stale state, and bindable meter values.
- `tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj` — xUnit test project.
- `tests/CodexUsageWidget.Tests/UsageSnapshotParserTests.cs` — protocol-shape and percentage tests.
- `tests/CodexUsageWidget.Tests/ResetTimeFormatterTests.cs` — deterministic display-format tests.
- `tests/CodexUsageWidget.Tests/SettingsStoreTests.cs` — settings round-trip and malformed-file tests.
- `tests/CodexUsageWidget.Tests/WindowPlacementServiceTests.cs` — off-screen position clamping tests.
- `tests/CodexUsageWidget.Tests/MainViewModelTests.cs` — refresh and stale-state tests using a fake client and clock.
- `scripts/create-desktop-shortcut.ps1` — creates or updates the current-user desktop shortcut.
- `README.md` — build, launch, interaction, and troubleshooting instructions.

## Verification level

Use strict RED→GREEN TDD for JSON parsing, time formatting, settings fallback, and view-model state transitions because those behaviors are easy to isolate and likely to regress. Implement WPF layout, registry integration, process lifecycle, packaging, and shortcut creation directly, then verify them with focused integration and manual checks; adding heavy UI automation would cost more than it protects for this local widget.

### Task 1: Scaffold the solution

**Files:**
- Create: `CodexUsageWidget.sln`
- Create: `src/CodexUsageWidget/CodexUsageWidget.csproj`
- Create: `tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj`

- [ ] **Step 1: Generate the solution and projects**

Run from the repository root:

```bash
dotnet new sln -n CodexUsageWidget
dotnet new wpf -n CodexUsageWidget -o src/CodexUsageWidget --framework net8.0
dotnet new xunit -n CodexUsageWidget.Tests -o tests/CodexUsageWidget.Tests --framework net8.0
```

Expected: the solution and both project directories are generated without errors.

- [ ] **Step 2: Configure both projects for the same Windows target**

Set `src/CodexUsageWidget/CodexUsageWidget.csproj` to:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <AssemblyName>CodexUsageWidget</AssemblyName>
    <RootNamespace>CodexUsageWidget</RootNamespace>
  </PropertyGroup>
</Project>
```

Set `tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj` to:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Wire the projects together and verify the scaffold**

Delete `tests/CodexUsageWidget.Tests/UnitTest1.cs`, then run:

```bash
dotnet sln CodexUsageWidget.sln add src/CodexUsageWidget/CodexUsageWidget.csproj tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj
dotnet add tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj reference src/CodexUsageWidget/CodexUsageWidget.csproj
dotnet build CodexUsageWidget.sln
dotnet test CodexUsageWidget.sln --no-build
```

Expected: the project reference is accepted, the build succeeds with zero warnings or errors, and the test command succeeds with no tests discovered.

- [ ] **Step 4: Commit the scaffold**

```bash
git add CodexUsageWidget.sln src tests
git commit -m "build: scaffold Codex usage widget"
```

### Task 2: Parse Codex rate-limit snapshots

**Files:**
- Create: `src/CodexUsageWidget/Domain/UsageSnapshot.cs`
- Create: `src/CodexUsageWidget/Services/UsageSnapshotParser.cs`
- Create: `tests/CodexUsageWidget.Tests/UsageSnapshotParserTests.cs`

- [ ] **Step 1: Write the failing parser tests**

Create `tests/CodexUsageWidget.Tests/UsageSnapshotParserTests.cs`:

```csharp
using CodexUsageWidget.Services;

namespace CodexUsageWidget.Tests;

public sealed class UsageSnapshotParserTests
{
    [Fact]
    public void ParsesGeneralBucketAndConvertsUsedToRemaining()
    {
        const string json = """
        {"id":2,"result":{"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":25,"windowDurationMins":300,"resetsAt":2000},"secondary":{"usedPercent":40,"windowDurationMins":10080,"resetsAt":3000}}}}}
        """;

        var snapshot = UsageSnapshotParser.ParseResponse(json);

        Assert.Equal(75, snapshot.FiveHour!.RemainingPercent);
        Assert.Equal(2000, snapshot.FiveHour.ResetsAt.ToUnixTimeSeconds());
        Assert.Equal(60, snapshot.Weekly!.RemainingPercent);
        Assert.Equal(3000, snapshot.Weekly.ResetsAt.ToUnixTimeSeconds());
    }

    [Fact]
    public void FindsWindowsRegardlessOfPrimarySecondaryOrder()
    {
        const string json = """
        {"result":{"rateLimits":{"primary":{"usedPercent":10,"windowDurationMins":10080,"resetsAt":3000},"secondary":{"usedPercent":90,"windowDurationMins":300,"resetsAt":2000}}}}
        """;

        var snapshot = UsageSnapshotParser.ParseResponse(json);

        Assert.Equal(10, snapshot.FiveHour!.RemainingPercent);
        Assert.Equal(90, snapshot.Weekly!.RemainingPercent);
    }

    [Fact]
    public void MissingFiveHourWindowIsUnavailable()
    {
        const string json = """
        {"result":{"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":23,"windowDurationMins":10080,"resetsAt":3000},"secondary":null}}}}
        """;

        var snapshot = UsageSnapshotParser.ParseResponse(json);

        Assert.Null(snapshot.FiveHour);
        Assert.Equal(77, snapshot.Weekly!.RemainingPercent);
    }

    [Theory]
    [InlineData(-10, 100)]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(140, 0)]
    public void RemainingPercentIsClamped(int used, int expected)
    {
        var json = $$"""{"result":{"rateLimits":{"primary":{"usedPercent":{{used}},"windowDurationMins":300,"resetsAt":2000}}}}""";
        Assert.Equal(expected, UsageSnapshotParser.ParseResponse(json).FiveHour!.RemainingPercent);
    }

    [Fact]
    public void MalformedOrIncompleteResponseThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => UsageSnapshotParser.ParseResponse("{\"result\":{}}"));
        Assert.Throws<FormatException>(() => UsageSnapshotParser.ParseResponse("not json"));
    }
}
```

- [ ] **Step 2: Run the parser tests and verify RED**

```bash
dotnet test tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj --filter UsageSnapshotParserTests
```

Expected: FAIL because `UsageSnapshotParser` and domain records do not exist.

- [ ] **Step 3: Add the domain records**

Create `src/CodexUsageWidget/Domain/UsageSnapshot.cs`:

```csharp
namespace CodexUsageWidget.Domain;

public sealed record UsageWindow(int RemainingPercent, DateTimeOffset ResetsAt);

public sealed record UsageSnapshot(UsageWindow? FiveHour, UsageWindow? Weekly)
{
    public bool HasAnyValue => FiveHour is not null || Weekly is not null;
}
```

- [ ] **Step 4: Implement the parser**

Create `src/CodexUsageWidget/Services/UsageSnapshotParser.cs`:

```csharp
using System.Text.Json;
using CodexUsageWidget.Domain;

namespace CodexUsageWidget.Services;

public static class UsageSnapshotParser
{
    public static UsageSnapshot ParseResponse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var result = document.RootElement.GetProperty("result");
            JsonElement limits;

            if (result.TryGetProperty("rateLimitsByLimitId", out var buckets) &&
                buckets.ValueKind == JsonValueKind.Object &&
                buckets.TryGetProperty("codex", out var codex))
            {
                limits = codex;
            }
            else if (result.TryGetProperty("rateLimits", out var fallback) &&
                     fallback.ValueKind == JsonValueKind.Object)
            {
                limits = fallback;
            }
            else
            {
                throw new FormatException("Codex rate-limit data was not present.");
            }

            UsageWindow? fiveHour = null;
            UsageWindow? weekly = null;
            ReadWindow(limits, "primary", ref fiveHour, ref weekly);
            ReadWindow(limits, "secondary", ref fiveHour, ref weekly);
            return new UsageSnapshot(fiveHour, weekly);
        }
        catch (FormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new FormatException("The Codex rate-limit response was malformed.", exception);
        }
    }

    private static void ReadWindow(JsonElement limits, string name, ref UsageWindow? fiveHour, ref UsageWindow? weekly)
    {
        if (!limits.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object)
            return;
        if (!element.TryGetProperty("usedPercent", out var usedElement) ||
            !element.TryGetProperty("windowDurationMins", out var durationElement) ||
            !element.TryGetProperty("resetsAt", out var resetElement) ||
            !usedElement.TryGetInt32(out var used) ||
            !durationElement.TryGetInt64(out var duration) ||
            !resetElement.TryGetInt64(out var reset))
            return;

        var window = new UsageWindow(Math.Clamp(100 - used, 0, 100), DateTimeOffset.FromUnixTimeSeconds(reset));
        if (duration == 300) fiveHour = window;
        if (duration == 10080) weekly = window;
    }
}
```

- [ ] **Step 5: Run parser tests and verify GREEN**

```bash
dotnet test tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj --filter UsageSnapshotParserTests
```

Expected: all parser tests pass.

- [ ] **Step 6: Commit parser behavior**

```bash
git add src/CodexUsageWidget/Domain src/CodexUsageWidget/Services/UsageSnapshotParser.cs tests/CodexUsageWidget.Tests/UsageSnapshotParserTests.cs
git commit -m "feat: parse Codex usage windows"
```

### Task 3: Format reset timing

**Files:**
- Create: `src/CodexUsageWidget/Services/ResetTimeFormatter.cs`
- Create: `tests/CodexUsageWidget.Tests/ResetTimeFormatterTests.cs`

- [ ] **Step 1: Write failing deterministic formatter tests**

Create `tests/CodexUsageWidget.Tests/ResetTimeFormatterTests.cs`:

```csharp
using CodexUsageWidget.Services;

namespace CodexUsageWidget.Tests;

public sealed class ResetTimeFormatterTests
{
    [Fact]
    public void FormatsCountdownAndLocalClockTogether()
    {
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.FromHours(-4));
        var reset = now.AddHours(2).AddMinutes(14);
        var text = ResetTimeFormatter.Format(reset, now, TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(-4), "test", "test"));
        Assert.Equal("2h 14m · resets Fri 2:14 PM", text);
    }

    [Fact]
    public void ExpiredWindowShowsResetting()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal("resetting…", ResetTimeFormatter.Format(now.AddSeconds(-1), now, TimeZoneInfo.Utc));
    }

    [Fact]
    public void MultiDayCountdownIncludesDays()
    {
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var text = ResetTimeFormatter.Format(now.AddDays(2).AddHours(3), now, TimeZoneInfo.Utc);
        Assert.Equal("2d 3h · resets Sun 3:00 PM", text);
    }
}
```

- [ ] **Step 2: Run tests and verify RED**

```bash
dotnet test tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj --filter ResetTimeFormatterTests
```

Expected: FAIL because `ResetTimeFormatter` does not exist.

- [ ] **Step 3: Implement formatting**

Create `src/CodexUsageWidget/Services/ResetTimeFormatter.cs`:

```csharp
namespace CodexUsageWidget.Services;

public static class ResetTimeFormatter
{
    public static string Format(DateTimeOffset reset, DateTimeOffset now, TimeZoneInfo zone)
    {
        var remaining = reset - now;
        if (remaining <= TimeSpan.Zero) return "resetting…";

        var countdown = remaining.TotalDays >= 1
            ? $"{(int)remaining.TotalDays}d {remaining.Hours}h"
            : remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours}h {remaining.Minutes}m"
                : $"{Math.Max(1, remaining.Minutes)}m";
        var local = TimeZoneInfo.ConvertTime(reset, zone);
        return $"{countdown} · resets {local:ddd h:mm tt}";
    }
}
```

- [ ] **Step 4: Run formatter and full tests**

```bash
dotnet test CodexUsageWidget.sln
```

Expected: all tests pass.

- [ ] **Step 5: Commit formatting**

```bash
git add src/CodexUsageWidget/Services/ResetTimeFormatter.cs tests/CodexUsageWidget.Tests/ResetTimeFormatterTests.cs
git commit -m "feat: format Codex reset timing"
```

### Task 4: Connect to Codex app-server

**Files:**
- Create: `src/CodexUsageWidget/Services/ICodexUsageClient.cs`
- Create: `src/CodexUsageWidget/Services/CodexExecutableLocator.cs`
- Create: `src/CodexUsageWidget/Services/CodexAppServerClient.cs`

- [ ] **Step 1: Add the client contract and executable locator**

Create `src/CodexUsageWidget/Services/ICodexUsageClient.cs`:

```csharp
using CodexUsageWidget.Domain;

namespace CodexUsageWidget.Services;

public interface ICodexUsageClient : IAsyncDisposable
{
    Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken);
}
```

Create `src/CodexUsageWidget/Services/CodexExecutableLocator.cs`:

```csharp
using System.Diagnostics;
using Microsoft.Win32;

namespace CodexUsageWidget.Services;

public static class CodexExecutableLocator
{
    private const string PackageRepositoryKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    public static string? Find()
    {
        var candidates = new List<string>();
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        candidates.AddRange(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => Path.Combine(directory, "codex.exe")));
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        candidates.Add(Path.Combine(profile, ".codex", ".sandbox-bin", "codex.exe"));
        candidates.AddRange(StagePackagedCliCandidates());
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(IsRunnable);
    }

    private static IEnumerable<string> StagePackagedCliCandidates()
    {
        using var packages = Registry.CurrentUser.OpenSubKey(PackageRepositoryKey);
        if (packages is null) yield break;
        foreach (var packageId in packages.GetSubKeyNames()
                     .Where(name => name.StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase))
        {
            using var package = packages.OpenSubKey(packageId);
            if (package?.GetValue("PackageRootFolder") is not string root) continue;
            var source = Path.Combine(root, "app", "resources", "codex.exe");
            if (!File.Exists(source)) continue;
            var cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexUsageWidget", "CodexCli", packageId);
            var destination = Path.Combine(cacheDirectory, "codex.exe");
            if (!File.Exists(destination))
            {
                Directory.CreateDirectory(cacheDirectory);
                var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.Copy(source, temporary, overwrite: false);
                    try { File.Move(temporary, destination, overwrite: false); }
                    catch (IOException) when (File.Exists(destination)) { File.Delete(temporary); }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    try { File.Delete(temporary); } catch { }
                    continue;
                }
            }
            yield return destination;
        }
    }

    private static bool IsRunnable(string candidate)
    {
        if (!File.Exists(candidate)) return false;
        try
        {
            using var process = Process.Start(new ProcessStartInfo(candidate, "--version")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process is null || !process.WaitForExit(3000))
            {
                try { process?.Kill(entireProcessTree: true); } catch { }
                return false;
            }
            return process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
```

- [ ] **Step 2: Implement the persistent JSONL client**

Create `src/CodexUsageWidget/Services/CodexAppServerClient.cs` with these concrete behaviors:

```csharp
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using CodexUsageWidget.Domain;

namespace CodexUsageWidget.Services;

public sealed class CodexAppServerClient : ICodexUsageClient
{
    private readonly string _executable;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<string>> _pending = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private Process? _process;
    private CancellationTokenSource? _readerCancellation;
    private Task? _readerTask;
    private Task? _stderrTask;
    private long _requestId;
    private int _disposed;

    public CodexAppServerClient(string executable) => _executable = executable;

    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCancellation.Token);
        await _gate.WaitAsync(linked.Token);
        try
        {
            await EnsureStartedAsync(linked.Token);
            var response = await SendRequestAsync("account/rateLimits/read", null, linked.Token);
            return UsageSnapshotParser.ParseResponse(response);
        }
        catch
        {
            await StopProcessAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_process is { HasExited: false }) return;
        await StopProcessAsync();

        var startInfo = new ProcessStartInfo(_executable, "app-server")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = System.Text.Encoding.UTF8,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Codex app-server.");
        _readerCancellation = new CancellationTokenSource();
        _readerTask = ReadStdoutAsync(_process, _readerCancellation.Token);
        _stderrTask = DrainStderrAsync(_process, _readerCancellation.Token);

        var initialize = await SendRequestAsync("initialize", new
        {
            clientInfo = new { name = "codex-usage-widget", version = "1.0.0" }
        }, cancellationToken);
        using var document = JsonDocument.Parse(initialize);
        if (!document.RootElement.TryGetProperty("result", out _))
            throw new InvalidOperationException("Codex app-server initialization failed.");
        await WriteLineAsync(JsonSerializer.Serialize(new { method = "initialized", @params = new { } }), cancellationToken);
    }

    private async Task<string> SendRequestAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _requestId);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;
        try
        {
            await WriteLineAsync(JsonSerializer.Serialize(new { method, id, @params = parameters }), cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            return await completion.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task WriteLineAsync(string line, CancellationToken cancellationToken)
    {
        var process = _process ?? throw new InvalidOperationException("Codex app-server is not running.");
        await process.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
    }

    private async Task ReadStdoutAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                if (line is null) break;
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.TryGetProperty("id", out var idElement) && idElement.TryGetInt64(out var id) && _pending.TryGetValue(id, out var completion))
                    completion.TrySetResult(line);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or JsonException or IOException) { }
        finally
        {
            foreach (var completion in _pending.Values)
                completion.TrySetException(new IOException("Codex app-server disconnected."));
        }
    }

    private static async Task DrainStderrAsync(Process process, CancellationToken cancellationToken)
    {
        try { while (await process.StandardError.ReadLineAsync(cancellationToken) is not null) { } }
        catch (OperationCanceledException) { }
    }

    private async Task StopProcessAsync()
    {
        var process = _process;
        var cancellation = _readerCancellation;
        var readerTask = _readerTask;
        var stderrTask = _stderrTask;
        _process = null;
        _readerCancellation = null;
        _readerTask = null;
        _stderrTask = null;

        cancellation?.Cancel();
        if (process is { HasExited: false })
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }
        try
        {
            await Task.WhenAll(readerTask ?? Task.CompletedTask, stderrTask ?? Task.CompletedTask);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException) { }
        process?.Dispose();
        cancellation?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetimeCancellation.Cancel();
        await _gate.WaitAsync();
        try { await StopProcessAsync(); }
        finally
        {
            _gate.Release();
            _gate.Dispose();
            _lifetimeCancellation.Dispose();
        }
    }
}
```

- [ ] **Step 3: Build before integration testing**

```bash
dotnet build CodexUsageWidget.sln
```

Expected: build succeeds without warnings.

- [ ] **Step 4: Add and run a temporary live integration probe**

Create `tests/CodexUsageWidget.Tests/LiveCodexProbeTests.cs`:

```csharp
using CodexUsageWidget.Services;

namespace CodexUsageWidget.Tests;

public sealed class LiveCodexProbeTests
{
    [Fact]
    [Trait("Category", "Live")]
    public async Task ReadsAuthenticatedUsageFromInstalledCodex()
    {
        var executable = CodexExecutableLocator.Find();
        Assert.NotNull(executable);
        await using var client = new CodexAppServerClient(executable!);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var snapshot = await client.GetUsageAsync(timeout.Token);
        Assert.True(snapshot.HasAnyValue);
    }
}
```

Run:

```bash
dotnet test tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj --filter "Category=Live"
```

Expected: PASS using the current Codex login and no credential prompt. Keep this test but exclude the `Live` category from normal offline verification commands.

- [ ] **Step 5: Commit the app-server client**

```bash
git add src/CodexUsageWidget/Services tests/CodexUsageWidget.Tests/LiveCodexProbeTests.cs
git commit -m "feat: read live Codex usage"
```

### Task 5: Persist settings, startup, and placement

**Files:**
- Create: `src/CodexUsageWidget/Services/SettingsStore.cs`
- Create: `src/CodexUsageWidget/Services/StartupManager.cs`
- Create: `src/CodexUsageWidget/Services/WindowPlacementService.cs`
- Create: `tests/CodexUsageWidget.Tests/SettingsStoreTests.cs`
- Create: `tests/CodexUsageWidget.Tests/WindowPlacementServiceTests.cs`

- [ ] **Step 1: Write failing settings and placement tests**

Create `tests/CodexUsageWidget.Tests/SettingsStoreTests.cs`:

```csharp
using CodexUsageWidget.Services;

namespace CodexUsageWidget.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"CodexUsageWidget-{Guid.NewGuid():N}");

    [Fact]
    public void RoundTripsPositionAndStartupPreference()
    {
        var store = new SettingsStore(_directory);
        var expected = new WidgetSettings(12.5, 34.5, "DISPLAY1", true);
        store.Save(expected);
        Assert.Equal(expected, store.Load());
    }

    [Fact]
    public void MalformedFileFallsBackToDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "settings.json"), "broken");
        Assert.Equal(WidgetSettings.Default, new SettingsStore(_directory).Load());
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
```

Create `tests/CodexUsageWidget.Tests/WindowPlacementServiceTests.cs`:

```csharp
using CodexUsageWidget.Services;

namespace CodexUsageWidget.Tests;

public sealed class WindowPlacementServiceTests
{
    [Fact]
    public void ClampMovesOffScreenWindowIntoWorkArea()
    {
        var result = WindowPlacementService.Clamp(-900, 1200, 330, 86, 0, 0, 1920, 1040);
        Assert.Equal(0d, result.Left);
        Assert.Equal(954d, result.Top);
    }

    [Fact]
    public void ClampPreservesVisiblePosition()
    {
        var result = WindowPlacementService.Clamp(120, 80, 330, 86, 0, 0, 1920, 1040);
        Assert.Equal(new WindowPosition(120d, 80d), result);
    }
}
```

- [ ] **Step 2: Run settings and placement tests and verify RED**

```bash
dotnet test tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj --filter "SettingsStoreTests|WindowPlacementServiceTests"
```

Expected: FAIL because the settings and placement types do not exist.

- [ ] **Step 3: Implement atomic settings storage**

Create `src/CodexUsageWidget/Services/SettingsStore.cs`:

```csharp
using System.Text.Json;

namespace CodexUsageWidget.Services;

public sealed record WidgetSettings(double? Left, double? Top, string? MonitorDeviceName, bool StartWithWindows)
{
    public static WidgetSettings Default { get; } = new(null, null, null, false);
}

public sealed class SettingsStore
{
    private readonly string _directory;
    private string PathName => Path.Combine(_directory, "settings.json");

    public SettingsStore(string? directory = null) => _directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsageWidget");

    public WidgetSettings Load()
    {
        try
        {
            return File.Exists(PathName)
                ? JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(PathName)) ?? WidgetSettings.Default
                : WidgetSettings.Default;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return WidgetSettings.Default;
        }
    }

    public void Save(WidgetSettings settings)
    {
        Directory.CreateDirectory(_directory);
        var temporary = PathName + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, PathName, true);
    }
}
```

- [ ] **Step 4: Run settings tests and verify the settings slice is GREEN**

```bash
dotnet test tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj --filter SettingsStoreTests
```

Expected: both tests pass.

- [ ] **Step 5: Implement startup and placement services**

Create `src/CodexUsageWidget/Services/StartupManager.cs`:

```csharp
using Microsoft.Win32;

namespace CodexUsageWidget.Services;

public sealed class StartupManager
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexUsageWidget";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) is string value && string.Equals(value, QuotedExecutable(), StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled) key.SetValue(ValueName, QuotedExecutable(), RegistryValueKind.String);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string QuotedExecutable() => $"\"{Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable.")}\"";
}
```

Create `src/CodexUsageWidget/Services/WindowPlacementService.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace CodexUsageWidget.Services;

public readonly record struct WindowPosition(double Left, double Top);
public readonly record struct CapturedPlacement(double Left, double Top, string MonitorDeviceName);

public static class WindowPlacementService
{
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    public static WindowPosition Clamp(double left, double top, double width, double height,
        double workLeft, double workTop, double workRight, double workBottom) =>
        new(
            Math.Clamp(left, workLeft, workRight - width),
            Math.Clamp(top, workTop, workBottom - height));

    public static void Restore(Window window, WidgetSettings settings)
    {
        var handle = new WindowInteropHelper(window).Handle;
        GetWindowRect(handle, out var current);
        var width = Math.Max(1, current.Right - current.Left);
        var height = Math.Max(1, current.Bottom - current.Top);
        var savedPoint = new System.Drawing.Point((int)(settings.Left ?? current.Left), (int)(settings.Top ?? current.Top));
        var screen = Forms.Screen.AllScreens.FirstOrDefault(candidate => candidate.DeviceName == settings.MonitorDeviceName)
            ?? Forms.Screen.FromPoint(savedPoint);
        var work = screen.WorkingArea;
        var desiredLeft = settings.Left ?? work.Right - width - 20;
        var desiredTop = settings.Top ?? work.Bottom - height - 20;
        var position = Clamp(desiredLeft, desiredTop, width, height, work.Left, work.Top, work.Right, work.Bottom);
        SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(position.Left), (int)Math.Round(position.Top), 0, 0,
            SwpNoZOrder | SwpNoActivate);
    }

    public static CapturedPlacement Capture(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        GetWindowRect(handle, out var rect);
        var center = new System.Drawing.Point((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
        return new CapturedPlacement(rect.Left, rect.Top, Forms.Screen.FromPoint(center).DeviceName);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}
```

- [ ] **Step 6: Run all offline tests and commit**

```bash
dotnet test tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj --filter WindowPlacementServiceTests
dotnet test CodexUsageWidget.sln --filter "Category!=Live"
git add src/CodexUsageWidget/Services tests/CodexUsageWidget.Tests/SettingsStoreTests.cs tests/CodexUsageWidget.Tests/WindowPlacementServiceTests.cs
git commit -m "feat: persist widget preferences"
```

Expected: placement and full offline tests pass; commit succeeds.

### Task 6: Build the view model and HUD

**Files:**
- Create: `src/CodexUsageWidget/ViewModels/MainViewModel.cs`
- Create: `tests/CodexUsageWidget.Tests/MainViewModelTests.cs`
- Modify: `src/CodexUsageWidget/App.xaml`
- Modify: `src/CodexUsageWidget/App.xaml.cs`
- Modify: `src/CodexUsageWidget/MainWindow.xaml`
- Modify: `src/CodexUsageWidget/MainWindow.xaml.cs`

- [ ] **Step 1: Write a focused failing stale-state test**

Create `src/CodexUsageWidget/Properties/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("CodexUsageWidget.Tests")]
```

Create `tests/CodexUsageWidget.Tests/MainViewModelTests.cs`:

```csharp
using CodexUsageWidget.Domain;
using CodexUsageWidget.Services;
using CodexUsageWidget.ViewModels;

namespace CodexUsageWidget.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task FailedRefreshPreservesLastSnapshotAndMarksItStale()
    {
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        await using var client = new FakeClient(
            new UsageSnapshot(null, new UsageWindow(77, now.AddDays(2))),
            new IOException("offline"));
        await using var viewModel = new MainViewModel(client, () => now, TimeZoneInfo.Utc);

        await viewModel.RefreshAsync();
        Assert.Equal(77, viewModel.WeeklyRemaining);
        Assert.False(viewModel.IsStale);

        await viewModel.RefreshAsync();
        Assert.Equal(77, viewModel.WeeklyRemaining);
        Assert.True(viewModel.IsStale);
        Assert.StartsWith("STALE", viewModel.StatusText);
    }

    [Fact]
    public async Task InitialFailureShowsUnavailableWithoutInventingValues()
    {
        await using var client = new FakeClient(new IOException("offline"));
        await using var viewModel = new MainViewModel(client, () => DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc);

        await viewModel.RefreshAsync();

        Assert.Null(viewModel.FiveHourRemaining);
        Assert.Null(viewModel.WeeklyRemaining);
        Assert.Equal("Codex unavailable", viewModel.StatusText);
    }

    private sealed class FakeClient : ICodexUsageClient
    {
        private readonly Queue<object> _results;
        public FakeClient(params object[] results) => _results = new Queue<object>(results);

        public Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken)
        {
            var result = _results.Dequeue();
            return result is Exception exception
                ? Task.FromException<UsageSnapshot>(exception)
                : Task.FromResult((UsageSnapshot)result);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the view-model test and verify RED**

```bash
dotnet test tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj --filter MainViewModelTests
```

Expected: FAIL because `MainViewModel` does not exist.

- [ ] **Step 3: Implement `MainViewModel`**

Create `src/CodexUsageWidget/ViewModels/MainViewModel.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using CodexUsageWidget.Domain;
using CodexUsageWidget.Services;

namespace CodexUsageWidget.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly ICodexUsageClient _client;
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeZoneInfo _zone;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(60) };
    private readonly DispatcherTimer _countdownTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private UsageSnapshot? _snapshot;
    private bool _started;
    private int? _fiveHourRemaining;
    private int? _weeklyRemaining;
    private string _fiveHourResetText = string.Empty;
    private string _weeklyResetText = string.Empty;
    private string _statusText = "Connecting…";
    private bool _isStale;
    private DateTimeOffset? _lastSuccessfulUpdate;

    public MainViewModel(ICodexUsageClient client, Func<DateTimeOffset>? clock = null, TimeZoneInfo? zone = null)
    {
        _client = client;
        _clock = clock ?? (() => DateTimeOffset.Now);
        _zone = zone ?? TimeZoneInfo.Local;
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _countdownTimer.Tick += (_, _) => UpdateCountdowns();
    }

    public int? FiveHourRemaining { get => _fiveHourRemaining; private set => Set(ref _fiveHourRemaining, value); }
    public int? WeeklyRemaining { get => _weeklyRemaining; private set => Set(ref _weeklyRemaining, value); }
    public string FiveHourPercentText => FiveHourRemaining is int value ? $"{value}%" : "—";
    public string WeeklyPercentText => WeeklyRemaining is int value ? $"{value}%" : "—";
    public string FiveHourResetText { get => _fiveHourResetText; private set => Set(ref _fiveHourResetText, value); }
    public string WeeklyResetText { get => _weeklyResetText; private set => Set(ref _weeklyResetText, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public bool IsStale { get => _isStale; private set => Set(ref _isStale, value); }
    public DateTimeOffset? LastSuccessfulUpdate { get => _lastSuccessfulUpdate; private set => Set(ref _lastSuccessfulUpdate, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;
        await RefreshAsync();
        _refreshTimer.Start();
        _countdownTimer.Start();
    }

    public async Task RefreshAsync()
    {
        if (!await _refreshGate.WaitAsync(0)) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var snapshot = await _client.GetUsageAsync(timeout.Token);
            _snapshot = snapshot;
            FiveHourRemaining = snapshot.FiveHour?.RemainingPercent;
            WeeklyRemaining = snapshot.Weekly?.RemainingPercent;
            OnPropertyChanged(nameof(FiveHourPercentText));
            OnPropertyChanged(nameof(WeeklyPercentText));
            LastSuccessfulUpdate = _clock();
            IsStale = false;
            StatusText = string.Empty;
            UpdateCountdowns();
        }
        catch (Exception exception) when (exception is IOException or FormatException or FileNotFoundException or InvalidOperationException or OperationCanceledException)
        {
            if (LastSuccessfulUpdate is null)
            {
                FiveHourRemaining = null;
                WeeklyRemaining = null;
                OnPropertyChanged(nameof(FiveHourPercentText));
                OnPropertyChanged(nameof(WeeklyPercentText));
                StatusText = "Codex unavailable";
            }
            else
            {
                IsStale = true;
                var local = TimeZoneInfo.ConvertTime(LastSuccessfulUpdate.Value, _zone);
                StatusText = $"STALE · updated {local:h:mm tt}";
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void UpdateCountdowns()
    {
        var now = _clock();
        FiveHourResetText = _snapshot?.FiveHour is { } five
            ? ResetTimeFormatter.Format(five.ResetsAt, now, _zone)
            : string.Empty;
        WeeklyResetText = _snapshot?.Weekly is { } week
            ? ResetTimeFormatter.Format(week.ResetsAt, now, _zone)
            : string.Empty;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public async ValueTask DisposeAsync()
    {
        _refreshTimer.Stop();
        _countdownTimer.Stop();
        await _client.DisposeAsync();
        _refreshGate.Dispose();
    }
}
```

- [ ] **Step 4: Run the view-model and offline test suites**

```bash
dotnet test tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj --filter MainViewModelTests
dotnet test CodexUsageWidget.sln --filter "Category!=Live"
```

Expected: all selected tests pass.

- [ ] **Step 5: Replace the generated WPF view**

Replace `src/CodexUsageWidget/App.xaml` with:

```xml
<Application x:Class="CodexUsageWidget.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnMainWindowClose">
    <Application.Resources />
</Application>
```

Replace `src/CodexUsageWidget/MainWindow.xaml` with:

```xml
<Window x:Class="CodexUsageWidget.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Codex Usage Widget" Width="330" Height="86"
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        ResizeMode="NoResize" ShowInTaskbar="False" Topmost="False"
        SourceInitialized="Window_SourceInitialized" Loaded="Window_Loaded"
        Closed="Window_Closed">
    <Window.ContextMenu>
        <ContextMenu>
            <MenuItem Header="Refresh now" Click="Refresh_Click" />
            <Separator />
            <MenuItem x:Name="StartWithWindowsMenuItem" Header="Start with Windows"
                      IsCheckable="True" Click="Startup_Click" />
            <Separator />
            <MenuItem Header="Exit" Click="Exit_Click" />
        </ContextMenu>
    </Window.ContextMenu>
    <Border x:Name="Panel" CornerRadius="9" Padding="10,7" Background="#E810161B"
            BorderBrush="#394852" BorderThickness="1"
            MouseLeftButtonDown="Panel_MouseLeftButtonDown"
            MouseEnter="Panel_MouseEnter" MouseLeave="Panel_MouseLeave">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="34" />
                <RowDefinition Height="34" />
            </Grid.RowDefinitions>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="58" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="42" />
            </Grid.ColumnDefinitions>

            <TextBlock Grid.Row="0" Grid.Column="0" Text="5 HOUR" Foreground="#55DF79"
                       FontFamily="Consolas" FontWeight="Bold" FontSize="11" VerticalAlignment="Top" />
            <Grid x:Name="FiveHourTrack" Grid.Row="0" Grid.Column="1" Height="7" Margin="4,3,7,0" VerticalAlignment="Top">
                <Border Background="#28343B" CornerRadius="4" />
                <Border x:Name="FiveHourFill" Background="#8798A3" CornerRadius="4" HorizontalAlignment="Left" />
            </Grid>
            <TextBlock Grid.Row="0" Grid.Column="2" Text="{Binding FiveHourPercentText}" Foreground="#EDF5FA"
                       FontFamily="Consolas" FontWeight="Bold" FontSize="12" TextAlignment="Right" />
            <TextBlock Grid.Row="0" Grid.Column="1" Grid.ColumnSpan="2" Text="{Binding FiveHourResetText}"
                       Foreground="#8798A3" FontFamily="Consolas" FontSize="9" Margin="4,15,0,0" />

            <TextBlock Grid.Row="1" Grid.Column="0" Text="WEEK" Foreground="#54C6FF"
                       FontFamily="Consolas" FontWeight="Bold" FontSize="11" VerticalAlignment="Top" />
            <Grid x:Name="WeeklyTrack" Grid.Row="1" Grid.Column="1" Height="7" Margin="4,3,7,0" VerticalAlignment="Top">
                <Border Background="#28343B" CornerRadius="4" />
                <Border x:Name="WeeklyFill" Background="#8798A3" CornerRadius="4" HorizontalAlignment="Left" />
            </Grid>
            <TextBlock Grid.Row="1" Grid.Column="2" Text="{Binding WeeklyPercentText}" Foreground="#EDF5FA"
                       FontFamily="Consolas" FontWeight="Bold" FontSize="12" TextAlignment="Right" />
            <TextBlock Grid.Row="1" Grid.Column="1" Grid.ColumnSpan="2" Text="{Binding WeeklyResetText}"
                       Foreground="#8798A3" FontFamily="Consolas" FontSize="9" Margin="4,15,0,0" />

            <TextBlock Grid.RowSpan="2" Grid.ColumnSpan="3" Text="{Binding StatusText}" Foreground="#FFCA58"
                       FontFamily="Consolas" FontSize="9" HorizontalAlignment="Left" VerticalAlignment="Bottom" />
            <Button x:Name="CloseButton" Grid.RowSpan="2" Grid.ColumnSpan="3" Content="×"
                    Visibility="Collapsed" Width="18" Height="18" Padding="0" Margin="0,-5,-5,0"
                    HorizontalAlignment="Right" VerticalAlignment="Top" Click="Close_Click"
                    Background="#CC293139" Foreground="#EDF5FA" BorderThickness="0" />
        </Grid>
    </Border>
</Window>
```

- [ ] **Step 6: Implement composition and window interactions**

Replace `src/CodexUsageWidget/App.xaml.cs` with:

```csharp
using System.Windows;
using CodexUsageWidget.Services;
using CodexUsageWidget.ViewModels;

namespace CodexUsageWidget;

public partial class App : Application
{
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var executable = CodexExecutableLocator.Find();
        ICodexUsageClient client = executable is null
            ? new UnavailableUsageClient()
            : new CodexAppServerClient(executable);
        var settings = new SettingsStore();
        var startup = new StartupManager();
        _viewModel = new MainViewModel(client);
        var window = new MainWindow(_viewModel, settings, startup);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnExit(e);
    }

    private sealed class UnavailableUsageClient : ICodexUsageClient
    {
        public Task<Domain.UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken) =>
            Task.FromException<Domain.UsageSnapshot>(new FileNotFoundException("Codex CLI was not found."));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
```

Replace `src/CodexUsageWidget/MainWindow.xaml.cs` with:

```csharp
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using CodexUsageWidget.Services;
using CodexUsageWidget.ViewModels;

namespace CodexUsageWidget;

public partial class MainWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExAppWindow = 0x00040000;
    private readonly MainViewModel _viewModel;
    private readonly SettingsStore _settingsStore;
    private readonly StartupManager _startupManager;
    private WidgetSettings _settings;

    public MainWindow(MainViewModel viewModel, SettingsStore settingsStore, StartupManager startupManager)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsStore = settingsStore;
        _startupManager = startupManager;
        _settings = settingsStore.Load();
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        SizeChanged += (_, _) => UpdateMeters();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        WindowPlacementService.Restore(this, _settings);
        var startupEnabled = _startupManager.IsEnabled();
        StartWithWindowsMenuItem.IsChecked = startupEnabled;
        _settings = _settings with { StartWithWindows = startupEnabled };
        _settingsStore.Save(_settings);
        await _viewModel.StartAsync();
        UpdateMeters();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        style = (style | WsExToolWindow) & ~WsExAppWindow;
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style));
    }

    private void Panel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        try { DragMove(); }
        finally { SaveSettings(); }
    }

    private void Panel_MouseEnter(object sender, MouseEventArgs e) => CloseButton.Visibility = Visibility.Visible;
    private void Panel_MouseLeave(object sender, MouseEventArgs e) => CloseButton.Visibility = Visibility.Collapsed;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await _viewModel.RefreshAsync();

    private void Startup_Click(object sender, RoutedEventArgs e)
    {
        _startupManager.SetEnabled(StartWithWindowsMenuItem.IsChecked);
        StartWithWindowsMenuItem.IsChecked = _startupManager.IsEnabled();
        SaveSettings();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.FiveHourRemaining) or nameof(MainViewModel.WeeklyRemaining))
            UpdateMeters();
    }

    private void UpdateMeters()
    {
        SetMeter(FiveHourFill, FiveHourTrack, _viewModel.FiveHourRemaining);
        SetMeter(WeeklyFill, WeeklyTrack, _viewModel.WeeklyRemaining);
    }

    private static void SetMeter(FrameworkElement fill, FrameworkElement track, int? remaining)
    {
        fill.Width = track.ActualWidth * (remaining ?? 0) / 100.0;
        fill.SetValue(System.Windows.Controls.Border.BackgroundProperty, remaining switch
        {
            null => new SolidColorBrush(Color.FromRgb(0x87, 0x98, 0xA3)),
            > 50 => new SolidColorBrush(Color.FromRgb(0x55, 0xDF, 0x79)),
            >= 20 => new SolidColorBrush(Color.FromRgb(0xFF, 0xCA, 0x58)),
            _ => new SolidColorBrush(Color.FromRgb(0xFF, 0x66, 0x5C))
        });
    }

    private void SaveSettings()
    {
        if (!IsLoaded) return;
        _settings = new WidgetSettings(Left, Top, WindowPlacementService.CurrentMonitor(this), _startupManager.IsEnabled());
        _settingsStore.Save(_settings);
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        SaveSettings();
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int index);
    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int index) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, index) : GetWindowLongPtr32(hWnd, index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int index, IntPtr value);
    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, index, value) : SetWindowLongPtr32(hWnd, index, value);
}
```

- [ ] **Step 7: Build and manually launch the debug HUD**

```bash
dotnet build CodexUsageWidget.sln
dotnet run --project src/CodexUsageWidget/CodexUsageWidget.csproj
```

Expected: a borderless two-meter HUD opens without a console, displays the live weekly remaining value, displays `5 HOUR —` for the current general Codex bucket, can be dragged, and reveals the close control on hover.

- [ ] **Step 8: Commit the complete HUD**

```bash
git add src/CodexUsageWidget tests/CodexUsageWidget.Tests
git commit -m "feat: add Codex usage HUD"
```

### Task 7: Package, document, and verify the deliverable

**Files:**
- Create: `scripts/create-desktop-shortcut.ps1`
- Create: `README.md`
- Create: `artifacts/win-x64/` through publish; add `artifacts/` to `.gitignore` and deliver the published executable directly from that folder.

- [ ] **Step 1: Add the shortcut script**

Create `scripts/create-desktop-shortcut.ps1`:

```powershell
param([Parameter(Mandatory=$true)][string]$Executable)
$resolved = (Resolve-Path -LiteralPath $Executable).Path
$desktop = [Environment]::GetFolderPath('Desktop')
$shell = New-Object -ComObject WScript.Shell
$shortcutPath = Join-Path $desktop 'Codex Usage Widget.lnk'
if (Test-Path -LiteralPath $shortcutPath) {
    $existing = $shell.CreateShortcut($shortcutPath)
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($existing.TargetPath, $resolved)) {
        $suffix = 2
        do {
            $shortcutPath = Join-Path $desktop "Codex Usage Widget ($suffix).lnk"
            $suffix++
        } while (Test-Path -LiteralPath $shortcutPath)
    }
}
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $resolved
$shortcut.WorkingDirectory = Split-Path -Parent $resolved
$shortcut.Description = 'Show remaining Codex usage'
$shortcut.Save()
Write-Output $shortcutPath
```

- [ ] **Step 2: Add concise usage documentation**

Document these exact commands in `README.md`:

```bash
dotnet test CodexUsageWidget.sln --filter "Category!=Live"
dotnet test CodexUsageWidget.sln --filter "Category=Live"
dotnet publish src/CodexUsageWidget/CodexUsageWidget.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
```

Also document left-drag, hover-close, right-click actions, settings path, Codex executable discovery paths, and the experimental app-server dependency.

- [ ] **Step 3: Run fresh complete verification**

```bash
dotnet test CodexUsageWidget.sln --filter "Category!=Live" -c Release
dotnet test CodexUsageWidget.sln --filter "Category=Live" -c Release
dotnet publish src/CodexUsageWidget/CodexUsageWidget.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
git diff --check
```

Expected: every command exits 0; all offline and live tests pass; `artifacts/win-x64/CodexUsageWidget.exe` exists.

- [ ] **Step 4: Exercise the published executable**

Launch `artifacts/win-x64/CodexUsageWidget.exe` and verify against a fresh direct `account/rateLimits/read` probe:

- Weekly remaining equals `100 - usedPercent` from the general `codex` bucket.
- Five-hour displays `—` when the 300-minute general bucket is absent.
- Countdown and absolute local reset time are both visible.
- A normal application window covers the HUD.
- The widget has no taskbar or Alt-Tab entry.
- Dragging persists position after close and relaunch.
- Hover reveals the close control.
- Context-menu refresh works.
- Startup toggle creates the exact current-user Run value, read-back shows enabled, and disabling removes it. Leave it disabled unless the user explicitly enables it.
- Closing leaves no `codex app-server` child created by the widget.

- [ ] **Step 5: Create and verify the desktop shortcut**

Run the script with the published executable using PowerShell, then resolve the `.lnk` target through `WScript.Shell` and confirm it equals the published executable path. Do not overwrite unrelated shortcuts.

- [ ] **Step 6: Commit source and documentation**

```bash
git add README.md scripts .gitignore
git commit -m "docs: package Codex usage widget"
git status --short --branch
```

Expected: clean working tree after excluding or intentionally tracking deliverable artifacts according to repository policy.
