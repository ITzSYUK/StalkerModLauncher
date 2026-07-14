using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

internal sealed record UsvfsLaunchTarget(
    string ExecutablePath,
    string ExecutableRelativePath,
    string Arguments,
    string WorkingDirectory,
    string SourceName,
    bool BypassedLauncher);

internal sealed class AnomalyUsvfsLaunchTargetResolver
{
    private const string LauncherFileName = "AnomalyLauncher.exe";

    public UsvfsLaunchTarget Resolve(
        ModProfile profile,
        FileLayerPlan layerPlan,
        LaunchPlanResolution launchResolution)
    {
        var plan = launchResolution.Plan
            ?? throw new InvalidOperationException("USVFS launch plan is not ready.");
        var executable = launchResolution.Executable
            ?? throw new InvalidOperationException("USVFS launch executable is not ready.");

        var isLauncher = Path.GetFileName(plan.ExecutablePath).Equals(LauncherFileName, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(profile.UsvfsExecutableOverrideRelativePath))
        {
            if (!AnomalyUsvfsEngineSelection.TryParseRelativePath(
                    profile.UsvfsExecutableOverrideRelativePath,
                    out _,
                    out _))
            {
                throw new InvalidOperationException(
                    $"Некорректный движок Anomaly для USVFS: {profile.UsvfsExecutableOverrideRelativePath}");
            }

            var selectedEngine = layerPlan.FindFinalFile(profile.UsvfsExecutableOverrideRelativePath)
                ?? throw new FileNotFoundException(
                    $"Выбранный движок Anomaly не найден во включенных слоях: {profile.UsvfsExecutableOverrideRelativePath}");
            return CreateEngineTarget(profile, layerPlan, plan, selectedEngine, isLauncher);
        }

        if (!isLauncher)
        {
            return new UsvfsLaunchTarget(
                plan.ExecutablePath,
                executable.RelativePath,
                plan.Arguments,
                plan.WorkingDirectory,
                executable.SourceName,
                BypassedLauncher: false);
        }

        // The official USVFS hook follows child processes and uses its cross-bitness
        // proxy when this x86 launcher starts an x64 Anomaly engine. A manually chosen
        // AnomalyDX executable above remains the explicit direct-launch option.
        return new UsvfsLaunchTarget(
            plan.ExecutablePath,
            executable.RelativePath,
            plan.Arguments,
            plan.WorkingDirectory,
            executable.SourceName,
            BypassedLauncher: false);
    }

    private static UsvfsLaunchTarget CreateEngineTarget(
        ModProfile profile,
        FileLayerPlan layerPlan,
        LaunchPlan plan,
        FileLayerFile engine,
        bool BypassedLauncher)
    {
        var commandLine = layerPlan.FindFinalFile("commandline.txt");
        var launcherArguments = commandLine is null
            ? string.Empty
            : File.ReadAllText(commandLine.FullPath).Trim();

        return new UsvfsLaunchTarget(
            engine.FullPath,
            engine.RelativePath,
            CombineArguments(launcherArguments, profile.LaunchArguments),
            plan.WorkingDirectory,
            engine.SourceName,
            BypassedLauncher);
    }

    private static string CombineArguments(string first, string second)
    {
        return string.Join(
            " ",
            new[] { first.Trim(), second.Trim() }.Where(value => value.Length > 0));
    }
}
