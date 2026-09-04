using Microsoft.Win32;

namespace CodexUsageWidget.Services;

public sealed class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexUsageWidget";

    public bool IsEnabled()
    {
        var expectedValue = GetExpectedValue();
        if (expectedValue is null)
        {
            return false;
        }

        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return runKey?.GetValue(ValueName) is string actualValue
            && string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            using var existingRunKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            existingRunKey?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var expectedValue = GetExpectedValue()
            ?? throw new InvalidOperationException("The current process does not have an executable path.");
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("The current-user startup registry key could not be opened.");
        runKey.SetValue(ValueName, expectedValue, RegistryValueKind.String);
    }

    private static string? GetExpectedValue()
    {
        var processPath = Environment.ProcessPath;
        return processPath is null ? null : $"\"{processPath}\"";
    }
}
