using System.Text.Json;
using CodexUsageWidget.Domain;

namespace CodexUsageWidget.Services;

public static class UsageSnapshotParser
{
    public static UsageSnapshot ParseResponse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var result = document.RootElement.GetProperty("result");
            JsonElement limits;
            if (result.TryGetProperty("rateLimitsByLimitId", out var buckets) &&
                buckets.ValueKind == JsonValueKind.Object &&
                buckets.TryGetProperty("codex", out var codex))
            {
                if (codex.ValueKind != JsonValueKind.Object)
                {
                    throw new FormatException("Codex rate-limit data was not present.");
                }

                limits = codex;
            }
            else if (result.TryGetProperty("rateLimits", out var fallback) &&
                     fallback.ValueKind == JsonValueKind.Object)
            {
                limits = fallback;
            }
            else
            {
                throw new FormatException("Codex rate-limit data was not present.");
            }

            UsageWindow? fiveHour = null;
            UsageWindow? weekly = null;
            ReadWindow(limits, "primary", ref fiveHour, ref weekly);
            ReadWindow(limits, "secondary", ref fiveHour, ref weekly);

            return new UsageSnapshot(fiveHour, weekly);
        }
        catch (FormatException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new FormatException("The Codex rate-limit response was malformed.");
        }
    }

    private static void ReadWindow(
        JsonElement limits,
        string propertyName,
        ref UsageWindow? fiveHour,
        ref UsageWindow? weekly)
    {
        if (!limits.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("usedPercent", out var usedElement) ||
            !usedElement.TryGetInt32(out var usedPercent) ||
            !element.TryGetProperty("windowDurationMins", out var durationElement) ||
            !durationElement.TryGetInt64(out var durationMinutes) ||
            !element.TryGetProperty("resetsAt", out var resetsAtElement) ||
            !resetsAtElement.TryGetInt64(out var resetsAtSeconds))
        {
            return;
        }

        UsageWindow window;
        try
        {
            window = new UsageWindow(
                (int)Math.Clamp(100L - usedPercent, 0L, 100L),
                DateTimeOffset.FromUnixTimeSeconds(resetsAtSeconds));
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        switch (durationMinutes)
        {
            case 300:
                fiveHour = window;
                break;
            case 10080:
                weekly = window;
                break;
        }
    }
}
