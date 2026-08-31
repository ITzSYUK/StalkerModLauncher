using System.Net.Http;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed class LauncherSettingsViewModel : ObservableObject
{
    private readonly string _settingsDirectory;
    private readonly Func<LauncherPreferences, Task> _save;
    private readonly DialogService _dialogService;
    private readonly Func<Task<LauncherUpdateResult>>? _checkForUpdates;
    private readonly Func<bool> _confirmReset;
    private bool _showTrayIcon;
    private bool _startWithWindows;
    private bool _startMinimizedToTrayOnWindowsStartup;
    private bool _isPdaInterfaceEnabled;
    private bool _minimizeToTrayOnClose;
    private bool _autoCheckForUpdates;
    private bool _showUpdateNotifications;
    private LauncherLogLevel _logLevel;
    private bool _isSaving;
    private string _updateStatus = string.Empty;
    private string? _releaseUrl;

    public LauncherSettingsViewModel(
        LauncherPreferences preferences,
        string settingsDirectory,
        Func<LauncherPreferences, Task> save,
        DialogService dialogService,
        Func<Task<LauncherUpdateResult>>? checkForUpdates = null,
        Func<bool>? confirmReset = null)
    {
        _isPdaInterfaceEnabled = preferences.IsPdaInterfaceEnabled;
        _showTrayIcon = preferences.ShowTrayIcon;
        _startWithWindows = preferences.StartWithWindows;
        _startMinimizedToTrayOnWindowsStartup =
            preferences.ShowTrayIcon && preferences.StartMinimizedToTrayOnWindowsStartup;
        _minimizeToTrayOnClose = preferences.ShowTrayIcon && preferences.MinimizeToTrayOnClose;
        _autoCheckForUpdates = preferences.AutoCheckForUpdates;
        _showUpdateNotifications = preferences.ShowUpdateNotifications;
        _logLevel = preferences.LogLevel;
        _settingsDirectory = settingsDirectory;
        _save = save;
        _dialogService = dialogService;
        _checkForUpdates = checkForUpdates;
        _confirmReset = confirmReset ?? (() => DialogService.Confirm(
            "Сбросить настройки лаунчера?",
            "Будут восстановлены настройки интерфейса, поведения, журналирования и обновлений.\n\n" +
            "Профили, моды и игровые файлы останутся без изменений."));
        OpenSettingsFolderCommand = new RelayCommand(OpenSettingsFolder);
        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, () => _checkForUpdates is not null);
        OpenReleaseCommand = new RelayCommand(OpenRelease, () => HasAvailableUpdate);
        ResetCommand = new RelayCommand(ResetToDefaults);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (SetProperty(ref _startWithWindows, value))
            {
                OnPropertyChanged(nameof(CanStartMinimizedToTray));
            }
        }
    }

    public bool ShowTrayIcon
    {
        get => _showTrayIcon;
        set
        {
            if (!SetProperty(ref _showTrayIcon, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanUseTray));
            OnPropertyChanged(nameof(CanStartMinimizedToTray));
            if (!value)
            {
                StartMinimizedToTrayOnWindowsStartup = false;
                MinimizeToTrayOnClose = false;
            }
        }
    }

    public bool CanUseTray => ShowTrayIcon;

    public bool CanStartMinimizedToTray => ShowTrayIcon && StartWithWindows;

    public bool StartMinimizedToTrayOnWindowsStartup
    {
        get => _startMinimizedToTrayOnWindowsStartup;
        set => SetProperty(ref _startMinimizedToTrayOnWindowsStartup, value);
    }

    public bool UseClassicInterface
    {
        get => !_isPdaInterfaceEnabled;
        set
        {
            if (value)
            {
                SetPdaInterface(false);
            }
        }
    }

    public bool UsePdaInterface
    {
        get => _isPdaInterfaceEnabled;
        set
        {
            if (value)
            {
                SetPdaInterface(true);
            }
        }
    }

    public bool MinimizeToTrayOnClose
    {
        get => _minimizeToTrayOnClose;
        set => SetProperty(ref _minimizeToTrayOnClose, value);
    }

    public bool AutoCheckForUpdates
    {
        get => _autoCheckForUpdates;
        set => SetProperty(ref _autoCheckForUpdates, value);
    }

    public bool ShowUpdateNotifications
    {
        get => _showUpdateNotifications;
        set => SetProperty(ref _showUpdateNotifications, value);
    }

    public LauncherLogLevel LogLevel
    {
        get => _logLevel;
        set => SetProperty(ref _logLevel, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set => SetProperty(ref _isSaving, value);
    }

    public string SettingsDirectory => _settingsDirectory;
    public string UpdateStatus
    {
        get => _updateStatus;
        private set => SetProperty(ref _updateStatus, value);
    }

    public bool HasAvailableUpdate => !string.IsNullOrWhiteSpace(_releaseUrl);
    public RelayCommand OpenSettingsFolderCommand { get; }
    public AsyncRelayCommand CheckForUpdatesCommand { get; }
    public RelayCommand OpenReleaseCommand { get; }
    public RelayCommand ResetCommand { get; }

    public async Task CheckForUpdatesAsync()
    {
        if (_checkForUpdates is null)
        {
            return;
        }

        SetReleaseUrl(null);
        UpdateStatus = "Проверяем GitHub...";

        try
        {
            var result = await _checkForUpdates();
            if (result.IsUpdateAvailable)
            {
                SetReleaseUrl(result.ReleaseUrl);
                UpdateStatus = $"Доступна версия {result.LatestVersion}. Установлена {result.CurrentVersion}.";
            }
            else
            {
                UpdateStatus = $"Установлена актуальная версия лаунчера: {result.CurrentVersion}.";
            }
        }
        catch (TaskCanceledException)
        {
            UpdateStatus = "GitHub не ответил вовремя. Проверьте подключение к интернету.";
        }
        catch (HttpRequestException)
        {
            UpdateStatus = "Не удалось подключиться к GitHub. Проверьте подключение к интернету.";
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Не удалось проверить обновления: {ex.Message}";
        }
    }

    public async Task<bool> TrySaveAsync()
    {
        if (IsSaving)
        {
            return false;
        }

        IsSaving = true;
        try
        {
            await _save(new LauncherPreferences(
                _isPdaInterfaceEnabled,
                ShowTrayIcon,
                StartWithWindows,
                StartMinimizedToTrayOnWindowsStartup,
                MinimizeToTrayOnClose,
                AutoCheckForUpdates,
                ShowUpdateNotifications,
                LogLevel));
            return true;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Не удалось сохранить настройки лаунчера", ex.Message);
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void OpenSettingsFolder()
    {
        try
        {
            Directory.CreateDirectory(_settingsDirectory);
            DialogService.OpenFolder(_settingsDirectory);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Не удалось открыть папку настроек", ex.Message);
        }
    }

    private void ResetToDefaults()
    {
        if (!_confirmReset())
        {
            return;
        }

        var defaults = LauncherPreferences.Default;
        SetPdaInterface(defaults.IsPdaInterfaceEnabled);
        ShowTrayIcon = defaults.ShowTrayIcon;
        StartWithWindows = defaults.StartWithWindows;
        StartMinimizedToTrayOnWindowsStartup = defaults.StartMinimizedToTrayOnWindowsStartup;
        MinimizeToTrayOnClose = defaults.MinimizeToTrayOnClose;
        AutoCheckForUpdates = defaults.AutoCheckForUpdates;
        ShowUpdateNotifications = defaults.ShowUpdateNotifications;
        LogLevel = defaults.LogLevel;
    }

    private void OpenRelease()
    {
        if (string.IsNullOrWhiteSpace(_releaseUrl))
        {
            return;
        }

        try
        {
            DialogService.OpenUrl(_releaseUrl);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Не удалось открыть страницу релиза", ex.Message);
        }
    }

    private void SetReleaseUrl(string? value)
    {
        _releaseUrl = value;
        OnPropertyChanged(nameof(HasAvailableUpdate));
        OpenReleaseCommand.RaiseCanExecuteChanged();
    }

    private void SetPdaInterface(bool value)
    {
        if (_isPdaInterfaceEnabled == value)
        {
            return;
        }

        _isPdaInterfaceEnabled = value;
        OnPropertyChanged(nameof(UseClassicInterface));
        OnPropertyChanged(nameof(UsePdaInterface));
    }
}
