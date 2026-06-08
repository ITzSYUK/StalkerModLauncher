using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Win32;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly AppPaths _paths;
    private readonly SettingsStore _settingsStore;
    private readonly LaunchCoordinator _launchCoordinator;
    private readonly DialogService _dialogService;
    private readonly ModConflictAnalyzer _modConflictAnalyzer;
    private readonly ProfileTransferService _profileTransferService;
    private readonly ModScannerService _modScannerService;
    private readonly ModListEditor _modListEditor;
    private readonly ProfileManager _profileManager;
    private readonly GameExitDiagnosticsService _gameExitDiagnosticsService;
    private readonly ProfileReadinessService _profileReadinessService;
    private readonly ApplicationLogService _applicationLogService;
    private readonly DebouncedAsyncAction _autoSave;
    private CancellationTokenSource? _conflictAnalysisCancellation;
    private string _gameInstallPath = string.Empty;
    private ModProfile? _selectedProfile;
    private ModEntry? _selectedMod;
    private string _validationSummary = "Выберите папку с установленной игрой.";
    private string _logText = string.Empty;
    private bool _isGameValid;
    private bool _isBuilding;
    private string _buildProgressText = string.Empty;
    private bool _isLogVisible = true;

    public MainViewModel(
        AppPaths paths,
        SettingsStore settingsStore,
        LaunchCoordinator launchCoordinator,
        DialogService dialogService,
        ModConflictAnalyzer modConflictAnalyzer,
        ProfileTransferService profileTransferService,
        ModScannerService modScannerService,
        ModListEditor modListEditor,
        ProfileManager profileManager,
        GameExitDiagnosticsService gameExitDiagnosticsService,
        ProfileReadinessService profileReadinessService,
        ApplicationLogService applicationLogService)
    {
        _paths = paths;
        _settingsStore = settingsStore;
        _launchCoordinator = launchCoordinator;
        _dialogService = dialogService;
        _modConflictAnalyzer = modConflictAnalyzer;
        _profileTransferService = profileTransferService;
        _modScannerService = modScannerService;
        _modListEditor = modListEditor;
        _profileManager = profileManager;
        _gameExitDiagnosticsService = gameExitDiagnosticsService;
        _profileReadinessService = profileReadinessService;
        _applicationLogService = applicationLogService;
        _autoSave = new DebouncedAsyncAction(SaveAsync, TimeSpan.FromMilliseconds(500));

        Profiles.CollectionChanged += ProfilesOnCollectionChanged;

        ChooseGameFolderCommand = new RelayCommand(ChooseGameFolder);
        NewProfileCommand = new RelayCommand(NewProfile);
        DuplicateProfileCommand = new RelayCommand(DuplicateProfile, () => SelectedProfile is not null);
        DeleteProfileCommand = new RelayCommand(DeleteProfile, () => SelectedProfile is not null);
        BrowseExecutableCommand = new RelayCommand(BrowseExecutable, () => SelectedProfile is not null);
        AddModCommand = new RelayCommand(AddMod, CanAddMod);
        RemoveModCommand = new RelayCommand(RemoveMod, () => SelectedProfile is not null && SelectedMod is not null);
        MoveModUpCommand = new RelayCommand(() => MoveSelectedMod(-1), () => CanMoveSelectedMod(-1));
        MoveModDownCommand = new RelayCommand(() => MoveSelectedMod(1), () => CanMoveSelectedMod(1));
        LaunchCommand = new AsyncRelayCommand(LaunchAsync, CanLaunch);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        OpenProfileFolderCommand = new RelayCommand(OpenProfileFolder, () => SelectedProfile is not null);
        OpenSelectedModFolderCommand = new RelayCommand(OpenSelectedModFolder, () => SelectedMod is not null);
        ExportProfileCommand = new RelayCommand(ExportProfile, () => SelectedProfile is not null);
        ImportProfileCommand = new RelayCommand(ImportProfile);
        ScanForModsCommand = new AsyncRelayCommand(ScanForModsAsync, () => SelectedProfile is not null && !SelectedProfile.IsStandalone);
        ToggleLogCommand = new RelayCommand(() => IsLogVisible = !IsLogVisible);

        _ = LoadAsync();
    }

    public ObservableCollection<ModProfile> Profiles { get; } = new();

    public ObservableCollection<string> LogEntries { get; } = new();

    public string GameInstallPath
    {
        get => SelectedProfile?.GameInstallPath ?? _gameInstallPath;
        set
        {
            _gameInstallPath = value;
            if (SelectedProfile is not null)
            {
                if (SelectedProfile.GameInstallPath != value)
                {
                    SelectedProfile.GameInstallPath = value;
                    OnPropertyChanged(nameof(GameInstallPath));
                    RefreshValidation();
                    _autoSave.Schedule();
                }
            }
            else
            {
                OnPropertyChanged(nameof(GameInstallPath));
                RefreshValidation();
                _autoSave.Schedule();
            }
        }
    }

    public ModProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            var oldProfile = _selectedProfile;
            if (SetProperty(ref _selectedProfile, value))
            {
                if (oldProfile is not null)
                {
                    oldProfile.PropertyChanged -= OnSelectedProfilePropertyChanged;
                }

                SelectedMod = null;
                RecalculateLockedMods();
                RefreshValidation();
                RaiseCommandStates();
                OnPropertyChanged(nameof(GameInstallPath));

                if (_selectedProfile is not null)
                {
                    _selectedProfile.PropertyChanged += OnSelectedProfilePropertyChanged;
                }
            }
        }
    }

    private void OnSelectedProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModProfile.IsStandalone))
        {
            if (SelectedProfile is { IsStandalone: true })
            {
                AutoDetectStandaloneExecutable();
            }

            RaiseCommandStates();
            _autoSave.Schedule();
        }
    }

    public ModEntry? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (SetProperty(ref _selectedMod, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetProperty(ref _validationSummary, value);
    }

    public string LogText
    {
        get => _logText;
        private set => SetProperty(ref _logText, value);
    }

    public bool IsGameValid
    {
        get => _isGameValid;
        private set => SetProperty(ref _isGameValid, value);
    }

    public bool IsBuilding
    {
        get => _isBuilding;
        private set => SetProperty(ref _isBuilding, value);
    }

    public string BuildProgressText
    {
        get => _buildProgressText;
        private set => SetProperty(ref _buildProgressText, value);
    }

    public bool IsLogVisible
    {
        get => _isLogVisible;
        set
        {
            if (SetProperty(ref _isLogVisible, value))
            {
                OnPropertyChanged(nameof(LogToggleText));
                OnPropertyChanged(nameof(LogRowHeight));
                _autoSave.Schedule();
            }
        }
    }

    public string LogToggleText => IsLogVisible ? "Скрыть журнал" : "Показать журнал";

    public System.Windows.GridLength LogRowHeight => IsLogVisible ? new System.Windows.GridLength(125) : new System.Windows.GridLength(0);

    public RelayCommand ToggleLogCommand { get; }

    public RelayCommand ChooseGameFolderCommand { get; }
    public RelayCommand NewProfileCommand { get; }
    public RelayCommand DuplicateProfileCommand { get; }
    public RelayCommand DeleteProfileCommand { get; }
    public RelayCommand BrowseExecutableCommand { get; }
    public RelayCommand AddModCommand { get; }
    public RelayCommand RemoveModCommand { get; }
    public RelayCommand MoveModUpCommand { get; }
    public RelayCommand MoveModDownCommand { get; }
    public AsyncRelayCommand LaunchCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand OpenProfileFolderCommand { get; }
    public RelayCommand OpenSelectedModFolderCommand { get; }
    public RelayCommand ExportProfileCommand { get; }
    public RelayCommand ImportProfileCommand { get; }
    public AsyncRelayCommand ScanForModsCommand { get; }

    public void AddDroppedMods(IEnumerable<string> paths)
    {
        if (SelectedProfile is null)
        {
            return;
        }

        foreach (var path in paths.Where(Directory.Exists))
        {
            SelectedMod = _modListEditor.Add(SelectedProfile, path);
        }

        RefreshValidation();
        _ = SaveAsync();
    }

    public void MoveMod(ModEntry source, ModEntry target)
    {
        if (SelectedProfile is null || !_modListEditor.Move(SelectedProfile, source, target))
        {
            return;
        }

        SelectedMod = source;
        RecalculateLockedMods();
        _ = SaveAsync();
    }

    public void MoveModToEnd(ModEntry source)
    {
        if (SelectedProfile is null || !_modListEditor.MoveToEnd(SelectedProfile, source))
        {
            return;
        }

        SelectedMod = source;
        RecalculateLockedMods();
        _ = SaveAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var settings = await _settingsStore.LoadAsync();
            _gameInstallPath = settings.GameInstallPath;
            OnPropertyChanged(nameof(GameInstallPath));
            _isLogVisible = settings.IsLogVisible;
            OnPropertyChanged(nameof(IsLogVisible));
            OnPropertyChanged(nameof(LogToggleText));
            OnPropertyChanged(nameof(LogRowHeight));

            if (!string.IsNullOrWhiteSpace(settings.DiscordClientId))
            {
                _launchCoordinator.ConfigureDiscord(settings.DiscordClientId);
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
                GameInstallPath = _gameInstallPath,
                Profiles = Profiles.ToList(),
                DontShowAboutOnStartup = existing.DontShowAboutOnStartup,
                IsLogVisible = _isLogVisible,
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

    private async Task ScanForModsAsync()
    {
        if (SelectedProfile is null)
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

    private void ChooseGameFolder()
    {
        var selected = _dialogService.PickFolder("Choose S.T.A.L.K.E.R. GOG folder", GameInstallPath);
        if (selected is null)
        {
            return;
        }

        GameInstallPath = selected;
        RefreshValidation();
        Log($"Game folder selected: {selected}");
    }

    private void NewProfile()
    {
        var profile = _profileManager.Create(Profiles, GameInstallPath);
        Profiles.Add(profile);
        SelectedProfile = profile;
        Log($"Profile created: {profile.Name}");
        _ = SaveAsync();
    }

    private void DuplicateProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var profile = _profileManager.Duplicate(Profiles, SelectedProfile);
        Profiles.Add(profile);
        SelectedProfile = profile;
        Log($"Profile duplicated: {profile.Name}");
        _ = SaveAsync();
    }

    private void DeleteProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var profile = SelectedProfile;
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
            : _gameInstallPath;
        var selected = _dialogService.PickExecutable("Choose launch executable", initialPath);
        if (selected is null)
        {
            return;
        }

        var relativePath = TryGetWorkspaceRelativePath(selected);
        if (relativePath is null)
        {
            _dialogService.ShowError(
                "Executable is outside profile sources",
                "Choose an executable from the game folder, an enabled mod folder, or the generated profile workspace.");
            return;
        }

        SelectedProfile.ExecutableRelativePath = relativePath;
        Log($"Launch executable selected: {relativePath}");
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

        var found = Directory.EnumerateFiles(modRoot, "*.exe", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(modRoot, p))
            .OrderBy(p => Path.GetFileNameWithoutExtension(p).Equals("xrEngine", StringComparison.OrdinalIgnoreCase) ? 0
                       : Path.GetFileNameWithoutExtension(p).Equals("xr_3da", StringComparison.OrdinalIgnoreCase) ? 1
                       : 2)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (found is null)
        {
            return;
        }

        SelectedProfile.ExecutableRelativePath = found;
        Log($"Standalone executable auto-detected: {found}");
    }

    public void RemoveMods(IReadOnlyList<ModEntry> mods)
    {
        if (SelectedProfile is null || mods.Count == 0)
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
        if (SelectedProfile is null || SelectedMod is null)
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
        if (SelectedProfile is null || SelectedMod is null)
        {
            return;
        }

        if (!_modListEditor.MoveByOffset(SelectedProfile, SelectedMod, direction))
        {
            return;
        }

        RecalculateLockedMods();
        _ = SaveAsync();
        RaiseCommandStates();
    }

    private bool CanMoveSelectedMod(int direction)
    {
        if (SelectedProfile is null || SelectedMod is null)
        {
            return false;
        }

        return _modListEditor.CanMoveByOffset(SelectedProfile, SelectedMod, direction);
    }

    private bool CanLaunch()
    {
        return !IsBuilding && IsGameValid && SelectedProfile is { IsEnabled: true, IsRunning: false };
    }

    private bool CanAddMod()
    {
        if (SelectedProfile is null)
        {
            return false;
        }

        if (SelectedProfile.IsStandalone && SelectedProfile.Mods.Count >= 1)
        {
            return false;
        }

        return true;
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
            BuildProgressText = "Building workspace...";
            RaiseCommandStates();

            var progress = new Progress<string>(msg =>
            {
                Log(msg);
                BuildProgressText = msg;
            });
            var gamePath = SelectedProfile.GameInstallPath;
            if (string.IsNullOrWhiteSpace(gamePath))
            {
                gamePath = _gameInstallPath;
            }

            var session = await _launchCoordinator.StartAsync(gamePath, SelectedProfile, progress);
            await SaveAsync();
            Log($"Game process started. PID: {session.ProcessId}");
            SelectedProfile.IsRunning = true;
            RaiseCommandStates();

            _ = CompleteGameSessionAsync(session.Completion, SelectedProfile);
        }
        catch (Exception ex)
        {
            Log($"Launch failed: {ex.Message}");
            _dialogService.ShowError("Launch failed", ex.Message);
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

    private void OpenProfileFolder()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            var path = _profileManager.GetProfileFolderPath(SelectedProfile, _gameInstallPath)
                ?? throw new DirectoryNotFoundException("Папка включенного автономного мода не найдена.");

            Directory.CreateDirectory(path);
            _dialogService.OpenFolder(path);
        }
        catch (Exception ex)
        {
            Log($"Could not open profile folder: {ex.Message}");
        }
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

    private void RefreshValidation()
    {
        var result = _profileReadinessService.Validate(SelectedProfile, _gameInstallPath);
        IsGameValid = result.IsValid;
        ValidationSummary = result.Summary;
        RaiseCommandStates();
    }

    private void ProfilesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (ModProfile profile in e.NewItems)
            {
                profile.PropertyChanged += ProfileOnPropertyChanged;
                profile.Mods.CollectionChanged += ModsOnCollectionChanged;
                foreach (var mod in profile.Mods)
                {
                    mod.PropertyChanged += ModOnPropertyChanged;
                }
            }
        }

        if (e.OldItems is not null)
        {
            foreach (ModProfile profile in e.OldItems)
            {
                profile.PropertyChanged -= ProfileOnPropertyChanged;
                profile.Mods.CollectionChanged -= ModsOnCollectionChanged;
                foreach (var mod in profile.Mods)
                {
                    mod.PropertyChanged -= ModOnPropertyChanged;
                }
            }
        }
    }

    private void ModsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (ModEntry mod in e.NewItems)
            {
                mod.PropertyChanged += ModOnPropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (ModEntry mod in e.OldItems)
            {
                mod.PropertyChanged -= ModOnPropertyChanged;
            }
        }

        if (SelectedProfile is not null)
        {
            _modListEditor.Renumber(SelectedProfile);
        }
        RecalculateLockedMods();
        RefreshValidation();
        _autoSave.Schedule();
    }

    private void ProfileOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModProfile.IsRunning))
        {
            return;
        }

        RefreshValidation();
        _autoSave.Schedule();
    }

    private void ModOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ModEntry.IsLocked) or nameof(ModEntry.HasOverlapsAbove))
        {
            return;
        }

        RefreshValidation();
        _autoSave.Schedule();
        if (e.PropertyName == nameof(ModEntry.IsEnabled))
        {
            RecalculateLockedMods();
        }
    }

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
            var result = await _modConflictAnalyzer.AnalyzeAsync(inputs, cancellationToken);
            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested || SelectedProfile != profile)
                {
                    return;
                }

                foreach (var mod in profile.Mods)
                {
                    var state = result.GetValueOrDefault(mod.Id);
                    mod.IsLocked = state?.IsLocked ?? false;
                    mod.HasOverlapsAbove = state?.HasOverlapsAbove ?? false;
                }
            });
        }
        catch (OperationCanceledException)
        {
            // A newer profile or mod state superseded this analysis.
        }
    }

    private void RaiseCommandStates()
    {
        DeleteProfileCommand.RaiseCanExecuteChanged();
        DuplicateProfileCommand.RaiseCanExecuteChanged();
        BrowseExecutableCommand.RaiseCanExecuteChanged();
        AddModCommand.RaiseCanExecuteChanged();
        RemoveModCommand.RaiseCanExecuteChanged();
        MoveModUpCommand.RaiseCanExecuteChanged();
        MoveModDownCommand.RaiseCanExecuteChanged();
        LaunchCommand.RaiseCanExecuteChanged();
        OpenProfileFolderCommand.RaiseCanExecuteChanged();
        OpenSelectedModFolderCommand.RaiseCanExecuteChanged();
        ExportProfileCommand.RaiseCanExecuteChanged();
        ImportProfileCommand.RaiseCanExecuteChanged();
        ScanForModsCommand.RaiseCanExecuteChanged();
    }

    private string? TryGetWorkspaceRelativePath(string selectedPath)
    {
        if (SelectedProfile is null)
        {
            return null;
        }

        var roots = new List<string>();
        var gamePath = SelectedProfile.GameInstallPath;
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            gamePath = _gameInstallPath;
        }

        if (Directory.Exists(gamePath))
        {
            roots.Add(GameInstallPath);
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

    private void Log(string message)
    {
        var entry = _applicationLogService.Write(message);

        App.Current.Dispatcher.Invoke(() =>
        {
            LogEntries.Insert(0, entry);
            while (LogEntries.Count > 200)
            {
                LogEntries.RemoveAt(LogEntries.Count - 1);
            }

            LogText = string.Join(Environment.NewLine, LogEntries);
        });
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
