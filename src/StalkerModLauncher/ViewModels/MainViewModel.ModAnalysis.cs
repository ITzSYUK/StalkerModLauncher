using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed partial class MainViewModel
{
    private void RecalculateLockedMods()
    {
        _conflictAnalysisCancellation?.Cancel();
        _conflictAnalysisCancellation?.Dispose();
        _conflictAnalysisCancellation = null;

        var profile = SelectedProfile;
        if (profile is null)
        {
            return;
        }

        var inputs = profile.Mods.Select(ModConflictInput.FromMod).ToArray();
        var cancellation = new CancellationTokenSource();
        _conflictAnalysisCancellation = cancellation;
        _ = ApplyConflictAnalysisAsync(profile, inputs, cancellation.Token);
    }

    private async Task ApplyConflictAnalysisAsync(
        ModProfile profile,
        IReadOnlyList<ModConflictInput> inputs,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _modConflictAnalyzer.AnalyzeAsync(
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

    private static void ApplyConflictState(ModEntry mod, ModConflictState? state, string executableRelativePath)
    {
        mod.IsLocked = state?.IsLocked ?? false;
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
