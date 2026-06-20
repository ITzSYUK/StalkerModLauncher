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
        await _store.SaveAsync(new AppSettings { LastBrowsedGamePath = "first" });
        await _store.SaveAsync(new AppSettings { LastBrowsedGamePath = "second" });
        await File.WriteAllTextAsync(_paths.SettingsFile, "{ broken json");

        var loaded = await _store.LoadAsync();

        Assert.Equal("first", loaded.LastBrowsedGamePath);
    }

    [Fact]
    public async Task UpdateAsync_AppliesConcurrentChangesWithoutLosingFields()
    {
        await _store.SaveAsync(new AppSettings());

        await Task.WhenAll(
            _store.UpdateAsync(settings =>
            {
                settings.LastBrowsedGamePath = "game";
                return settings;
            }),
            _store.UpdateAsync(settings =>
            {
                settings.DontShowAboutOnStartup = true;
                return settings;
            }));

        var loaded = await _store.LoadAsync();
        Assert.Equal("game", loaded.LastBrowsedGamePath);
        Assert.True(loaded.DontShowAboutOnStartup);
    }

    [Fact]
    public async Task SaveAsync_CapturesSnapshotBeforeWaitingForWrite()
    {
        var settings = new AppSettings { LastBrowsedGamePath = "snapshot" };
        var save = _store.SaveAsync(settings);
        settings.LastBrowsedGamePath = "mutated";
        await save;

        var loaded = await _store.LoadAsync();
        Assert.Equal("snapshot", loaded.LastBrowsedGamePath);
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaultsWhenPrimaryAndBackupAreCorrupted()
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFile, "{ broken primary");
        await File.WriteAllTextAsync(_paths.SettingsBackupFile, "{ broken backup");

        var loaded = await _store.LoadAsync();

        Assert.Equal(string.Empty, loaded.LastBrowsedGamePath);
        Assert.Empty(loaded.Profiles);
    }

    [Fact]
    public async Task SaveAsync_PreservesProfileAndModOrder()
    {
        var first = new ModProfile { Name = "First" };
        first.Mods.Add(new ModEntry { Name = "Low priority", Order = 1 });
        first.Mods.Add(new ModEntry { Name = "High priority", Order = 2 });
        var second = new ModProfile { Name = "Second" };

        await _store.SaveAsync(new AppSettings { Profiles = [second, first] });
        var loaded = await _store.LoadAsync();

        Assert.Equal(["Second", "First"], loaded.Profiles.Select(profile => profile.Name));
        Assert.Equal(
            ["Low priority", "High priority"],
            loaded.Profiles[1].Mods.Select(mod => mod.Name));
    }

    [Fact]
    public async Task SaveAndLoadAsync_PreservesPerProfileDiscordStatus()
    {
        var profile = new ModProfile { Name = "No Discord", IsDiscordStatusEnabled = false };

        await _store.SaveAsync(new AppSettings { Profiles = [profile] });
        var loaded = await _store.LoadAsync();

        Assert.False(loaded.Profiles.Single().IsDiscordStatusEnabled);
    }

    [Fact]
    public async Task LoadAsync_MigratesLegacyGlobalGamePath()
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        await File.WriteAllTextAsync(
            _paths.SettingsFile,
            """{"GameInstallPath":"D:\\Games\\STALKER","Profiles":[]}""");

        var loaded = await _store.LoadAsync();

        Assert.Equal(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(@"D:\Games\STALKER", loaded.LastBrowsedGamePath);
        Assert.Null(loaded.LegacyGameInstallPath);
    }

    [Fact]
    public async Task SaveAsync_DoesNotPersistRuntimeProperties()
    {
        var profile = new ModProfile { IsRunning = true };
        profile.Mods.Add(new ModEntry { IsLocked = true, HasOverlapsAbove = true });

        await _store.SaveAsync(new AppSettings { Profiles = [profile] });
        var json = await File.ReadAllTextAsync(_paths.SettingsFile);

        Assert.DoesNotContain("\"IsRunning\"", json);
        Assert.DoesNotContain("\"IsLocked\"", json);
        Assert.DoesNotContain("\"HasOverlapsAbove\"", json);
        Assert.DoesNotContain("\"PlaytimeDisplay\"", json);
        Assert.DoesNotContain("\"LastPlayedDisplay\"", json);
    }

    [Fact]
    public async Task LoadAsync_RepairsDuplicateIdsAndModOrder()
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        await File.WriteAllTextAsync(
            _paths.SettingsFile,
            """
            {
              "Profiles": [
                { "Id": "same", "Mods": [{ "Id": "mod", "Order": 8 }, { "Id": "mod", "Order": 3 }] },
                { "Id": "same", "Mods": [] }
              ]
            }
            """);

        var loaded = await _store.LoadAsync();

        Assert.NotEqual(loaded.Profiles[0].Id, loaded.Profiles[1].Id);
        Assert.NotEqual(loaded.Profiles[0].Mods[0].Id, loaded.Profiles[0].Mods[1].Id);
        Assert.Equal([1, 2], loaded.Profiles[0].Mods.Select(mod => mod.Order));
    }

    [Fact]
    public async Task SaveAndLoadAsync_HandlesLargeProfileCollection()
    {
        var settings = new AppSettings();
        for (var profileIndex = 0; profileIndex < 100; profileIndex++)
        {
            var profile = new ModProfile { Name = $"Profile {profileIndex}" };
            for (var modIndex = 0; modIndex < 50; modIndex++)
            {
                profile.Mods.Add(new ModEntry
                {
                    Name = $"Mod {modIndex}",
                    SourcePath = $@"D:\Mods\Profile-{profileIndex}\Mod-{modIndex}",
                    Order = modIndex + 1
                });
            }

            settings.Profiles.Add(profile);
        }

        await _store.SaveAsync(settings);
        var loaded = await _store.LoadAsync();

        Assert.Equal(100, loaded.Profiles.Count);
        Assert.All(loaded.Profiles, profile => Assert.Equal(50, profile.Mods.Count));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
