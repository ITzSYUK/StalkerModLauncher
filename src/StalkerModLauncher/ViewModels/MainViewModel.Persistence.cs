using System.Collections.Specialized;
using System.ComponentModel;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed partial class MainViewModel
{
    private async Task LoadAsync()
    {
        try
        {
            var loadResult = await _settingsStore.LoadWithRecoveryAsync();
            var settings = loadResult.Settings;
            _lastBrowsedGamePath = settings.LastBrowsedGamePath;
            _isPdaInterfaceEnabled = settings.IsPdaInterfaceEnabled;
            OnPropertyChanged(nameof(IsPdaInterfaceEnabled));
            OnPropertyChanged(nameof(GameInstallPath));
            ActivityLog.Load([], settings.IsLogVisible);

            if (!string.IsNullOrWhiteSpace(settings.DiscordClientId))
            {
                _launchCoordinator.ConfigureDiscord(settings.DiscordClientId, Log);
            }

            Profiles.Clear();
            foreach (var profile in settings.Profiles)
            {
                _profileManager.EnsureDefaults(profile);
                Profiles.Add(profile);
            }

            SelectedProfile = Profiles.FirstOrDefault();
            RefreshValidation();
            Log("Settings loaded.");

            if (loadResult.Recovery is not null)
            {
                ReportSettingsRecovery(loadResult.Recovery);
            }
        }
        catch (SettingsPersistenceException ex)
        {
            Log($"Settings load blocked: {ex}");
            _dialogService.ShowError(
                "Настройки недоступны",
                $"{ex.Message}{Environment.NewLine}{Environment.NewLine}" +
                "Файл не изменён. Закройте программу, которая может удерживать его, и перезапустите лаунчер.");
        }
        catch (Exception ex)
        {
            Log($"Settings load failed: {ex.Message}");
        }
    }

    private async Task SaveAsync()
    {
        _autoSave.Cancel();
        try
        {
            foreach (var profile in Profiles)
            {
                _modListEditor.Renumber(profile);
            }

            await _settingsStore.UpdateAsync(existing => new AppSettings
            {
                LastBrowsedGamePath = _lastBrowsedGamePath,
                Profiles = Profiles.ToList(),
                DontShowAboutOnStartup = existing.DontShowAboutOnStartup,
                IsLogVisible = ActivityLog.IsVisible,
                IsPdaInterfaceEnabled = IsPdaInterfaceEnabled,
                DiscordClientId = existing.DiscordClientId
            });
            Log("Settings saved.");
        }
        catch (Exception ex)
        {
            Log($"Settings save failed: {ex.Message}");
        }
    }

    public async Task SaveAboutPreferenceAsync(bool dontShowAgain)
    {
        await _settingsStore.UpdateAsync(settings =>
        {
            settings.DontShowAboutOnStartup = dontShowAgain;
            return settings;
        });
    }

    private void ProfilesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasProfiles));

        if (e.NewItems is not null)
        {
            foreach (ModProfile profile in e.NewItems)
            {
                profile.PropertyChanged += ProfileOnPropertyChanged;
                profile.Mods.CollectionChanged += ModsOnCollectionChanged;
                foreach (var mod in profile.Mods) mod.PropertyChanged += ModOnPropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (ModProfile profile in e.OldItems)
            {
                profile.PropertyChanged -= ProfileOnPropertyChanged;
                profile.Mods.CollectionChanged -= ModsOnCollectionChanged;
                foreach (var mod in profile.Mods) mod.PropertyChanged -= ModOnPropertyChanged;
            }
        }
    }

    private void ModsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (ModEntry mod in e.NewItems) mod.PropertyChanged += ModOnPropertyChanged;
        }
        if (e.OldItems is not null)
        {
            foreach (ModEntry mod in e.OldItems) mod.PropertyChanged -= ModOnPropertyChanged;
        }

        if (SelectedProfile is not null) _modListEditor.Renumber(SelectedProfile);
        RecalculateModOverlayInfo();
        RefreshValidation();
        _autoSave.Schedule();
    }

    private void ProfileOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModProfile.IsRunning))
        {
            OnPropertyChanged(nameof(CanEditSelectedProfile));
            RaiseCommandStates();
            return;
        }

        RefreshValidation();
        _autoSave.Schedule();
    }

    private void ModOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ModEntry.HasOverlapsAbove)
            or nameof(ModEntry.OverwrittenFileCount)
            or nameof(ModEntry.OverwrittenModCount)
            or nameof(ModEntry.ProvidesLaunchExecutable)
            or nameof(ModEntry.OverlayDetails)
            or nameof(ModEntry.OverlaySummary)
            or nameof(ModEntry.HasOverlayInfo))
        {
            return;
        }

        if (e.PropertyName == nameof(ModEntry.IsEnabled) && sender is ModEntry changedMod)
        {
            RefreshAutomaticExecutableSelection(changedMod);
            RecalculateModOverlayInfo();
        }

        RefreshValidation();
        _autoSave.Schedule();
    }

    private void ReportSettingsRecovery(SettingsRecoveryInfo recovery)
    {
        foreach (var file in recovery.Files)
        {
            Log($"Settings recovery: {file.OriginalPath} -> {file.RecoveryPath}. {file.Error}");
        }

        var message = recovery.Mode == SettingsRecoveryMode.Backup
            ? recovery.Files.Count > 0
                ? "Основной файл настроек повреждён. Настройки восстановлены из резервной копии."
                : "Основной файл настроек отсутствовал. Настройки восстановлены из резервной копии."
            : "Файлы настроек повреждены и не читаются. Создана новая конфигурация; профили автоматически восстановить не удалось.";

        Log(message);
        var preservedFiles = recovery.Files.Count == 0
            ? string.Empty
            : $"{Environment.NewLine}{Environment.NewLine}Повреждённые файлы сохранены:{Environment.NewLine}" +
              string.Join(Environment.NewLine, recovery.Files.Select(file => file.RecoveryPath));
        _dialogService.ShowInfo(
            "Восстановление настроек",
            message + preservedFiles);
    }

    private void SettingsStoreOnRecoveryCompleted(object? sender, SettingsRecoveryInfo recovery)
    {
        _ = InvokeOnUiAsync(() => ReportSettingsRecovery(recovery));
    }

    private void RefreshAutomaticExecutableSelection(ModEntry changedMod)
    {
        var profile = Profiles.FirstOrDefault(candidate => candidate.Mods.Contains(changedMod));
        if (profile is null ||
            profile.IsStandalone ||
            !string.IsNullOrWhiteSpace(profile.ExecutableSourcePath))
        {
            return;
        }

        var selection = ProfileExecutableSourceResolver.DetectAutomaticSelection(
            profile,
            includeWorkspace: false);
        if (selection is null ||
            profile.ExecutableRelativePath.Equals(selection.RelativePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        profile.ExecutableRelativePath = selection.RelativePath;
        Log($"Launch executable auto-detected after mod state change: {selection.RelativePath} from {selection.SourceName}");
    }

    public async Task CleanupAsync()
    {
        _settingsStore.RecoveryCompleted -= SettingsStoreOnRecoveryCompleted;
        await SaveAsync();
        _autoSave.Dispose();
        _conflictAnalysisCancellation?.Cancel();
        _conflictAnalysisCancellation?.Dispose();
        _launchCoordinator.Dispose();
    }
}
