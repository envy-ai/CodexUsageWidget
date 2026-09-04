using System.IO;
using System.Security;
using System.Text.Json;

namespace CodexUsageWidget.Services;

public sealed record WidgetSettings(
    double? Left,
    double? Top,
    string? MonitorDeviceName,
    bool StartWithWindows)
{
    public static WidgetSettings Default { get; } = new(null, null, null, false);
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _directory;
    private readonly string _settingsPath;

    public SettingsStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexUsageWidget");
        _settingsPath = Path.Combine(_directory, "settings.json");
    }

    public WidgetSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return WidgetSettings.Default;
            }

            return JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(_settingsPath), SerializerOptions)
                ?? WidgetSettings.Default;
        }
        catch (IOException)
        {
            return WidgetSettings.Default;
        }
        catch (JsonException)
        {
            return WidgetSettings.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return WidgetSettings.Default;
        }
    }

    public void Save(WidgetSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(_directory);
        var temporaryPath = Path.Combine(_directory, $"settings.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(settings, SerializerOptions));

            if (File.Exists(_settingsPath))
            {
                File.Replace(temporaryPath, _settingsPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, _settingsPath);
            }
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (SecurityException)
        {
        }
    }
}
