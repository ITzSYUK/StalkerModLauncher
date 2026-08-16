using System.Windows;
using StalkerModLauncher.Models;
using StalkerModLauncher.ViewModels;
using StalkerModLauncher.Views;

namespace StalkerModLauncher.Services;

public sealed class WindowNavigationService
{
    private readonly DialogService _dialogService;
    private readonly SettingsStore _settingsStore;
    private readonly ProfileHealthService _profileHealthService;
    private readonly WorkspaceManagementService _workspaceManagementService;
    private readonly IScreenshotClipboardService _screenshotClipboardService;
    private readonly ApProCatalogService _apProCatalogService;
    private readonly LauncherUpdateService _launcherUpdateService;

    public WindowNavigationService(
        DialogService dialogService,
        SettingsStore settingsStore,
        ProfileHealthService profileHealthService,
        WorkspaceManagementService workspaceManagementService,
        IScreenshotClipboardService screenshotClipboardService,
        ApProCatalogService apProCatalogService,
        LauncherUpdateService launcherUpdateService)
    {
        _dialogService = dialogService;
        _settingsStore = settingsStore;
        _profileHealthService = profileHealthService;
        _workspaceManagementService = workspaceManagementService;
        _screenshotClipboardService = screenshotClipboardService;
        _apProCatalogService = apProCatalogService;
        _launcherUpdateService = launcherUpdateService;
    }

    public static void ShowProfileCreation(Window owner, MainViewModel mainViewModel)
    {
        var wizard = new ProfileCreationWindow(CreateProfileCreationViewModel())
        { Owner = owner };
        if (wizard.ShowDialog() == true && wizard.CreatedProfile is not null)
        {
            mainViewModel.AddCreatedProfile(wizard.CreatedProfile);
        }
    }

    public static ProfileCreationViewModel CreateProfileCreationViewModel() => new();

    public void ShowMo2Import(Window owner, MainViewModel mainViewModel)
    {
        try
        {
            var window = new Mo2ImportWindow(mainViewModel.CreateMo2ImportViewModel())
            {
                Owner = owner
            };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(
                "Не удалось открыть импорт MO2",
                $"Мастер не был открыт. Лаунчер продолжит работу.{Environment.NewLine}{Environment.NewLine}{ex.Message}");
        }
    }

    public static void ShowProfileSettings(Window owner, ProfileSettingsViewModel viewModel)
    {
        new ProfileSettingsWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    public static void ShowConflictExplorer(Window owner, ConflictExplorerViewModel viewModel)
    {
        new ConflictExplorerWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    public void ShowScreenshots(Window owner, ModProfile profile)
    {
        var viewModel = new ScreenshotsViewModel(profile, _screenshotClipboardService);
        new ScreenshotsWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    public ScreenshotsViewModel CreateScreenshotsViewModel(ModProfile profile) =>
        new(profile, _screenshotClipboardService);

    public void ShowModCatalog(Window owner)
    {
        var viewModel = new ModCatalogViewModel(_apProCatalogService);
        new ModCatalogWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    public ModCatalogViewModel CreateModCatalogViewModel() =>
        new(_apProCatalogService);

    public void ShowProfileHealth(Window owner, ModProfile profile, Action<string>? log = null)
    {
        var viewModel = new ProfileHealthViewModel(
            profile,
            _profileHealthService,
            _dialogService,
            _workspaceManagementService,
            log);
        new ProfileHealthWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    public ProfileHealthViewModel CreateProfileHealthViewModel(ModProfile profile, Action<string>? log = null) =>
        new(profile, _profileHealthService, _dialogService, _workspaceManagementService, log);

    public Task<LauncherUpdateResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
        _launcherUpdateService.CheckAsync(cancellationToken);

    public static void OpenUrl(string url) => DialogService.OpenUrl(url);

    public async Task ShowAboutAsync(Window? owner = null, bool onlyIfNeeded = false)
    {
        var settings = await _settingsStore.LoadAsync();
        if (onlyIfNeeded && settings.DontShowAboutOnStartup)
        {
            return;
        }

        var aboutWindow = new AboutWindow(
            _launcherUpdateService,
            _dialogService,
            owner?.DataContext is MainViewModel mainViewModel
                ? () => mainViewModel.ToggleInterfaceCommand.Execute(null)
                : null)
        {
            DontShowAgain = settings.DontShowAboutOnStartup,
            Owner = owner
        };
        aboutWindow.ShowDialog();

        if (aboutWindow.DontShowAgain != settings.DontShowAboutOnStartup)
        {
            await _settingsStore.UpdateAsync(current =>
            {
                current.DontShowAboutOnStartup = aboutWindow.DontShowAgain;
                return current;
            });
        }
    }
}
