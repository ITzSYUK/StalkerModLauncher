namespace StalkerModLauncher.Models;

public sealed record LaunchPreflightReport(IReadOnlyList<ProfileHealthCheck> Checks)
{
    public int ErrorCount => Checks.Count(check => check.Status == ProfileHealthStatus.Error);
    public int WarningCount => Checks.Count(check => check.Status == ProfileHealthStatus.Warning);
    public bool CanLaunch => ErrorCount == 0;

    public string ToErrorMessage()
    {
        var errors = Checks
            .Where(check => check.Status == ProfileHealthStatus.Error)
            .Select(check => $"• {check.Title}: {check.Details}");
        return "Профиль не прошёл проверку перед запуском:" +
               Environment.NewLine + Environment.NewLine +
               string.Join(Environment.NewLine, errors);
    }
}
