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

        var configuration = layerPlan.FindFinalFile("AnomalyLauncher.cfg")
            ?? throw new InvalidOperationException(
                "AnomalyLauncher.cfg was not found. Select a 64-bit AnomalyDX executable manually for USVFS mode.");
        var lines = File.ReadAllLines(configuration.FullPath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
        if (lines.Length < 2)
        {
            throw new InvalidOperationException(
                "AnomalyLauncher.cfg is incomplete. Select a 64-bit AnomalyDX executable manually for USVFS mode.");
        }

        var renderer = NormalizeToken(lines[0], "renderer");
        var instructionSet = lines[1].Equals("AVX", StringComparison.OrdinalIgnoreCase) ? "AVX" : string.Empty;
        var candidates = instructionSet.Length > 0
            ? new[]
            {
                Path.Combine("bin", $"Anomaly{renderer}{instructionSet}.exe"),
                Path.Combine("bin", $"Anomaly{renderer}.exe")
            }
            : new[] { Path.Combine("bin", $"Anomaly{renderer}.exe") };

        var engine = candidates
            .Select(layerPlan.FindFinalFile)
            .FirstOrDefault(candidate => candidate is not null)
            ?? throw new FileNotFoundException(
                $"The Anomaly engine selected in AnomalyLauncher.cfg was not found: {string.Join(" or ", candidates)}");

        return CreateEngineTarget(profile, layerPlan, plan, engine, BypassedLauncher: true);
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

    private static string NormalizeToken(string value, string fieldName)
    {
        if (value.Length == 0 || value.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new InvalidOperationException($"Anomaly launcher {fieldName} value is invalid: '{value}'.");
        }

        return value.ToUpperInvariant();
    }

    private static string CombineArguments(string first, string second)
    {
        return string.Join(
            " ",
            new[] { first.Trim(), second.Trim() }.Where(value => value.Length > 0));
    }
}
