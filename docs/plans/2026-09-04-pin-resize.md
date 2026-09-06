# Pin and Resize Controls Implementation Plan

**Goal:** Add hover-only pin and bottom-right resize controls whose state persists across widget launches.

**Architecture:** Extend the existing settings record with optional size and topmost values so legacy JSON remains valid. Keep resize arithmetic in a small pure helper, while `MainWindow` owns WPF interaction, visuals, and persistence triggers.

**Tech Stack:** C# 12, .NET 8, WPF, xUnit, `System.Text.Json`

---

### Task 1: Persist size and pin state

**Files:**
- Modify: `tests/CodexUsageWidget.Tests/SettingsStoreTests.cs`
- Modify: `src/CodexUsageWidget/Services/SettingsStore.cs`

- [ ] **Step 1: Write failing persistence tests**

Update the round-trip fixture to construct:

```csharp
var expected = new WidgetSettings(12.5, 34.5, "DISPLAY1", true, 480, 140, true);
```

Add a legacy-file test that writes JSON containing only `Left`, `Top`, `MonitorDeviceName`, and `StartWithWindows`, then asserts:

```csharp
Assert.Null(actual.Width);
Assert.Null(actual.Height);
Assert.False(actual.Topmost);
```

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj -c Release --no-restore --filter SettingsStoreTests
```

Expected: compilation fails because `WidgetSettings` does not expose the new constructor arguments or properties.

- [ ] **Step 3: Extend the settings record**

Append optional parameters so existing four-argument construction and legacy JSON stay compatible:

```csharp
public sealed record WidgetSettings(
    double? Left,
    double? Top,
    string? MonitorDeviceName,
    bool StartWithWindows,
    double? Width = null,
    double? Height = null,
    bool Topmost = false)
```

- [ ] **Step 4: Verify GREEN**

Run the Task 1 test command again. Expected: all `SettingsStoreTests` pass.

### Task 2: Clamp interactive and restored sizes

**Files:**
- Create: `src/CodexUsageWidget/Services/WindowSizeService.cs`
- Create: `tests/CodexUsageWidget.Tests/WindowSizeServiceTests.cs`

- [ ] **Step 1: Write failing size tests**

Define tests for an ordinary resize and a resize below the minimum:

```csharp
[Fact]
public void AppliesDragDeltaToBothDimensions()
{
    var result = WindowSizeService.Resize(330, 98, 70, 22, 330, 98);
    Assert.Equal(new WindowSize(400, 120), result);
}

[Fact]
public void ClampsResizeToMinimumDimensions()
{
    var result = WindowSizeService.Resize(400, 120, -500, -500, 330, 98);
    Assert.Equal(new WindowSize(330, 98), result);
}
```

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test tests/CodexUsageWidget.Tests/CodexUsageWidget.Tests.csproj -c Release --no-restore --filter WindowSizeServiceTests
```

Expected: compilation fails because `WindowSizeService` and `WindowSize` do not exist.

- [ ] **Step 3: Implement the pure helper**

Create:

```csharp
namespace CodexUsageWidget.Services;

public readonly record struct WindowSize(double Width, double Height);

public static class WindowSizeService
{
    public static WindowSize Resize(
        double currentWidth,
        double currentHeight,
        double horizontalChange,
        double verticalChange,
        double minimumWidth,
        double minimumHeight) =>
        new(
            Math.Max(minimumWidth, currentWidth + horizontalChange),
            Math.Max(minimumHeight, currentHeight + verticalChange));

    public static WindowSize Restore(
        double? width,
        double? height,
        double defaultWidth,
        double defaultHeight,
        double minimumWidth,
        double minimumHeight)
    {
        var restoredWidth = width is { } savedWidth && double.IsFinite(savedWidth)
            ? savedWidth
            : defaultWidth;
        var restoredHeight = height is { } savedHeight && double.IsFinite(savedHeight)
            ? savedHeight
            : defaultHeight;
        return new WindowSize(
            Math.Max(minimumWidth, restoredWidth),
            Math.Max(minimumHeight, restoredHeight));
    }
}
```

- [ ] **Step 4: Add restore coverage and verify GREEN**

Add tests proving missing and below-minimum saved dimensions resolve to 330 × 98, then rerun the Task 2 test command. Expected: all `WindowSizeServiceTests` pass.

### Task 3: Add hover controls and wire window behavior

**Files:**
- Modify: `src/CodexUsageWidget/MainWindow.xaml`
- Modify: `src/CodexUsageWidget/MainWindow.xaml.cs`

- [ ] **Step 1: Make the borderless window resizable by the custom grip**

Keep `ResizeMode="NoResize"`, retain `MinWidth="330"` and `MinHeight="98"`, and remove `MaxWidth` and `MaxHeight`. This prevents native resize chrome while allowing code-driven dimensions above the minimum.

- [ ] **Step 2: Replace the single close control with the approved hover controls**

Create a top-right horizontal container that fades with the existing 180 ms easing and contains:

```xml
<ToggleButton x:Name="PinButton"
              Width="18" Height="18"
              Click="PinButton_Click"
              ToolTip="Pin above other apps">
    <TextBlock FontFamily="Segoe MDL2 Assets" Text="&#xE718;" />
</ToggleButton>
<Button x:Name="CloseButton" Width="18" Height="18"
        Click="CloseButton_Click" ToolTip="Exit" Content="×" />
```

Style the toggle as muted when unchecked and cyan when checked. Keep the container non-hit-testable while hidden.

- [ ] **Step 3: Add the approved resize grip**

Add a 14 × 14 bottom-right `Thumb` with a three-line diagonal `Path`. Use the same hover opacity and hit-testing behavior as the top controls, and wire:

```xml
DragDelta="ResizeGrip_DragDelta"
DragCompleted="ResizeGrip_DragCompleted"
```

- [ ] **Step 4: Restore persisted state before placement**

After loading settings in the constructor, call `WindowSizeService.Restore`, assign `Width` and `Height`, set `Topmost`, and synchronize `PinButton.IsChecked`. Because size is established before `Window_SourceInitialized`, the existing placement service clamps against the restored native rectangle.

- [ ] **Step 5: Implement pin and resize events**

Add:

```csharp
private void PinButton_Click(object sender, RoutedEventArgs e)
{
    Topmost = PinButton.IsChecked == true;
    PinButton.ToolTip = Topmost ? "Unpin" : "Pin above other apps";
    SaveSettings();
}

private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
{
    var size = WindowSizeService.Resize(
        ActualWidth, ActualHeight,
        e.HorizontalChange, e.VerticalChange,
        MinWidth, MinHeight);
    Width = size.Width;
    Height = size.Height;
}

private void ResizeGrip_DragCompleted(object sender, DragCompletedEventArgs e) =>
    SaveSettings();
```

Add the required `System.Windows` and `System.Windows.Controls.Primitives` imports.

- [ ] **Step 6: Save the expanded settings**

Update `SaveSettings` to pass `ActualWidth`, `ActualHeight`, and `Topmost` after the existing startup value:

```csharp
_settings = new WidgetSettings(
    placement.Left,
    placement.Top,
    placement.MonitorDeviceName,
    _startupManager.IsEnabled(),
    ActualWidth,
    ActualHeight,
    Topmost);
```

- [ ] **Step 7: Compile the WPF wiring**

Run:

```powershell
dotnet build CodexUsageWidget.sln -c Release --no-restore
```

Expected: build succeeds with zero warnings and errors.

### Task 4: Complete verification

**Files:**
- Verify all modified source and test files.

- [ ] **Step 1: Run all offline tests**

```powershell
dotnet test CodexUsageWidget.sln -c Release --no-restore --filter "Category!=Live"
```

Expected: all tests pass.

- [ ] **Step 2: Run the live integration test**

```powershell
dotnet test CodexUsageWidget.sln -c Release --no-build --no-restore --filter "Category=Live"
```

Expected: the installed Codex app-server returns a non-empty usage snapshot.

- [ ] **Step 3: Check the working diff**

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors; only the intended implementation, tests, plan, and inherited XAML changes are present.

- [ ] **Step 4: Manually exercise the widget**

Launch the Release executable and verify hover visibility, pin/unpin z-order, two-axis resizing, minimum-size enforcement, close/relaunch persistence, and restored on-screen placement.

### Task 5: Apply compact-layout review feedback

**Files:**
- Modify: `tests/CodexUsageWidget.Tests/WindowPlacementServiceTests.cs`
- Modify: `src/CodexUsageWidget/Services/WindowPlacementService.cs`
- Modify: `src/CodexUsageWidget/MainWindow.xaml`

- [ ] **Step 1: Write a failing upper-right placement test**

Add a test for a 220 × 66 window in a 1920 × 1040 working area:

```csharp
var result = WindowPlacementService.GetDefaultPosition(0, 0, 1920, 220, 20);
Assert.Equal(new WindowPosition(1680, 20), result);
```

- [ ] **Step 2: Verify RED, then implement the placement helper**

Run the focused placement tests and confirm compilation fails because `GetDefaultPosition` is absent. Add the pure helper and use it in `Restore` whenever saved coordinates are missing.

- [ ] **Step 3: Set the compact minimum and responsive row gaps**

Set `MinWidth="220"` and `MinHeight="66"`. Replace the four fixed row heights with four `Auto` content rows at indices 0, 2, 4, and 6, separated by three `Height="*" MinHeight="1"` spacer rows.

- [ ] **Step 4: Add readability shadows**

Define subtle black `DropShadowEffect` resources. Apply the text effect to the widget content and hover buttons, and apply the bar effect through `MeterProgressBarStyle`.

- [ ] **Step 5: Verify the feedback changes**

Run the focused placement tests, complete offline suite, live integration test, Release build, and `git diff --check`. Launch the widget and verify the 220 × 66 minimum, responsive row spacing, upper-right first-launch position, and shadows.
