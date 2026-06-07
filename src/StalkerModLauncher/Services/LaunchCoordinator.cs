using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class LaunchCoordinator : IDisposable
{
    private readonly IProfileLauncher _profileLauncher;
    private readonly IGameSessionTracker _sessionTracker;

    public LaunchCoordinator(IProfileLauncher profileLauncher, IGameSessionTracker sessionTracker)
    {
        _profileLauncher = profileLauncher;
        _sessionTracker = sessionTracker;
    }

    public void ConfigureDiscord(string clientId)
    {
        _sessionTracker.ConfigureDiscord(clientId);
    }

    public async Task<LaunchedGameSession> StartAsync(
        string gamePath,
        ModProfile profile,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        var process = await _profileLauncher.LaunchAsync(gamePath, profile, progress, cancellationToken);
        var processId = process.Id;
        var completion = _sessionTracker.TrackAsync(process, profile.Name);
        return new LaunchedGameSession(processId, completion);
    }

    public void Dispose()
    {
        _sessionTracker.Dispose();
    }
}

public sealed record LaunchedGameSession(int ProcessId, Task<GameSessionResult> Completion);
