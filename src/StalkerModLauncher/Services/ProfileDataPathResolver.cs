using System.Text;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class ProfileDataPathResolver
{
    public IReadOnlyList<string> GetLogDirectories(ModProfile profile)
    {
        return GetDataRoots(profile)
            .Select(root => Path.Combine(root, "logs"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> GetSavedGameDirectories(ModProfile profile)
    {
        return GetDataRoots(profile)
            .Select(root => Path.Combine(root, "savedgames"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> GetDataRoots(ModProfile profile)
    {
        if (!profile.IsStandalone)
        {
            return string.IsNullOrWhiteSpace(profile.WorkspacePath)
                ? []
                : [Path.Combine(profile.WorkspacePath, "userdata")];
        }

        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(profile.WorkspacePath))
        {
            roots.Add(Path.Combine(profile.WorkspacePath, "userdata"));
        }

        foreach (var modRoot in profile.Mods
                     .Where(mod => mod.IsEnabled && Directory.Exists(mod.SourcePath))
                     .Select(mod => Path.GetFullPath(mod.SourcePath)))
        {
            var configuredRoot = TryResolveFsgameAppDataRoot(modRoot);
            if (configuredRoot is not null)
            {
                roots.Add(configuredRoot);
            }

            roots.Add(Path.Combine(modRoot, "appdata"));
            roots.Add(Path.Combine(modRoot, "userdata"));
            roots.Add(Path.Combine(modRoot, "_appdata_"));
            roots.Add(Path.Combine(modRoot, "bin", "_appdata_"));
            roots.Add(Path.Combine(modRoot, "bin_x64", "_appdata_"));
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string? TryResolveFsgameAppDataRoot(string modRoot)
    {
        try
        {
            var fsgamePath = FindFsgame(modRoot);
            if (fsgamePath is null)
            {
                return null;
            }

            foreach (var line in File.ReadLines(fsgamePath, Encoding.Default))
            {
                var trimmed = line.TrimStart();
                if (!trimmed.StartsWith("$app_data_root$", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parts = trimmed.Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length < 3)
                {
                    return null;
                }

                var configuredPath = parts
                    .Skip(2)
                    .LastOrDefault(part => !string.IsNullOrWhiteSpace(part) && !part.StartsWith('$'));
                if (configuredPath is null)
                {
                    return null;
                }

                return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fsgamePath)!, configuredPath));
            }
        }
        catch
        {
            // Invalid fsgame.ltx should not prevent fallback path discovery.
        }

        return null;
    }

    private static string? FindFsgame(string modRoot)
    {
        var rootFile = Path.Combine(modRoot, "fsgame.ltx");
        if (File.Exists(rootFile))
        {
            return rootFile;
        }

        return Directory.EnumerateDirectories(modRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(directory => Path.Combine(directory, "fsgame.ltx"))
            .FirstOrDefault(File.Exists);
    }
}
