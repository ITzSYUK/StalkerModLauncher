using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ProfileVirtualFileDiagnosticsServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherProfileVirtualFileDiagnosticsTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void InspectLinkedWorkspaceFile_ReportsWinningLayerAndWriteTarget()
    {
        var appPaths = new AppPaths(
            Path.Combine(_root, "config"),
            Path.Combine(_root, "workspaces"),
            preferGameDriveWorkspace: false);
        var workspaceBuilder = new WorkspaceBuilder(appPaths);
        var profileManager = new ProfileManager(appPaths, workspaceBuilder);
        var game = Path.Combine(_root, "game");
        var mod = Path.Combine(_root, "mod");
        var patch = Path.Combine(_root, "patch");
        CreateFile(game, "gamedata/config/system.ltx", "base");
        CreateFile(mod, "gamedata/config/system.ltx", "mod");
        CreateFile(patch, "gamedata/config/system.ltx", "patch");
        var profile = new ModProfile
        {
            Name = "Layered profile",
            GameInstallPath = game
        };
        profile.Mods.Add(new ModEntry { Id = "mod", Name = "Mod", SourcePath = mod, Order = 1 });
        profile.Mods.Add(new ModEntry { Id = "patch", Name = "Patch", SourcePath = patch, Order = 2 });
        profile.WorkspacePath = profileManager.GetProfileFolderPath(profile)!;

        var inspection = new ProfileVirtualFileDiagnosticsService(profileManager)
            .InspectLinkedWorkspaceFile(profile, @"gamedata\config\system.ltx");

        Assert.True(inspection.Exists);
        Assert.Contains("Patch", inspection.ReadSourceDisplay);
        Assert.Contains("overwrite", inspection.WriteTargetDisplay);
        Assert.Contains("3", inspection.ProvidersDisplay);
    }

    private static void CreateFile(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
