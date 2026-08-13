using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed class Mo2ImportViewModel : ObservableObject
{
    private readonly Mo2ImportService _importService;
    private readonly DialogService _dialogService;
    private readonly Func<Models.ModProfile, Task<bool>> _commitProfileAsync;
    private Mo2ImportDiscovery? _discovery;
    private Mo2ProfileSource? _selectedMo2Profile;
    private Mo2ImportPreview? _preview;
    private int _step = 1;
    private string _sourcePath = string.Empty;
    private string _gamePath = string.Empty;
    private string _modsPath = string.Empty;
    private string _overwritePath = string.Empty;
    private string _profileName = string.Empty;
    private string _message = "Выберите папку MO2, папку профиля или modlist.txt.";
    private bool _isMessageWarning;
    private bool _includeOverwrite;

    public Mo2ImportViewModel(
        Mo2ImportService importService,
        DialogService dialogService,
        Func<Models.ModProfile, Task<bool>> commitProfileAsync)
    {
        _importService = importService;
        _dialogService = dialogService;
        _commitProfileAsync = commitProfileAsync;
        BrowseSourceFolderCommand = new RelayCommand(BrowseSourceFolder);
        BrowseModListCommand = new RelayCommand(BrowseModList);
        BrowseGameCommand = new RelayCommand(BrowseGame);
        BrowseModsCommand = new RelayCommand(BrowseMods);
        BrowseOverwriteCommand = new RelayCommand(BrowseOverwrite);
        NextCommand = new RelayCommand(CreatePreview, CanCreatePreview);
        BackCommand = new RelayCommand(Back, () => Step == 2);
        ImportCommand = new AsyncRelayCommand(ImportAsync, CanImport);
    }

    public event EventHandler? Completed;

    public ObservableCollection<Mo2ProfileSource> Profiles { get; } = new();

    public int Step
    {
        get => _step;
        private set
        {
            if (SetProperty(ref _step, value))
            {
                OnPropertyChanged(nameof(IsSourceStep));
                OnPropertyChanged(nameof(IsPreviewStep));
                RaiseCommandStates();
            }
        }
    }

    public bool IsSourceStep => Step == 1;
    public bool IsPreviewStep => Step == 2;

    public string SourcePath
    {
        get => _sourcePath;
        private set => SetProperty(ref _sourcePath, value);
    }

    public string GamePath
    {
        get => _gamePath;
        set
        {
            if (SetProperty(ref _gamePath, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string ModsPath
    {
        get => _modsPath;
        set
        {
            if (SetProperty(ref _modsPath, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string OverwritePath
    {
        get => _overwritePath;
        set => SetProperty(ref _overwritePath, value);
    }

    public string ProfileName
    {
        get => _profileName;
        set
        {
            if (SetProperty(ref _profileName, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public Mo2ProfileSource? SelectedMo2Profile
    {
        get => _selectedMo2Profile;
        set
        {
            if (SetProperty(ref _selectedMo2Profile, value))
            {
                if (value is not null)
                {
                    ProfileName = value.Name;
                }

                ReplacePreview(null);
                OnPropertyChanged(nameof(Preview));
                RaiseCommandStates();
            }
        }
    }

    public Mo2ImportPreview? Preview => _preview;

    public bool IncludeOverwrite
    {
        get => _includeOverwrite;
        set => SetProperty(ref _includeOverwrite, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public bool IsMessageWarning
    {
        get => _isMessageWarning;
        private set => SetProperty(ref _isMessageWarning, value);
    }

    public string PreviewSummary => Preview is null
        ? string.Empty
        : $"Найдено модов: {Preview.FoundModCount}; включено: {Preview.EnabledModCount}; " +
          $"отсутствует: {Preview.MissingModCount}; неоднозначно: {Preview.AmbiguousModCount}; " +
          $"разделителей: {Preview.SeparatorCount}.";

    public ICommand BrowseSourceFolderCommand { get; }
    public ICommand BrowseModListCommand { get; }
    public ICommand BrowseGameCommand { get; }
    public ICommand BrowseModsCommand { get; }
    public ICommand BrowseOverwriteCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand ImportCommand { get; }

    public void LoadSource(string path)
    {
        try
        {
            var discovery = _importService.Discover(path);
            _discovery = discovery;
            SourcePath = path;
            GamePath = discovery.GamePath;
            ModsPath = discovery.ModsPath;
            OverwritePath = discovery.OverwritePath;
            Profiles.Clear();
            foreach (var profile in discovery.Profiles)
            {
                Profiles.Add(profile);
            }

            SelectedMo2Profile = discovery.SelectedProfile;
            SetMessage($"Найдено профилей MO2: {Profiles.Count}. Проверьте обнаруженные пути.");
        }
        catch (Exception ex)
        {
            SetMessage(ex.Message, isWarning: true);
        }
    }

    private void BrowseSourceFolder()
    {
        var path = _dialogService.PickFolder("Выберите папку Mod Organizer 2 или профиль MO2", SourcePath);
        if (path is not null)
        {
            LoadSource(path);
        }
    }

    private void BrowseModList()
    {
        var path = _dialogService.PickFile(
            "Выберите modlist.txt из профиля Mod Organizer 2",
            "Mod Organizer mod list (modlist.txt)|modlist.txt|Text files (*.txt)|*.txt");
        if (path is not null)
        {
            LoadSource(path);
        }
    }

    private void BrowseGame()
    {
        var path = _dialogService.PickFolder("Выберите папку базовой игры", GamePath);
        if (path is not null)
        {
            GamePath = path;
        }
    }

    private void BrowseMods()
    {
        var path = _dialogService.PickFolder("Выберите папку mods Mod Organizer 2", ModsPath);
        if (path is not null)
        {
            ModsPath = path;
        }
    }

    private void BrowseOverwrite()
    {
        var path = _dialogService.PickFolder("Выберите папку overwrite Mod Organizer 2", OverwritePath);
        if (path is not null)
        {
            OverwritePath = path;
        }
    }

    private bool CanCreatePreview() =>
        _discovery is not null &&
        SelectedMo2Profile is not null &&
        Directory.Exists(GamePath) &&
        Directory.Exists(ModsPath);

    private void CreatePreview()
    {
        if (_discovery is null || SelectedMo2Profile is null)
        {
            return;
        }

        try
        {
            var preview = _importService.CreatePreview(
                _discovery,
                SelectedMo2Profile,
                GamePath,
                ModsPath,
                OverwritePath);
            ReplacePreview(preview);
            OnPropertyChanged(nameof(Preview));
            OnPropertyChanged(nameof(PreviewSummary));
            IncludeOverwrite = false;
            Step = 2;
            SetMessage(
                preview.AmbiguousModCount > 0
                    ? "Для неоднозначных модов выберите правильную папку в столбце «Папка». До этого профиль создать нельзя."
                    : preview.MissingModCount > 0
                        ? "Некоторые папки модов отсутствуют. Такие моды показаны в предпросмотре и не будут добавлены."
                    : "Проверьте порядок, состояние модов и найденный EXE перед импортом.",
                preview.MissingModCount > 0 || preview.AmbiguousModCount > 0);
        }
        catch (Exception ex)
        {
            SetMessage(ex.Message, isWarning: true);
        }
    }

    private void Back()
    {
        Step = 1;
        SetMessage("Проверьте источник, профиль MO2 и обнаруженные пути.");
    }

    private bool CanImport() =>
        Step == 2 &&
        Preview is not null &&
        !string.IsNullOrWhiteSpace(ProfileName) &&
        Preview.FoundModCount > 0 &&
        Preview.AmbiguousModCount == 0;

    private async Task ImportAsync()
    {
        if (Preview is null)
        {
            return;
        }

        try
        {
            var profile = _importService.CreateProfile(Preview, ProfileName, IncludeOverwrite);
            if (await _commitProfileAsync(profile))
            {
                Completed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            SetMessage(ex.Message, isWarning: true);
        }
    }

    private void SetMessage(string message, bool isWarning = false)
    {
        Message = message;
        IsMessageWarning = isWarning;
    }

    private void ReplacePreview(Mo2ImportPreview? preview)
    {
        if (_preview is not null)
        {
            foreach (var entry in _preview.Entries)
            {
                entry.PropertyChanged -= PreviewEntryOnPropertyChanged;
            }
        }

        _preview = preview;
        if (_preview is not null)
        {
            foreach (var entry in _preview.Entries)
            {
                entry.PropertyChanged += PreviewEntryOnPropertyChanged;
            }
        }
    }

    private void PreviewEntryOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Mo2ImportPreviewEntry.SourcePath))
        {
            return;
        }

        OnPropertyChanged(nameof(PreviewSummary));
        if (Preview?.AmbiguousModCount == 0)
        {
            SetMessage("Папки неоднозначных модов выбраны. Проверьте результат и создайте профиль.");
        }

        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        ((RelayCommand)NextCommand).RaiseCanExecuteChanged();
        ((RelayCommand)BackCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ImportCommand).RaiseCanExecuteChanged();
    }
}
