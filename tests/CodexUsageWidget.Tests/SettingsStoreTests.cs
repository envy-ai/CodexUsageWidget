using CodexUsageWidget.Services;

namespace CodexUsageWidget.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void RoundTripsPositionAndStartupPreference()
    {
        var directory = CreateTempDirectory();

        try
        {
            var store = new SettingsStore(directory);
            var expected = new WidgetSettings(12.5, 34.5, "DISPLAY1", true, 480, 140, true);

            store.Save(expected);
            var actual = store.Load();

            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MalformedFileFallsBackToDefaults()
    {
        var directory = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "settings.json"), "broken");
            var store = new SettingsStore(directory);

            var actual = store.Load();

            Assert.Equal(WidgetSettings.Default, actual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LegacyFileDefaultsMissingSizeAndPinState()
    {
        var directory = CreateTempDirectory();

        try
        {
            const string json = """
                {
                  "Left": 12.5,
                  "Top": 34.5,
                  "MonitorDeviceName": "DISPLAY1",
                  "StartWithWindows": true
                }
                """;
            File.WriteAllText(Path.Combine(directory, "settings.json"), json);
            var store = new SettingsStore(directory);

            var actual = store.Load();

            Assert.Equal(12.5, actual.Left);
            Assert.Equal(34.5, actual.Top);
            Assert.Equal("DISPLAY1", actual.MonitorDeviceName);
            Assert.True(actual.StartWithWindows);
            Assert.Null(actual.Width);
            Assert.Null(actual.Height);
            Assert.False(actual.Topmost);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CodexUsageWidget.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
