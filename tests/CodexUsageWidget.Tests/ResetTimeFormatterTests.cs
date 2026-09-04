using CodexUsageWidget.Services;

namespace CodexUsageWidget.Tests;

public sealed class ResetTimeFormatterTests
{
    private static readonly TimeZoneInfo UtcMinusFour = TimeZoneInfo.CreateCustomTimeZone(
        "UTC-04:00",
        TimeSpan.FromHours(-4),
        "UTC-04:00",
        "UTC-04:00");

    [Fact]
    public void FormatsHoursMinutesAndLocalResetTime()
    {
        var now = new DateTimeOffset(2025, 1, 3, 16, 0, 0, TimeSpan.Zero);
        var reset = new DateTimeOffset(2025, 1, 3, 18, 14, 0, TimeSpan.Zero);

        var result = ResetTimeFormatter.Format(reset, now, UtcMinusFour);

        Assert.Equal("2h 14m · resets Fri 2:14 PM", result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void ReturnsResettingWhenResetIsNotInTheFuture(int secondsUntilReset)
    {
        var now = new DateTimeOffset(2025, 1, 3, 16, 0, 0, TimeSpan.Zero);
        var reset = now.AddSeconds(secondsUntilReset);

        var result = ResetTimeFormatter.Format(reset, now, UtcMinusFour);

        Assert.Equal("resetting…", result);
    }

    [Fact]
    public void FormatsWholeDaysAndRemainingHours()
    {
        var now = new DateTimeOffset(2025, 1, 3, 16, 0, 0, TimeSpan.Zero);
        var reset = new DateTimeOffset(2025, 1, 5, 19, 0, 0, TimeSpan.Zero);

        var result = ResetTimeFormatter.Format(reset, now, UtcMinusFour);

        Assert.Equal("2d 3h · resets Sun 3:00 PM", result);
    }

    [Fact]
    public void ClampsSubMinuteCountdownToOneMinute()
    {
        var now = new DateTimeOffset(2025, 1, 3, 16, 0, 0, TimeSpan.Zero);
        var reset = now.AddSeconds(30);

        var result = ResetTimeFormatter.Format(reset, now, UtcMinusFour);

        Assert.Equal("1m · resets Fri 12:00 PM", result);
    }
}
