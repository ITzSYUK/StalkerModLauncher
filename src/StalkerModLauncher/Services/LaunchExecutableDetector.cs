namespace StalkerModLauncher.Services;

public sealed record LaunchExecutableSearchRoot(
    string RootPath,
    string DisplayName,
    int Order);

public sealed record LaunchExecutableDetection(
    string FullPath,
    string RelativePath,
    string SourceName,
    string Reason,
    int Score,
    int CandidateCount)
{
    public string Summary => $"{RelativePath} — {Reason}. Источник: {SourceName}.";
}

public static class LaunchExecutableDetector
{
    public static LaunchExecutableDetection? DetectBest(
        IEnumerable<LaunchExecutableSearchRoot> roots,
        string? requestedRelativePath,
        bool allowDedicated = false,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequested = Normalize(requestedRelativePath);
        var candidates = new List<Candidate>();
        foreach (var root in roots.Where(root => Directory.Exists(root.RootPath)))
        {
            foreach (var file in EnumerateExecutables(root.RootPath, cancellationToken))
            {
                var relative = Normalize(Path.GetRelativePath(root.RootPath, file));
                if (!allowDedicated && IsDedicatedExecutable(relative))
                {
                    continue;
                }

                var score = GetExecutableScore(relative, normalizedRequested, out var reason);
                candidates.Add(new Candidate(file, relative, root.DisplayName, root.Order, score, reason));
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var best = candidates
            .OrderBy(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.SourceOrder)
            .ThenBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .First();

        return new LaunchExecutableDetection(
            best.FullPath,
            best.RelativePath,
            best.SourceName,
            best.Reason,
            best.Score,
            candidates.Count);
    }

    public static bool IsDedicatedExecutable(string relativePath) =>
        Normalize(relativePath)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Contains("dedicated", StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateExecutables(string root, CancellationToken cancellationToken)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "*.exe", SafeEnumerationOptions);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return file;
        }
    }

    private static int GetExecutableScore(string relativePath, string requestedRelativePath, out string reason)
    {
        var normalized = Normalize(relativePath);
        var fileName = Path.GetFileName(normalized);

        if (!string.IsNullOrWhiteSpace(requestedRelativePath) &&
            normalized.Equals(requestedRelativePath, StringComparison.OrdinalIgnoreCase))
        {
            reason = "найден выбранный пользователем путь";
            return 0;
        }

        if (normalized.Equals("AnomalyLauncher.exe", StringComparison.OrdinalIgnoreCase))
        {
            reason = "найден лаунчер автономной сборки";
            return 5;
        }

        if (normalized.Equals(@"bin_x64\xrEngine.exe", StringComparison.OrdinalIgnoreCase))
        {
            reason = "найден основной 64-битный движок OGSR/X-Ray";
            return 10;
        }

        if (normalized.Equals(@"bin\xrEngine.exe", StringComparison.OrdinalIgnoreCase))
        {
            reason = "найден движок X-Ray/OGSR";
            return 11;
        }

        if (normalized.Equals(@"bin\xr_3da.exe", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(@"bin\XR_3DA.exe", StringComparison.OrdinalIgnoreCase))
        {
            reason = "найден стандартный бинарник S.T.A.L.K.E.R.";
            return 12;
        }

        if (fileName.Equals("xr_3da.exe", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("XR_3DA.exe", StringComparison.OrdinalIgnoreCase))
        {
            reason = "найден бинарник X-Ray";
            return 20;
        }

        if (fileName.Contains("xrEngine", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("OGSR", StringComparison.OrdinalIgnoreCase))
        {
            reason = "имя похоже на движок X-Ray/OGSR";
            return 25;
        }

        if (fileName.StartsWith("Anomaly", StringComparison.OrdinalIgnoreCase))
        {
            reason = "имя похоже на бинарник Anomaly";
            return 35;
        }

        if (fileName.Contains("xr", StringComparison.OrdinalIgnoreCase))
        {
            reason = "имя похоже на X-Ray бинарник";
            return 45;
        }

        reason = "единственный или запасной исполняемый файл";
        return 100;
    }

    private static string Normalize(string? path)
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

    private sealed record Candidate(
        string FullPath,
        string RelativePath,
        string SourceName,
        int SourceOrder,
        int Score,
        string Reason);
}
