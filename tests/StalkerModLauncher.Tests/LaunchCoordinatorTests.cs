using System.Diagnostics;
using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class LaunchCoordinatorTests
{
    [Fact]
    public async Task StartAsync_LaunchesProfileAndStartsSessionTracking()
    {
        using var process = Process.GetCurrentProcess();
        var launcher = new FakeProfileLauncher(process);
        var tracker = new FakeSessionTracker();
        using var coordinator = new LaunchCoordinator(launcher, tracker);
        var profile = new ModProfile { Name = "Test profile" };

        var session = await coordinator.StartAsync("game path", profile, new Progress<string>());
        var result = await session.Completion;

        Assert.Equal(process.Id, session.ProcessId);
        Assert.Equal("game path", launcher.GamePath);
        Assert.Same(profile, launcher.Profile);
        Assert.Same(process, tracker.Process);
        Assert.Equal("Test profile", tracker.ProfileName);
        Assert.Equal(TimeSpan.FromMinutes(2), result.Duration);
    }

    [Fact]
    public void ConfigureDiscordAndDispose_AreDelegatedToTracker()
    {
        using var process = Process.GetCurrentProcess();
        var tracker = new FakeSessionTracker();
        var coordinator = new LaunchCoordinator(new FakeProfileLauncher(process), tracker);

        coordinator.ConfigureDiscord("client-id");
        coordinator.Dispose();

        Assert.Equal("client-id", tracker.DiscordClientId);
        Assert.True(tracker.IsDisposed);
    }

    private sealed class FakeProfileLauncher(Process process) : IProfileLauncher
    {
        public string? GamePath { get; private set; }
        public ModProfile? Profile { get; private set; }

        public Task<Process> LaunchAsync(
            string gamePath,
            ModProfile profile,
            IProgress<string> progress,
            CancellationToken cancellationToken = default)
        {
            GamePath = gamePath;
            Profile = profile;
            return Task.FromResult(process);
        }
    }

    private sealed class FakeSessionTracker : IGameSessionTracker
    {
        public Process? Process { get; private set; }
        public string? ProfileName { get; private set; }
        public string? DiscordClientId { get; private set; }
        public bool IsDisposed { get; private set; }

        public void ConfigureDiscord(string clientId)
        {
            DiscordClientId = clientId;
        }

        public Task<GameSessionResult> TrackAsync(Process process, string profileName)
        {
            Process = process;
            ProfileName = profileName;
            return Task.FromResult(new GameSessionResult(TimeSpan.FromMinutes(2), true));
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
