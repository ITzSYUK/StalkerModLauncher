using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class WorkspaceManagementServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "StalkerModLauncherTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InspectAsync_ReadsStatisticsFromManifestWithoutScanningWorkspace()
    {
        var paths = new AppPaths(_root, Path.Combine(_root, "workspaces"), false);
        var service = new WorkspaceManagementService(new WorkspaceBuilder(paths));
        var workspace = Path.Combine(_root, "workspaces", "profile");
        Directory.CreateDirectory(Path.Combine(workspace, "current"));
        File.WriteAllText(Path.Combine(workspace, "current", "fsgame.ltx"), "12345");
        File.WriteAllText(
            Path.Combine(workspace, "build-manifest.json"),
            """
            {
              "BuiltAtUtc": "2026-06-15T00:00:00Z",
              "HasStatistics": true,
              "FileCount": 12,
              "HardLinkCount": 7,
              "SymbolicLinkCount": 4,
              "LocalFileCount": 1,
              "LogicalSizeBytes": 12345,
              "PhysicalSizeBytes": 5
            }
            """);
        var profile = new ModProfile { WorkspacePath = workspace };

        var status = await service.InspectAsync(profile);

        Assert.True(status.Exists);
        Assert.Equal(12, status.FileCount);
        Assert.Equal(12345, status.LogicalSizeBytes);
        Assert.Equal(1, status.LocalFileCount);
        Assert.Equal(7, status.HardLinkCount);
        Assert.Equal(4, status.SymbolicLinkCount);
    }

    [Fact]
    public async Task InspectAsync_ReturnsImmediatelyWithoutDetailedStatisticsForOldManifest()
    {
        var paths = new AppPaths(_root, Path.Combine(_root, "workspaces"), false);
        var service = new WorkspaceManagementService(new WorkspaceBuilder(paths));
        var workspace = Path.Combine(_root, "workspaces", "old-profile");
        Directory.CreateDirectory(Path.Combine(workspace, "current"));
        for (var index = 0; index < 5000; index++)
        {
            File.WriteAllText(Path.Combine(workspace, "current", $"{index}.bin"), "test");
        }
        File.WriteAllText(Path.Combine(workspace, "build-manifest.json"), """{"BuiltAtUtc":"2026-06-15T00:00:00Z"}""");
        var profile = new ModProfile { WorkspacePath = workspace };
        var started = DateTime.UtcNow;

        var status = await service.InspectAsync(profile);

        Assert.False(status.StatisticsAvailable);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task MoveAsync_MovesUserDataButNotCurrentCache()
    {
        var paths = new AppPaths(_root, Path.Combine(_root, "workspaces"), false);
        var service = new WorkspaceManagementService(new WorkspaceBuilder(paths));
        var oldWorkspace = Path.Combine(_root, "workspaces", "old-profile");
        Directory.CreateDirectory(Path.Combine(oldWorkspace, "current"));
        File.WriteAllText(Path.Combine(oldWorkspace, ".stalker-launcher-workspace"), "marker");
        File.WriteAllText(Path.Combine(oldWorkspace, "current", "cache.bin"), "cache");
        Directory.CreateDirectory(Path.Combine(oldWorkspace, "userdata", "savedgames"));
        File.WriteAllText(Path.Combine(oldWorkspace, "userdata", "savedgames", "save.sav"), "save");
        var profile = new ModProfile
        {
            Id = "12345678",
            Name = "Moved profile",
            WorkspacePath = oldWorkspace
        };
        var destinationRoot = Path.Combine(_root, "destination");

        await service.MoveAsync(profile, destinationRoot, new Progress<string>());

        Assert.True(File.Exists(Path.Combine(profile.WorkspacePath, "userdata", "savedgames", "save.sav")));
        Assert.False(Directory.Exists(Path.Combine(profile.WorkspacePath, "current")));
        Assert.False(Directory.Exists(oldWorkspace));
    }

    [Fact]
    public async Task MoveAsync_DoesNotCopyTemporaryUsvfsBootstrap()
    {
        var paths = new AppPaths(_root, Path.Combine(_root, "workspaces"), false);
        var service = new WorkspaceManagementService(new WorkspaceBuilder(paths));
        var oldWorkspace = Path.Combine(_root, "workspaces", "usvfs-profile");
        File.WriteAllText(
            CreateDirectoryAndReturnFile(oldWorkspace, ".stalker-launcher-workspace"),
            "marker");
        File.WriteAllText(
            CreateDirectoryAndReturnFile(Path.Combine(oldWorkspace, "userdata", "savedgames"), "save.sav"),
            "save");
        File.WriteAllText(
            CreateDirectoryAndReturnFile(Path.Combine(oldWorkspace, "userdata", "usvfs-bootstrap", "bin"), "xrEngine.exe"),
            "cache");
        var profile = new ModProfile
        {
            Id = "87654321",
            Name = "USVFS profile",
            WorkspacePath = oldWorkspace,
            LaunchBackendKind = LaunchBackendKind.VirtualFileSystem
        };

        await service.MoveAsync(profile, Path.Combine(_root, "destination-usvfs"), new Progress<string>());

        Assert.True(File.Exists(Path.Combine(profile.WorkspacePath, "userdata", "savedgames", "save.sav")));
        Assert.False(Directory.Exists(Path.Combine(profile.WorkspacePath, "userdata", "usvfs-bootstrap")));
        Assert.False(Directory.Exists(oldWorkspace));
    }

    private static string CreateDirectoryAndReturnFile(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
