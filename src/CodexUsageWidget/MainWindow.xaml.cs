using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using CodexUsageWidget.Services;
using CodexUsageWidget.ViewModels;

namespace CodexUsageWidget;

public partial class MainWindow : System.Windows.Window
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;

    private readonly MainViewModel _viewModel;
    private readonly SettingsStore _settingsStore;
    private readonly StartupManager _startupManager;
    private WidgetSettings _settings;
    private bool _isExiting;

    public MainWindow(
        MainViewModel viewModel,
        SettingsStore settingsStore,
        StartupManager startupManager)
    {
        _viewModel = viewModel;
        _settingsStore = settingsStore;
        _startupManager = startupManager;

        InitializeComponent();
        DataContext = viewModel;
        _settings = settingsStore.Load();

        var restoredSize = WindowSizeService.Restore(
            _settings.Width,
            _settings.Height,
            Width,
            Height,
            MinWidth,
            MinHeight);
        Width = restoredSize.Width;
        Height = restoredSize.Height;
        Topmost = _settings.Topmost;
        PinButton.IsChecked = Topmost;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLongPtrW(handle, GwlExStyle).ToInt64();
        SetWindowLongPtrW(handle, GwlExStyle, new IntPtr(extendedStyle | WsExToolWindow));

        WindowPlacementService.Restore(this, _settings);
    }

    private async void Window_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        await _viewModel.StartAsync();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SaveSettings();
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
        }
    }

    public void ShowWidget()
    {
        Show();
        Activate();
    }

    public void ExitWidget()
    {
        _isExiting = true;
        Close();
    }

    private void Panel_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
        SaveSettings();
    }

    private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e) => Close();

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = PinButton.IsChecked == true;
        SaveSettings();
    }

    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var size = WindowSizeService.Resize(
            ActualWidth,
            ActualHeight,
            e.HorizontalChange,
            e.VerticalChange,
            MinWidth,
            MinHeight);
        Width = size.Width;
        Height = size.Height;
    }

    private void ResizeGrip_DragCompleted(object sender, DragCompletedEventArgs e) => SaveSettings();

    private void ContextMenu_Opened(object sender, System.Windows.RoutedEventArgs e)
    {
        StartWithWindowsMenuItem.IsChecked = _startupManager.IsEnabled();
    }

    private async void RefreshNow_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        await _viewModel.RefreshAsync();
    }

    private void StartWithWindows_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _startupManager.SetEnabled(StartWithWindowsMenuItem.IsChecked == true);
        SaveSettings();
    }

    private void Exit_Click(object sender, System.Windows.RoutedEventArgs e) => ExitWidget();

    private void SaveSettings()
    {
        var placement = WindowPlacementService.Capture(this);
        _settings = new WidgetSettings(
            placement.Left,
            placement.Top,
            placement.MonitorDeviceName,
            _startupManager.IsEnabled(),
            ActualWidth,
            ActualHeight,
            Topmost);
        _settingsStore.Save(_settings);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtrW(IntPtr windowHandle, int index, IntPtr value);
}
