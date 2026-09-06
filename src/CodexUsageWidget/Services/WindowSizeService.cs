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
