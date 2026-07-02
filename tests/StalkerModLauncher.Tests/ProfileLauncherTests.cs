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
    public async Task LaunchAsync_UsesProfileSelectedBackend()
    {
        using var process = Process.GetCurrentProcess();
        var linkedBackend = new RecordingLaunchBackend(LaunchBackendKind.LinkedWorkspace);
        var virtualBackend = new RecordingLaunchBackend(LaunchBackendKind.VirtualFileSystem);
        var executor = new RecordingLaunchPlanExecutor(process);
        var launcher = new ProfileLauncher([linkedBackend, virtualBackend], executor);
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
    public async Task LaunchAsync_RejectsVirtualFileSystemWhenBackendIsNotRegistered()
    {
        using var process = Process.GetCurrentProcess();
        var launcher = new ProfileLauncher(
            [new RecordingLaunchBackend(LaunchBackendKind.LinkedWorkspace)],
            new RecordingLaunchPlanExecutor(process));
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

        public Task<LaunchPlan> PrepareAsync(
            string gamePath,
            ModProfile profile,
            IProgress<string> progress,
            CancellationToken cancellationToken = default)
        {
            GamePath = gamePath;
            Profile = profile;
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
}
