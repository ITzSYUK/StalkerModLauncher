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
    private ProfileHealthReport? _report;
    private string _summary = "Проверка состояния профиля...";
    private bool _isChecking;
    private WorkspaceStatus? _workspace;
    private CancellationTokenSource? _refreshCancellation;

    public ProfileHealthViewModel(
        ModProfile profile,
        ProfileHealthService healthService,
        DialogService dialogService,
        WorkspaceManagementService workspaceManagementService)
    {
        _profile = profile;
        _healthService = healthService;
        _dialogService = dialogService;
        _workspaceManagementService = workspaceManagementService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsChecking);
        OpenProfileCommand = new RelayCommand(OpenProfile, () => Directory.Exists(_report?.ProfileFolderPath));
        OpenSavesCommand = new RelayCommand(OpenSaves, () => Directory.Exists(_report?.SavedGamesPath));
        OpenLatestLogCommand = new RelayCommand(OpenLatestLog, () => File.Exists(_report?.LatestLogPath));
        OpenCrashDumpCommand = new RelayCommand(OpenCrashDump, () => File.Exists(_report?.LatestCrashDumpPath));
        CopyReportCommand = new RelayCommand(CopyReport, () => _report is not null);
        ClearWorkspaceCommand = new RelayCommand(ClearWorkspace, () => CanManageWorkspace);
        RebuildWorkspaceCommand = new AsyncRelayCommand(RebuildWorkspaceAsync, () => CanManageWorkspace && !IsChecking);
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
            }
        }
    }

    public bool CanManageWorkspace => !_profile.IsStandalone && !string.IsNullOrWhiteSpace(_profile.WorkspacePath);

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

        RunAction(() => _workspaceManagementService.ClearCache(_profile));
        _ = RefreshAsync();
    }

    private async Task RebuildWorkspaceAsync()
    {
        try
        {
            IsChecking = true;
            Summary = "Пересборка workspace...";
            var progress = new Progress<string>(message => Summary = message);
            await _workspaceManagementService.RebuildAsync(_profile, progress);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
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
                "Перенести workspace",
                $"Перенести userdata профиля в выбранную папку?{Environment.NewLine}{Environment.NewLine}Папка current не копируется и будет пересобрана при следующем запуске."))
        {
            return;
        }

        try
        {
            IsChecking = true;
            var progress = new Progress<string>(message => Summary = message);
            await _workspaceManagementService.MoveAsync(_profile, destination, progress);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
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
}
