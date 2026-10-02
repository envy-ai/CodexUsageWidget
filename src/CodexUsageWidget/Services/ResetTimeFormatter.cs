using System.Globalization;

namespace CodexUsageWidget.Services;

public static class ResetTimeFormatter
{
    public static string Format(DateTimeOffset reset, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (reset <= now)
        {
            return "resetting…";
        }

        var remaining = reset - now;
        string countdown;

        if (remaining >= TimeSpan.FromDays(1))
        {
            countdown = string.Create(
                CultureInfo.InvariantCulture,
                $"{(int)remaining.TotalDays}d {remaining.Hours}h");
        }
        else if (remaining >= TimeSpan.FromHours(1))
        {
            countdown = string.Create(
                CultureInfo.InvariantCulture,
                $"{(int)remaining.TotalHours}h {remaining.Minutes}m");
        }
        else
        {
            countdown = string.Create(
                CultureInfo.InvariantCulture,
                $"{Math.Max(1, (int)remaining.TotalMinutes)}m");
        }

        var localReset = TimeZoneInfo.ConvertTime(reset, zone);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{localReset:ddd h:mm tt} · {countdown}");
    }
}
