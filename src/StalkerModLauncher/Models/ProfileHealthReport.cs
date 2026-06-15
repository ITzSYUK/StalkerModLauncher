namespace StalkerModLauncher.Models;

public enum ProfileHealthStatus
{
    Healthy,
    Warning,
    Error
}

public sealed record ProfileHealthCheck(
    ProfileHealthStatus Status,
    string Title,
    string Details);

public sealed record ProfileHealthReport(
    IReadOnlyList<ProfileHealthCheck> Checks,
    string ProfileFolderPath,
    string SavedGamesPath,
    string? LatestLogPath,
    string? LatestCrashDumpPath,
    WorkspaceStatus? Workspace = null)
{
    public int ErrorCount => Checks.Count(check => check.Status == ProfileHealthStatus.Error);
    public int WarningCount => Checks.Count(check => check.Status == ProfileHealthStatus.Warning);
    public bool IsReady => ErrorCount == 0;

    public string Summary => IsReady
        ? WarningCount == 0 ? "Профиль готов к запуску." : $"Профиль готов, но есть предупреждения: {WarningCount}."
        : $"Профиль требует внимания. Ошибок: {ErrorCount}, предупреждений: {WarningCount}.";

    public string ToText(string profileName)
    {
        var lines = new List<string>
        {
            $"Профиль: {profileName}",
            Summary,
            string.Empty
        };

        lines.AddRange(Checks.Select(check => $"[{check.Status}] {check.Title}: {check.Details}"));
        if (Workspace is { Exists: true } workspace)
        {
            lines.Add(string.Empty);
            lines.Add($"Workspace: логический размер {workspace.LogicalSizeDisplay}, реально занимает около {workspace.PhysicalSizeDisplay}.");
            lines.Add($"Файлы: {workspace.FileCount:N0}; hardlink: {workspace.HardLinkCount:N0}; symlink: {workspace.SymbolicLinkCount:N0}; локальные: {workspace.LocalFileCount:N0}.");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
