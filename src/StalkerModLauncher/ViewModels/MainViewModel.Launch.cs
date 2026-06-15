using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed partial class MainViewModel
{
    private bool CanLaunch()
    {
        return !IsBuilding && IsGameValid && SelectedProfile is { IsEnabled: true, IsRunning: false };
    }

    private async Task LaunchAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            RefreshValidation();
            if (!CanLaunch())
            {
                Log("Launch blocked: profile is not ready.");
                return;
            }

            IsBuilding = true;
            BuildProgressText = "Проверка профиля перед запуском...";
            RaiseCommandStates();

            var preflight = await _launchPreflightService.AnalyzeAsync(SelectedProfile);
            foreach (var warning in preflight.Checks.Where(check => check.Status == ProfileHealthStatus.Warning))
            {
                Log($"Preflight warning: {warning.Title}: {warning.Details}");
            }

            if (!preflight.CanLaunch)
            {
                throw new InvalidOperationException(preflight.ToErrorMessage());
            }

            BuildProgressText = "Building workspace...";
            var progress = new Progress<string>(message =>
            {
                Log(message);
                BuildProgressText = message;
            });

            var session = await _launchCoordinator.StartAsync(SelectedProfile.GameInstallPath, SelectedProfile, progress);
            await SaveAsync();
            Log($"Game process started. PID: {session.ProcessId}");
            SelectedProfile.IsRunning = true;
            RaiseCommandStates();
            _ = CompleteGameSessionAsync(session.Completion, SelectedProfile);
        }
        catch (Exception ex)
        {
            Log($"Launch failed: {ex.Message}");
            _dialogService.ShowError("Не удалось запустить профиль", ex.Message);
        }
        finally
        {
            IsBuilding = false;
            BuildProgressText = string.Empty;
            RaiseCommandStates();
        }
    }

    private async Task CompleteGameSessionAsync(Task<GameSessionResult> sessionTask, ModProfile profile)
    {
        try
        {
            var result = await sessionTask;
            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                profile.IsRunning = false;
                RaiseCommandStates();
                LogGameExitDiagnostics(profile, result);
            });

            if (!result.ShouldRecord)
            {
                return;
            }

            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                profile.TotalPlaytimeSeconds += result.Duration.TotalSeconds;
                profile.LastPlayedAt = DateTime.Now;
                Log($"Playtime recorded: {result.Duration:g} (total: {profile.PlaytimeDisplay})");
            });
            await SaveAsync();
        }
        catch (Exception ex)
        {
            Log($"Playtime tracking failed: {ex.Message}");
            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                profile.IsRunning = false;
                RaiseCommandStates();
            });
        }
    }

    private void LogGameExitDiagnostics(ModProfile profile, GameSessionResult result)
    {
        var diagnostics = _gameExitDiagnosticsService.Analyze(profile, result);
        if (diagnostics.IsQuickExit)
        {
            var exitCode = diagnostics.ExitCode.HasValue ? $" Exit code: {diagnostics.ExitCode}." : string.Empty;
            Log($"Game exited shortly after launch ({result.Duration:g}).{exitCode}");
        }
        else if (diagnostics.ExitCode is not null and not 0)
        {
            Log($"Game process exited with code {diagnostics.ExitCode}.");
        }

        if (diagnostics.IsSuspiciousExit && diagnostics.LatestLogPath is not null)
        {
            Log($"Latest game log: {diagnostics.LatestLogPath}");
        }

        if (diagnostics.LatestCrashDumpPath is not null)
        {
            Log($"Crash dump detected: {diagnostics.LatestCrashDumpPath}");
        }
    }
}
