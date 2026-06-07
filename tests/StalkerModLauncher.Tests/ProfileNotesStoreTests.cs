using StalkerModLauncher.Models;
using StalkerModLauncher.Services;
using Xunit;

namespace StalkerModLauncher.Tests;

public sealed class ProfileNotesStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "StalkerModLauncherTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_UsesStableProfileId()
    {
        var profile = new ModProfile { Id = "stable-id", Name = "Original name" };
        var store = CreateStore();

        store.Save(profile, "my notes");
        profile.Name = "Renamed profile";

        Assert.Equal("my notes", store.Load(profile));
        Assert.EndsWith("notes-stable-id.txt", store.GetNotesFile(profile), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_MigratesLegacyNameBasedNotes()
    {
        var profile = new ModProfile { Id = "stable-id", Name = "Zona: Test" };
        var store = CreateStore();
        Directory.CreateDirectory(store.NotesDirectory);
        var legacyFile = Path.Combine(
            store.NotesDirectory,
            $"notes-{FileSystemSafety.SanitizeName(profile.Name)}.txt");
        File.WriteAllText(legacyFile, "legacy notes");

        var notes = store.Load(profile);

        Assert.Equal("legacy notes", notes);
        Assert.False(File.Exists(legacyFile));
        Assert.True(File.Exists(store.GetNotesFile(profile)));
    }

    private ProfileNotesStore CreateStore()
    {
        var paths = new AppPaths(_root, Path.Combine(_root, "workspaces"));
        return new ProfileNotesStore(paths);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
