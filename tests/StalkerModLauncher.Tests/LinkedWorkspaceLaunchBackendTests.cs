using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class LinkedWorkspaceLaunchBackendTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherBackendTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PrepareAsync_BuildsWorkspaceAndReturnsLaunchPlan()
    {
        var game = Path.Combine(_root, "game");
        var mod = Path.Combine(_root, "mods", "patch");
        var workspaceRoot = Path.Combine(_root, "workspaces");
        var backend = new LinkedWorkspaceLaunchBackend(new WorkspaceBuilder(new AppPaths(
            Path.Combine(_root, "config"),
            workspaceRoot,
            preferGameDriveWorkspace: false)));
        var profile = new ModProfile
        {
            Name = "Backend profile",
            ExecutableRelativePath = @"bin\xr_3da.exe",
            LaunchArguments = "  -nointro -dbg  "
        };
        profile.Mods.Add(new ModEntry { Name = "Patch", SourcePath = mod, IsEnabled = true, Order = 1 });
        CreateFile(game, "bin/xr_3da.exe", "base executable");
        CreateFile(game, "fsgame.ltx", "$app_data_root$ = true | false | appdata");
        CreateFile(mod, "bin/xr_3da.exe", "mod executable");

        var fileLayerPlan = FileLayerPlan.CreateLinkedWorkspace(game, profile, Path.Combine(workspaceRoot, $"Backend profile-{profile.Id[..8]}"));
        var launchContext = new ProfileLaunchBackendContext(game, profile, fileLayerPlan);

        var plan = await backend.PrepareAsync(launchContext, new ProgressLog());

        Assert.Equal(LaunchBackendKind.LinkedWorkspace, plan.BackendKind);
        Assert.Equal("-nointro -dbg", plan.Arguments);
        Assert.EndsWith(Path.Combine("current", "bin", "xr_3da.exe"), plan.ExecutablePath);
        Assert.EndsWith("current", plan.WorkingDirectory);
        Assert.EndsWith($"Backend profile-{profile.Id[..8]}", profile.WorkspacePath);
        Assert.Equal(@"bin\xr_3da.exe", profile.ExecutableRelativePath);
        Assert.Empty(profile.WorkingDirectoryRelative);
        Assert.Equal("mod executable", File.ReadAllText(plan.ExecutablePath));
        Assert.Equal("base executable", File.ReadAllText(Path.Combine(game, "bin", "xr_3da.exe")));
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

    private sealed class ProgressLog : IProgress<string>
    {
        public List<string> Messages { get; } = [];

        public void Report(string value)
        {
            Messages.Add(value);
        }
    }
}
