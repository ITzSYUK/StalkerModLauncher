using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel;
using System.Windows.Data;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly AppPaths _paths;
    private readonly SettingsStore _settingsStore;
    private readonly LaunchCoordinator _launchCoordinator;
    private readonly DialogService _dialogService;
    private readonly ModConflictAnalyzer _modConflictAnalyzer;
    private readonly ProfileTransferService _profileTransferService;
    private readonly ModScannerService _modScannerService;
    private readonly ModListEditor _modListEditor;
    private readonly Mo2ImportService _mo2ImportService;
    private readonly ProfileManager _profileManager;
    private readonly GameExitDiagnosticsService _gameExitDiagnosticsService;
    private readonly ProfileReadinessService _profileReadinessService;
    private readonly LaunchPreflightService _launchPreflightService;
    private readonly ApplicationLogService _applicationLogService;
    private readonly DebouncedAsyncAction _autoSave;
    private CancellationTokenSource? _conflictAnalysisCancellation;
    private string _lastBrowsedGamePath = string.Empty;
    private ModProfile? _selectedProfile;
    private ModEntry? _selectedMod;
    private string _validationSummary = "Выберите папку с установленной игрой.";
    private bool _isGameValid;
    private bool _isBuilding;
    private bool _isPdaInterfaceEnabled;
    private string _buildProgressText = string.Empty;
    private ICollectionView? _filteredMods;
    private string _modSearchText = string.Empty;
    private ModListFilter _selectedModFilter;
    private bool _disposed;

    public MainViewModel(
        AppPaths paths,
        SettingsStore settingsStore,
        LaunchCoordinator launchCoordinator,
        DialogService dialogService,
        ModConflictAnalyzer modConflictAnalyzer,
        ProfileTransferService profileTransferService,
        ModScannerService modScannerService,
        ModListEditor modListEditor,
        Mo2ImportService mo2ImportService,
        ProfileManager profileManager,
        GameExitDiagnosticsService gameExitDiagnosticsService,
        ProfileReadinessService profileReadinessService,
        LaunchPreflightService launchPreflightService,
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
        _mo2ImportService = mo2ImportService;
        _profileManager = profileManager;
        _gameExitDiagnosticsService = gameExitDiagnosticsService;
        _profileReadinessService = profileReadinessService;
        _launchPreflightService = launchPreflightService;
        _applicationLogService = applicationLogService;
        _autoSave = new DebouncedAsyncAction(SaveAsync, TimeSpan.FromMilliseconds(500));
        ActivityLog = new ActivityLogViewModel(_applicationLogService, _autoSave.Schedule);

        Profiles.CollectionChanged += ProfilesOnCollectionChanged;
        _settingsStore.RecoveryCompleted += SettingsStoreOnRecoveryCompleted;

        ChooseGameFolderCommand = new RelayCommand(ChooseGameFolder);
        NewProfileCommand = new RelayCommand(NewProfile);
        DuplicateProfileCommand = new RelayCommand(DuplicateProfile, () => SelectedProfile is not null);
        DeleteProfileCommand = new RelayCommand(DeleteProfile, () => SelectedProfile is not null);
        InlineDuplicateProfileCommand = new RelayCommand(
            parameter => DuplicateProfile(parameter as ModProfile),
            parameter => parameter is ModProfile);
        InlineExportProfileCommand = new RelayCommand(
            parameter => ExportProfile(parameter as ModProfile),
            parameter => parameter is ModProfile);
        InlineDeleteProfileCommand = new RelayCommand(
            parameter => DeleteProfile(parameter as ModProfile),
            parameter => parameter is ModProfile { IsRunning: false });
        BrowseExecutableCommand = new RelayCommand(BrowseExecutable, () => SelectedProfile is not null);
        AddModCommand = new RelayCommand(AddMod, CanAddMod);
        RemoveModCommand = new RelayCommand(RemoveMod, () => CanEditSelectedProfile && SelectedMod is not null);
        MoveModUpCommand = new RelayCommand(() => MoveSelectedMod(-1), () => CanMoveSelectedMod(-1));
        MoveModDownCommand = new RelayCommand(() => MoveSelectedMod(1), () => CanMoveSelectedMod(1));
        InlineRemoveModCommand = new RelayCommand(
            parameter => RemoveInlineMod(parameter as ModEntry),
            parameter => CanEditSelectedProfile && parameter is ModEntry);
        InlineMoveModUpCommand = new RelayCommand(
            parameter => MoveInlineMod(parameter as ModEntry, -1),
            parameter => CanMoveInlineMod(parameter as ModEntry, -1));
        InlineMoveModDownCommand = new RelayCommand(
            parameter => MoveInlineMod(parameter as ModEntry, 1),
            parameter => CanMoveInlineMod(parameter as ModEntry, 1));
        InlineOpenModFolderCommand = new RelayCommand(
            parameter => OpenInlineModFolder(parameter as ModEntry),
            parameter => parameter is ModEntry mod && Directory.Exists(mod.SourcePath));
        LaunchCommand = new AsyncRelayCommand(LaunchAsync, CanLaunch);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        OpenProfileFolderCommand = new RelayCommand(OpenProfileFolder, () => SelectedProfile is not null);
        OpenSelectedModFolderCommand = new RelayCommand(OpenSelectedModFolder, () => SelectedMod is not null);
        ExportProfileCommand = new RelayCommand(ExportProfile, () => SelectedProfile is not null);
        ImportProfileCommand = new RelayCommand(ImportProfile);
        ImportMo2CollectionCommand = new RelayCommand(() => Mo2ImportRequested?.Invoke(this, EventArgs.Empty));
        ShowSelectedModConflictsCommand = new RelayCommand(
            parameter => ConflictExplorerRequested?.Invoke(this, parameter as ModEntry ?? SelectedMod),
            parameter => SelectedProfile is { IsStandalone: false } && (parameter is ModEntry || SelectedMod is not null));
        ShowFileTreeCommand = new RelayCommand(
            () => ConflictExplorerRequested?.Invoke(this, SelectedMod),
            () => SelectedProfile is { IsStandalone: false });
        ScanForModsCommand = new AsyncRelayCommand(
            ScanForModsAsync,
            () => CanEditSelectedProfile && SelectedProfile is { IsStandalone: false });
        ToggleInterfaceCommand = new RelayCommand(() => IsPdaInterfaceEnabled = !IsPdaInterfaceEnabled);
        Initialization = LoadAsync();
    }

    public ObservableCollection<ModProfile> Profiles { get; } = new();

    public ActivityLogViewModel ActivityLog { get; }

    public Task Initialization { get; }

    public bool HasProfiles => Profiles.Count > 0;

    public bool IsPdaInterfaceEnabled
    {
        get => _isPdaInterfaceEnabled;
        set
        {
            if (SetProperty(ref _isPdaInterfaceEnabled, value))
            {
                _autoSave.Schedule();
            }
        }
    }

    public event EventHandler? ProfileCreationRequested;
    public event EventHandler? Mo2ImportRequested;
    public event EventHandler<ModScanSelectionRequest>? ModScanSelectionRequested;
    public event EventHandler<ModEntry?>? ConflictExplorerRequested;

    public string GameInstallPath
    {
        get => SelectedProfile?.GameInstallPath ?? _lastBrowsedGamePath;
        set
        {
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
                _lastBrowsedGamePath = value;
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
            if (!SetProperty(ref _selectedProfile, value))
            {
                return;
            }

            if (oldProfile is not null)
            {
                oldProfile.PropertyChanged -= OnSelectedProfilePropertyChanged;
            }

            SelectedMod = null;
            CreateFilteredModsView();
            RecalculateModOverlayInfo();
            RefreshValidation();
            RaiseCommandStates();
            OnPropertyChanged(nameof(GameInstallPath));
            OnPropertyChanged(nameof(CanEditSelectedProfile));

            if (_selectedProfile is not null)
            {
                _selectedProfile.PropertyChanged += OnSelectedProfilePropertyChanged;
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

        if (e.PropertyName == nameof(ModProfile.ExecutableRelativePath) ||
            e.PropertyName == nameof(ModProfile.ExecutableSourcePath))
        {
            RecalculateModOverlayInfo();
        }
    }

    public ModEntry? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (SetProperty(ref _selectedMod, value))
            {
                UpdateRelatedModHighlights();
                RaiseCommandStates();
            }
        }
    }

    public ICollectionView? FilteredMods
    {
        get => _filteredMods;
        private set => SetProperty(ref _filteredMods, value);
    }

    public IReadOnlyList<ModListFilterOption> ModFilterOptions { get; } =
    [
        new(ModListFilter.All, "Все моды"),
        new(ModListFilter.Conflicts, "Конфликтующие"),
        new(ModListFilter.Overwrite, "Перезаписывающие"),
        new(ModListFilter.Overwritten, "Перезаписанные"),
        new(ModListFilter.Mixed, "Смешанные"),
        new(ModListFilter.Redundant, "Полностью перекрытые"),
        new(ModListFilter.Binaries, "EXE и DLL")
    ];

    public string ModSearchText
    {
        get => _modSearchText;
        set
        {
            if (SetProperty(ref _modSearchText, value))
            {
                FilteredMods?.Refresh();
            }
        }
    }

    public ModListFilter SelectedModFilter
    {
        get => _selectedModFilter;
        set
        {
            if (SetProperty(ref _selectedModFilter, value))
            {
                FilteredMods?.Refresh();
            }
        }
    }

    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetProperty(ref _validationSummary, value);
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

    public bool CanEditSelectedProfile => SelectedProfile is { IsRunning: false };

    public string BuildProgressText
    {
        get => _buildProgressText;
        private set => SetProperty(ref _buildProgressText, value);
    }

    public RelayCommand ChooseGameFolderCommand { get; }
    public RelayCommand NewProfileCommand { get; }
    public RelayCommand DuplicateProfileCommand { get; }
    public RelayCommand DeleteProfileCommand { get; }
    public RelayCommand InlineDuplicateProfileCommand { get; }
    public RelayCommand InlineExportProfileCommand { get; }
    public RelayCommand InlineDeleteProfileCommand { get; }
    public RelayCommand BrowseExecutableCommand { get; }
    public RelayCommand AddModCommand { get; }
    public RelayCommand RemoveModCommand { get; }
    public RelayCommand MoveModUpCommand { get; }
    public RelayCommand MoveModDownCommand { get; }
    public RelayCommand InlineRemoveModCommand { get; }
    public RelayCommand InlineMoveModUpCommand { get; }
    public RelayCommand InlineMoveModDownCommand { get; }
    public RelayCommand InlineOpenModFolderCommand { get; }
    public AsyncRelayCommand LaunchCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand OpenProfileFolderCommand { get; }
    public RelayCommand OpenSelectedModFolderCommand { get; }
    public RelayCommand ExportProfileCommand { get; }
    public RelayCommand ImportProfileCommand { get; }
    public RelayCommand ImportMo2CollectionCommand { get; }
    public RelayCommand ShowSelectedModConflictsCommand { get; }
    public RelayCommand ShowFileTreeCommand { get; }
    public AsyncRelayCommand ScanForModsCommand { get; }
    public RelayCommand ToggleInterfaceCommand { get; }

    public void AppendLog(string message) => Log(message);

    private void RefreshValidation()
    {
        var result = _profileReadinessService.Validate(SelectedProfile);
        IsGameValid = result.IsValid;
        ValidationSummary = result.Summary;
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        DeleteProfileCommand.RaiseCanExecuteChanged();
        DuplicateProfileCommand.RaiseCanExecuteChanged();
        InlineDuplicateProfileCommand.RaiseCanExecuteChanged();
        InlineExportProfileCommand.RaiseCanExecuteChanged();
        InlineDeleteProfileCommand.RaiseCanExecuteChanged();
        BrowseExecutableCommand.RaiseCanExecuteChanged();
        AddModCommand.RaiseCanExecuteChanged();
        RemoveModCommand.RaiseCanExecuteChanged();
        MoveModUpCommand.RaiseCanExecuteChanged();
        MoveModDownCommand.RaiseCanExecuteChanged();
        InlineRemoveModCommand.RaiseCanExecuteChanged();
        InlineMoveModUpCommand.RaiseCanExecuteChanged();
        InlineMoveModDownCommand.RaiseCanExecuteChanged();
        InlineOpenModFolderCommand.RaiseCanExecuteChanged();
        LaunchCommand.RaiseCanExecuteChanged();
        OpenProfileFolderCommand.RaiseCanExecuteChanged();
        OpenSelectedModFolderCommand.RaiseCanExecuteChanged();
        ExportProfileCommand.RaiseCanExecuteChanged();
        ImportProfileCommand.RaiseCanExecuteChanged();
        ImportMo2CollectionCommand.RaiseCanExecuteChanged();
        ShowSelectedModConflictsCommand.RaiseCanExecuteChanged();
        ShowFileTreeCommand.RaiseCanExecuteChanged();
        ScanForModsCommand.RaiseCanExecuteChanged();
    }

    private void Log(string message)
    {
        var app = App.Current;
        if (app is null)
        {
            ActivityLog.Append(message);
            return;
        }

        app.Dispatcher.Invoke(() => ActivityLog.Append(message));
    }

    public ConflictExplorerViewModel CreateConflictExplorerViewModel(ModEntry? selectedMod)
    {
        if (SelectedProfile is not { IsStandalone: false } profile)
        {
            throw new InvalidOperationException("Выберите обычный профиль.");
        }

        return new ConflictExplorerViewModel(
            profile,
            selectedMod,
            _modConflictAnalyzer,
            new FileLayerExplorerService(),
            _dialogService,
            SaveOrThrowAsync,
            () =>
            {
                _modListEditor.Renumber(profile);
                RecalculateModOverlayInfo();
                RefreshValidation();
                CreateFilteredModsView();
            });
    }

    private void CreateFilteredModsView()
    {
        if (SelectedProfile is null)
        {
            FilteredMods = null;
            return;
        }

        var view = new ListCollectionView((IList)SelectedProfile.Mods)
        {
            Filter = item => item is ModEntry mod && MatchesModFilter(mod)
        };
        FilteredMods = view;
    }

    private bool MatchesModFilter(ModEntry mod)
    {
        var searchMatches = string.IsNullOrWhiteSpace(ModSearchText) ||
                            mod.Name.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase) ||
                            mod.SourcePath.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase) ||
                            mod.GroupName.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase);
        if (!searchMatches)
        {
            return false;
        }

        return SelectedModFilter switch
        {
            ModListFilter.Conflicts => mod.ConflictKind is not ModConflictKind.None and not ModConflictKind.Disabled,
            ModListFilter.Overwrite => mod.ConflictKind == ModConflictKind.Overwrite,
            ModListFilter.Overwritten => mod.ConflictKind == ModConflictKind.Overwritten,
            ModListFilter.Mixed => mod.ConflictKind == ModConflictKind.Mixed,
            ModListFilter.Redundant => mod.ConflictKind == ModConflictKind.Redundant,
            ModListFilter.Binaries => mod.OverwrittenBinaryCount > 0 || mod.OverwrittenByBinaryCount > 0 || mod.ProvidesLaunchExecutable,
            _ => true
        };
    }

    private void UpdateRelatedModHighlights()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var related = SelectedMod?.RelatedModIds.ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in SelectedProfile.Mods)
        {
            mod.IsConflictRelated = related.Contains(mod.Id);
        }
    }

    private static Task InvokeOnUiAsync(Action action)
    {
        var app = App.Current;
        if (app is null)
        {
            action();
            return Task.CompletedTask;
        }

        return app.Dispatcher.InvokeAsync(action).Task;
    }
}
