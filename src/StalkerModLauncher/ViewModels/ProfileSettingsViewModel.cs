using System.Windows.Input;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed class ProfileSettingsViewModel : ObservableObject
{
    private readonly ModProfile _profile;
    private readonly DialogService _dialogService;
    private readonly Func<Task> _onSave;
    private readonly Func<string, ProfileExecutableSelection?> _createExecutableSelection;
    private readonly Func<ProfileExecutableSelection?> _detectAutomaticExecutableSelection;
    private readonly Func<string, bool> _isNameTaken;
    private readonly ProfileSettingsValidator _validator;
    private readonly Mo2ModListImporter _mo2ModListImporter = new();
    private readonly bool _isUsvfsAvailable;
    private string _profileName;
    private string _profileDescription;
    private string _executableRelativePath;
    private string _executableSourcePath;
    private string _launchArguments;
    private string _workspacePath;
    private bool _isEnabled;
    private bool _isDiscordStatusEnabled;
    private bool _isStandalone;
    private LaunchBackendKind _launchBackendKind;
    private string _usvfsExecutableOverrideRelativePath;
    private string _anomalyRenderer = string.Empty;
    private bool _anomalyUseAvx;
    private readonly bool _isAnomalyProfile;

    public ProfileSettingsViewModel(
        ModProfile profile,
        DialogService dialogService,
        Func<Task> onSave,
        Func<string, ProfileExecutableSelection?> createExecutableSelection,
        Func<ProfileExecutableSelection?> detectAutomaticExecutableSelection,
        Func<string, bool> isNameTaken,
        bool? usvfsAvailable = null)
    {
        _profile = profile;
        _dialogService = dialogService;
        _onSave = onSave;
        _createExecutableSelection = createExecutableSelection;
        _detectAutomaticExecutableSelection = detectAutomaticExecutableSelection;
        _isNameTaken = isNameTaken;
        _validator = new ProfileSettingsValidator();
        _isUsvfsAvailable = usvfsAvailable ?? UsvfsFeatureGate.IsEnabled();
        _profileName = profile.Name.Trim();
        _profileDescription = profile.Description;
        _executableRelativePath = profile.ExecutableRelativePath;
        _executableSourcePath = profile.ExecutableSourcePath;
        _launchArguments = profile.LaunchArguments;
        _workspacePath = profile.WorkspacePath;
        _isEnabled = profile.IsEnabled;
        _isDiscordStatusEnabled = profile.IsDiscordStatusEnabled;
        _isStandalone = profile.IsStandalone;
        _launchBackendKind = profile.LaunchBackendKind;
        _usvfsExecutableOverrideRelativePath = profile.UsvfsExecutableOverrideRelativePath;
        _isAnomalyProfile = IsAnomalyProfile(profile);
        if (AnomalyUsvfsEngineSelection.TryParseRelativePath(
                _usvfsExecutableOverrideRelativePath,
                out var renderer,
                out var useAvx))
        {
            _anomalyRenderer = renderer;
            _anomalyUseAvx = useAvx;
        }

        SaveCommand = new AsyncRelayCommand(async () => await TrySaveAsync());
        BrowseExecutableCommand = new RelayCommand(BrowseExecutable);
        ClearExecutableSourceCommand = new RelayCommand(ClearExecutableSource, () => !string.IsNullOrWhiteSpace(ExecutableSourcePath));
        OpenProfileFolderCommand = new RelayCommand(OpenProfileFolder);
        ImportMo2ModListCommand = new AsyncRelayCommand(ImportMo2ModListAsync);
    }

    public string ProfileName
    {
        get => _profileName;
        set => SetProperty(ref _profileName, value);
    }

    public string ProfileDescription
    {
        get => _profileDescription;
        set => SetProperty(ref _profileDescription, value);
    }

    public string ExecutableRelativePath
    {
        get => _executableRelativePath;
        set
        {
            if (SetProperty(ref _executableRelativePath, value))
            {
                ExecutableSourcePath = string.Empty;
            }
        }
    }

    public string ExecutableSourcePath
    {
        get => _executableSourcePath;
        private set
        {
            if (SetProperty(ref _executableSourcePath, value))
            {
                OnPropertyChanged(nameof(ExecutableSourceDisplay));
                OnPropertyChanged(nameof(HasManualExecutableSource));
                ((RelayCommand)ClearExecutableSourceCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasManualExecutableSource => !string.IsNullOrWhiteSpace(ExecutableSourcePath);

    public string ExecutableSourceDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ExecutableSourcePath))
            {
                return "Источник EXE: автоматически по порядку модов. Нижний мод в списке имеет больший приоритет.";
            }

            var source = ProfileExecutableSourceResolver.GetSourceRoots(_profile, includeWorkspace: false)
                .FirstOrDefault(root => FileSystemSafety.IsSameDirectory(root.RootPath, ExecutableSourcePath));
            return source is null
                ? $"Источник EXE: выбран вручную, но папка сейчас недоступна: {ExecutableSourcePath}"
                : $"Источник EXE: вручную закреплен за модом: {source.DisplayName}.";
        }
    }

    public string LaunchArguments
    {
        get => _launchArguments;
        set => SetProperty(ref _launchArguments, value);
    }

    public string WorkspacePath
    {
        get => _workspacePath;
        set => SetProperty(ref _workspacePath, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool IsDiscordStatusEnabled
    {
        get => _isDiscordStatusEnabled;
        set => SetProperty(ref _isDiscordStatusEnabled, value);
    }

    public bool IsStandalone
    {
        get => _isStandalone;
        set
        {
            if (!SetProperty(ref _isStandalone, value))
            {
                return;
            }

            if (value)
            {
                SetLaunchBackend(LaunchBackendKind.LinkedWorkspace);
            }

            OnPropertyChanged(nameof(CanUseUsvfs));
            OnPropertyChanged(nameof(IsAnomalyUsvfsOptionsVisible));
        }
    }

    public bool IsUsvfsAvailable => _isUsvfsAvailable;

    public bool CanUseUsvfs => IsUsvfsAvailable && !IsStandalone;

    public string UsvfsAvailabilityText => IsUsvfsAvailable
        ? "USVFS подключает моды без сборки полного workspace. Режим пока считается экспериментальным."
        : "Компоненты USVFS не найдены рядом с лаунчером. Доступен только стабильный Workspace.";

    public bool UseLinkedWorkspace
    {
        get => _launchBackendKind == LaunchBackendKind.LinkedWorkspace;
        set
        {
            if (value)
            {
                SetLaunchBackend(LaunchBackendKind.LinkedWorkspace);
            }
        }
    }

    public bool UseVirtualFileSystem
    {
        get => _launchBackendKind == LaunchBackendKind.VirtualFileSystem;
        set
        {
            if (value && CanUseUsvfs)
            {
                SetLaunchBackend(LaunchBackendKind.VirtualFileSystem);
            }
        }
    }

    public bool IsAnomalyUsvfsOptionsVisible =>
        _isAnomalyProfile && UseVirtualFileSystem;

    public bool UseAutomaticAnomalyRenderer
    {
        get => _anomalyRenderer.Length == 0;
        set
        {
            if (value)
            {
                SetAnomalyRenderer(string.Empty);
            }
        }
    }

    public bool UseAnomalyDx8
    {
        get => _anomalyRenderer == "DX8";
        set { if (value) SetAnomalyRenderer("DX8"); }
    }

    public bool UseAnomalyDx9
    {
        get => _anomalyRenderer == "DX9";
        set { if (value) SetAnomalyRenderer("DX9"); }
    }

    public bool UseAnomalyDx10
    {
        get => _anomalyRenderer == "DX10";
        set { if (value) SetAnomalyRenderer("DX10"); }
    }

    public bool UseAnomalyDx11
    {
        get => _anomalyRenderer == "DX11";
        set { if (value) SetAnomalyRenderer("DX11"); }
    }

    public bool HasManualAnomalyRenderer => _anomalyRenderer.Length > 0;

    public bool AnomalyUseAvx
    {
        get => _anomalyUseAvx;
        set
        {
            if (SetProperty(ref _anomalyUseAvx, value))
            {
                UpdateAnomalyExecutableOverride();
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand BrowseExecutableCommand { get; }
    public ICommand ClearExecutableSourceCommand { get; }
    public ICommand OpenProfileFolderCommand { get; }
    public ICommand ImportMo2ModListCommand { get; }

    public async Task<bool> TrySaveAsync()
    {
        var validation = _validator.Validate(ProfileName, ExecutableRelativePath, _isNameTaken);
        if (!validation.IsValid)
        {
            _dialogService.ShowError("Некорректные настройки профиля", string.Join(Environment.NewLine, validation.Messages));
            return false;
        }

        ApplyToProfile();
        await _onSave();
        return true;
    }

    private void ApplyToProfile()
    {
        _profile.Name = ProfileName.Trim();
        _profile.Description = ProfileDescription;
        _profile.ExecutableRelativePath = ExecutableRelativePath;
        _profile.ExecutableSourcePath = IsStandalone ? string.Empty : ExecutableSourcePath;
        _profile.LaunchArguments = LaunchArguments;
        _profile.WorkspacePath = WorkspacePath;
        _profile.IsEnabled = IsEnabled;
        _profile.IsDiscordStatusEnabled = IsDiscordStatusEnabled;
        _profile.IsStandalone = IsStandalone;
        _profile.LaunchBackendKind = IsStandalone
            ? LaunchBackendKind.LinkedWorkspace
            : _launchBackendKind;
        _profile.UsvfsExecutableOverrideRelativePath = _usvfsExecutableOverrideRelativePath;
    }

    private void BrowseExecutable()
    {
        if (_isStandalone)
        {
            BrowseStandaloneExecutable();
            return;
        }

        var initialPath = Directory.Exists(_workspacePath) ? _workspacePath : null;
        var selected = _dialogService.PickExecutable("Choose launch executable", initialPath);
        if (selected is null)
        {
            return;
        }

        var selection = _createExecutableSelection(selected);
        if (selection is null)
        {
            _dialogService.ShowError(
                "Invalid executable",
                "Choose an executable from the game folder, an enabled mod folder, or the generated profile workspace.");
            return;
        }

        SetExecutableSelection(selection);
    }

    private void BrowseStandaloneExecutable()
    {
        var modRoot = _profile.Mods
            .FirstOrDefault(m => m.IsEnabled && Directory.Exists(m.SourcePath))
            ?.SourcePath;

        if (modRoot is null)
        {
            _dialogService.ShowError(
                "No mod folder",
                "Add a mod to the profile first, then choose the executable from its folder.");
            return;
        }

        var selected = _dialogService.PickExecutable("Choose game executable", modRoot);
        if (selected is null)
        {
            return;
        }

        var relative = Path.GetRelativePath(Path.GetFullPath(modRoot), selected);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            _dialogService.ShowError(
                "Outside mod folder",
                "The executable must be inside the standalone mod's folder.");
            return;
        }

        SetExecutableSelection(new ProfileExecutableSelection(relative, modRoot, "автономный мод", true));
    }

    private void SetExecutableSelection(ProfileExecutableSelection selection)
    {
        _executableRelativePath = selection.RelativePath;
        OnPropertyChanged(nameof(ExecutableRelativePath));
        ExecutableSourcePath = !IsStandalone && selection.PinsSource ? selection.SourceRootPath : string.Empty;
    }

    private void ClearExecutableSource()
    {
        var selection = _detectAutomaticExecutableSelection();
        if (selection is null)
        {
            _dialogService.ShowError(
                "Не удалось выбрать EXE автоматически",
                "Лаунчер не нашел подходящий .exe в папке базовой игры или во включенных модах. Выберите файл запуска вручную.");
            return;
        }

        SetExecutableSelection(selection);
    }

    private void OpenProfileFolder()
    {
        try
        {
            var path = _isStandalone
                ? _profile.Mods.FirstOrDefault(m => m.IsEnabled && Directory.Exists(m.SourcePath))?.SourcePath
                : null;

            path ??= string.IsNullOrWhiteSpace(_workspacePath) ? null : _workspacePath;

            if (path is null)
            {
                return;
            }

            Directory.CreateDirectory(path);
            _dialogService.OpenFolder(path);
        }
        catch
        {
            // ignored
        }
    }

    private async Task ImportMo2ModListAsync()
    {
        var initialPath = _profile.Mods
            .Select(mod => Path.GetDirectoryName(mod.SourcePath))
            .FirstOrDefault(Directory.Exists);
        var filePath = _dialogService.PickFile(
            "Выберите modlist.txt из профиля Mod Organizer 2",
            "Mod Organizer mod list (modlist.txt)|modlist.txt|Text files (*.txt)|*.txt",
            initialPath);
        if (filePath is null)
        {
            return;
        }

        try
        {
            var result = _mo2ModListImporter.Import(_profile, filePath);
            await _onSave();

            var report = new List<string>
            {
                "Порядок модов из Mod Organizer 2 применён.",
                string.Empty,
                $"Сопоставлено модов: {result.MatchedCount}",
                $"Изменено состояний включения: {result.EnabledStateChanges}",
                $"Не найдены среди добавленных модов: {result.MissingProfileMods.Count}",
                $"Отсутствуют в modlist.txt: {result.UnlistedLauncherMods.Count}"
            };

            if (result.MissingProfileMods.Count > 0)
            {
                report.Add(string.Empty);
                report.Add("Первые несопоставленные записи:");
                report.AddRange(result.MissingProfileMods.Take(8).Select(name => $"• {name}"));
            }

            _dialogService.ShowInfo("Импорт порядка MO2", string.Join(Environment.NewLine, report));
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Не удалось импортировать порядок MO2", ex.Message);
        }
    }

    private void SetLaunchBackend(LaunchBackendKind backend)
    {
        if (_launchBackendKind == backend)
        {
            return;
        }

        _launchBackendKind = backend;
        OnPropertyChanged(nameof(UseLinkedWorkspace));
        OnPropertyChanged(nameof(UseVirtualFileSystem));
        OnPropertyChanged(nameof(IsAnomalyUsvfsOptionsVisible));
    }

    private void SetAnomalyRenderer(string renderer)
    {
        if (_anomalyRenderer == renderer)
        {
            return;
        }

        _anomalyRenderer = renderer;
        OnPropertyChanged(nameof(UseAutomaticAnomalyRenderer));
        OnPropertyChanged(nameof(UseAnomalyDx8));
        OnPropertyChanged(nameof(UseAnomalyDx9));
        OnPropertyChanged(nameof(UseAnomalyDx10));
        OnPropertyChanged(nameof(UseAnomalyDx11));
        OnPropertyChanged(nameof(HasManualAnomalyRenderer));
        UpdateAnomalyExecutableOverride();
    }

    private void UpdateAnomalyExecutableOverride()
    {
        _usvfsExecutableOverrideRelativePath = _anomalyRenderer.Length == 0
            ? string.Empty
            : AnomalyUsvfsEngineSelection.CreateRelativePath(_anomalyRenderer, _anomalyUseAvx);
    }

    private static bool IsAnomalyProfile(ModProfile profile)
    {
        return (!string.IsNullOrWhiteSpace(profile.GameInstallPath) &&
                File.Exists(Path.Combine(profile.GameInstallPath, "AnomalyLauncher.exe"))) ||
               Path.GetFileName(profile.ExecutableRelativePath)
                   .StartsWith("Anomaly", StringComparison.OrdinalIgnoreCase);
    }
}
