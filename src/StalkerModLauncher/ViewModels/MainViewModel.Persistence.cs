using System.Collections.Specialized;
using System.ComponentModel;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.ViewModels;

public sealed partial class MainViewModel
{
    private async Task LoadAsync()
    {
        try
        {
            var settings = await _settingsStore.LoadAsync();
            _lastBrowsedGamePath = settings.LastBrowsedGamePath;
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
        RecalculateLockedMods();
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
        if (e.PropertyName is nameof(ModEntry.IsLocked)
            or nameof(ModEntry.HasOverlapsAbove)
            or nameof(ModEntry.OverwrittenFileCount)
            or nameof(ModEntry.OverwrittenModCount)
            or nameof(ModEntry.ProvidesLaunchExecutable)
            or nameof(ModEntry.OverlayDetails)
            or nameof(ModEntry.OverlaySummary)
            or nameof(ModEntry.HasOverlayInfo))
        {
            return;
        }

        RefreshValidation();
        _autoSave.Schedule();
        if (e.PropertyName == nameof(ModEntry.IsEnabled)) RecalculateLockedMods();
    }

    public async Task CleanupAsync()
    {
        await SaveAsync();
        _autoSave.Dispose();
        _conflictAnalysisCancellation?.Cancel();
        _conflictAnalysisCancellation?.Dispose();
        _launchCoordinator.Dispose();
    }
}
