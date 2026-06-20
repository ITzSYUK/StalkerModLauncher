using System.Diagnostics;

namespace StalkerModLauncher.Services;

public interface IGameSessionTracker : IDisposable
{
    void ConfigureDiscord(string clientId, Action<string>? diagnostic = null);
    Task<GameSessionResult> TrackAsync(Process process, string profileName, bool publishDiscordStatus);
}

public sealed class GameSessionTracker : IGameSessionTracker
{
    private readonly object _sync = new();
    private readonly Dictionary<int, DiscordSession> _activeDiscordSessions = [];
    private DiscordPresenceService _discordPresence = new(string.Empty);
    private long _discordSessionOrder;

    public void ConfigureDiscord(string clientId, Action<string>? diagnostic = null)
    {
        lock (_sync)
        {
            _discordPresence.Dispose();
            _discordPresence = new DiscordPresenceService(clientId, diagnostic);
            UpdateDiscordPresenceLocked();
        }
    }

    public async Task<GameSessionResult> TrackAsync(Process process, string profileName, bool publishDiscordStatus)
    {
        var startedAtUtc = DateTime.UtcNow;
        var processId = process.Id;
        if (publishDiscordStatus)
        {
            lock (_sync)
            {
                _activeDiscordSessions[processId] = new DiscordSession(profileName, ++_discordSessionOrder);
                UpdateDiscordPresenceLocked();
            }
        }

        try
        {
            await process.WaitForExitAsync();
            return CreateResult(startedAtUtc, DateTime.UtcNow, process.ExitCode);
        }
        finally
        {
            if (publishDiscordStatus)
            {
                lock (_sync)
                {
                    _activeDiscordSessions.Remove(processId);
                    UpdateDiscordPresenceLocked();
                }
            }

            process.Dispose();
        }
    }

    public static GameSessionResult CreateResult(DateTime startedAtUtc, DateTime endedAtUtc, int? exitCode = null)
    {
        var duration = endedAtUtc - startedAtUtc;
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return new GameSessionResult(duration, duration >= TimeSpan.FromSeconds(5), exitCode, startedAtUtc, endedAtUtc);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _activeDiscordSessions.Clear();
            _discordPresence.Dispose();
        }
    }

    private void UpdateDiscordPresenceLocked()
    {
        var activeSession = _activeDiscordSessions.Values
            .OrderByDescending(session => session.Order)
            .FirstOrDefault();

        if (activeSession is null)
        {
            _discordPresence.Clear();
            return;
        }

        _discordPresence.Initialize();
        _discordPresence.SetPlaying(activeSession.ProfileName);
    }

    private sealed record DiscordSession(string ProfileName, long Order);
}

public sealed record GameSessionResult(
    TimeSpan Duration,
    bool ShouldRecord,
    int? ExitCode = null,
    DateTime? StartedAtUtc = null,
    DateTime? EndedAtUtc = null);
