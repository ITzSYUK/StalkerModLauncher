using StalkerModLauncher.ViewModels;

namespace StalkerModLauncher.Services;

public sealed class AppServices
{
    public AppServices()
    {
        Paths = new AppPaths();
        SettingsStore = new SettingsStore(Paths);
        DialogService = new DialogService();

        var workspaceBuilder = new WorkspaceBuilder(Paths);
        ProfileManager = new ProfileManager(Paths, workspaceBuilder);
        LaunchCoordinator = new LaunchCoordinator(new ProfileLauncher(workspaceBuilder), new GameSessionTracker());
        GameValidator = new GameInstallationValidator();
        ProfileReadinessService = new ProfileReadinessService(GameValidator);
        ApplicationLogService = new ApplicationLogService(Paths);
        ModConflictAnalyzer = new ModConflictAnalyzer();
        ProfileTransferService = new ProfileTransferService();
        ModScannerService = new ModScannerService();
        ModListEditor = new ModListEditor();
        ProfileDataPathResolver = new ProfileDataPathResolver();
        ScreenshotScannerService = new ScreenshotScannerService(ProfileDataPathResolver);
        GameExitDiagnosticsService = new GameExitDiagnosticsService(ProfileDataPathResolver);
        ProfileHealthService = new ProfileHealthService(GameValidator, ProfileManager, ProfileDataPathResolver);
    }

    public AppPaths Paths { get; }
    public SettingsStore SettingsStore { get; }
    public DialogService DialogService { get; }
    public ProfileManager ProfileManager { get; }
    public LaunchCoordinator LaunchCoordinator { get; }
    public GameInstallationValidator GameValidator { get; }
    public ProfileReadinessService ProfileReadinessService { get; }
    public ApplicationLogService ApplicationLogService { get; }
    public ModConflictAnalyzer ModConflictAnalyzer { get; }
    public ProfileTransferService ProfileTransferService { get; }
    public ModScannerService ModScannerService { get; }
    public ModListEditor ModListEditor { get; }
    public GameExitDiagnosticsService GameExitDiagnosticsService { get; }
    public ProfileHealthService ProfileHealthService { get; }
    public ProfileDataPathResolver ProfileDataPathResolver { get; }
    public ScreenshotScannerService ScreenshotScannerService { get; }

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
            ApplicationLogService);
    }
}
