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
    private readonly ProfileVirtualFileDiagnosticsService _profileVirtualFileDiagnosticsService;
    private readonly WorkspaceManagementService _workspaceManagementService;
    private readonly ScreenshotScannerService _screenshotScannerService;
    private readonly IScreenshotClipboardService _screenshotClipboardService;
    private readonly ApProCatalogService _apProCatalogService;
    private readonly WindowSystemIntegrationService _windowSystemIntegrationService;

    public WindowNavigationService(
        DialogService dialogService,
        SettingsStore settingsStore,
        ProfileHealthService profileHealthService,
        ProfileVirtualFileDiagnosticsService profileVirtualFileDiagnosticsService,
        WorkspaceManagementService workspaceManagementService,
        ScreenshotScannerService screenshotScannerService,
        IScreenshotClipboardService screenshotClipboardService,
        ApProCatalogService apProCatalogService,
        WindowSystemIntegrationService windowSystemIntegrationService)
    {
        _dialogService = dialogService;
        _settingsStore = settingsStore;
        _profileHealthService = profileHealthService;
        _profileVirtualFileDiagnosticsService = profileVirtualFileDiagnosticsService;
        _workspaceManagementService = workspaceManagementService;
        _screenshotScannerService = screenshotScannerService;
        _screenshotClipboardService = screenshotClipboardService;
        _apProCatalogService = apProCatalogService;
        _windowSystemIntegrationService = windowSystemIntegrationService;
    }

    public void ShowProfileCreation(Window owner, MainViewModel mainViewModel)
    {
        var wizard = new ProfileCreationWindow(new ProfileCreationViewModel(_dialogService)) { Owner = owner };
        if (wizard.ShowDialog() == true && wizard.CreatedProfile is not null)
        {
            mainViewModel.AddCreatedProfile(wizard.CreatedProfile);
        }
    }

    public void ShowProfileSettings(Window owner, ProfileSettingsViewModel viewModel)
    {
        new ProfileSettingsWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    public void ShowScreenshots(Window owner, ModProfile profile)
    {
        var viewModel = new ScreenshotsViewModel(profile, _screenshotScannerService, _screenshotClipboardService);
        new ScreenshotsWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    public void ShowModCatalog(Window owner)
    {
        var viewModel = new ModCatalogViewModel(_apProCatalogService, _dialogService);
        new ModCatalogWindow(viewModel, _windowSystemIntegrationService) { Owner = owner }.ShowDialog();
    }

    public void ShowProfileHealth(Window owner, ModProfile profile, Action<string>? log = null)
    {
        var viewModel = new ProfileHealthViewModel(
            profile,
            _profileHealthService,
            _profileVirtualFileDiagnosticsService,
            _dialogService,
            _workspaceManagementService,
            log);
        new ProfileHealthWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    public async Task ShowAboutAsync(Window? owner = null, bool onlyIfNeeded = false)
    {
        var settings = await _settingsStore.LoadAsync();
        if (onlyIfNeeded && settings.DontShowAboutOnStartup)
        {
            return;
        }

        var aboutWindow = new AboutWindow
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
