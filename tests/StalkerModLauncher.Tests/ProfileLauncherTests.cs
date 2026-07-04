using System.Diagnostics;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ProfileLauncherTests
{
    [Fact]
    public async Task LaunchAsync_UsesLinkedWorkspaceBackendByDefault()
    {
        using var process = Process.GetCurrentProcess();
        var linkedBackend = new RecordingLaunchBackend(LaunchBackendKind.LinkedWorkspace);
        var virtualBackend = new RecordingLaunchBackend(LaunchBackendKind.VirtualFileSystem);
        var executor = new RecordingLaunchPlanExecutor(process);
        var launcher = new ProfileLauncher([linkedBackend, virtualBackend], executor);
        var profile = new ModProfile();

        var launchedProcess = await launcher.LaunchAsync("game", profile, new Progress<string>());

        Assert.Same(process, launchedProcess);
        Assert.Same(profile, linkedBackend.Profile);
        Assert.Null(virtualBackend.Profile);
        Assert.Equal(LaunchBackendKind.LinkedWorkspace, executor.Plan?.BackendKind);
    }

    [Fact]
    public async Task LaunchAsync_UsesProfileSelectedBackendWhenExperimentalVirtualFileSystemIsEnabled()
    {
        using var process = Process.GetCurrentProcess();
        var linkedBackend = new RecordingLaunchBackend(LaunchBackendKind.LinkedWorkspace);
        var virtualBackend = new RecordingLaunchBackend(LaunchBackendKind.VirtualFileSystem);
        var executor = new RecordingLaunchPlanExecutor(process);
        var launcher = new ProfileLauncher(
            [linkedBackend, virtualBackend],
            executor,
            allowExperimentalVirtualFileSystem: true);
        var profile = new ModProfile { LaunchBackendKind = LaunchBackendKind.VirtualFileSystem };

        await launcher.LaunchAsync("game", profile, new Progress<string>());

        Assert.Null(linkedBackend.Profile);
        Assert.Same(profile, virtualBackend.Profile);
        Assert.Equal(LaunchBackendKind.VirtualFileSystem, executor.Plan?.BackendKind);
    }

    [Fact]
    public async Task LaunchAsync_ReportsSelectedBackend()
    {
        using var process = Process.GetCurrentProcess();
        var launcher = new ProfileLauncher(
            [new RecordingLaunchBackend(LaunchBackendKind.LinkedWorkspace)],
            new RecordingLaunchPlanExecutor(process));
        var progress = new ListProgress();

        await launcher.LaunchAsync("game", new ModProfile(), progress);

        Assert.Contains("Launch backend: LinkedWorkspace.", progress.Messages);
        Assert.Contains("Starting: C:\\Game\\LinkedWorkspace.exe", progress.Messages);
    }

    [Fact]
    public async Task LaunchAsync_PassesFileLayerPlanAndOverlayManifestToBackend()
    {
        using var process = Process.GetCurrentProcess();
        var root = Path.Combine(Path.GetTempPath(), "StalkerModLauncherProfileLauncherTests", Guid.NewGuid().ToString("N"));
        try
        {
            var game = Path.Combine(root, "game");
            var mod = Path.Combine(root, "mod");
            Directory.CreateDirectory(game);
            Directory.CreateDirectory(mod);
            var paths = new AppPaths(Path.Combine(root, "config"), Path.Combine(root, "workspaces"), false);
            var profileManager = new ProfileManager(paths, new NoopWorkspaceManager());
            var linkedBackend = new RecordingLaunchBackend(LaunchBackendKind.LinkedWorkspace);
            var launcher = new ProfileLauncher(
                [linkedBackend],
                new RecordingLaunchPlanExecutor(process),
                profileManager);
            var profile = new ModProfile
            {
                Name = "Layered profile",
                GameInstallPath = game
            };
            profile.Mods.Add(new ModEntry { Id = "mod", Name = "Patch", SourcePath = mod, Order = 1 });

            await launcher.LaunchAsync(game, profile, new Progress<string>());

            Assert.NotNull(linkedBackend.Context?.FileLayerPlan);
            Assert.NotNull(linkedBackend.Context?.OverlayManifest);
            Assert.Equal(["__base_game", "mod", "__userdata"], linkedBackend.Context.FileLayerPlan.Layers.Select(layer => layer.Id));
            Assert.Equal(3, linkedBackend.Context.OverlayManifest.Layers.Count);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LaunchAsync_RejectsVirtualFileSystemWhenFeatureFlagIsDisabled()
    {
        using var process = Process.GetCurrentProcess();
        var launcher = new ProfileLauncher(
            [
                new RecordingLaunchBackend(LaunchBackendKind.LinkedWorkspace),
                new RecordingLaunchBackend(LaunchBackendKind.VirtualFileSystem)
            ],
            new RecordingLaunchPlanExecutor(process));
        var profile = new ModProfile { LaunchBackendKind = LaunchBackendKind.VirtualFileSystem };

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => launcher.LaunchAsync("game", profile, new Progress<string>()));

        Assert.Contains("experimental and disabled", exception.Message);
    }

    [Fact]
    public async Task LaunchAsync_RejectsVirtualFileSystemWhenBackendIsNotRegistered()
    {
        using var process = Process.GetCurrentProcess();
        var launcher = new ProfileLauncher(
            [new RecordingLaunchBackend(LaunchBackendKind.LinkedWorkspace)],
            new RecordingLaunchPlanExecutor(process),
            allowExperimentalVirtualFileSystem: true);
        var profile = new ModProfile { LaunchBackendKind = LaunchBackendKind.VirtualFileSystem };

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => launcher.LaunchAsync("game", profile, new Progress<string>()));

        Assert.Contains("Virtual file system", exception.Message);
    }

    private sealed class RecordingLaunchBackend(LaunchBackendKind kind) : IProfileLaunchBackend
    {
        public LaunchBackendKind Kind { get; } = kind;
        public string? GamePath { get; private set; }
        public ModProfile? Profile { get; private set; }
        public ProfileLaunchBackendContext? Context { get; private set; }

        public Task<LaunchPlan> PrepareAsync(
            ProfileLaunchBackendContext context,
            IProgress<string> progress,
            CancellationToken cancellationToken = default)
        {
            Context = context;
            GamePath = context.GamePath;
            Profile = context.Profile;
            return Task.FromResult(new LaunchPlan(
                Kind,
                $"C:\\Game\\{Kind}.exe",
                "-nointro",
                "C:\\Game"));
        }
    }

    private sealed class RecordingLaunchPlanExecutor(Process process) : ILaunchPlanExecutor
    {
        public LaunchPlan? Plan { get; private set; }

        public Process Start(LaunchPlan plan)
        {
            Plan = plan;
            return process;
        }
    }

    private sealed class ListProgress : IProgress<string>
    {
        public List<string> Messages { get; } = [];

        public void Report(string value)
        {
            Messages.Add(value);
        }
    }

    private sealed class NoopWorkspaceManager : IProfileWorkspaceManager
    {
        public void DeleteProfileWorkspace(ModProfile profile, string gamePath)
        {
        }

        public void ClearProfileWorkspaceCache(ModProfile profile, string gamePath)
        {
        }
    }
}
