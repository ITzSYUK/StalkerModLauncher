using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class ModConflictAnalyzer
{
    public Task<IReadOnlyDictionary<string, ModConflictState>> AnalyzeAsync(
        IReadOnlyList<ModConflictInput> mods,
        CancellationToken cancellationToken = default)
    {
        return AnalyzeAsync(mods, null, cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, ModConflictState>> AnalyzeAsync(
        IReadOnlyList<ModConflictInput> mods,
        string? launchExecutableRelativePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Analyze(mods, launchExecutableRelativePath, cancellationToken), cancellationToken);
    }

    private static IReadOnlyDictionary<string, ModConflictState> Analyze(
        IReadOnlyList<ModConflictInput> mods,
        string? launchExecutableRelativePath,
        CancellationToken cancellationToken)
    {
        var fileCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in mods.Where(mod => mod.IsEnabled && !string.IsNullOrWhiteSpace(mod.SourcePath)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            fileCache[mod.Id] = GetModFileList(mod.SourcePath, cancellationToken);
        }

        var normalizedExecutable = NormalizeRelativePath(launchExecutableRelativePath);
        var executableProviderId = mods
            .Where(mod => mod.IsEnabled)
            .LastOrDefault(mod => fileCache.GetValueOrDefault(mod.Id)?.Contains(normalizedExecutable) == true)
            ?.Id;

        var result = new Dictionary<string, ModConflictState>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < mods.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentFiles = fileCache.GetValueOrDefault(mods[index].Id);
            var hasEnabledBelow = false;
            for (var belowIndex = index + 1; belowIndex < mods.Count; belowIndex++)
            {
                var lowerFiles = fileCache.GetValueOrDefault(mods[belowIndex].Id);
                if (HasOverlap(currentFiles, lowerFiles))
                {
                    hasEnabledBelow = true;
                    break;
                }
            }

            var overwrittenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var overwrittenModNames = new List<string>();
            for (var aboveIndex = 0; aboveIndex < index; aboveIndex++)
            {
                var upperFiles = fileCache.GetValueOrDefault(mods[aboveIndex].Id);
                if (currentFiles is null || upperFiles is null)
                {
                    continue;
                }

                var overlap = currentFiles.Intersect(upperFiles, StringComparer.OrdinalIgnoreCase).ToArray();
                if (overlap.Length > 0)
                {
                    overwrittenFiles.UnionWith(overlap);
                    overwrittenModNames.Add(mods[aboveIndex].Name);
                }
            }

            result[mods[index].Id] = new ModConflictState(
                hasEnabledBelow,
                overwrittenFiles.Count > 0,
                overwrittenFiles.Count,
                overwrittenModNames,
                string.Equals(mods[index].Id, executableProviderId, StringComparison.OrdinalIgnoreCase));
        }

        return result;
    }

    private static bool HasOverlap(HashSet<string>? left, HashSet<string>? right)
    {
        return left is { Count: > 0 } && right is { Count: > 0 } && left.Overlaps(right);
    }

    private static HashSet<string> GetModFileList(string modPath, CancellationToken cancellationToken)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(modPath))
        {
            return files;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(modPath, "*", SafeEnumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                files.Add(NormalizeRelativePath(Path.GetRelativePath(modPath, file)));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // An inaccessible mod is handled by profile validation; conflict analysis stays best-effort.
        }

        return files;
    }

    private static string NormalizeRelativePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
    }

    private static EnumerationOptions SafeEnumerationOptions { get; } = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };
}

public sealed record ModConflictInput(string Id, string Name, string SourcePath, bool IsEnabled)
{
    public static ModConflictInput FromMod(ModEntry mod)
    {
        return new ModConflictInput(mod.Id, mod.Name, mod.SourcePath, mod.IsEnabled);
    }
}

public sealed record ModConflictState(
    bool IsLocked,
    bool HasOverlapsAbove,
    int OverwrittenFileCount,
    IReadOnlyList<string> OverwrittenModNames,
    bool ProvidesLaunchExecutable);
