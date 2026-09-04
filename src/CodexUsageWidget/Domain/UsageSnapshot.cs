namespace CodexUsageWidget.Domain;

public sealed record UsageWindow(int RemainingPercent, DateTimeOffset ResetsAt);

public sealed record UsageSnapshot(UsageWindow? FiveHour, UsageWindow? Weekly)
{
    public bool HasAnyValue => FiveHour is not null || Weekly is not null;
}
