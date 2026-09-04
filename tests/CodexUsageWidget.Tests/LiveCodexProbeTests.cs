using CodexUsageWidget.Services;

namespace CodexUsageWidget.Tests;

public sealed class LiveCodexProbeTests
{
    [Fact]
    [Trait("Category", "Live")]
    public async Task ReadsUsageFromInstalledCodex()
    {
        var executablePath = CodexExecutableLocator.Find();
        Assert.False(string.IsNullOrWhiteSpace(executablePath));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var client = new CodexAppServerClient(executablePath!);

        var snapshot = await client.GetUsageAsync(timeout.Token);

        Assert.True(snapshot.HasAnyValue);
    }
}
