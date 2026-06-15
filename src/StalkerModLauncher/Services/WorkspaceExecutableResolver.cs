namespace StalkerModLauncher.Services;

internal sealed class WorkspaceExecutableResolver
{
    public string? Resolve(string workspaceRoot, string requestedRelativePath, IProgress<string> progress)
    {
        var requestedName = Path.GetFileName(requestedRelativePath);
        var requestedIsDedicated = IsDedicatedExecutable(requestedRelativePath);
        var candidates = Directory.EnumerateFiles(workspaceRoot, "*.exe", SafeEnumerationOptions)
            .Select(path => new { FullPath = path, RelativePath = Path.GetRelativePath(workspaceRoot, path) })
            .Where(candidate => requestedIsDedicated || !IsDedicatedExecutable(candidate.RelativePath))
            .OrderBy(candidate => GetExecutableRank(candidate.RelativePath, requestedName))
            .ThenBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var best = candidates.FirstOrDefault();
        if (best is null || GetExecutableRank(best.RelativePath, requestedName) > 50 && candidates.Length != 1)
        {
            return null;
        }

        progress.Report($"Requested executable '{requestedRelativePath}' was not found. Using detected executable '{best.RelativePath}'.");
        return best.FullPath;
    }

    public string? ResolveStandalone(string modRoot, string requestedRelativePath, CancellationToken cancellationToken)
    {
        return Directory.EnumerateFiles(modRoot, "*.exe", SearchOption.AllDirectories)
            .Select(path =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Path.GetRelativePath(modRoot, path);
            })
            .OrderBy(path => path.Equals(requestedRelativePath, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(path => GetExecutableRank(path, Path.GetFileName(requestedRelativePath)))
            .FirstOrDefault();
    }

    private static bool IsDedicatedExecutable(string relativePath) =>
        relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Contains("dedicated", StringComparer.OrdinalIgnoreCase);

    private static int GetExecutableRank(string relativePath, string requestedName)
    {
        var normalized = relativePath.Replace('/', '\\');
        var fileName = Path.GetFileName(normalized);
        if (!string.IsNullOrWhiteSpace(requestedName) && fileName.Equals(requestedName, StringComparison.OrdinalIgnoreCase)) return 0;
        if (normalized.Equals(@"bin_x64\xrEngine.exe", StringComparison.OrdinalIgnoreCase)) return 1;
        if (normalized.Equals(@"bin\xrEngine.exe", StringComparison.OrdinalIgnoreCase)) return 2;
        if (normalized.Equals(@"bin\xr_3da.exe", StringComparison.OrdinalIgnoreCase)) return 3;
        if (fileName.Contains("xrEngine", StringComparison.OrdinalIgnoreCase) || fileName.Contains("OGSR", StringComparison.OrdinalIgnoreCase)) return 10;
        if (fileName.Contains("xr", StringComparison.OrdinalIgnoreCase)) return 20;
        return 100;
    }

    private static EnumerationOptions SafeEnumerationOptions { get; } = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };
}
