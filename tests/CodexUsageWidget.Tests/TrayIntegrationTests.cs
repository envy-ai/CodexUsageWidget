using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CodexUsageWidget.ViewModels;

namespace CodexUsageWidget.Tests;

public sealed class TrayIntegrationTests
{
    [Fact]
    [Trait("Category", "Live")]
    public async Task LiveApplicationTrayRestoresRefreshesAndExits()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var app = new App();
            app.InitializeComponent();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            var deadline = DateTime.UtcNow.AddSeconds(20);
            var phase = 0;
            DateTimeOffset? firstUpdate = null;
            Exception? failure = null;
            timer.Tick += (_, _) =>
            {
                try
                {
                    if (DateTime.UtcNow > deadline)
                        throw new TimeoutException("Live widget did not refresh in time.");
                    if (app.MainWindow is not MainWindow window)
                        return;
                    var vm = (MainViewModel)window.DataContext;
                    if (vm.LastSuccessfulUpdate is null)
                        return;
                    var tray = (System.Windows.Forms.NotifyIcon)typeof(App)
                        .GetField("_trayIcon", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
                    if (phase == 0)
                    {
                        Assert.True(tray.Visible);
                        Assert.NotNull(tray.Icon);
                        if (!vm.HasFiveHourLimit)
                        {
                            var root = (FrameworkElement)window.Content;
                            var label = FindText(root, "5 HOUR");
                            Assert.NotNull(label);
                            Assert.False(label.IsVisible);
                        }
                        window.Close();
                        Assert.False(window.IsVisible);
                        var menu = tray.ContextMenuStrip!;
                        var widgetMenu = ((FrameworkElement)window.Content).ContextMenu;
                        Assert.Contains(widgetMenu.Items.OfType<MenuItem>(), item => Equals(item.Header, "Refresh Immediately"));
                        Assert.Contains(widgetMenu.Items.OfType<MenuItem>(), item => Equals(item.Header, "Exit"));
                        var items = menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().ToArray();
                        var closed = false;
                        window.Closed += (_, _) => closed = true;
                        items.Single(item => item.Text == "Show widget").PerformClick();
                        Assert.True(window.IsVisible);
                        window.Close();
                        Assert.False(closed);
                        typeof(System.Windows.Forms.NotifyIcon).GetMethod("OnDoubleClick",
                            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(tray, [EventArgs.Empty]);
                        Assert.True(window.IsVisible);
                        firstUpdate = vm.LastSuccessfulUpdate;
                        phase = 1;
                        items.Single(item => item.Text == "Refresh Immediately").PerformClick();
                    }
                    else if (vm.LastSuccessfulUpdate > firstUpdate)
                    {
                        timer.Stop();
                        tray.ContextMenuStrip!.Items.OfType<System.Windows.Forms.ToolStripMenuItem>()
                            .Single(item => item.Text == "Exit").PerformClick();
                    }
                }
                catch (Exception exception)
                {
                    failure = exception;
                    timer.Stop();
                    if (app.MainWindow is MainWindow window)
                        window.ExitWidget();
                    else
                        app.Shutdown();
                }
            };
            try
            {
                timer.Start();
                app.Run();
                if (failure is not null)
                    completion.SetException(failure);
                else
                {
                    Assert.Equal(1, phase);
                    completion.SetResult();
                }
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private static TextBlock? FindText(DependencyObject root, string text)
    {
        if (root is TextBlock label && label.Text == text)
            return label;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindText(System.Windows.Media.VisualTreeHelper.GetChild(root, i), text);
            if (found is not null)
                return found;
        }
        return null;
    }
}
