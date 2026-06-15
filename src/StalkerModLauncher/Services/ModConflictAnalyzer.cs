using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class ModConflictAnalyzer
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);
    private const int MaxCacheEntries = 128;
    private readonly object _cacheSync = new();
    private readonly Dictionary<string, FileListCacheEntry> _fileCache = new(StringComparer.OrdinalIgnoreCase);

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

    private IReadOnlyDictionary<string, ModConflictState> Analyze(
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
                string.Equals(mods[index].Id, executableProviderId, StringComparison.OrdinalIgnoreCase),
                overwrittenFiles.Count(IsConfigurationFile),
                overwrittenFiles.Count(IsBinaryFile));
        }

        return result;
    }

    private static bool HasOverlap(HashSet<string>? left, HashSet<string>? right)
    {
        return left is { Count: > 0 } && right is { Count: > 0 } && left.Overlaps(right);
    }

    private static bool IsConfigurationFile(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() is ".ltx" or ".xml" or ".ini" or ".cfg" or ".script";
    }

    private static bool IsBinaryFile(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() is ".exe" or ".dll";
    }

    private HashSet<string> GetModFileList(string modPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(modPath))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var fullPath = Path.GetFullPath(modPath);
        var rootWriteTime = Directory.GetLastWriteTimeUtc(fullPath);
        lock (_cacheSync)
        {
            if (_fileCache.TryGetValue(fullPath, out var cached) &&
                cached.RootWriteTimeUtc == rootWriteTime &&
                DateTime.UtcNow - cached.CreatedAtUtc < CacheLifetime)
            {
                return cached.Files;
            }
        }

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var file in Directory.EnumerateFiles(fullPath, "*", SafeEnumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                files.Add(NormalizeRelativePath(Path.GetRelativePath(fullPath, file)));
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

        lock (_cacheSync)
        {
            _fileCache[fullPath] = new FileListCacheEntry(files, rootWriteTime, DateTime.UtcNow);
            if (_fileCache.Count > MaxCacheEntries)
            {
                var oldest = _fileCache.MinBy(pair => pair.Value.CreatedAtUtc).Key;
                _fileCache.Remove(oldest);
            }
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

    private sealed record FileListCacheEntry(
        HashSet<string> Files,
        DateTime RootWriteTimeUtc,
        DateTime CreatedAtUtc);
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
    bool ProvidesLaunchExecutable,
    int OverwrittenConfigurationCount,
    int OverwrittenBinaryCount);
