using CodexUsageWidget.Services;

namespace CodexUsageWidget.Tests;

public sealed class UsageSnapshotParserTests
{
    [Fact]
    public void UsesCodexBucketWithoutBorrowingModelSpecificWindows()
    {
        const string json = """
        {
          "result": {
            "rateLimitsByLimitId": {
              "codex": {
                "primary": {
                  "usedPercent": 23,
                  "windowDurationMins": 10080,
                  "resetsAt": 3000
                },
                "secondary": null
              },
              "codex-model": {
                "primary": {
                  "usedPercent": 90,
                  "windowDurationMins": 300,
                  "resetsAt": 2000
                }
              }
            },
            "rateLimits": {
              "primary": {
                "usedPercent": 1,
                "windowDurationMins": 300,
                "resetsAt": 4000
              }
            }
          }
        }
        """;

        var snapshot = UsageSnapshotParser.ParseResponse(json);

        Assert.Null(snapshot.FiveHour);
        Assert.Equal(77, snapshot.Weekly!.RemainingPercent);
        Assert.Equal(3000, snapshot.Weekly.ResetsAt.ToUnixTimeSeconds());
        Assert.True(snapshot.HasAnyValue);
    }

    [Fact]
    public void UsesFallbackRateLimitsContainerWhenCodexBucketIsAbsent()
    {
        const string json = """
        {"result":{"rateLimits":{"primary":{"usedPercent":25,"windowDurationMins":300,"resetsAt":2000}}}}
        """;

        var snapshot = UsageSnapshotParser.ParseResponse(json);

        Assert.Equal(75, snapshot.FiveHour!.RemainingPercent);
        Assert.Equal(2000, snapshot.FiveHour.ResetsAt.ToUnixTimeSeconds());
        Assert.Null(snapshot.Weekly);
    }

    [Fact]
    public void IdentifiesWindowsByDurationWhenPrimaryAndSecondaryAreReversed()
    {
        const string json = """
        {"result":{"rateLimits":{"primary":{"usedPercent":10,"windowDurationMins":10080,"resetsAt":3000},"secondary":{"usedPercent":90,"windowDurationMins":300,"resetsAt":2000}}}}
        """;

        var snapshot = UsageSnapshotParser.ParseResponse(json);

        Assert.Equal(10, snapshot.FiveHour!.RemainingPercent);
        Assert.Equal(2000, snapshot.FiveHour.ResetsAt.ToUnixTimeSeconds());
        Assert.Equal(90, snapshot.Weekly!.RemainingPercent);
        Assert.Equal(3000, snapshot.Weekly.ResetsAt.ToUnixTimeSeconds());
    }

    [Fact]
    public void TreatsIncompleteWindowAsUnavailable()
    {
        const string json = """
        {"result":{"rateLimits":{"primary":{"usedPercent":20,"windowDurationMins":300},"secondary":{"usedPercent":40,"windowDurationMins":10080,"resetsAt":3000}}}}
        """;

        var snapshot = UsageSnapshotParser.ParseResponse(json);

        Assert.Null(snapshot.FiveHour);
        Assert.Equal(60, snapshot.Weekly!.RemainingPercent);
    }

    [Fact]
    public void ClampsRemainingPercentAtOneHundred()
    {
        const string json = """
        {"result":{"rateLimits":{"primary":{"usedPercent":-10,"windowDurationMins":300,"resetsAt":2000}}}}
        """;

        var snapshot = UsageSnapshotParser.ParseResponse(json);

        Assert.Equal(100, snapshot.FiveHour!.RemainingPercent);
    }

    [Fact]
    public void ClampsRemainingPercentAtZero()
    {
        const string json = """
        {"result":{"rateLimits":{"primary":{"usedPercent":140,"windowDurationMins":300,"resetsAt":2000}}}}
        """;

        var snapshot = UsageSnapshotParser.ParseResponse(json);

        Assert.Equal(0, snapshot.FiveHour!.RemainingPercent);
    }

    [Theory]
    [InlineData("{\"result\":{}}")]
    [InlineData("not json raw-response-marker")]
    public void InvalidResponseThrowsFormatExceptionWithoutEchoingRawJson(string json)
    {
        var exception = Assert.Throws<FormatException>(() => UsageSnapshotParser.ParseResponse(json));

        Assert.False(exception.Message.Contains(json, StringComparison.Ordinal));
    }
}
