using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public static class GameExitDiagnosticsService
{
    private static readonly TimeSpan QuickExitThreshold = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FileTimeTolerance = TimeSpan.FromMinutes(2);

    public static GameExitDiagnostics Analyze(ModProfile profile, GameSessionResult session)
    {
        var isQuickExit = session.Duration < QuickExitThreshold;
        var logPaths = ProfileDataPathResolver.GetLogDirectories(profile);
        var earliestRelevantUtc = (session.StartedAtUtc ?? DateTime.UtcNow - session.Duration) - FileTimeTolerance;
        var latestLog = FindLatest(
            logPaths,
            earliestRelevantUtc,
            file => !Path.GetFileName(file).StartsWith("usvfs", StringComparison.OrdinalIgnoreCase),
            ".log",
            ".txt");
        var latestDump = FindLatest(logPaths, earliestRelevantUtc, ".mdmp", ".dmp");
        return new GameExitDiagnostics(isQuickExit, session.ExitCode, latestLog, latestDump);
    }

    private static string? FindLatest(IEnumerable<string> roots, DateTime earliestRelevantUtc, params string[] extensions)
    {
        return FindLatest(roots, earliestRelevantUtc, _ => true, extensions);
    }

    private static string? FindLatest(
        IEnumerable<string> roots,
        DateTime earliestRelevantUtc,
        Func<string, bool> predicate,
        params string[] extensions)
    {
        try
        {
            return roots.Where(Directory.Exists)
                .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                .Where(predicate)
                .Select(path => new FileInfo(path))
                .Where(file => extensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
                .Where(file => file.LastWriteTimeUtc >= earliestRelevantUtc)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Select(file => file.FullName)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}

public sealed record GameExitDiagnostics(
    bool IsQuickExit,
    int? ExitCode,
    string? LatestLogPath,
    string? LatestCrashDumpPath)
{
    public bool IsSuspiciousExit => IsQuickExit || ExitCode is not null and not 0 || LatestCrashDumpPath is not null;
}
