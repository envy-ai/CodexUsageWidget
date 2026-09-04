using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using CodexUsageWidget.Domain;
using CodexUsageWidget.Services;

namespace CodexUsageWidget.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CountdownInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(10);

    private readonly ICodexUsageClient _client;
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _countdownTimer;

    private UsageSnapshot? _snapshot;
    private int? _fiveHourRemaining;
    private int? _weeklyRemaining;
    private string _fiveHourResetText = string.Empty;
    private string _weeklyResetText = string.Empty;
    private string _statusText = "Connecting…";
    private bool _isStale;
    private DateTimeOffset? _lastSuccessfulUpdate;
    private int _started;
    private int _disposed;

    public MainViewModel(
        ICodexUsageClient client,
        Func<DateTimeOffset>? clock = null,
        TimeZoneInfo? timeZone = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _clock = clock ?? (() => DateTimeOffset.Now);
        _timeZone = timeZone ?? TimeZoneInfo.Local;

        _refreshTimer = new DispatcherTimer { Interval = RefreshInterval };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _countdownTimer = new DispatcherTimer { Interval = CountdownInterval };
        _countdownTimer.Tick += CountdownTimer_Tick;
    }

    public int? FiveHourRemaining
    {
        get => _fiveHourRemaining;
        private set
        {
            if (SetField(ref _fiveHourRemaining, value))
            {
                OnPropertyChanged(nameof(FiveHourPercentageText));
            }
        }
    }

    public int? WeeklyRemaining
    {
        get => _weeklyRemaining;
        private set
        {
            if (SetField(ref _weeklyRemaining, value))
            {
                OnPropertyChanged(nameof(WeeklyPercentageText));
            }
        }
    }

    public string FiveHourPercentageText => FiveHourRemaining is int value ? $"{value}%" : "—";

    public string WeeklyPercentageText => WeeklyRemaining is int value ? $"{value}%" : "—";

    public string FiveHourResetText
    {
        get => _fiveHourResetText;
        private set => SetField(ref _fiveHourResetText, value);
    }

    public string WeeklyResetText
    {
        get => _weeklyResetText;
        private set => SetField(ref _weeklyResetText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public bool IsStale
    {
        get => _isStale;
        private set => SetField(ref _isStale, value);
    }

    public DateTimeOffset? LastSuccessfulUpdate
    {
        get => _lastSuccessfulUpdate;
        private set => SetField(ref _lastSuccessfulUpdate, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task StartAsync()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        await RefreshAsync();
        if (Volatile.Read(ref _disposed) == 0)
        {
            _refreshTimer.Start();
            _countdownTimer.Start();
        }
    }

    public async Task RefreshAsync()
    {
        if (Volatile.Read(ref _disposed) != 0 || !await _refreshGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token);
            timeout.CancelAfter(RefreshTimeout);

            var snapshot = await _client.GetUsageAsync(timeout.Token);
            if (_lifetimeCancellation.IsCancellationRequested)
            {
                return;
            }

            _snapshot = snapshot;
            FiveHourRemaining = snapshot.FiveHour?.RemainingPercent;
            WeeklyRemaining = snapshot.Weekly?.RemainingPercent;
            LastSuccessfulUpdate = _clock();
            IsStale = false;
            StatusText = string.Empty;
            UpdateCountdowns();
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (
            exception is IOException or FormatException or InvalidOperationException or OperationCanceledException)
        {
            ApplyRefreshFailure();
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void ApplyRefreshFailure()
    {
        if (LastSuccessfulUpdate is null)
        {
            _snapshot = null;
            FiveHourRemaining = null;
            WeeklyRemaining = null;
            FiveHourResetText = string.Empty;
            WeeklyResetText = string.Empty;
            IsStale = false;
            StatusText = "Codex unavailable";
            return;
        }

        IsStale = true;
        var local = TimeZoneInfo.ConvertTime(LastSuccessfulUpdate.Value, _timeZone);
        StatusText = $"STALE · updated {local.ToString("h:mm tt", CultureInfo.InvariantCulture)}";
        UpdateCountdowns();
    }

    private void UpdateCountdowns()
    {
        var now = _clock();
        FiveHourResetText = _snapshot?.FiveHour is { } fiveHour
            ? ResetTimeFormatter.Format(fiveHour.ResetsAt, now, _timeZone)
            : string.Empty;
        WeeklyResetText = _snapshot?.Weekly is { } weekly
            ? ResetTimeFormatter.Format(weekly.ResetsAt, now, _timeZone)
            : string.Empty;
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshAsync();
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        UpdateCountdowns();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _refreshTimer.Stop();
        _countdownTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _countdownTimer.Tick -= CountdownTimer_Tick;
        _lifetimeCancellation.Cancel();

        await _refreshGate.WaitAsync();
        try
        {
            await _client.DisposeAsync();
        }
        finally
        {
            _refreshGate.Release();
        }
    }
}
