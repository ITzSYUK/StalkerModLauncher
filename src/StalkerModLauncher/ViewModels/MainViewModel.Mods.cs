using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed partial class MainViewModel
{
    public void AddDroppedMods(IEnumerable<string> paths)
    {
        if (!CanEditSelectedProfile || SelectedProfile is null)
        {
            return;
        }

        foreach (var path in paths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (SelectedProfile.IsStandalone && SelectedProfile.Mods.Count >= 1)
            {
                break;
            }

            if (SelectedProfile.Mods.Any(mod => AreSamePaths(mod.SourcePath, path)))
            {
                continue;
            }

            SelectedMod = _modListEditor.Add(SelectedProfile, path);
        }

        RefreshValidation();
        _ = SaveAsync();
    }

    private static bool AreSamePaths(string left, string right)
    {
        try
        {
            return FileSystemSafety.IsSameDirectory(left, right);
        }
        catch
        {
            return false;
        }
    }

    public void MoveMod(ModEntry source, ModEntry target)
    {
        if (!CanEditSelectedProfile ||
            SelectedProfile is null ||
            !_modListEditor.Move(SelectedProfile, source, target))
        {
            return;
        }

        SelectedMod = source;
        RaiseCommandStates();
    }

    public void MoveModToEnd(ModEntry source)
    {
        if (!CanEditSelectedProfile ||
            SelectedProfile is null ||
            !_modListEditor.MoveToEnd(SelectedProfile, source))
        {
            return;
        }

        SelectedMod = source;
        RaiseCommandStates();
    }

    public void MoveModToInsertionIndex(ModEntry source, int insertionIndex)
    {
        MoveModsToInsertionIndex([source], insertionIndex);
    }

    public void MoveModsToInsertionIndex(IReadOnlyList<ModEntry> sources, int insertionIndex)
    {
        if (!CanEditSelectedProfile ||
            SelectedProfile is null ||
            sources.Count == 0 ||
            !_modListEditor.MoveManyToInsertionIndex(SelectedProfile, sources, insertionIndex))
        {
            return;
        }

        SelectedMod = sources[^1];
        RecalculateModOverlayInfo();
        _autoSave.Schedule();
        RaiseCommandStates();
    }

    public void MoveModsToStart(IReadOnlyList<ModEntry> sources)
    {
        MoveModsToBoundary(sources, moveToEnd: false);
    }

    public void MoveModsToEnd(IReadOnlyList<ModEntry> sources)
    {
        MoveModsToBoundary(sources, moveToEnd: true);
    }

    private void MoveModsToBoundary(IReadOnlyList<ModEntry> sources, bool moveToEnd)
    {
        if (!CanEditSelectedProfile || SelectedProfile is null || sources.Count == 0)
        {
            return;
        }

        var moved = moveToEnd
            ? _modListEditor.MoveManyToEnd(SelectedProfile, sources)
            : _modListEditor.MoveManyToStart(SelectedProfile, sources);
        if (!moved)
        {
            return;
        }

        SelectedMod = sources[^1];
        RecalculateModOverlayInfo();
        _autoSave.Schedule();
        RaiseCommandStates();
    }

    private async Task ScanForModsAsync()
    {
        if (!CanEditSelectedProfile || SelectedProfile is null)
        {
            return;
        }

        var folder = _dialogService.PickFolder("Выберите папку для поиска модов");
        if (folder is null)
        {
            return;
        }

        try
        {
            IsBuilding = true;
            BuildProgressText = "Сканирование модов...";
            RaiseCommandStates();
            var discovered = await _modScannerService.ScanFolderAsync(folder);

            if (discovered.Count == 0)
            {
                Log("No mods found in selected folder.");
                _dialogService.ShowError("Не найдено", "В выбранной папке не обнаружено модов.");
                return;
            }

            var window = new Views.ScanResultsWindow();
            foreach (var mod in discovered)
            {
                window.Mods.Add(SelectableMod.FromDiscovered(mod));
            }

            if (window.ShowDialog() == true)
            {
                var selected = window.GetSelectedMods();
                Log($"Scan results: {window.Mods.Count} total, {selected.Count} selected.");
                if (selected.Count == 0)
                {
                    Log("No mods selected.");
                    return;
                }

                var existingPaths = new HashSet<string>(SelectedProfile.Mods.Select(m => m.SourcePath), StringComparer.OrdinalIgnoreCase);
                var added = 0;

                foreach (var mod in selected)
                {
                    if (existingPaths.Contains(mod.Path))
                    {
                        continue;
                    }

                    _modListEditor.Add(SelectedProfile, mod.Path, mod.Name);
                    existingPaths.Add(mod.Path);
                    added++;
                }

                RefreshValidation();
                _ = SaveAsync();
                Log($"Added {added} mod(s) from scan.");
            }
        }
        catch (Exception ex)
        {
            Log($"Scan failed: {ex.Message}");
            _dialogService.ShowError("Ошибка сканирования", ex.Message);
        }
        finally
        {
            IsBuilding = false;
            BuildProgressText = string.Empty;
            RaiseCommandStates();
        }
    }

    private void AddMod()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var selected = _dialogService.PickFolder("Choose mod folder");
        if (selected is null)
        {
            return;
        }

        SelectedMod = _modListEditor.Add(SelectedProfile, selected);
        RefreshValidation();
        Log($"Mod added: {selected}");
        _ = SaveAsync();
    }

    private void BrowseExecutable()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var initialPath = Directory.Exists(SelectedMod?.SourcePath) ? SelectedMod.SourcePath
            : !string.IsNullOrWhiteSpace(SelectedProfile.GameInstallPath) ? SelectedProfile.GameInstallPath
            : _lastBrowsedGamePath;
        var selected = _dialogService.PickExecutable("Choose launch executable", initialPath);
        if (selected is null)
        {
            return;
        }

        var selection = TryGetExecutableSelection(selected);
        if (selection is null)
        {
            _dialogService.ShowError(
                "Executable is outside profile sources",
                "Choose an executable from the game folder, an enabled mod folder, or the generated profile workspace.");
            return;
        }

        SelectedProfile.ExecutableRelativePath = selection.RelativePath;
        SelectedProfile.ExecutableSourcePath = !SelectedProfile.IsStandalone && selection.PinsSource
            ? selection.SourceRootPath
            : string.Empty;
        Log(!SelectedProfile.IsStandalone && selection.PinsSource
            ? $"Launch executable selected: {selection.RelativePath} from {selection.SourceName}"
            : $"Launch executable selected: {selection.RelativePath}");
        RefreshValidation();
        _ = SaveAsync();
    }

    private void AutoDetectStandaloneExecutable()
    {
        var modRoot = SelectedProfile?.Mods
            .FirstOrDefault(m => m.IsEnabled && Directory.Exists(m.SourcePath))
            ?.SourcePath;

        if (modRoot is null)
        {
            return;
        }

        var currentExe = SelectedProfile!.ExecutableRelativePath;
        if (!string.IsNullOrWhiteSpace(currentExe) && File.Exists(Path.Combine(modRoot, currentExe)))
        {
            return;
        }

        var found = LaunchExecutableDetector.DetectBest(
            [new LaunchExecutableSearchRoot(modRoot, "автономный мод", 1)],
            currentExe);

        if (found is null)
        {
            return;
        }

        SelectedProfile.ExecutableRelativePath = found.RelativePath;
        SelectedProfile.ExecutableSourcePath = string.Empty;
        Log($"Standalone executable auto-detected: {found.RelativePath}");
    }

    public void RemoveMods(IReadOnlyList<ModEntry> mods)
    {
        if (!CanEditSelectedProfile || SelectedProfile is null || mods.Count == 0)
        {
            return;
        }

        var removed = _modListEditor.Remove(SelectedProfile, mods);
        RefreshValidation();
        Log($"Removed {removed} mod(s).");
        _ = SaveAsync();
    }

    private void RemoveMod()
    {
        if (!CanEditSelectedProfile || SelectedProfile is null || SelectedMod is null)
        {
            return;
        }

        var removed = SelectedMod;
        _modListEditor.Remove(SelectedProfile, [removed]);
        RefreshValidation();
        Log($"Mod removed: {removed.Name}");
        _ = SaveAsync();
    }

    private void MoveSelectedMod(int direction)
    {
        if (!CanEditSelectedProfile || SelectedProfile is null || SelectedMod is null)
        {
            return;
        }

        if (!_modListEditor.MoveByOffset(SelectedProfile, SelectedMod, direction))
        {
            return;
        }

        RaiseCommandStates();
    }

    private bool CanMoveSelectedMod(int direction)
    {
        return CanEditSelectedProfile &&
               SelectedProfile is not null &&
               SelectedMod is not null &&
               _modListEditor.CanMoveByOffset(SelectedProfile, SelectedMod, direction);
    }

    private void RemoveInlineMod(ModEntry? mod)
    {
        if (mod is not null)
        {
            RemoveMods([mod]);
        }
    }

    private void MoveInlineMod(ModEntry? mod, int direction)
    {
        if (!CanMoveInlineMod(mod, direction) ||
            SelectedProfile is null ||
            mod is null ||
            !_modListEditor.MoveByOffset(SelectedProfile, mod, direction))
        {
            return;
        }

        SelectedMod = mod;
        RaiseCommandStates();
    }

    private bool CanMoveInlineMod(ModEntry? mod, int direction)
    {
        return CanEditSelectedProfile &&
               SelectedProfile is not null &&
               mod is not null &&
               _modListEditor.CanMoveByOffset(SelectedProfile, mod, direction);
    }

    private void OpenInlineModFolder(ModEntry? mod)
    {
        if (mod is null)
        {
            return;
        }

        try
        {
            _dialogService.OpenFolder(mod.SourcePath);
        }
        catch (Exception ex)
        {
            Log($"Could not open mod folder: {ex.Message}");
        }
    }

    private bool CanAddMod()
    {
        if (!CanEditSelectedProfile || SelectedProfile is null)
        {
            return false;
        }

        return !SelectedProfile.IsStandalone || SelectedProfile.Mods.Count < 1;
    }

    private void OpenSelectedModFolder()
    {
        if (SelectedMod is null)
        {
            return;
        }

        try
        {
            _dialogService.OpenFolder(SelectedMod.SourcePath);
        }
        catch (Exception ex)
        {
            Log($"Could not open mod folder: {ex.Message}");
        }
    }
}
