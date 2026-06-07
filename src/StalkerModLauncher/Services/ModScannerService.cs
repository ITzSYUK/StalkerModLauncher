namespace StalkerModLauncher.Services;

public sealed class DiscoveredMod
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string DetectedBy { get; set; } = string.Empty;
}

public sealed class ModScannerService
{
    public Task<IReadOnlyList<DiscoveredMod>> ScanFolderAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ScanFolder(rootPath, cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<DiscoveredMod> ScanFolder(string rootPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(rootPath))
        {
            return Array.Empty<DiscoveredMod>();
        }

        var result = new List<DiscoveredMod>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ScanDirectory(rootPath, result, visited, cancellationToken);

        return result
            .OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ScanDirectory(
        string directoryPath,
        ICollection<DiscoveredMod> result,
        ISet<string> visited,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(directoryPath);
        }
        catch
        {
            return;
        }

        if (!visited.Add(fullPath))
        {
            return;
        }

        try
        {
            var detectedBy = GetDetectionReasons(fullPath);
            if (detectedBy.Count > 0)
            {
                result.Add(new DiscoveredMod
                {
                    Name = Path.GetFileName(fullPath),
                    Path = fullPath,
                    DetectedBy = string.Join(", ", detectedBy)
                });
                return;
            }

            foreach (var subDirectory in Directory.EnumerateDirectories(fullPath, "*", SafeEnumerationOptions))
            {
                ScanDirectory(subDirectory, result, visited, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Inaccessible folders do not prevent scanning the rest of the selected tree.
        }
    }

    private static List<string> GetDetectionReasons(string directoryPath)
    {
        var detectedBy = new List<string>();

        if (File.Exists(Path.Combine(directoryPath, "fsgame.ltx")))
        {
            detectedBy.Add("fsgame.ltx");
        }

        if (Directory.Exists(Path.Combine(directoryPath, "gamedata")))
        {
            detectedBy.Add("gamedata");
        }

        foreach (var binName in new[] { "bin", "bin_x64" })
        {
            var binPath = Path.Combine(directoryPath, binName);
            if (!Directory.Exists(binPath))
            {
                continue;
            }

            var executable = Directory.EnumerateFiles(binPath, "*.exe", SafeEnumerationOptions).FirstOrDefault();
            if (executable is not null)
            {
                detectedBy.Add($"{binName}{Path.DirectorySeparatorChar}{Path.GetFileName(executable)}");
            }
        }

        return detectedBy;
    }

    private static EnumerationOptions SafeEnumerationOptions { get; } = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };
}
