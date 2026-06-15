using Microsoft.Win32;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed partial class MainViewModel
{
    public void MoveProfileToInsertionIndex(ModProfile profile, int insertionIndex)
    {
        if (!_profileManager.MoveToInsertionIndex(Profiles, profile, insertionIndex))
        {
            return;
        }

        SelectedProfile = profile;
        _autoSave.Schedule();
    }

    private void ExportProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Экспорт профиля",
            Filter = "Profile files (*.stalkerprofile)|*.stalkerprofile|JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"{SelectedProfile.Name}.stalkerprofile"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _profileTransferService.Export(dialog.FileName, SelectedProfile);
            Log($"Profile exported: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            Log($"Export failed: {ex.Message}");
            _dialogService.ShowError("Ошибка экспорта", ex.Message);
        }
    }

    private void ImportProfile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Импорт профиля",
            Filter = "Profile files (*.stalkerprofile)|*.stalkerprofile|JSON files (*.json)|*.json|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var profile = _profileTransferService.Import(dialog.FileName);
            _profileManager.PrepareImported(Profiles, profile);

            Profiles.Add(profile);
            SelectedProfile = profile;
            _ = SaveAsync();
            Log($"Profile imported: {profile.Name}");
        }
        catch (Exception ex)
        {
            Log($"Import failed: {ex.Message}");
            _dialogService.ShowError("Ошибка импорта", ex.Message);
        }
    }

    private void ChooseGameFolder()
    {
        var selected = _dialogService.PickFolder("Choose S.T.A.L.K.E.R. GOG folder", GameInstallPath);
        if (selected is null)
        {
            return;
        }

        _lastBrowsedGamePath = selected;
        GameInstallPath = selected;
        RefreshValidation();
        Log($"Game folder selected: {selected}");
    }

    private void NewProfile()
    {
        ProfileCreationRequested?.Invoke(this, EventArgs.Empty);
    }

    public void AddCreatedProfile(ModProfile profile)
    {
        profile.Name = ProfileManager.GetUniqueName(Profiles, profile.Name);
        Profiles.Add(profile);
        SelectedProfile = profile;
        Log($"Profile created: {profile.Name}");
        _ = SaveAsync();
    }

    private void DuplicateProfile()
    {
        DuplicateProfile(SelectedProfile);
    }

    private void DuplicateProfile(ModProfile? sourceProfile)
    {
        if (sourceProfile is null)
        {
            return;
        }

        var profile = _profileManager.Duplicate(Profiles, sourceProfile);
        Profiles.Add(profile);
        SelectedProfile = profile;
        Log($"Profile duplicated: {profile.Name}");
        _ = SaveAsync();
    }

    private void DeleteProfile()
    {
        DeleteProfile(SelectedProfile);
    }

    private void DeleteProfile(ModProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        var deleteMessage = profile.IsStandalone
            ? $"Удалить профиль '{profile.Name}'? Файлы мода останутся нетронутыми."
            : $"Удалить профиль '{profile.Name}' вместе с его рабочей папкой, сохранениями и логами?";
        if (!_dialogService.Confirm("Удалить профиль", deleteMessage))
        {
            return;
        }

        try
        {
            SelectedProfile = _profileManager.Delete(Profiles, profile);
            Log(profile.IsStandalone ? $"Profile deleted: {profile.Name}" : $"Profile and workspace deleted: {profile.Name}");
            _ = SaveAsync();
        }
        catch (Exception ex)
        {
            Log($"Profile delete failed: {ex.Message}");
            _dialogService.ShowError("Не удалось удалить профиль", ex.Message);
        }
    }

    private void OpenProfileFolder()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            var path = _profileManager.GetProfileFolderPath(SelectedProfile)
                ?? throw new DirectoryNotFoundException("Папка включенного автономного мода не найдена.");

            Directory.CreateDirectory(path);
            _dialogService.OpenFolder(path);
        }
        catch (Exception ex)
        {
            Log($"Could not open profile folder: {ex.Message}");
        }
    }

    private string? TryGetWorkspaceRelativePath(string selectedPath)
    {
        if (SelectedProfile is null)
        {
            return null;
        }

        var roots = new List<string>();
        var gamePath = SelectedProfile.GameInstallPath;

        if (Directory.Exists(gamePath))
        {
            roots.Add(gamePath);
        }

        roots.AddRange(SelectedProfile.Mods.Where(mod => Directory.Exists(mod.SourcePath)).Select(mod => mod.SourcePath));

        if (!string.IsNullOrWhiteSpace(SelectedProfile.WorkspacePath))
        {
            var currentWorkspace = Path.Combine(SelectedProfile.WorkspacePath, "current");
            if (Directory.Exists(currentWorkspace))
            {
                roots.Add(currentWorkspace);
            }
        }

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(root => root.Length))
        {
            var relative = Path.GetRelativePath(root, selectedPath);
            if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
            {
                return relative;
            }
        }

        return null;
    }

    public ProfileSettingsViewModel? CreateProfileSettingsViewModel()
    {
        if (SelectedProfile is null)
        {
            return null;
        }

        return new ProfileSettingsViewModel(
            SelectedProfile,
            _dialogService,
            () => SaveAsync(),
            TryGetWorkspaceRelativePath,
            name => Profiles.Any(p => p != SelectedProfile && p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)));
    }
}
