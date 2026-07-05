using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Services;

public sealed class AppServices
{
    public AppServices()
    {
        Paths = new AppPaths();
        SettingsStore = new SettingsStore(Paths);
        DialogService = new DialogService();
        WindowSystemIntegrationService = new WindowSystemIntegrationService();

        var workspaceBuilder = new WorkspaceBuilder(Paths);
        WorkspaceManagementService = new WorkspaceManagementService(workspaceBuilder);
        ProfileManager = new ProfileManager(Paths, workspaceBuilder);
        LaunchCoordinator = new LaunchCoordinator(
            new ProfileLauncher(
                [
                    new LinkedWorkspaceLaunchBackend(workspaceBuilder)
                ],
                profileManager: ProfileManager),
            new GameSessionTracker());
        GameValidator = new GameInstallationValidator();
        ProfileReadinessService = new ProfileReadinessService(GameValidator);
        LaunchPreflightService = new LaunchPreflightService(GameValidator, ProfileManager);
        ApplicationLogService = new ApplicationLogService(Paths);
        ModConflictAnalyzer = new ModConflictAnalyzer();
        ProfileTransferService = new ProfileTransferService();
        ModScannerService = new ModScannerService();
        ModListEditor = new ModListEditor();
        ProfileDataPathResolver = new ProfileDataPathResolver();
        ScreenshotScannerService = new ScreenshotScannerService(ProfileDataPathResolver);
        ScreenshotClipboardService = new ScreenshotClipboardService();
        ApProCatalogService = new ApProCatalogService();
        GameExitDiagnosticsService = new GameExitDiagnosticsService(ProfileDataPathResolver);
        ProfileHealthService = new ProfileHealthService(GameValidator, ProfileManager, ProfileDataPathResolver, WorkspaceManagementService);
        ProfileVirtualFileDiagnosticsService = new ProfileVirtualFileDiagnosticsService(ProfileManager);
        WindowNavigationService = new WindowNavigationService(
            DialogService,
            SettingsStore,
            ProfileHealthService,
            ProfileVirtualFileDiagnosticsService,
            WorkspaceManagementService,
            ScreenshotScannerService,
            ScreenshotClipboardService,
            ApProCatalogService,
            WindowSystemIntegrationService);
    }

    public AppPaths Paths { get; }
    public SettingsStore SettingsStore { get; }
    public DialogService DialogService { get; }
    public WindowSystemIntegrationService WindowSystemIntegrationService { get; }
    public ProfileManager ProfileManager { get; }
    public LaunchCoordinator LaunchCoordinator { get; }
    public GameInstallationValidator GameValidator { get; }
    public ProfileReadinessService ProfileReadinessService { get; }
    public LaunchPreflightService LaunchPreflightService { get; }
    public WorkspaceManagementService WorkspaceManagementService { get; }
    public ApplicationLogService ApplicationLogService { get; }
    public ModConflictAnalyzer ModConflictAnalyzer { get; }
    public ProfileTransferService ProfileTransferService { get; }
    public ModScannerService ModScannerService { get; }
    public ModListEditor ModListEditor { get; }
    public GameExitDiagnosticsService GameExitDiagnosticsService { get; }
    public ProfileHealthService ProfileHealthService { get; }
    public ProfileVirtualFileDiagnosticsService ProfileVirtualFileDiagnosticsService { get; }
    public ProfileDataPathResolver ProfileDataPathResolver { get; }
    public ScreenshotScannerService ScreenshotScannerService { get; }
    public ScreenshotClipboardService ScreenshotClipboardService { get; }
    public ApProCatalogService ApProCatalogService { get; }
    public WindowNavigationService WindowNavigationService { get; }

    public MainViewModel CreateMainViewModel()
    {
        return new MainViewModel(
            Paths,
            SettingsStore,
            LaunchCoordinator,
            DialogService,
            ModConflictAnalyzer,
            ProfileTransferService,
            ModScannerService,
            ModListEditor,
            ProfileManager,
            GameExitDiagnosticsService,
            ProfileReadinessService,
            LaunchPreflightService,
            ApplicationLogService);
    }
}
