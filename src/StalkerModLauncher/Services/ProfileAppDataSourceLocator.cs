using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

internal static class ProfileAppDataSourceLocator
{
    private static readonly string[] ConventionalRelativePaths =
    [
        "appdata",
        "userdata",
        "_appdata_",
        Path.Combine("bin", "_appdata_")
    ];

    public static IEnumerable<string> EnumerateRoots(FileLayer layer) =>
        EnumerateRoots(layer.RootPath);

    public static IEnumerable<string> EnumerateRoots(string layerRoot)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var configuredRoot = TryResolveConfiguredRoot(layerRoot);
        if (configuredRoot is not null && Directory.Exists(configuredRoot) && seen.Add(configuredRoot))
        {
            yield return configuredRoot;
        }

        foreach (var relativePath in ConventionalRelativePaths)
        {
            var candidate = Path.GetFullPath(Path.Combine(layerRoot, relativePath));
            if (Directory.Exists(candidate) && seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static string? TryResolveConfiguredRoot(string layerRoot)
    {
        var fsgamePath = Path.Combine(layerRoot, "fsgame.ltx");
        if (!File.Exists(fsgamePath))
        {
            return null;
        }

        try
        {
            foreach (var line in File.ReadLines(fsgamePath, XRayTextEncoding.Config))
            {
                if (!line.TrimStart().StartsWith("$app_data_root$", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var assignment = line.IndexOf('=');
                if (assignment < 0)
                {
                    return null;
                }

                var parts = line[(assignment + 1)..]
                    .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    return null;
                }

                var rootToken = parts[2];
                if (rootToken.Equals("$fs_root$", StringComparison.OrdinalIgnoreCase))
                {
                    return parts.Length >= 4
                        ? Path.GetFullPath(Path.Combine(layerRoot, parts[3]))
                        : Path.GetFullPath(layerRoot);
                }

                if (rootToken.StartsWith('$'))
                {
                    return null;
                }

                return Path.IsPathRooted(rootToken)
                    ? Path.GetFullPath(rootToken)
                    : Path.GetFullPath(Path.Combine(layerRoot, rootToken));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }
}
