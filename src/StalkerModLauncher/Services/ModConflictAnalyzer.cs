using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class ModConflictAnalyzer
{
    public Task<IReadOnlyDictionary<string, ModConflictState>> AnalyzeAsync(
        IReadOnlyList<ModConflictInput> mods,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Analyze(mods, cancellationToken), cancellationToken);
    }

    private static IReadOnlyDictionary<string, ModConflictState> Analyze(
        IReadOnlyList<ModConflictInput> mods,
        CancellationToken cancellationToken)
    {
        var fileCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in mods.Where(mod => mod.IsEnabled && !string.IsNullOrWhiteSpace(mod.SourcePath)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            fileCache[mod.Id] = GetModFileList(mod.SourcePath, cancellationToken);
        }

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

            var hasOverlapsAbove = false;
            for (var aboveIndex = 0; aboveIndex < index; aboveIndex++)
            {
                var upperFiles = fileCache.GetValueOrDefault(mods[aboveIndex].Id);
                if (HasOverlap(currentFiles, upperFiles))
                {
                    hasOverlapsAbove = true;
                    break;
                }
            }

            result[mods[index].Id] = new ModConflictState(hasEnabledBelow, hasOverlapsAbove);
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
                files.Add(Path.GetRelativePath(modPath, file));
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

    private static EnumerationOptions SafeEnumerationOptions { get; } = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };
}

public sealed record ModConflictInput(string Id, string SourcePath, bool IsEnabled)
{
    public static ModConflictInput FromMod(ModEntry mod)
    {
        return new ModConflictInput(mod.Id, mod.SourcePath, mod.IsEnabled);
    }
}

public sealed record ModConflictState(bool IsLocked, bool HasOverlapsAbove);
