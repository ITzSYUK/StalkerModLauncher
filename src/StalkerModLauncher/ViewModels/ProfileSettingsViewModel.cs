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
    private readonly Func<string, string?> _convertToRelativePath;
    private readonly Func<string, bool> _isNameTaken;
    private readonly ProfileSettingsValidator _validator;
    private string _profileName;
    private string _profileDescription;
    private string _executableRelativePath;
    private string _launchArguments;
    private string _workspacePath;
    private bool _isEnabled;
    private bool _isStandalone;

    public ProfileSettingsViewModel(ModProfile profile, DialogService dialogService, Func<Task> onSave, Func<string, string?> convertToRelativePath, Func<string, bool> isNameTaken)
    {
        _profile = profile;
        _dialogService = dialogService;
        _onSave = onSave;
        _convertToRelativePath = convertToRelativePath;
        _isNameTaken = isNameTaken;
        _validator = new ProfileSettingsValidator();
        _profileName = profile.Name.Trim();
        _profileDescription = profile.Description;
        _executableRelativePath = profile.ExecutableRelativePath;
        _launchArguments = profile.LaunchArguments;
        _workspacePath = profile.WorkspacePath;
        _isEnabled = profile.IsEnabled;
        _isStandalone = profile.IsStandalone;

        SaveCommand = new AsyncRelayCommand(async () => await TrySaveAsync());
        BrowseExecutableCommand = new RelayCommand(BrowseExecutable);
        OpenProfileFolderCommand = new RelayCommand(OpenProfileFolder);
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
        set => SetProperty(ref _executableRelativePath, value);
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

    public bool IsStandalone
    {
        get => _isStandalone;
        set => SetProperty(ref _isStandalone, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand BrowseExecutableCommand { get; }
    public ICommand OpenProfileFolderCommand { get; }

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
        _profile.LaunchArguments = LaunchArguments;
        _profile.WorkspacePath = WorkspacePath;
        _profile.IsEnabled = IsEnabled;
        _profile.IsStandalone = IsStandalone;
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

        var relativePath = _convertToRelativePath(selected);
        if (relativePath is null)
        {
            _dialogService.ShowError(
                "Invalid executable",
                "Choose an executable from the game folder, an enabled mod folder, or the generated profile workspace.");
            return;
        }

        ExecutableRelativePath = relativePath;
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

        ExecutableRelativePath = relative;
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
}
