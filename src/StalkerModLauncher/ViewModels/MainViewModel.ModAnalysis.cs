using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed partial class MainViewModel
{
    private void RecalculateModOverlayInfo()
    {
        _conflictAnalysisCancellation?.Cancel();
        _conflictAnalysisCancellation?.Dispose();
        _conflictAnalysisCancellation = null;

        var profile = SelectedProfile;
        if (profile is null)
        {
            return;
        }

        var plan = TryCreateConflictAnalysisPlan(profile);
        var inputs = plan is null
            ? profile.Mods
                .OrderBy(mod => mod.Order)
                .Select(ModConflictInput.FromMod)
                .ToArray()
            : Array.Empty<ModConflictInput>();
        var cancellation = new CancellationTokenSource();
        _conflictAnalysisCancellation = cancellation;
        _ = ApplyConflictAnalysisAsync(profile, plan, inputs, cancellation.Token);
    }

    private async Task ApplyConflictAnalysisAsync(
        ModProfile profile,
        FileLayerPlan? plan,
        IReadOnlyList<ModConflictInput> inputs,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = plan is not null
                ? await _modConflictAnalyzer.AnalyzeAsync(
                    plan,
                    profile.ExecutableRelativePath,
                    profile.ExecutableSourcePath,
                    cancellationToken)
                : await _modConflictAnalyzer.AnalyzeAsync(
                    inputs,
                    profile.ExecutableRelativePath,
                    profile.ExecutableSourcePath,
                    cancellationToken);
            await InvokeOnUiAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested || SelectedProfile != profile)
                {
                    return;
                }

                foreach (var mod in profile.Mods)
                {
                    ApplyConflictState(mod, result.GetValueOrDefault(mod.Id), profile.ExecutableRelativePath);
                }
            });
        }
        catch (OperationCanceledException)
        {
            // A newer profile or mod state superseded this analysis.
        }
    }

    private static FileLayerPlan? TryCreateConflictAnalysisPlan(ModProfile profile)
    {
        if (profile.IsStandalone || string.IsNullOrWhiteSpace(profile.GameInstallPath))
        {
            return null;
        }

        var workspaceRoot = string.IsNullOrWhiteSpace(profile.WorkspacePath)
            ? Path.Combine(Path.GetTempPath(), "StalkerModLauncher", "analysis", profile.Id)
            : profile.WorkspacePath;
        return FileLayerPlan.CreateLinkedWorkspace(profile.GameInstallPath, profile, workspaceRoot);
    }

    private static void ApplyConflictState(ModEntry mod, ModConflictState? state, string executableRelativePath)
    {
        mod.HasOverlapsAbove = state?.HasOverlapsAbove ?? false;
        mod.OverwrittenFileCount = state?.OverwrittenFileCount ?? 0;
        mod.OverwrittenModCount = state?.OverwrittenModNames.Count ?? 0;
        mod.ProvidesLaunchExecutable = state?.ProvidesLaunchExecutable ?? false;
        mod.OverwrittenConfigurationCount = state?.OverwrittenConfigurationCount ?? 0;
        mod.OverwrittenBinaryCount = state?.OverwrittenBinaryCount ?? 0;
        mod.OverlayDetails = state is { OverwrittenModNames.Count: > 0 }
            ? $"Заменяет файлы из: {string.Join(", ", state.OverwrittenModNames)}.{Environment.NewLine}" +
              $"Конфигурации и скрипты: {state.OverwrittenConfigurationCount:N0}; бинарные файлы: {state.OverwrittenBinaryCount:N0}."
            : state?.ProvidesLaunchExecutable == true
                ? $"Итоговый запускаемый файл: {executableRelativePath}"
                : string.Empty;
    }
}
