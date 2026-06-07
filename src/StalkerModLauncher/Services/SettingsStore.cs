using System.Text.Json;
using StalkerModLauncher.Models;

namespace StalkerModLauncher.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public SettingsStore(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<AppSettings> LoadAsync()
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);

        if (!File.Exists(_paths.SettingsFile))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(_paths.SettingsFile);
        return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions) ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await _saveLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(_paths.ConfigDirectory);
            var tempPath = _paths.SettingsFile + ".tmp";

            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
            }

            if (File.Exists(_paths.SettingsFile))
            {
                File.Replace(tempPath, _paths.SettingsFile, null);
            }
            else
            {
                File.Move(tempPath, _paths.SettingsFile);
            }
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
