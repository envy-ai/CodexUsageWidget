using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using CodexUsageWidget.Domain;

namespace CodexUsageWidget.Services;

public sealed class CodexAppServerClient : ICodexUsageClient
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly string _executable;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<string>> _pending = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    private Process? _process;
    private CancellationTokenSource? _readerCancellation;
    private Task? _readerTask;
    private Task? _stderrTask;
    private long _requestId;
    private int _disposed;

    public CodexAppServerClient(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        _executable = executable;
    }

    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token);
        await _gate.WaitAsync(linked.Token);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            await EnsureStartedAsync(linked.Token);
            var response = await SendRequestAsync("account/rateLimits/read", parameters: null, linked.Token);
            return UsageSnapshotParser.ParseResponse(response);
        }
        catch
        {
            await StopProcessAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_process is not null && IsRunning(_process))
        {
            return;
        }

        await StopProcessAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = _executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("app-server");

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new IOException("Could not start Codex app-server.");
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            throw new IOException("Could not start Codex app-server.", exception);
        }

        _process = process;
        _readerCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _readerTask = ReadStdoutAsync(process, _readerCancellation.Token);
        _stderrTask = DrainStderrAsync(process, _readerCancellation.Token);

        var initializeResponse = await SendRequestAsync(
            "initialize",
            new
            {
                clientInfo = new
                {
                    name = "codex-usage-widget",
                    title = "Codex Usage Widget",
                    version = "1.0.0",
                },
            },
            cancellationToken);

        using (var document = JsonDocument.Parse(initializeResponse))
        {
            if (document.RootElement.TryGetProperty("error", out _) ||
                !document.RootElement.TryGetProperty("result", out _))
            {
                throw new InvalidOperationException("Codex app-server initialization failed.");
            }
        }

        await WriteLineAsync(
            JsonSerializer.Serialize(new { method = "initialized", @params = new { } }),
            cancellationToken);
    }

    private async Task<string> SendRequestAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _requestId);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, completion))
        {
            throw new InvalidOperationException("Could not register a Codex app-server request.");
        }

        try
        {
            var request = parameters is null
                ? JsonSerializer.Serialize(new { method, id })
                : JsonSerializer.Serialize(new { method, id, @params = parameters });
            await WriteLineAsync(request, cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);
            return await completion.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task WriteLineAsync(string line, CancellationToken cancellationToken)
    {
        var process = _process;
        if (process is null || !IsRunning(process))
        {
            throw new IOException("Codex app-server is not running.");
        }

        await process.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
    }

    private async Task ReadStdoutAsync(Process process, CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    failure = new IOException("Codex app-server disconnected.");
                    break;
                }

                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("id", out var idElement) ||
                    !idElement.TryGetInt64(out var id) ||
                    !_pending.TryGetValue(id, out var completion))
                {
                    continue;
                }

                if (root.TryGetProperty("error", out _))
                {
                    completion.TrySetException(
                        new InvalidOperationException("Codex app-server request failed."));
                }
                else if (!root.TryGetProperty("result", out _))
                {
                    completion.TrySetException(
                        new InvalidOperationException("Codex app-server returned an invalid response."));
                }
                else
                {
                    completion.TrySetResult(line);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or InvalidOperationException or ObjectDisposedException)
        {
            failure = new IOException("Codex app-server disconnected.");
        }
        finally
        {
            foreach (var completion in _pending.Values)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellationToken);
                }
                else
                {
                    completion.TrySetException(failure ?? new IOException("Codex app-server disconnected."));
                }
            }
        }
    }

    private static async Task DrainStderrAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync(cancellationToken) is not null)
            {
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (
            exception is IOException or InvalidOperationException or ObjectDisposedException)
        {
        }
    }

    private async Task StopProcessAsync()
    {
        var process = _process;
        var readerCancellation = _readerCancellation;
        var readerTask = _readerTask;
        var stderrTask = _stderrTask;

        _process = null;
        _readerCancellation = null;
        _readerTask = null;
        _stderrTask = null;

        readerCancellation?.Cancel();

        if (process is not null && IsRunning(process))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception exception) when (
                exception is Win32Exception or InvalidOperationException or NotSupportedException)
            {
            }
        }

        try
        {
            await Task.WhenAll(readerTask ?? Task.CompletedTask, stderrTask ?? Task.CompletedTask);
        }
        catch (Exception exception) when (
            exception is OperationCanceledException or IOException or InvalidOperationException or ObjectDisposedException)
        {
        }

        foreach (var completion in _pending.Values)
        {
            completion.TrySetCanceled();
        }

        process?.Dispose();
        readerCancellation?.Dispose();
    }

    private static bool IsRunning(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifetimeCancellation.Cancel();
        await _gate.WaitAsync();
        try
        {
            await StopProcessAsync();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
            _lifetimeCancellation.Dispose();
        }
    }
}
