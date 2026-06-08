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
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    public SettingsStore(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<AppSettings> LoadAsync()
    {
        await _ioLock.WaitAsync();
        try
        {
            return await LoadCoreAsync();
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        var snapshot = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
        await _ioLock.WaitAsync();
        try
        {
            await SaveSnapshotCoreAsync(snapshot);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<AppSettings> UpdateAsync(Func<AppSettings, AppSettings> update)
    {
        await _ioLock.WaitAsync();
        try
        {
            var current = await LoadCoreAsync();
            var updated = update(current);
            await SaveSnapshotCoreAsync(JsonSerializer.SerializeToUtf8Bytes(updated, JsonOptions));
            return updated;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<AppSettings> LoadCoreAsync()
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        var primary = await TryLoadFileAsync(_paths.SettingsFile);
        if (primary is not null)
        {
            return primary;
        }

        var backup = await TryLoadFileAsync(_paths.SettingsBackupFile);
        return backup ?? new AppSettings();
    }

    private static async Task<AppSettings?> TryLoadFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task SaveSnapshotCoreAsync(byte[] snapshot)
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        var tempPath = _paths.SettingsFile + ".tmp";
        await File.WriteAllBytesAsync(tempPath, snapshot);

        if (File.Exists(_paths.SettingsFile))
        {
            File.Replace(tempPath, _paths.SettingsFile, _paths.SettingsBackupFile);
        }
        else
        {
            File.Move(tempPath, _paths.SettingsFile);
        }
    }
}
