using CodexUsageWidget.Domain;
using CodexUsageWidget.Services;
using CodexUsageWidget.ViewModels;

namespace CodexUsageWidget;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private System.Drawing.Icon? _icon;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var executablePath = CodexExecutableLocator.Find();
        ICodexUsageClient client = executablePath is null
            ? new UnavailableCodexUsageClient()
            : new CodexAppServerClient(executablePath);

        var settingsStore = new SettingsStore();
        var startupManager = new StartupManager();
        var viewModel = new MainViewModel(client, timeZone: TimeZoneInfo.Local);
        var window = new MainWindow(viewModel, settingsStore, startupManager);

        var iconResource = GetResourceStream(new Uri("pack://application:,,,/CodexUsageWidget;component/Assets/widget.ico"))
            ?? throw new InvalidOperationException("Widget icon resource was not found.");
        using (iconResource.Stream)
        {
            _icon = new System.Drawing.Icon(iconResource.Stream);
        }
        var trayMenu = new System.Windows.Forms.ContextMenuStrip();
        trayMenu.Items.Add("Show widget", null, (_, _) => Dispatcher.Invoke(window.ShowWidget));
        trayMenu.Items.Add("Refresh Immediately", null, (_, _) =>
            Dispatcher.InvokeAsync(async () => await viewModel.RefreshAsync()));
        trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        trayMenu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(window.ExitWidget));
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "Codex Usage Widget",
            ContextMenuStrip = trayMenu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(window.ShowWidget);

        window.Closed += async (_, _) =>
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            trayMenu.Dispose();
            _icon.Dispose();
            try
            {
                await viewModel.DisposeAsync();
            }
            finally
            {
                Shutdown();
            }
        };

        MainWindow = window;
        window.Show();
    }

    private sealed class UnavailableCodexUsageClient : ICodexUsageClient
    {
        public Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<UsageSnapshot>(
                new InvalidOperationException("Codex executable was not found."));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
