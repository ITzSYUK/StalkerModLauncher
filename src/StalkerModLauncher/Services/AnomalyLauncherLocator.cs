using System.Diagnostics;

namespace StalkerModLauncher.Services;

public static class AnomalyLauncherLocator
{
    private const string LauncherFileName = "AnomalyLauncher.exe";
    private const string ConfigurationFileName = "AnomalyLauncher.cfg";

    public static string? TryFind(string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            return null;
        }

        var root = Path.GetFullPath(gameRoot);
        var wellKnownPath = Path.Combine(root, LauncherFileName);
        if (File.Exists(wellKnownPath))
        {
            return wellKnownPath;
        }

        string[] executables;
        try
        {
            executables = Directory.EnumerateFiles(root, "*.exe", SearchOption.TopDirectoryOnly).ToArray();
        }
        catch
        {
            return null;
        }

        var metadataMatches = executables.Where(HasAnomalyLauncherMetadata).ToArray();
        if (metadataMatches.Length == 1)
        {
            return metadataMatches[0];
        }

        return HasConfiguration(root) && executables.Length == 1
            ? executables[0]
            : null;
    }

    public static bool HasConfiguration(string? gameRoot) =>
        !string.IsNullOrWhiteSpace(gameRoot) &&
        File.Exists(Path.Combine(gameRoot, ConfigurationFileName));

    public static bool IsBaseGameLauncher(string? gameRoot, string executablePath)
    {
        var launcherPath = TryFind(gameRoot);
        return launcherPath is not null &&
               string.Equals(
                   Path.GetFullPath(launcherPath),
                   Path.GetFullPath(executablePath),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAnomalyLauncherMetadata(string executablePath)
    {
        try
        {
            var version = FileVersionInfo.GetVersionInfo(executablePath);
            return IsLauncherFileName(version.OriginalFilename) ||
                   IsLauncherFileName(version.InternalName) ||
                   (string.Equals(version.FileDescription, "Anomaly Launcher", StringComparison.OrdinalIgnoreCase) &&
                    version.ProductName?.Contains("Anomaly", StringComparison.OrdinalIgnoreCase) == true);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLauncherFileName(string? value) =>
        string.Equals(value, LauncherFileName, StringComparison.OrdinalIgnoreCase);
}
