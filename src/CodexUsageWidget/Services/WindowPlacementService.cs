using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace CodexUsageWidget.Services;

public readonly record struct WindowPosition(double Left, double Top);

public readonly record struct CapturedPlacement(double Left, double Top, string? MonitorDeviceName);

public static class WindowPlacementService
{
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const int DefaultMargin = 20;

    public static WindowPosition Clamp(
        double left,
        double top,
        double width,
        double height,
        double workLeft,
        double workTop,
        double workWidth,
        double workHeight)
    {
        var maximumLeft = Math.Max(workLeft, workLeft + workWidth - width);
        var maximumTop = Math.Max(workTop, workTop + workHeight - height);

        return new WindowPosition(
            Math.Clamp(left, workLeft, maximumLeft),
            Math.Clamp(top, workTop, maximumTop));
    }

    public static void Restore(Window window, WidgetSettings settings)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(settings);

        var handle = new WindowInteropHelper(window).EnsureHandle();
        var rectangle = GetRectangle(handle);
        var width = rectangle.Right - rectangle.Left;
        var height = rectangle.Bottom - rectangle.Top;
        var screen = SelectScreen(settings, rectangle);
        var workingArea = screen.WorkingArea;

        var desiredLeft = settings.Left ?? workingArea.Right - width - DefaultMargin;
        var desiredTop = settings.Top ?? workingArea.Bottom - height - DefaultMargin;
        var position = Clamp(
            desiredLeft,
            desiredTop,
            width,
            height,
            workingArea.Left,
            workingArea.Top,
            workingArea.Width,
            workingArea.Height);

        if (!SetWindowPos(
                handle,
                IntPtr.Zero,
                checked((int)Math.Round(position.Left)),
                checked((int)Math.Round(position.Top)),
                0,
                0,
                SwpNoSize | SwpNoZOrder | SwpNoActivate))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public static CapturedPlacement Capture(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new WindowInteropHelper(window).EnsureHandle();
        var rectangle = GetRectangle(handle);
        var center = new System.Drawing.Point(
            rectangle.Left + ((rectangle.Right - rectangle.Left) / 2),
            rectangle.Top + ((rectangle.Bottom - rectangle.Top) / 2));
        var screen = Forms.Screen.FromPoint(center);

        return new CapturedPlacement(rectangle.Left, rectangle.Top, screen.DeviceName);
    }

    private static Forms.Screen SelectScreen(WidgetSettings settings, NativeRectangle rectangle)
    {
        if (!string.IsNullOrWhiteSpace(settings.MonitorDeviceName))
        {
            var savedScreen = Forms.Screen.AllScreens.FirstOrDefault(
                screen => string.Equals(
                    screen.DeviceName,
                    settings.MonitorDeviceName,
                    StringComparison.OrdinalIgnoreCase));

            if (savedScreen is not null)
            {
                return savedScreen;
            }
        }

        var currentCenterX = rectangle.Left + ((rectangle.Right - rectangle.Left) / 2);
        var currentCenterY = rectangle.Top + ((rectangle.Bottom - rectangle.Top) / 2);
        var nearestPoint = new System.Drawing.Point(
            checked((int)Math.Round(settings.Left ?? currentCenterX)),
            checked((int)Math.Round(settings.Top ?? currentCenterY)));

        return Forms.Screen.FromPoint(nearestPoint);
    }

    private static NativeRectangle GetRectangle(IntPtr handle)
    {
        if (!GetWindowRect(handle, out var rectangle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return rectangle;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRectangle rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
