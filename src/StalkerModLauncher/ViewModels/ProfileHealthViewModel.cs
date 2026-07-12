using System.Collections.ObjectModel;
using System.Windows.Input;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed class ProfileHealthViewModel : ObservableObject, IDisposable
{
    private readonly ModProfile _profile;
    private readonly ProfileHealthService _healthService;
    private readonly DialogService _dialogService;
    private readonly WorkspaceManagementService _workspaceManagementService;
    private readonly Action<string>? _log;
    private ProfileHealthReport? _report;
    private string _summary = "Проверка состояния профиля...";
    private bool _isChecking;
    private WorkspaceStatus? _workspace;
    private CancellationTokenSource? _refreshCancellation;

    public ProfileHealthViewModel(
        ModProfile profile,
        ProfileHealthService healthService,
        DialogService dialogService,
        WorkspaceManagementService workspaceManagementService,
        Action<string>? log = null)
    {
        _profile = profile;
        _healthService = healthService;
        _dialogService = dialogService;
        _workspaceManagementService = workspaceManagementService;
        _log = log;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsChecking);
        OpenProfileCommand = new RelayCommand(OpenProfile, () => Directory.Exists(_report?.ProfileFolderPath));
        OpenSavesCommand = new RelayCommand(OpenSaves, () => Directory.Exists(_report?.SavedGamesPath));
        OpenLatestLogCommand = new RelayCommand(OpenLatestLog, () => File.Exists(_report?.LatestLogPath));
        OpenCrashDumpCommand = new RelayCommand(OpenCrashDump, () => File.Exists(_report?.LatestCrashDumpPath));
        CopyReportCommand = new RelayCommand(CopyReport, () => _report is not null);
        ClearWorkspaceCommand = new RelayCommand(ClearWorkspace, () => CanManageWorkspace && UsesLinkedWorkspace);
        RebuildWorkspaceCommand = new AsyncRelayCommand(RebuildWorkspaceAsync, () => CanManageWorkspace && UsesLinkedWorkspace && !IsChecking);
        MoveWorkspaceCommand = new AsyncRelayCommand(MoveWorkspaceAsync, () => CanManageWorkspace && !IsChecking);

        _ = RefreshAsync();
    }

    public string ProfileName => _profile.Name;
    public ObservableCollection<ProfileHealthCheck> Checks { get; } = new();

    public string ProfileKind => _profile.IsStandalone
        ? "Автономная игра или мод"
        : "Мод поверх базовой игры";

    public string PreflightExplanation =>
        "Ошибки остановят запуск. Предупреждения подскажут, что стоит проверить.";

    public WorkspaceStatus? Workspace
    {
        get => _workspace;
        private set
        {
            if (SetProperty(ref _workspace, value))
            {
                OnPropertyChanged(nameof(CanManageWorkspace));
                RaiseStorageProperties();
            }
        }
    }

    public bool ShowStoragePanel => !_profile.IsStandalone;
    public bool CanManageWorkspace => ShowStoragePanel && !string.IsNullOrWhiteSpace(_profile.WorkspacePath);
    public bool UsesLinkedWorkspace => !_profile.IsStandalone && _profile.LaunchBackendKind == LaunchBackendKind.LinkedWorkspace;
    public bool UsesVirtualFileSystem => !_profile.IsStandalone && _profile.LaunchBackendKind == LaunchBackendKind.VirtualFileSystem;
    public string StoragePanelTitle => UsesVirtualFileSystem ? "USVFS" : "Workspace";
    public string StorageStateDisplay => UsesVirtualFileSystem
        ? "Файлы игры и модов подключаются виртуально. Папка current не создаётся."
        : Workspace?.StateDisplay ?? "Состояние рабочей папки ещё не получено.";
    public string FirstMetricTitle => UsesVirtualFileSystem ? "Слои" : "Видимый размер";
    public string FirstMetricValue => UsesVirtualFileSystem
        ? $"{1 + _profile.Mods.Count(mod => mod.IsEnabled):N0}"
        : Workspace?.LogicalSizeDisplay ?? "—";
    public string FirstMetricToolTip => UsesVirtualFileSystem
        ? "Базовая игра и включённые моды, которые USVFS объединит при запуске."
        : "Сколько данных видит игра внутри рабочей папки. Из-за ссылок это не равно расходу места на диске.";
    public string SecondMetricTitle => UsesVirtualFileSystem ? "Профильные данные" : "Реально занято";
    public string SecondMetricValue => UsesVirtualFileSystem
        ? ProfileDataStateDisplay
        : Workspace?.PhysicalSizeDisplay ?? "—";
    public string SecondMetricToolTip => UsesVirtualFileSystem
        ? "Сохранения, настройки и логи хранятся отдельно в userdata профиля."
        : "Сколько места примерно занимает workspace с учетом hardlink и symlink.";
    public string ThirdMetricTitle => UsesVirtualFileSystem ? "Папка current" : "Файлы";
    public string ThirdMetricValue => UsesVirtualFileSystem ? "не используется" : Workspace?.FileCountDisplay ?? "—";
    public string ThirdMetricToolTip => UsesVirtualFileSystem
        ? "USVFS формирует представление игры в памяти процесса и не собирает папку current."
        : Workspace?.LinkSummaryDisplay ?? string.Empty;

    private string ProfileDataStateDisplay => !string.IsNullOrWhiteSpace(_profile.WorkspacePath) &&
                                              Directory.Exists(Path.Combine(_profile.WorkspacePath, "userdata"))
        ? "созданы"
        : "при запуске";

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public bool IsChecking
    {
        get => _isChecking;
        private set
        {
            if (SetProperty(ref _isChecking, value))
            {
                ((AsyncRelayCommand)RefreshCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)RebuildWorkspaceCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)MoveWorkspaceCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenProfileCommand { get; }
    public ICommand OpenSavesCommand { get; }
    public ICommand OpenLatestLogCommand { get; }
    public ICommand OpenCrashDumpCommand { get; }
    public ICommand CopyReportCommand { get; }
    public ICommand ClearWorkspaceCommand { get; }
    public ICommand RebuildWorkspaceCommand { get; }
    public ICommand MoveWorkspaceCommand { get; }

    private async Task RefreshAsync()
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = new CancellationTokenSource();

        try
        {
            IsChecking = true;
            Summary = "Проверка состояния профиля...";
            var report = await _healthService.AnalyzeAsync(_profile, _refreshCancellation.Token);
            _report = report;
            Workspace = report.Workspace;
            RaiseStorageProperties();
            Checks.Clear();
            foreach (var check in report.Checks)
            {
                Checks.Add(check);
            }

            Summary = report.Summary;
            RaiseCommandStates();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Summary = $"Проверка не выполнена: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }

    public void Dispose()
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = null;
    }

    private void OpenProfile()
    {
        RunAction(() => _dialogService.OpenFolder(_report!.ProfileFolderPath));
    }

    private void OpenSaves()
    {
        RunAction(() => _dialogService.OpenFolder(_report!.SavedGamesPath));
    }

    private void OpenLatestLog()
    {
        RunAction(() => _dialogService.OpenFileLocation(_report!.LatestLogPath!));
    }

    private void OpenCrashDump()
    {
        RunAction(() => _dialogService.OpenFileLocation(_report!.LatestCrashDumpPath!));
    }

    private void CopyReport()
    {
        RunAction(() => _dialogService.CopyText(_report!.ToText(_profile.Name)));
    }

    private void ClearWorkspace()
    {
        if (!_dialogService.Confirm(
                "Очистить кэш workspace",
                "Удалить только подготовленную папку current? Сохранения, настройки и логи в userdata останутся на месте."))
        {
            return;
        }

        try
        {
            _workspaceManagementService.ClearCache(_profile, new Progress<string>(ReportWorkspaceProgress));
            Log($"Кэш workspace очищен из окна «Состояние»: {_profile.Name}");
        }
        catch (Exception ex)
        {
            Log($"Очистка кэша workspace не выполнена: {ex.Message}");
            _dialogService.ShowError("Не удалось очистить кэш workspace", ex.Message);
            return;
        }

        _ = RefreshAsync();
    }

    private async Task RebuildWorkspaceAsync()
    {
        try
        {
            IsChecking = true;
            Summary = "Пересборка workspace...";
            Log($"Пересборка workspace запущена из окна «Состояние»: {_profile.Name}");
            var progress = new Progress<string>(ReportWorkspaceProgress);
            await _workspaceManagementService.RebuildAsync(_profile, progress);
            Log($"Пересборка workspace завершена из окна «Состояние»: {_profile.Name}");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Log($"Пересборка workspace не выполнена: {ex.Message}");
            _dialogService.ShowError("Не удалось пересобрать workspace", ex.Message);
        }
        finally
        {
            IsChecking = false;
        }
    }

    private async Task MoveWorkspaceAsync()
    {
        var destination = _dialogService.PickFolder("Выберите папку, в которой будет храниться workspace");
        if (destination is null)
        {
            return;
        }

        if (!_dialogService.Confirm(
                UsesVirtualFileSystem ? "Перенести данные профиля" : "Перенести workspace",
                UsesVirtualFileSystem
                    ? $"Перенести сохранения, настройки и логи в выбранную папку?{Environment.NewLine}{Environment.NewLine}Временный USVFS-bootstrap не копируется и будет создан заново."
                    : $"Перенести userdata профиля в выбранную папку?{Environment.NewLine}{Environment.NewLine}Папка current не копируется и будет пересобрана при следующем запуске."))
        {
            return;
        }

        try
        {
            IsChecking = true;
            Log($"Перенос workspace запущен из окна «Состояние»: {_profile.Name}");
            var progress = new Progress<string>(ReportWorkspaceProgress);
            await _workspaceManagementService.MoveAsync(_profile, destination, progress);
            Log($"Workspace перенесён из окна «Состояние»: {_profile.Name}");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Log($"Перенос workspace не выполнен: {ex.Message}");
            _dialogService.ShowError("Не удалось перенести workspace", ex.Message);
        }
        finally
        {
            IsChecking = false;
        }
    }

    private void RunAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Состояние профиля", ex.Message);
        }
    }

    private void ReportWorkspaceProgress(string message)
    {
        Summary = message;
        Log(message);
    }

    private void Log(string message) => _log?.Invoke(message);

    private void RaiseCommandStates()
    {
        ((RelayCommand)OpenProfileCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenSavesCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenLatestLogCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenCrashDumpCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CopyReportCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ClearWorkspaceCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)RebuildWorkspaceCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)MoveWorkspaceCommand).RaiseCanExecuteChanged();
    }

    private void RaiseStorageProperties()
    {
        OnPropertyChanged(nameof(StorageStateDisplay));
        OnPropertyChanged(nameof(FirstMetricValue));
        OnPropertyChanged(nameof(SecondMetricValue));
        OnPropertyChanged(nameof(ThirdMetricValue));
    }
}
