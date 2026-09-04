using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace CodexUsageWidget.Services;

public static class CodexExecutableLocator
{
    private const string PackageRepositoryKey =
        @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    public static string? Find()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in GetCandidates())
        {
            if (seen.Add(candidate) && IsRunnable(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidates()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmedDirectory = directory.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(trimmedDirectory))
                {
                    yield return Path.Combine(trimmedDirectory, "codex.exe");
                }
            }
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            yield return Path.Combine(userProfile, ".codex", ".sandbox-bin", "codex.exe");
        }

        foreach (var candidate in GetPackagedCandidates())
        {
            yield return candidate;
        }
    }

    private static IReadOnlyList<string> GetPackagedCandidates()
    {
        string[] packageIds;

        try
        {
            using var packagesKey = Registry.CurrentUser.OpenSubKey(PackageRepositoryKey);
            if (packagesKey is null)
            {
                return [];
            }

            packageIds = packagesKey.GetSubKeyNames();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (SecurityException)
        {
            return [];
        }

        var candidates = new List<string>();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            return candidates;
        }

        foreach (var packageId in packageIds)
        {
            if (!packageId.StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? packageRoot;

            try
            {
                using var packageKey = Registry.CurrentUser.OpenSubKey($@"{PackageRepositoryKey}\{packageId}");
                packageRoot = packageKey?.GetValue("PackageRootFolder") as string;
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (SecurityException)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(packageRoot))
            {
                continue;
            }

            var source = Path.Combine(packageRoot, "app", "resources", "codex.exe");
            var destination = Path.Combine(
                localAppData,
                "CodexUsageWidget",
                "CodexCli",
                packageId,
                "codex.exe");
            var candidate = GetOrStagePackagedCandidate(source, destination);

            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    private static string? GetOrStagePackagedCandidate(string source, string destination)
    {
        if (File.Exists(destination))
        {
            return destination;
        }

        var temporaryPath = $"{destination}.{Guid.NewGuid():N}.tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, temporaryPath, overwrite: false);
            File.Move(temporaryPath, destination, overwrite: false);
            return destination;
        }
        catch (IOException)
        {
            DeleteTemporaryFile(temporaryPath);
            return File.Exists(destination) ? destination : null;
        }
        catch (UnauthorizedAccessException)
        {
            DeleteTemporaryFile(temporaryPath);
            return null;
        }
        catch (SecurityException)
        {
            DeleteTemporaryFile(temporaryPath);
            return null;
        }
    }

    private static void DeleteTemporaryFile(string temporaryPath)
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

    private static bool IsRunnable(string candidate)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(candidate, "--version")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };

            if (!process.Start())
            {
                return false;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
