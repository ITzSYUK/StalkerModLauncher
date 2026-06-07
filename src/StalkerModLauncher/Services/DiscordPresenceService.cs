using DiscordRPC;

namespace StalkerModLauncher.Services;

public sealed class DiscordPresenceService : IDisposable
{
    private DiscordRpcClient? _client;
    private readonly string _clientId;

    public DiscordPresenceService(string clientId)
    {
        _clientId = clientId;
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_clientId);
    public string ClientId => _clientId;

    public void Initialize()
    {
        if (!IsEnabled || _client is not null)
        {
            return;
        }

        try
        {
            _client = new DiscordRpcClient(_clientId);
            _client.Initialize();
        }
        catch
        {
            _client?.Dispose();
            _client = null;
        }
    }

    public void SetPlaying(string profileName)
    {
        if (_client is null || !_client.IsInitialized)
        {
            return;
        }

        _client.SetPresence(new RichPresence
        {
            Details = profileName,
            State = null,
            Assets = new Assets
            {
                LargeImageText = "S.T.A.L.K.E.R. Mod Launcher"
            }
        });
    }

    public void Clear()
    {
        if (_client is null || !_client.IsInitialized)
        {
            return;
        }

        _client.ClearPresence();
    }

    public void Dispose()
    {
        try
        {
            Clear();
            _client?.Dispose();
        }
        catch
        {
            // ignore
        }
    }
}
