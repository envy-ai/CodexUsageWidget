using CodexUsageWidget.Domain;

namespace CodexUsageWidget.Services;

public interface ICodexUsageClient : IAsyncDisposable
{
    Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken);
}
