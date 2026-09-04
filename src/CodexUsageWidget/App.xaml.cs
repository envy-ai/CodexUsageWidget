using CodexUsageWidget.Domain;
using CodexUsageWidget.Services;
using CodexUsageWidget.ViewModels;

namespace CodexUsageWidget;

public partial class App : System.Windows.Application
{
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

        window.Closed += async (_, _) =>
        {
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
