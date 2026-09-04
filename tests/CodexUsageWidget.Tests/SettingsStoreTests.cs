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
            var expected = new WidgetSettings(12.5, 34.5, "DISPLAY1", true);

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

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CodexUsageWidget.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
