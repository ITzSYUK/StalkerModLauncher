using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly SettingsStore _store;

    public SettingsStoreTests()
    {
        _paths = new AppPaths(_root, Path.Combine(_root, "workspaces"), false);
        _store = new SettingsStore(_paths);
    }

    [Fact]
    public async Task LoadAsync_ReturnsBackupWhenPrimaryJsonIsCorrupted()
    {
        await _store.SaveAsync(new AppSettings { GameInstallPath = "first" });
        await _store.SaveAsync(new AppSettings { GameInstallPath = "second" });
        await File.WriteAllTextAsync(_paths.SettingsFile, "{ broken json");

        var loaded = await _store.LoadAsync();

        Assert.Equal("first", loaded.GameInstallPath);
    }

    [Fact]
    public async Task UpdateAsync_AppliesConcurrentChangesWithoutLosingFields()
    {
        await _store.SaveAsync(new AppSettings());

        await Task.WhenAll(
            _store.UpdateAsync(settings =>
            {
                settings.GameInstallPath = "game";
                return settings;
            }),
            _store.UpdateAsync(settings =>
            {
                settings.DontShowAboutOnStartup = true;
                return settings;
            }));

        var loaded = await _store.LoadAsync();
        Assert.Equal("game", loaded.GameInstallPath);
        Assert.True(loaded.DontShowAboutOnStartup);
    }

    [Fact]
    public async Task SaveAsync_CapturesSnapshotBeforeWaitingForWrite()
    {
        var settings = new AppSettings { GameInstallPath = "snapshot" };
        var save = _store.SaveAsync(settings);
        settings.GameInstallPath = "mutated";
        await save;

        var loaded = await _store.LoadAsync();
        Assert.Equal("snapshot", loaded.GameInstallPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
