using System.Collections.ObjectModel;
using System.Windows.Input;
using StalkerModLauncher.Infrastructure;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;

namespace StalkerModLauncher.ViewModels;

public sealed class ProfileHealthViewModel : ObservableObject
{
    private readonly ModProfile _profile;
    private readonly string _defaultGamePath;
    private readonly ProfileHealthService _healthService;
    private readonly DialogService _dialogService;
    private ProfileHealthReport? _report;
    private string _summary = "Проверка состояния профиля...";
    private bool _isChecking;

    public ProfileHealthViewModel(
        ModProfile profile,
        string defaultGamePath,
        ProfileHealthService healthService,
        DialogService dialogService)
    {
        _profile = profile;
        _defaultGamePath = defaultGamePath;
        _healthService = healthService;
        _dialogService = dialogService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsChecking);
        OpenWorkspaceCommand = new RelayCommand(OpenWorkspace, () => Directory.Exists(_report?.WorkspacePath));
        OpenSavesCommand = new RelayCommand(OpenSaves, () => Directory.Exists(_report?.SavedGamesPath));
        OpenLatestLogCommand = new RelayCommand(OpenLatestLog, () => File.Exists(_report?.LatestLogPath));
        OpenCrashDumpCommand = new RelayCommand(OpenCrashDump, () => File.Exists(_report?.LatestCrashDumpPath));
        CopyReportCommand = new RelayCommand(CopyReport, () => _report is not null);

        _ = RefreshAsync();
    }

    public string ProfileName => _profile.Name;
    public ObservableCollection<ProfileHealthCheck> Checks { get; } = new();

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
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenWorkspaceCommand { get; }
    public ICommand OpenSavesCommand { get; }
    public ICommand OpenLatestLogCommand { get; }
    public ICommand OpenCrashDumpCommand { get; }
    public ICommand CopyReportCommand { get; }

    private async Task RefreshAsync()
    {
        try
        {
            IsChecking = true;
            Summary = "Проверка состояния профиля...";
            var report = await _healthService.AnalyzeAsync(_profile, _defaultGamePath);
            _report = report;
            Checks.Clear();
            foreach (var check in report.Checks)
            {
                Checks.Add(check);
            }

            Summary = report.Summary;
            RaiseCommandStates();
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

    private void OpenWorkspace()
    {
        RunAction(() => _dialogService.OpenFolder(_report!.WorkspacePath));
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
        ((RelayCommand)OpenWorkspaceCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenSavesCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenLatestLogCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenCrashDumpCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CopyReportCommand).RaiseCanExecuteChanged();
    }
}
