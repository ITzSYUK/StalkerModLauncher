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
        ModConflictAnalyzer = new ModConflictAnalyzer();
        ProfileTransferService = new ProfileTransferService();
        ModScannerService = new ModScannerService();
        ModListEditor = new ModListEditor();
    }

    public AppPaths Paths { get; }
    public SettingsStore SettingsStore { get; }
    public DialogService DialogService { get; }
    public ProfileManager ProfileManager { get; }
    public LaunchCoordinator LaunchCoordinator { get; }
    public GameInstallationValidator GameValidator { get; }
    public ModConflictAnalyzer ModConflictAnalyzer { get; }
    public ProfileTransferService ProfileTransferService { get; }
    public ModScannerService ModScannerService { get; }
    public ModListEditor ModListEditor { get; }

    public MainViewModel CreateMainViewModel()
    {
        return new MainViewModel(
            Paths,
            SettingsStore,
            GameValidator,
            LaunchCoordinator,
            DialogService,
            ModConflictAnalyzer,
            ProfileTransferService,
            ModScannerService,
            ModListEditor,
            ProfileManager);
    }
}
