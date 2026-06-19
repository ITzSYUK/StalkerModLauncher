using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed record ProfileFileSourceRoot(
    string RootPath,
    string DisplayName,
    int Order,
    bool CanPinExecutableSource);

public sealed record ProfileExecutableSelection(
    string RelativePath,
    string SourceRootPath,
    string SourceName,
    bool PinsSource);

public static class ProfileExecutableSourceResolver
{
    public static ProfileExecutableSelection? TryCreateSelection(
        ModProfile profile,
        string selectedPath,
        bool includeWorkspace)
    {
        var roots = GetSourceRoots(profile, includeWorkspace)
            .Where(root => Directory.Exists(root.RootPath))
            .OrderByDescending(root => root.RootPath.Length);

        foreach (var root in roots)
        {
            var relative = Path.GetRelativePath(root.RootPath, selectedPath);
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            {
                continue;
            }

            return new ProfileExecutableSelection(
                relative,
                root.CanPinExecutableSource ? Path.GetFullPath(root.RootPath) : string.Empty,
                root.DisplayName,
                root.CanPinExecutableSource);
        }

        return null;
    }

    public static ProfileExecutableSelection? DetectAutomaticSelection(
        ModProfile profile,
        bool includeWorkspace,
        CancellationToken cancellationToken = default)
    {
        var roots = GetSourceRoots(profile, includeWorkspace)
            .Where(root => Directory.Exists(root.RootPath))
            .ToList();
        var detected = LaunchExecutableDetector.DetectBest(
            roots.Select(root => new LaunchExecutableSearchRoot(root.RootPath, root.DisplayName, root.Order)),
            requestedRelativePath: null,
            cancellationToken: cancellationToken);

        return detected is null
            ? null
            : new ProfileExecutableSelection(
                detected.RelativePath,
                string.Empty,
                detected.SourceName,
                PinsSource: false);
    }

    public static ProfileFileSourceRoot? FindPinnedSourceRoot(ModProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.ExecutableSourcePath))
        {
            return null;
        }

        var pinnedPath = Path.GetFullPath(profile.ExecutableSourcePath);
        return GetSourceRoots(profile, includeWorkspace: false)
            .Where(root => root.CanPinExecutableSource && Directory.Exists(root.RootPath))
            .FirstOrDefault(root => FileSystemSafety.IsSameDirectory(root.RootPath, pinnedPath));
    }

    public static string DescribePinnedSource(ModProfile profile)
    {
        var root = FindPinnedSourceRoot(profile);
        return root is null
            ? "Автоматический выбор по приоритету модов."
            : $"Вручную выбран источник: {root.DisplayName}.";
    }

    public static IReadOnlyList<ProfileFileSourceRoot> GetSourceRoots(ModProfile profile, bool includeWorkspace)
    {
        var roots = new List<ProfileFileSourceRoot>();
        if (!profile.IsStandalone && Directory.Exists(profile.GameInstallPath))
        {
            roots.Add(new ProfileFileSourceRoot(
                Path.GetFullPath(profile.GameInstallPath),
                "базовая игра",
                0,
                true));
        }

        roots.AddRange(profile.Mods
            .Where(mod => mod.IsEnabled && Directory.Exists(mod.SourcePath))
            .OrderBy(mod => mod.Order)
            .Select(mod => new ProfileFileSourceRoot(
                Path.GetFullPath(mod.SourcePath),
                $"мод: {mod.Name}",
                mod.Order,
                true)));

        if (includeWorkspace && !string.IsNullOrWhiteSpace(profile.WorkspacePath))
        {
            var currentWorkspace = Path.Combine(profile.WorkspacePath, "current");
            if (Directory.Exists(currentWorkspace))
            {
                roots.Add(new ProfileFileSourceRoot(
                    Path.GetFullPath(currentWorkspace),
                    "workspace",
                    int.MaxValue,
                    false));
            }
        }

        return roots;
    }
}
