namespace StalkerModLauncher.Services;

public sealed class DiscoveredMod
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string DetectedBy { get; set; } = string.Empty;
}

public sealed class ModScannerService
{
    public List<DiscoveredMod> ScanFolder(string rootPath)
    {
        var result = new List<DiscoveredMod>();

        if (!Directory.Exists(rootPath))
        {
            return result;
        }

        foreach (var subDir in Directory.EnumerateDirectories(rootPath))
        {
            var mod = ExamineDirectory(subDir);
            if (mod is not null)
            {
                result.Add(mod);
            }
        }

        return result;
    }

    private static DiscoveredMod? ExamineDirectory(string dirPath)
    {
        var dirName = Path.GetFileName(dirPath);

        var detectedBy = new List<string>();

        if (File.Exists(Path.Combine(dirPath, "fsgame.ltx")))
        {
            detectedBy.Add("fsgame.ltx");
        }

        if (Directory.Exists(Path.Combine(dirPath, "gamedata")))
        {
            detectedBy.Add("gamedata");
        }

        if (Directory.Exists(Path.Combine(dirPath, "bin")))
        {
            var exes = Directory.EnumerateFiles(Path.Combine(dirPath, "bin"), "*.exe", SearchOption.TopDirectoryOnly).Take(2).ToList();
            if (exes.Count > 0)
            {
                detectedBy.Add($"bin{Path.DirectorySeparatorChar}{Path.GetFileName(exes[0])}");
            }
        }

        if (detectedBy.Count == 0)
        {
            foreach (var subDir in Directory.EnumerateDirectories(dirPath))
            {
                var inner = ExamineDirectory(subDir);
                if (inner is not null)
                {
                    return inner;
                }
            }

            return null;
        }

        return new DiscoveredMod
        {
            Name = dirName,
            Path = dirPath,
            DetectedBy = string.Join(", ", detectedBy)
        };
    }
}
