using CodexUsageWidget.Domain;
using CodexUsageWidget.Services;
using CodexUsageWidget.ViewModels;

namespace CodexUsageWidget.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task FailedRefreshPreservesLastSnapshotAndMarksItStale()
    {
        var now = new DateTimeOffset(2025, 1, 3, 16, 0, 0, TimeSpan.Zero);
        var client = new QueueCodexUsageClient(
            new UsageSnapshot(null, new UsageWindow(77, now.AddDays(2))),
            new IOException("codex stopped"));
        await using var viewModel = new MainViewModel(client, () => now, TimeZoneInfo.Utc);

        await viewModel.RefreshAsync();

        Assert.Equal(77, viewModel.WeeklyRemaining);
        Assert.False(viewModel.IsStale);

        await viewModel.RefreshAsync();

        Assert.Equal(77, viewModel.WeeklyRemaining);
        Assert.True(viewModel.IsStale);
        Assert.StartsWith("STALE", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitialFailureShowsUnavailableWithoutInventingValues()
    {
        var now = new DateTimeOffset(2025, 1, 3, 16, 0, 0, TimeSpan.Zero);
        var client = new QueueCodexUsageClient(new IOException("codex missing"));
        await using var viewModel = new MainViewModel(client, () => now, TimeZoneInfo.Utc);

        await viewModel.RefreshAsync();

        Assert.Null(viewModel.FiveHourRemaining);
        Assert.Null(viewModel.WeeklyRemaining);
        Assert.Equal("—", viewModel.FiveHourPercentageText);
        Assert.Equal("—", viewModel.WeeklyPercentageText);
        Assert.Equal("Codex unavailable", viewModel.StatusText);
        Assert.False(viewModel.IsStale);
    }

    private sealed class QueueCodexUsageClient(params object[] results) : ICodexUsageClient
    {
        private readonly Queue<object> _results = new(results);

        public Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = _results.Dequeue();

            return result switch
            {
                UsageSnapshot snapshot => Task.FromResult(snapshot),
                Exception exception => Task.FromException<UsageSnapshot>(exception),
                _ => throw new InvalidOperationException("Unsupported queued result."),
            };
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
