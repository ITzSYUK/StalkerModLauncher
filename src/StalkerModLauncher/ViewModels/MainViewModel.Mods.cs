using System.Diagnostics;
using System.Media;
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

            SelectedMod = ModListEditor.Add(SelectedProfile, path);
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
            !ModListEditor.Move(SelectedProfile, source, target))
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
            !ModListEditor.MoveToEnd(SelectedProfile, source))
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
            !ModListEditor.MoveManyToInsertionIndex(SelectedProfile, sources, insertionIndex))
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
            ? ModListEditor.MoveManyToEnd(SelectedProfile, sources)
            : ModListEditor.MoveManyToStart(SelectedProfile, sources);
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

        var folder = DialogService.PickFolder("Выберите папку для поиска модов");
        if (folder is null)
        {
            return;
        }

        try
        {
            IsBuilding = true;
            BuildProgressText = "Сканирование модов...";
            RaiseCommandStates();
            var discovered = await ModScannerService.ScanFolderAsync(folder);

            if (discovered.Count == 0)
            {
                Log("No mods found in selected folder.");
                _dialogService.ShowError("Не найдено", "В выбранной папке не обнаружено модов.");
                return;
            }

            var selectableMods = discovered.Select(SelectableMod.FromDiscovered).ToList();
            IReadOnlyList<SelectableMod>? selected;

            if (IsPdaInterfaceEnabled && ModScanSelectionRequested is not null)
            {
                var request = new ModScanSelectionEventArgs(selectableMods);
                ModScanSelectionRequested.Invoke(this, request);
                selected = await request.Completion;
            }
            else
            {
                var window = new Views.ScanResultsWindow();
                foreach (var mod in selectableMods)
                {
                    window.Mods.Add(mod);
                }

                selected = window.ShowDialog() == true
                    ? window.GetSelectedMods()
                    : null;
            }

            if (selected is not null)
            {
                Log($"Scan results: {selectableMods.Count} total, {selected.Count} selected.");
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

                    ModListEditor.Add(SelectedProfile, mod.Path, mod.Name);
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
            Log($"Scan failed: {ex.Message}", LauncherLogLevel.ErrorsOnly);
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

        var selected = DialogService.PickFolder("Choose mod folder");
        if (selected is null)
        {
            return;
        }

        SelectedMod = ModListEditor.Add(SelectedProfile, selected);
        RefreshValidation();
        Log($"Mod added: {selected}");
        _ = SaveAsync();
    }

    private async Task InstallModArchiveAsync()
    {
        if (SelectedProfile is not { } profile || !CanInstallModArchive())
        {
            return;
        }

        var archivePath = DialogService.PickFile(
            "Выберите архив мода",
            "Архивы модов (*.zip;*.7z;*.rar)|*.zip;*.7z;*.rar|Все файлы (*.*)|*.*");
        if (archivePath is null)
        {
            return;
        }

        try
        {
            IsBuilding = true;
            IsInstallingModArchive = true;
            IsModArchiveInstallCompleted = false;
            IsModArchiveInstallProgressIndeterminate = true;
            ModArchiveInstallProgress = 0;
            var archiveFileName = Path.GetFileName(archivePath);
            ModArchiveInstallProgressText = $"Подготовка {archiveFileName}...";
            BuildProgressText = ModArchiveInstallProgressText;
            RaiseCommandStates();

            var installRoot = string.IsNullOrWhiteSpace(profile.ModInstallPath)
                ? _paths.GetDefaultModInstallPath(profile.GameInstallPath)
                : profile.ModInstallPath;
            profile.ModInstallPath = ValidateModInstallPath(profile, installRoot);

            var destination = ModArchiveInstaller.PlanInstall(archivePath, profile.ModInstallPath);
            if (destination.RequiresConfirmation && !await ConfirmModArchiveInstallDestinationAsync(destination))
            {
                Log($"Mod archive installation cancelled because folder already exists: {destination.PackagePath}");
                return;
            }

            IsInstallingModArchive = true;
            IsModArchiveInstallProgressIndeterminate = true;

            var progressTimer = Stopwatch.StartNew();
            var progress = new Progress<ModArchiveInstallProgress>(value =>
                UpdateModArchiveInstallProgress(value, archiveFileName, progressTimer.Elapsed));
            var result = await ModArchiveInstaller.InstallAsync(
                archivePath,
                profile.ModInstallPath,
                destination.PackageDirectoryName,
                progress);
            var installedMod = ModListEditor.Add(profile, result.ModPath, result.ModName);

            if (ReferenceEquals(SelectedProfile, profile))
            {
                SelectedMod = installedMod;
                if (profile.IsStandalone)
                {
                    AutoDetectStandaloneExecutable();
                }

                RefreshValidation();
            }

            await SaveAsync();
            Log($"Mod archive installed: {archivePath} -> {result.ModPath}");

            var databaseNote = result.DatabaseArchivesRelocated
                ? Environment.NewLine + "Архивы .db* помещены в db\\mods."
                : string.Empty;
            var details = $"Файлов: {result.FileCount:N0}{databaseNote}";
            SystemSounds.Asterisk.Play();
            IsInstallingModArchive = false;
            InstalledModArchiveName = result.ModName;
            InstalledModArchivePath = result.ModPath;
            InstalledModArchiveDetails = details;
            IsModArchiveInstallCompleted = true;
        }
        catch (Exception ex)
        {
            Log($"Mod archive installation failed: {ex.Message}", LauncherLogLevel.ErrorsOnly);
            _dialogService.ShowError("Не удалось установить архив мода", ex.Message);
        }
        finally
        {
            IsBuilding = false;
            IsInstallingModArchive = false;
            IsModArchiveInstallProgressIndeterminate = false;
            IsModArchiveInstallDestinationConflict = false;
            _modArchiveInstallDestinationChoice = null;
            ModArchiveInstallProgress = 0;
            ModArchiveInstallProgressText = string.Empty;
            BuildProgressText = string.Empty;
            RaiseCommandStates();
        }
    }

    private void UpdateModArchiveInstallProgress(
        ModArchiveInstallProgress progress,
        string archiveFileName,
        TimeSpan elapsed)
    {
        switch (progress.Stage)
        {
            case ModArchiveInstallStage.Inspecting:
                IsModArchiveInstallProgressIndeterminate = true;
                ModArchiveInstallProgress = 0;
                ModArchiveInstallProgressText = $"Анализ {archiveFileName}...";
                break;

            case ModArchiveInstallStage.Extracting when progress.TotalBytes is > 0:
                IsModArchiveInstallProgressIndeterminate = false;
                ModArchiveInstallProgress = Math.Clamp(
                    progress.ExtractedBytes * 100d / progress.TotalBytes.Value,
                    0,
                    100);
                var remainingText = FormatArchiveInstallRemainingTime(progress, elapsed);
                ModArchiveInstallProgressText =
                    $"Распаковка {archiveFileName}: {ModArchiveInstallProgress:0}% · " +
                    $"{WorkspaceStatus.FormatSize(progress.ExtractedBytes)} / " +
                    $"{WorkspaceStatus.FormatSize(progress.TotalBytes.Value)} · {remainingText}";
                break;

            case ModArchiveInstallStage.Extracting:
                IsModArchiveInstallProgressIndeterminate = true;
                ModArchiveInstallProgressText =
                    $"Распаковка {archiveFileName}: {WorkspaceStatus.FormatSize(progress.ExtractedBytes)}";
                break;

            case ModArchiveInstallStage.Finalizing:
                IsModArchiveInstallProgressIndeterminate = false;
                ModArchiveInstallProgress = 100;
                ModArchiveInstallProgressText = $"Завершение установки {archiveFileName}...";
                break;
        }

        BuildProgressText = ModArchiveInstallProgressText;
    }

    private async Task<bool> ConfirmModArchiveInstallDestinationAsync(ModArchiveInstallDestination destination)
    {
        IsInstallingModArchive = false;
        IsModArchiveInstallProgressIndeterminate = false;
        ModArchiveInstallDestinationConflictText =
            $"Папка мода «{destination.ModName}» уже существует. Новая распаковка будет помещена в:";
        ModArchiveInstallDestinationConflictAction = destination.PackagePath;
        IsModArchiveInstallDestinationConflict = true;
        var choice = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _modArchiveInstallDestinationChoice = choice;
        try
        {
            return await choice.Task;
        }
        finally
        {
            IsModArchiveInstallDestinationConflict = false;
            _modArchiveInstallDestinationChoice = null;
        }
    }

    private void ResolveModArchiveInstallDestinationChoice(bool continueInstallation)
    {
        _modArchiveInstallDestinationChoice?.TrySetResult(continueInstallation);
    }

    private static string FormatArchiveInstallRemainingTime(
        ModArchiveInstallProgress progress,
        TimeSpan elapsed)
    {
        if (progress.TotalBytes is not > 0 ||
            progress.ExtractedBytes <= 0 ||
            progress.ExtractedBytes >= progress.TotalBytes.Value ||
            elapsed.TotalSeconds < 0.8)
        {
            return "оценка времени...";
        }

        var remainingSeconds = elapsed.TotalSeconds *
                               (progress.TotalBytes.Value - progress.ExtractedBytes) /
                               progress.ExtractedBytes;
        if (!double.IsFinite(remainingSeconds) || remainingSeconds < 0)
        {
            return "оценка времени...";
        }

        var remaining = TimeSpan.FromSeconds(Math.Min(remainingSeconds, TimeSpan.FromDays(99).TotalSeconds));
        if (remaining.TotalHours >= 1)
        {
            return $"осталось ~{(int)remaining.TotalHours} ч {remaining.Minutes} мин";
        }

        if (remaining.TotalMinutes >= 1)
        {
            return $"осталось ~{(int)remaining.TotalMinutes} мин {remaining.Seconds} с";
        }

        return $"осталось ~{Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))} с";
    }

    private bool CanInstallModArchive() => !IsBuilding && CanAddMod();

    private static string ValidateModInstallPath(ModProfile profile, string installRoot)
    {
        var fullInstallRoot = Path.GetFullPath(installRoot);
        if (!string.IsNullOrWhiteSpace(profile.GameInstallPath) &&
            FileSystemSafety.IsDirectoryInside(fullInstallRoot, profile.GameInstallPath))
        {
            throw new InvalidOperationException("Папка установленных модов не должна находиться внутри папки игры.");
        }

        if (!string.IsNullOrWhiteSpace(profile.WorkspacePath) &&
            (FileSystemSafety.IsDirectoryInside(fullInstallRoot, profile.WorkspacePath) ||
             FileSystemSafety.IsDirectoryInside(profile.WorkspacePath, fullInstallRoot)))
        {
            throw new InvalidOperationException("Папка установленных модов не должна пересекаться с workspace профиля.");
        }

        if (profile.Mods.Any(mod =>
                !string.IsNullOrWhiteSpace(mod.SourcePath) &&
                FileSystemSafety.IsDirectoryInside(fullInstallRoot, mod.SourcePath)))
        {
            throw new InvalidOperationException("Папка установленных модов не должна находиться внутри исходной папки другого мода.");
        }

        return fullInstallRoot;
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
        var selected = DialogService.PickExecutable("Choose launch executable", initialPath);
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
            [new LaunchExecutableSearchRoot(modRoot, "автономная сборка", 1, IsBaseGameRoot: true)],
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

        var removed = ModListEditor.Remove(SelectedProfile, mods);
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
        ModListEditor.Remove(SelectedProfile, [removed]);
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

        if (!ModListEditor.MoveByOffset(SelectedProfile, SelectedMod, direction))
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
               ModListEditor.CanMoveByOffset(SelectedProfile, SelectedMod, direction);
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
            !ModListEditor.MoveByOffset(SelectedProfile, mod, direction))
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
               ModListEditor.CanMoveByOffset(SelectedProfile, mod, direction);
    }

    private void OpenInlineModFolder(ModEntry? mod)
    {
        if (mod is null)
        {
            return;
        }

        try
        {
            DialogService.OpenFolder(mod.SourcePath);
        }
        catch (Exception ex)
        {
            Log($"Could not open mod folder: {ex.Message}", LauncherLogLevel.ErrorsOnly);
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
            DialogService.OpenFolder(SelectedMod.SourcePath);
        }
        catch (Exception ex)
        {
            Log($"Could not open mod folder: {ex.Message}", LauncherLogLevel.ErrorsOnly);
        }
    }
}
