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
    private readonly ScreenshotScannerService _screenshotScannerService;
    private readonly IScreenshotClipboardService _screenshotClipboardService;
    private readonly ApProCatalogService _apProCatalogService;
    private readonly WindowSystemIntegrationService _windowSystemIntegrationService;
    private readonly LauncherUpdateService _launcherUpdateService;

    public WindowNavigationService(
        DialogService dialogService,
        SettingsStore settingsStore,
        ProfileHealthService profileHealthService,
        WorkspaceManagementService workspaceManagementService,
        ScreenshotScannerService screenshotScannerService,
        IScreenshotClipboardService screenshotClipboardService,
        ApProCatalogService apProCatalogService,
        WindowSystemIntegrationService windowSystemIntegrationService,
        LauncherUpdateService launcherUpdateService)
    {
        _dialogService = dialogService;
        _settingsStore = settingsStore;
        _profileHealthService = profileHealthService;
        _workspaceManagementService = workspaceManagementService;
        _screenshotScannerService = screenshotScannerService;
        _screenshotClipboardService = screenshotClipboardService;
        _apProCatalogService = apProCatalogService;
        _windowSystemIntegrationService = windowSystemIntegrationService;
        _launcherUpdateService = launcherUpdateService;
    }

    public void ShowProfileCreation(Window owner, MainViewModel mainViewModel)
    {
        var wizard = new ProfileCreationWindow(CreateProfileCreationViewModel()) { Owner = owner };
        if (wizard.ShowDialog() == true && wizard.CreatedProfile is not null)
        {
            mainViewModel.AddCreatedProfile(wizard.CreatedProfile);
        }
    }

    public ProfileCreationViewModel CreateProfileCreationViewModel() => new(_dialogService);

    public void ShowProfileSettings(Window owner, ProfileSettingsViewModel viewModel)
    {
        new ProfileSettingsWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    public void ShowScreenshots(Window owner, ModProfile profile)
    {
        var viewModel = new ScreenshotsViewModel(profile, _screenshotScannerService, _screenshotClipboardService);
        new ScreenshotsWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    public ScreenshotsViewModel CreateScreenshotsViewModel(ModProfile profile) =>
        new(profile, _screenshotScannerService, _screenshotClipboardService);

    public void ShowModCatalog(Window owner)
    {
        var viewModel = new ModCatalogViewModel(_apProCatalogService, _dialogService);
        new ModCatalogWindow(viewModel, _windowSystemIntegrationService) { Owner = owner }.ShowDialog();
    }

    public ModCatalogViewModel CreateModCatalogViewModel() =>
        new(_apProCatalogService, _dialogService);

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

    public void OpenUrl(string url) => _dialogService.OpenUrl(url);

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
            _windowSystemIntegrationService,
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
