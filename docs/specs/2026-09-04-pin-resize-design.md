# Pin and Resize Controls — Design

**Date:** 2026-09-04

## Goal

Add two hover-only controls to the Codex usage widget: a pin that toggles whether the window stays above other applications, and a bottom-right handle that resizes the widget. Both states persist across launches.

## Interaction and appearance

- Place the pin immediately left of the existing close button.
- Keep both buttons unframed and hidden until the pointer is over the widget.
- Render the unpinned control in the existing muted color and the pinned control in cyan.
- Change the tooltip between `Pin above other apps` and `Unpin` to reflect the next action.
- Toggle WPF's `Topmost` property immediately when the pin is clicked.
- Show a subtle diagonal-line resize grip in the bottom-right only while the widget is hovered.
- Resize width and height independently as the grip is dragged.
- Use 220 × 66 device-independent pixels as the minimum size and impose no application-defined maximum.
- Separate the four content rows with three flexible spacer rows. Each spacer has a 1-pixel minimum and shares surplus height evenly as the widget grows.
- Add a subtle black drop shadow to all widget text and to each progress bar for readability over transparent desktop backgrounds.

## Persistence

Extend `WidgetSettings` with nullable width and height values and a persisted pin flag. Existing settings files that lack these properties must continue to load with the current default size and an unpinned window.

Save size and pin state together with the existing position, monitor, and startup preference. Restore the size before restoring and clamping the position so the entire resized widget remains within the selected monitor's working area. When no position has been saved, place the widget at the upper-right of the primary working area with the existing 20-pixel edge margin.

## Implementation boundaries

- `MainWindow.xaml` owns the hover visuals and the WPF resize `Thumb`.
- `MainWindow.xaml.cs` applies the pin state, handles pin clicks and resize drag events, and requests settings saves when either interaction completes.
- `WidgetSettings` stores the persistent values.
- A small size calculation helper clamps drag results to the minimum dimensions without coupling tests to a live WPF window.

## Verification

Automated tests cover:

- Round-tripping width, height, and pin state.
- Loading a legacy settings document without the new properties.
- Clamping resize calculations to the 220 × 66 minimum width and height.
- Resolving the default position to the upper-right working-area corner.

Release build and manual verification cover:

- Hover-only visibility of pin, close, and resize controls.
- Immediate topmost toggling and active pin feedback.
- Bottom-right resizing in both dimensions.
- Restoring size and pin state after relaunch.
- Keeping a restored resized widget inside the visible monitor work area.
- Expanding and collapsing inter-row spacing as height changes, down to 1 pixel per spacer.
- Showing subtle black shadows behind all widget text and progress bars.
