namespace StalkerModLauncher.Services;

internal sealed class WorkspaceExecutableResolver
{
    public LaunchExecutableDetection? Resolve(string workspaceRoot, string requestedRelativePath, IProgress<string> progress)
    {
        var requestedIsDedicated = LaunchExecutableDetector.IsDedicatedExecutable(requestedRelativePath);
        var best = LaunchExecutableDetector.DetectBest(
            [new LaunchExecutableSearchRoot(workspaceRoot, "workspace", 0)],
            requestedRelativePath,
            requestedIsDedicated);
        if (best is null || best.Score > 50 && best.CandidateCount != 1)
        {
            return null;
        }

        progress.Report($"Бинарник '{requestedRelativePath}' не найден. Использую '{best.RelativePath}': {best.Reason}.");
        return best;
    }

    public LaunchExecutableDetection? ResolveStandalone(string modRoot, string requestedRelativePath, CancellationToken cancellationToken)
    {
        return LaunchExecutableDetector.DetectBest(
            [new LaunchExecutableSearchRoot(modRoot, "автономный мод", 1)],
            requestedRelativePath,
            allowDedicated: false,
            cancellationToken);
    }
}
