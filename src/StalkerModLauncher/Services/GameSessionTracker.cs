using System.Diagnostics;

namespace StalkerModLauncher.Services;

public sealed class GameSessionTracker : IDisposable
{
    private DiscordPresenceService _discordPresence = new(string.Empty);

    public void ConfigureDiscord(string clientId)
    {
        _discordPresence.Dispose();
        _discordPresence = new DiscordPresenceService(clientId);
        _discordPresence.Initialize();
    }

    public async Task<GameSessionResult> TrackAsync(Process process, string profileName)
    {
        var startedAtUtc = DateTime.UtcNow;
        _discordPresence.SetPlaying(profileName);

        try
        {
            await process.WaitForExitAsync();
            return CreateResult(startedAtUtc, DateTime.UtcNow);
        }
        finally
        {
            _discordPresence.Clear();
            process.Dispose();
        }
    }

    public static GameSessionResult CreateResult(DateTime startedAtUtc, DateTime endedAtUtc)
    {
        var duration = endedAtUtc - startedAtUtc;
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return new GameSessionResult(duration, duration >= TimeSpan.FromSeconds(5));
    }

    public void Dispose()
    {
        _discordPresence.Dispose();
    }
}

public sealed record GameSessionResult(TimeSpan Duration, bool ShouldRecord);
