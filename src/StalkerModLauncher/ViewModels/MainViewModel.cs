using System.Collections.ObjectModel;
using System.ComponentModel;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed partial class MainViewModel : ObservableObject
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
    private string _buildProgressText = string.Empty;

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
        _profileManager = profileManager;
        _gameExitDiagnosticsService = gameExitDiagnosticsService;
        _profileReadinessService = profileReadinessService;
        _launchPreflightService = launchPreflightService;
        _applicationLogService = applicationLogService;
        _autoSave = new DebouncedAsyncAction(SaveAsync, TimeSpan.FromMilliseconds(500));
        ActivityLog = new ActivityLogViewModel(_applicationLogService, _autoSave.Schedule);

        Profiles.CollectionChanged += ProfilesOnCollectionChanged;

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
        ScanForModsCommand = new AsyncRelayCommand(
            ScanForModsAsync,
            () => CanEditSelectedProfile && SelectedProfile is { IsStandalone: false });
        Initialization = LoadAsync();
    }

    public ObservableCollection<ModProfile> Profiles { get; } = new();

    public ActivityLogViewModel ActivityLog { get; }

    public Task Initialization { get; }

    public bool HasProfiles => Profiles.Count > 0;

    public event EventHandler? ProfileCreationRequested;

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
            RecalculateLockedMods();
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
            RecalculateLockedMods();
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
    public AsyncRelayCommand ScanForModsCommand { get; }

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
