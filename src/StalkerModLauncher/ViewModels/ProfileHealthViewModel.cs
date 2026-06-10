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
    private ProfileHealthReport? _report;
    private string _summary = "Проверка состояния профиля...";
    private bool _isChecking;
    private CancellationTokenSource? _refreshCancellation;

    public ProfileHealthViewModel(
        ModProfile profile,
        ProfileHealthService healthService,
        DialogService dialogService)
    {
        _profile = profile;
        _healthService = healthService;
        _dialogService = dialogService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsChecking);
        OpenProfileCommand = new RelayCommand(OpenProfile, () => Directory.Exists(_report?.ProfileFolderPath));
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
    public ICommand OpenProfileCommand { get; }
    public ICommand OpenSavesCommand { get; }
    public ICommand OpenLatestLogCommand { get; }
    public ICommand OpenCrashDumpCommand { get; }
    public ICommand CopyReportCommand { get; }

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
    }
}
